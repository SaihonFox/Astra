using Astra.Core.Inference;
using Astra.ModelHost.Contracts;

namespace Astra.ModelHost.Tests;

public sealed class ModelHostIpcTests : IClassFixture<ModelHostFixture>
{
	private readonly ModelHostFixture _fixture;

	public ModelHostIpcTests(ModelHostFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task HandshakeReportsProtocolRuntimeAndProcess()
	{
		await ResetAsync();

		Assert.Equal(ModelHostProtocol.Version, _fixture.Client.Info.ProtocolVersion);
		Assert.Equal("echo", _fixture.Client.Info.RuntimeName);
		Assert.Equal(_fixture.Process.ProcessId, _fixture.Client.Info.ProcessId);
		Assert.True(_fixture.Process.IsRunning);
	}

	[Fact]
	public async Task LoadGenerateAndUnloadRoundTrip()
	{
		await ResetAsync();
		await _fixture.Client.LoadAsync("test-model", CreateOptions());

		Assert.Equal("test-model", _fixture.Client.LoadedModelId);

		var chunks = await CollectAsync(
			_fixture.Client.GenerateAsync(new GenerationRequest { Prompt = "hello" }));

		Assert.Equal("echo(test-model):hello", string.Concat(chunks.Select(chunk => chunk.Text)));
		Assert.All(chunks, chunk => Assert.NotEmpty(chunk.Text));

		await _fixture.Client.UnloadAsync();
		Assert.Null(_fixture.Client.LoadedModelId);
	}

	[Fact]
	public async Task LoadingSecondModelIsRejected()
	{
		await ResetAsync();
		await _fixture.Client.LoadAsync("first-model", CreateOptions());

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _fixture.Client.LoadAsync("second-model", CreateOptions()));
		Assert.Contains("already loaded", exception.Message, StringComparison.OrdinalIgnoreCase);

		await _fixture.Client.UnloadAsync();
	}

	[Fact]
	public async Task GenerationRequiresLoadedModel()
	{
		await ResetAsync();

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => CollectAsync(
				_fixture.Client.GenerateAsync(new GenerationRequest { Prompt = "hello" })));
	}

	[Fact]
	public async Task EmbeddingsAreReturnedWithDeclaredDimensions()
	{
		await ResetAsync();
		await _fixture.Client.LoadAsync("embedding-model", CreateOptions());

		var result = await _fixture.Client.EmbedAsync(
			new EmbeddingRequest { Inputs = ["alpha", "beta", "alpha"] });

		Assert.Equal(8, result.Dimensions);
		Assert.Equal(3, result.Vectors.Count);
		Assert.Equal(result.Vectors[0].ToArray(), result.Vectors[2].ToArray());
		Assert.NotEqual(result.Vectors[0].ToArray(), result.Vectors[1].ToArray());
		Assert.All(result.Vectors, vector => Assert.Equal(8, vector.Length));

		await _fixture.Client.UnloadAsync();
	}

	[Fact]
	public async Task CancellationStopsStreaming()
	{
		await ResetAsync();
		await _fixture.Client.LoadAsync("test-model", CreateOptions());

		using var cancellation = new CancellationTokenSource();
		var chunks = new List<GenerationChunk>();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			await foreach (var chunk in _fixture.Client.GenerateAsync(
				new GenerationRequest { Prompt = "cancel this stream" },
				cancellation.Token))
			{
				chunks.Add(chunk);
				await cancellation.CancelAsync();
			}
		});

		Assert.Single(chunks);
		await _fixture.Client.UnloadAsync();
	}

	[Fact]
	public async Task ClientRejectsRelativeModelPath()
	{
		await ResetAsync();

		await Assert.ThrowsAsync<ArgumentException>(
			() => _fixture.Client.LoadAsync(
				"test-model",
				new ModelLoadOptions { ModelPath = "relative/model.gguf" }));
	}

	private async Task ResetAsync()
	{
		if (_fixture.Client.LoadedModelId is not null)
		{
			await _fixture.Client.UnloadAsync();
		}
	}

	private static ModelLoadOptions CreateOptions() => new()
	{
		ModelPath = Path.Combine(
			Path.GetTempPath(),
			"astra-mh",
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
}
