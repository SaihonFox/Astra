using System.Runtime.CompilerServices;
using Astra.Core.Inference;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Logging.Abstractions;

namespace Astra.ModelHost.Services;

/// <summary>
/// Local runtime backed by LLamaSharp and llama.cpp.
/// </summary>
/// <remarks>
/// All native state is owned by this type. Load, generation, embedding and unload are
/// serialized because llama.cpp contexts and executors are not safe for concurrent use.
/// Native failures surface as managed exceptions only where LLamaSharp converts them; hard
/// native crashes are contained by the process boundary.
/// </remarks>
internal sealed class LlamaSharpModelRuntime : ILocalModelRuntime
{
	private readonly SemaphoreSlim _operationLock = new(1, 1);
	private readonly ModelHostRuntimeLimits _limits;
	private LLamaWeights? _weights;
	private LLamaContext? _context;
	private LLamaEmbedder? _embedder;
	private string? _loadedModelId;
	private string? _modelPath;
	private int _gpuLayerCount;
	private int _disposeRequested;

	/// <summary>Initializes a new instance of the <see cref="LlamaSharpModelRuntime"/> class.</summary>
	/// <param name="limits">Resource limits enforced before loading.</param>
	public LlamaSharpModelRuntime(ModelHostRuntimeLimits limits)
	{
		_limits = limits;
	}

	/// <inheritdoc />
	public string? LoadedModelId => Volatile.Read(ref _loadedModelId);

	/// <inheritdoc />
	public async Task LoadAsync(
		string modelId,
		ModelLoadOptions options,
		CancellationToken cancellationToken = default)
	{
		LocalRuntimeValidation.ValidateModelId(modelId);
		LocalRuntimeValidation.ValidateLoadOptions(options);

		if (!File.Exists(options.ModelPath))
		{
			throw new InvalidOperationException(
				"The model file does not exist. Register the model again after placing the file.");
		}

		if (_limits.MaxModelBytes is { } maxModelBytes)
		{
			var modelLength = new FileInfo(options.ModelPath).Length;

			if (modelLength > maxModelBytes)
			{
				throw new InvalidOperationException(
					$"The model file is {modelLength} bytes, which exceeds the configured limit " +
					$"of {maxModelBytes} bytes.");
			}
		}

		await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);

			if (_weights is not null)
			{
				throw new InvalidOperationException(
					$"Model '{_loadedModelId}' is already loaded. Unload it first.");
			}

			var parameters = new ModelParams(options.ModelPath)
			{
				ContextSize = options.ContextWindow is { } contextWindow
					? checked((uint)contextWindow)
					: null,
				GpuLayerCount = options.GpuLayerCount ?? 0,
			};

			LLamaWeights? weights = null;
			LLamaContext? context = null;

			try
			{
				weights = await LLamaWeights
					.LoadFromFileAsync(parameters, cancellationToken)
					.ConfigureAwait(false);

				cancellationToken.ThrowIfCancellationRequested();

				context = weights.CreateContext(parameters, NullLogger.Instance);
			}
			catch (OperationCanceledException)
			{
				context?.Dispose();
				weights?.Dispose();
				throw;
			}
			catch (Exception exception)
			{
				context?.Dispose();
				weights?.Dispose();

				throw new InvalidOperationException(
					"The local runtime could not load the model file.",
					exception);
			}

			_weights = weights;
			_context = context;
			_modelPath = options.ModelPath;
			_gpuLayerCount = options.GpuLayerCount ?? 0;
			Volatile.Write(ref _loadedModelId, modelId);
		}
		finally
		{
			_operationLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task UnloadAsync(CancellationToken cancellationToken = default)
	{
		await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			ReleaseModel();
		}
		finally
		{
			_operationLock.Release();
		}
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<GenerationChunk> GenerateAsync(
		GenerationRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		LocalRuntimeValidation.ValidateGenerationRequest(request);

		await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);

			var context = _context
				?? throw new InvalidOperationException("No model is loaded.");

			var prompt = request.SystemPrompt is { Length: > 0 } systemPrompt
				? $"{systemPrompt}\n\n{request.Prompt}"
				: request.Prompt;

			using var pipeline = new DefaultSamplingPipeline
			{
				Temperature = request.Temperature,
			};

			if (request.Seed is { } seed)
			{
				pipeline.Seed = unchecked((uint)seed);
			}

			var inference = new InferenceParams
			{
				MaxTokens = request.MaxTokens,
				SamplingPipeline = pipeline,
			};

			var executor = new InteractiveExecutor(context);

			await foreach (var token in executor
				.InferAsync(prompt, inference, cancellationToken)
				.ConfigureAwait(false))
			{
				if (token.Length == 0)
				{
					continue;
				}

				yield return new GenerationChunk { Text = token };
			}
		}
		finally
		{
			_operationLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task<EmbeddingResult> EmbedAsync(
		EmbeddingRequest request,
		CancellationToken cancellationToken = default)
	{
		LocalRuntimeValidation.ValidateEmbeddingRequest(request);

		await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);

			var weights = _weights
				?? throw new InvalidOperationException("No model is loaded.");

			_embedder ??= new LLamaEmbedder(
				weights,
				new ModelParams(_modelPath!)
				{
					ContextSize = _context!.ContextSize,
					GpuLayerCount = _gpuLayerCount,
					Embeddings = true,
				},
				NullLogger.Instance);

			var vectors = new List<ReadOnlyMemory<float>>(request.Inputs.Count);

			foreach (var input in request.Inputs)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var embeddings = await _embedder
					.GetEmbeddings(input, cancellationToken)
					.ConfigureAwait(false);

				if (embeddings.Count != 1)
				{
					throw new InvalidOperationException(
						"The local runtime returned an unexpected number of embeddings.");
				}

				vectors.Add(embeddings[0]);
			}

			return new EmbeddingResult
			{
				Vectors = vectors,
				Dimensions = _embedder.EmbeddingSize,
			};
		}
		finally
		{
			_operationLock.Release();
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
		{
			return;
		}

		await _operationLock.WaitAsync().ConfigureAwait(false);

		try
		{
			ReleaseModel();
		}
		finally
		{
			_operationLock.Release();
			_operationLock.Dispose();
		}
	}

	private void ReleaseModel()
	{
		_embedder?.Dispose();
		_embedder = null;

		_context?.Dispose();
		_context = null;

		_weights?.Dispose();
		_weights = null;

		_modelPath = null;
		Volatile.Write(ref _loadedModelId, null);
	}
}
