using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Astra.Core.Inference;

namespace Astra.Core.Tests;

internal sealed class FakeLocalModelRuntime : ILocalModelRuntime
{
	public const int EmbeddingDimensions = 8;
	public const int ChunkSize = 4;

	public string? LoadedModelId { get; private set; }

	public ModelLoadOptions? LastLoadOptions { get; private set; }

	public int LoadCount { get; private set; }

	public Task LoadAsync(
		string modelId,
		ModelLoadOptions options,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		LocalRuntimeValidation.ValidateModelId(modelId);
		LocalRuntimeValidation.ValidateLoadOptions(options);

		if (LoadedModelId is not null)
		{
			throw new InvalidOperationException($"Model '{LoadedModelId}' is already loaded.");
		}

		LoadedModelId = modelId;
		LastLoadOptions = options;
		LoadCount++;

		return Task.CompletedTask;
	}

	public Task UnloadAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		LoadedModelId = null;
		LastLoadOptions = null;

		return Task.CompletedTask;
	}

	public async IAsyncEnumerable<GenerationChunk> GenerateAsync(
		GenerationRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		if (LoadedModelId is null)
		{
			throw new InvalidOperationException("No model is loaded.");
		}

		LocalRuntimeValidation.ValidateGenerationRequest(request);

		var response = $"echo({LoadedModelId}):{request.Prompt}";
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
		LoadedModelId = null;
		LastLoadOptions = null;

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
