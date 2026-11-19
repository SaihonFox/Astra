using Astra.Core.Inference;
using Astra.Infrastructure;
using Astra.ModelHost.Client;

namespace Astra.ModelHost.Tests;

public sealed class ModelHostLimitsTests
{
	[Fact]
	public async Task ModelLargerThanTheConfiguredLimitIsRejected()
	{
		var root = Path.Combine(
			Path.GetTempPath(),
			"astra-mh",
			Guid.NewGuid().ToString("N")[..8]);

		var modelPath = Path.Combine(
			Path.GetTempPath(),
			$"astra-limit-{Guid.NewGuid():N}.gguf");

		var process = await ModelHostProcess.StartAsync(
			AstraPaths.Resolve(root),
			Path.Combine(AppContext.BaseDirectory, "modelhost", "Astra.ModelHost.dll"),
			new ModelHostProcessOptions
			{
				InstanceName = "limits",
				MaxModelBytes = 1024,
			});

		try
		{
			await File.WriteAllBytesAsync(modelPath, new byte[2048]);

			var exception = await Assert.ThrowsAsync<InvalidOperationException>(
				() => process.Client.LoadAsync(
					"oversized-model",
					new ModelLoadOptions { ModelPath = modelPath }));

			Assert.Contains(
				"exceeds the configured limit",
				exception.Message,
				StringComparison.OrdinalIgnoreCase);

			Assert.True(process.IsRunning);
			Assert.Null(process.Client.LoadedModelId);
		}
		finally
		{
			await process.DisposeAsync();

			if (File.Exists(modelPath))
			{
				File.Delete(modelPath);
			}

			if (Directory.Exists(root))
			{
				Directory.Delete(root, recursive: true);
			}
		}
	}

	[Fact]
	public async Task StartRejectsNonPositiveModelSizeLimit()
	{
		var options = new ModelHostProcessOptions { MaxModelBytes = 0 };

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => ModelHostProcess.StartAsync(
				AstraPaths.Resolve(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
				Path.Combine(AppContext.BaseDirectory, "modelhost", "Astra.ModelHost.dll"),
				options));
	}

	[Fact]
	public void SupervisorRejectsInvalidRestartLimits()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new ModelHostSupervisor(new ModelHostSupervisorOptions
			{
				Paths = AstraPaths.Resolve(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
				AssemblyPath = Path.Combine(
					AppContext.BaseDirectory,
					"modelhost",
					"Astra.ModelHost.dll"),
				MaxRestartAttempts = 0,
			}));

		Assert.Throws<ArgumentOutOfRangeException>(
			() => new ModelHostSupervisor(new ModelHostSupervisorOptions
			{
				Paths = AstraPaths.Resolve(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
				AssemblyPath = Path.Combine(
					AppContext.BaseDirectory,
					"modelhost",
					"Astra.ModelHost.dll"),
				RestartDelay = TimeSpan.FromSeconds(-1),
			}));
	}
}
