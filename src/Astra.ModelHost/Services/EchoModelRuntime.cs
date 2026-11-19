using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Astra.Core.Inference;

namespace Astra.ModelHost.Services;

/// <summary>
/// Deterministic placeholder runtime used until the LLamaSharp adapter lands.
/// </summary>
/// <remarks>
/// It never opens the model file and never performs inference. It exists so the IPC and
/// service contract can be exercised in CI without native assets or model downloads.
/// </remarks>
internal sealed class EchoModelRuntime : ILocalModelRuntime
{
	private const int EmbeddingDimensions = 8;
	private const int ChunkSize = 4;

	private readonly object _gate = new();
	private string? _loadedModelId;

	public string? LoadedModelId
	{
		get
		{
			lock (_gate)
			{
				return _loadedModelId;
			}
		}
	}

	public Task LoadAsync(
		string modelId,
		ModelLoadOptions options,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		LocalRuntimeValidation.ValidateModelId(modelId);
		LocalRuntimeValidation.ValidateLoadOptions(options);

		lock (_gate)
		{
			if (_loadedModelId is not null)
			{
				throw new InvalidOperationException($"Model '{_loadedModelId}' is already loaded.");
			}

			_loadedModelId = modelId;
		}

		return Task.CompletedTask;
	}

	public Task UnloadAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		lock (_gate)
		{
			_loadedModelId = null;
		}

		return Task.CompletedTask;
	}

	public async IAsyncEnumerable<GenerationChunk> GenerateAsync(
		GenerationRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var modelId = LoadedModelId
			?? throw new InvalidOperationException("No model is loaded.");

		LocalRuntimeValidation.ValidateGenerationRequest(request);

		var response = $"echo({modelId}):{request.Prompt}";
		var emitted = 0;

		for (var offset = 0;
			offset < response.Length && emitted < request.MaxTokens;
			offset += ChunkSize)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await Task.Yield();

			yield return new GenerationChunk
			{
				Text = response.Substring(
					offset,
					Math.Min(ChunkSize, response.Length - offset)),
			};
			emitted++;
		}
	}

	public Task<EmbeddingResult> EmbedAsync(
		EmbeddingRequest request,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (LoadedModelId is null)
		{
			throw new InvalidOperationException("No model is loaded.");
		}

		LocalRuntimeValidation.ValidateEmbeddingRequest(request);

		var vectors = request.Inputs.Select(CreateVector).ToArray();

		return Task.FromResult(new EmbeddingResult
		{
			Vectors = vectors,
			Dimensions = EmbeddingDimensions,
		});
	}

	public ValueTask DisposeAsync()
	{
		lock (_gate)
		{
			_loadedModelId = null;
		}

		return ValueTask.CompletedTask;
	}

	private static ReadOnlyMemory<float> CreateVector(string input)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		var vector = new float[EmbeddingDimensions];

		for (var index = 0; index < vector.Length; index++)
		{
			vector[index] = ((hash[index] / 255f) * 2f) - 1f;
		}

		return vector;
	}
}
