using Astra.Core.Inference;

namespace Astra.Core.Tests;

public sealed class LocalModelRuntimeContractTests
{
	private const string ModelId = "test-model";

	[Fact]
	public async Task LoadUnloadAndDisposeTrackState()
	{
		var runtime = new FakeLocalModelRuntime();
		var options = new ModelLoadOptions
		{
			ModelPath = NewOptions().ModelPath,
			ContextWindow = 4096,
			GpuLayerCount = 10,
		};

		Assert.Null(runtime.LoadedModelId);

		await runtime.LoadAsync(ModelId, options);
		Assert.Equal(ModelId, runtime.LoadedModelId);
		Assert.Equal(options, runtime.LastLoadOptions);

		await runtime.UnloadAsync();
		Assert.Null(runtime.LoadedModelId);
		Assert.Null(runtime.LastLoadOptions);

		await runtime.LoadAsync(ModelId, NewOptions());
		Assert.Equal(2, runtime.LoadCount);

		await runtime.DisposeAsync();
		Assert.Null(runtime.LoadedModelId);
	}

	[Fact]
	public async Task LoadingSecondModelIsRejectedUntilUnload()
	{
		await using var runtime = new FakeLocalModelRuntime();
		await runtime.LoadAsync(ModelId, NewOptions());

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => runtime.LoadAsync("other-model", NewOptions()));
		Assert.Contains("already loaded", exception.Message, StringComparison.OrdinalIgnoreCase);

		await runtime.UnloadAsync();
		await runtime.LoadAsync("other-model", NewOptions());
		Assert.Equal("other-model", runtime.LoadedModelId);
	}

	[Fact]
	public async Task GenerationWithoutLoadedModelIsRejected()
	{
		await using var runtime = new FakeLocalModelRuntime();

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => CollectTextAsync(runtime.GenerateAsync(new GenerationRequest { Prompt = "hello" })));
	}

	[Fact]
	public async Task GenerationIsDeterministicAndChunked()
	{
		await using var runtime = new FakeLocalModelRuntime();
		await runtime.LoadAsync(ModelId, NewOptions());

		var chunks = await CollectAsync(
			runtime.GenerateAsync(new GenerationRequest { Prompt = "hello" }));

		Assert.Equal("echo(test-model):hello", string.Concat(chunks.Select(chunk => chunk.Text)));
		Assert.All(chunks, chunk => Assert.NotEmpty(chunk.Text));
		Assert.All(chunks, chunk => Assert.InRange(chunk.Text.Length, 1, FakeLocalModelRuntime.ChunkSize));
	}

	[Fact]
	public async Task MaxTokensLimitsGeneratedChunks()
	{
		await using var runtime = new FakeLocalModelRuntime();
		await runtime.LoadAsync(ModelId, NewOptions());

		var chunks = await CollectAsync(
			runtime.GenerateAsync(new GenerationRequest { Prompt = "hello", MaxTokens = 3 }));

		Assert.Equal(3, chunks.Count);
	}

	[Fact]
	public async Task CancellationStopsStreaming()
	{
		await using var runtime = new FakeLocalModelRuntime();
		await runtime.LoadAsync(ModelId, NewOptions());

		using var cancellation = new CancellationTokenSource();
		var chunks = new List<GenerationChunk>();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			await foreach (var chunk in runtime.GenerateAsync(
				new GenerationRequest { Prompt = "cancel this" },
				cancellation.Token))
			{
				chunks.Add(chunk);
				await cancellation.CancelAsync();
			}
		});

		Assert.Single(chunks);
	}

	[Fact]
	public async Task EmbeddingsAreDeterministicAndAlignedWithInputs()
	{
		await using var runtime = new FakeLocalModelRuntime();
		await runtime.LoadAsync(ModelId, NewOptions());

		var result = await runtime.EmbedAsync(
			new EmbeddingRequest { Inputs = ["alpha", "beta", "alpha"] });

		Assert.Equal(FakeLocalModelRuntime.EmbeddingDimensions, result.Dimensions);
		Assert.Equal(3, result.Vectors.Count);
		Assert.Equal(result.Vectors[0].ToArray(), result.Vectors[2].ToArray());
		Assert.NotEqual(result.Vectors[0].ToArray(), result.Vectors[1].ToArray());
		Assert.All(result.Vectors, vector => Assert.Equal(FakeLocalModelRuntime.EmbeddingDimensions, vector.Length));
	}

	[Fact]
	public async Task EmbeddingWithoutLoadedModelIsRejected()
	{
		await using var runtime = new FakeLocalModelRuntime();

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => runtime.EmbedAsync(new EmbeddingRequest { Inputs = ["alpha"] }));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task LoadRejectsEmptyModelId(string modelId)
	{
		await using var runtime = new FakeLocalModelRuntime();

		await Assert.ThrowsAsync<ArgumentException>(() => runtime.LoadAsync(modelId, NewOptions()));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task LoadRejectsInvalidContextWindow(int contextWindow)
	{
		await using var runtime = new FakeLocalModelRuntime();

		await Assert.ThrowsAnyAsync<ArgumentException>(
			() => runtime.LoadAsync(
				ModelId,
				new ModelLoadOptions { ModelPath = NewOptions().ModelPath, ContextWindow = contextWindow }));
	}

	[Theory]
	[InlineData("", 16, 0.8f)]
	[InlineData("   ", 16, 0.8f)]
	[InlineData("hello", 0, 0.8f)]
	[InlineData("hello", 16, -0.1f)]
	[InlineData("hello", 16, 2.1f)]
	public async Task GenerationRejectsInvalidRequests(string prompt, int maxTokens, float temperature)
	{
		await using var runtime = new FakeLocalModelRuntime();
		await runtime.LoadAsync(ModelId, NewOptions());

		await Assert.ThrowsAnyAsync<ArgumentException>(
			() => CollectTextAsync(runtime.GenerateAsync(new GenerationRequest
			{
				Prompt = prompt,
				MaxTokens = maxTokens,
				Temperature = temperature,
			})));
	}

	[Fact]
	public async Task EmbeddingRejectsEmptyInputs()
	{
		await using var runtime = new FakeLocalModelRuntime();
		await runtime.LoadAsync(ModelId, NewOptions());

		await Assert.ThrowsAsync<ArgumentException>(
			() => runtime.EmbedAsync(new EmbeddingRequest { Inputs = [] }));
		await Assert.ThrowsAsync<ArgumentException>(
			() => runtime.EmbedAsync(new EmbeddingRequest { Inputs = ["alpha", ""] }));
	}

	private static ModelLoadOptions NewOptions() => new()
	{
		ModelPath = Path.Combine(
			Path.GetTempPath(),
			"astra-tests",
			"models",
			$"{Guid.NewGuid():N}.gguf"),
	};

	private static async Task<List<GenerationChunk>> CollectAsync(
		IAsyncEnumerable<GenerationChunk> stream)
	{
		var chunks = new List<GenerationChunk>();

		await foreach (var chunk in stream)
		{
			chunks.Add(chunk);
		}

		return chunks;
	}

	private static async Task<string> CollectTextAsync(IAsyncEnumerable<GenerationChunk> stream) =>
		string.Concat((await CollectAsync(stream)).Select(chunk => chunk.Text));
}
