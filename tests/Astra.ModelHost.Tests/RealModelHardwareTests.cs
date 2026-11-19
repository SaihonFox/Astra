using System.Diagnostics;
using System.Globalization;
using Astra.Core.Inference;
using Astra.Infrastructure;
using Astra.ModelHost.Client;
using Xunit.Abstractions;

namespace Astra.ModelHost.Tests;

/// <summary>
/// Manual, hardware-gated validation with a real GGUF model.
/// </summary>
/// <remarks>
/// CI leaves <c>ASTRA_TEST_MODEL</c> unset, so this test returns immediately. To run it, set
/// <c>ASTRA_TEST_MODEL</c> to an absolute GGUF path and optionally
/// <c>ASTRA_TEST_GPU_LAYERS</c> (for example <c>99</c>) and filter by the <c>Hardware</c>
/// trait. See <c>docs/STAGE_3_HARDWARE_VALIDATION.md</c>.
/// </remarks>
public sealed class RealModelHardwareTests
{
	private const int ContextWindow = 512;
	private const int GeneratedTokens = 8;

	private readonly ITestOutputHelper _output;

	public RealModelHardwareTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	[Trait("Category", "Hardware")]
	public async Task RealModelLoadsGeneratesAndEmbeds()
	{
		var modelPath = Environment.GetEnvironmentVariable("ASTRA_TEST_MODEL");

		if (string.IsNullOrWhiteSpace(modelPath))
		{
			_output.WriteLine(
				"ASTRA_TEST_MODEL is not set; skipping hardware validation. " +
				"This is expected in CI.");
			return;
		}

		var gpuLayers = ReadGpuLayers();
		var root = Path.Combine(
			Path.GetTempPath(),
			"astra-mh-hardware",
			Guid.NewGuid().ToString("N")[..8]);

		var process = await ModelHostProcess.StartAsync(
			AstraPaths.Resolve(root),
			Path.Combine(AppContext.BaseDirectory, "modelhost", "Astra.ModelHost.dll"),
			new ModelHostProcessOptions { InstanceName = "hardware" });

		try
		{
			using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
			var stopwatch = Stopwatch.StartNew();

			await process.Client.LoadAsync(
				"hardware-model",
				new ModelLoadOptions
				{
					ModelPath = modelPath,
					ContextWindow = ContextWindow,
					GpuLayerCount = gpuLayers,
				},
				timeout.Token);

			_output.WriteLine(
				$"load: {stopwatch.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s, " +
				$"gpu layers: {gpuLayers}");

			stopwatch.Restart();
			var generated = 0;
			var characters = 0;

			await foreach (var chunk in process.Client.GenerateAsync(
				new GenerationRequest
				{
					Prompt = "The capital of France is",
					MaxTokens = GeneratedTokens,
					Temperature = 0f,
					Seed = 42,
				},
				timeout.Token))
			{
				generated++;
				characters += chunk.Text.Length;
			}

			_output.WriteLine(
				$"generate: {generated} chunks, {characters} characters, " +
				$"{stopwatch.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s");

			stopwatch.Restart();
			var embeddings = await process.Client.EmbedAsync(
				new EmbeddingRequest { Inputs = ["hardware validation"] },
				timeout.Token);

			_output.WriteLine(
				$"embed: {embeddings.Dimensions} dimensions, " +
				$"{stopwatch.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s");

			Assert.True(generated > 0, "The model produced no tokens.");
			Assert.True(characters > 0, "The model produced empty output.");
			Assert.True(embeddings.Dimensions > 0, "The model produced no embedding dimensions.");
			Assert.Single(embeddings.Vectors);

			await process.Client.UnloadAsync(timeout.Token);
			Assert.Null(process.Client.LoadedModelId);
		}
		finally
		{
			await process.DisposeAsync();

			if (Directory.Exists(root))
			{
				Directory.Delete(root, recursive: true);
			}
		}
	}

	private static int ReadGpuLayers()
	{
		var value = Environment.GetEnvironmentVariable("ASTRA_TEST_GPU_LAYERS");

		if (string.IsNullOrWhiteSpace(value))
		{
			return 0;
		}

		if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var layers) ||
			layers < 0)
		{
			throw new InvalidOperationException(
				"ASTRA_TEST_GPU_LAYERS must be a non-negative integer.");
		}

		return layers;
	}
}
