using Astra.Core.Inference;

namespace Astra.ModelHost.Tests;

public sealed class LlamaRuntimeTests : IClassFixture<LlamaModelHostFixture>
{
	private readonly LlamaModelHostFixture _fixture;

	public LlamaRuntimeTests(LlamaModelHostFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task MissingModelFileIsRejectedWithoutKillingTheProcess()
	{
		await ResetAsync();

		var path = Path.Combine(
			Path.GetTempPath(),
			$"astra-llama-{Guid.NewGuid():N}.gguf");

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _fixture.Client.LoadAsync(
				"missing-model",
				new ModelLoadOptions { ModelPath = path }));

		Assert.Contains("does not exist", exception.Message, StringComparison.OrdinalIgnoreCase);
		Assert.True(_fixture.Process.IsRunning);
		Assert.Null(_fixture.Client.LoadedModelId);

		// Any successful call proves the process still serves requests.
		await _fixture.Client.UnloadAsync();
	}

	[Fact]
	public async Task CorruptModelFileIsRejectedWithoutKillingTheProcess()
	{
		await ResetAsync();

		var path = Path.Combine(
			Path.GetTempPath(),
			$"astra-llama-{Guid.NewGuid():N}.gguf");

		try
		{
			await File.WriteAllBytesAsync(path, new byte[512]);

			var exception = await Assert.ThrowsAsync<InvalidOperationException>(
				() => _fixture.Client.LoadAsync(
					"corrupt-model",
					new ModelLoadOptions { ModelPath = path }));

			Assert.Contains(
				"could not load",
				exception.Message,
				StringComparison.OrdinalIgnoreCase);
			Assert.True(_fixture.Process.IsRunning);
			Assert.Null(_fixture.Client.LoadedModelId);

			await _fixture.Client.UnloadAsync();
		}
		finally
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}

	private async Task ResetAsync()
	{
		if (_fixture.Client.LoadedModelId is not null)
		{
			await _fixture.Client.UnloadAsync();
		}
	}
}
