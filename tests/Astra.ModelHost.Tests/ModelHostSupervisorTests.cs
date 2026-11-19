using System.Diagnostics;
using Astra.Core.Inference;
using Astra.Infrastructure;
using Astra.ModelHost.Client;

namespace Astra.ModelHost.Tests;

public sealed class ModelHostSupervisorTests
{
	[Fact]
	public async Task ProcessIsRestartedAndModelReloadedAfterCrash()
	{
		var root = CreateRoot();
		var supervisor = CreateSupervisor(root);

		try
		{
			await supervisor.LoadAsync("supervised-model", CreateLoadOptions());
			Assert.Equal("supervised-model", supervisor.LoadedModelId);
			Assert.True(supervisor.IsRunning);

			var firstProcessId = supervisor.ProcessId
				?? throw new InvalidOperationException("The supervised process is not running.");

			Assert.Equal(
				"echo(supervised-model):one",
				await CollectAsync(
					supervisor.GenerateAsync(new GenerationRequest { Prompt = "one" })));

			await KillAndWaitAsync(firstProcessId);
			await WaitUntilStoppedAsync(supervisor);

			Assert.Equal(
				"echo(supervised-model):two",
				await CollectAsync(
					supervisor.GenerateAsync(new GenerationRequest { Prompt = "two" })));

			Assert.True(supervisor.IsRunning);
			Assert.NotEqual(firstProcessId, supervisor.ProcessId);
			Assert.Equal(1, supervisor.RestartCount);
			Assert.Equal("supervised-model", supervisor.LoadedModelId);
		}
		finally
		{
			await supervisor.DisposeAsync();
			DeleteRoot(root);
		}
	}

	[Fact]
	public async Task UnloadedModelIsNotReloadedAfterCrash()
	{
		var root = CreateRoot();
		var supervisor = CreateSupervisor(root);

		try
		{
			await supervisor.LoadAsync("transient-model", CreateLoadOptions());
			await supervisor.UnloadAsync();

			var processId = supervisor.ProcessId
				?? throw new InvalidOperationException("The supervised process is not running.");

			await KillAndWaitAsync(processId);
			await WaitUntilStoppedAsync(supervisor);

			var exception = await Assert.ThrowsAsync<InvalidOperationException>(
				() => CollectAsync(
					supervisor.GenerateAsync(new GenerationRequest { Prompt = "after unload" })));

			Assert.Contains("No model is loaded", exception.Message, StringComparison.OrdinalIgnoreCase);
			Assert.Equal(1, supervisor.RestartCount);
			Assert.Null(supervisor.LoadedModelId);
		}
		finally
		{
			await supervisor.DisposeAsync();
			DeleteRoot(root);
		}
	}

	[Fact]
	public async Task DisposeStopsTheSupervisedProcess()
	{
		var root = CreateRoot();
		var supervisor = CreateSupervisor(root);

		try
		{
			await supervisor.LoadAsync("disposable-model", CreateLoadOptions());

			var processId = supervisor.ProcessId
				?? throw new InvalidOperationException("The supervised process is not running.");

			await supervisor.DisposeAsync();

			Assert.False(supervisor.IsRunning);
			Assert.False(IsAlive(processId));
		}
		finally
		{
			DeleteRoot(root);
		}
	}

	private static ModelHostSupervisor CreateSupervisor(string root) => new(
		new ModelHostSupervisorOptions
		{
			Paths = AstraPaths.Resolve(root),
			AssemblyPath = Path.Combine(
				AppContext.BaseDirectory,
				"modelhost",
				"Astra.ModelHost.dll"),
			Process = new ModelHostProcessOptions
			{
				InstanceName = "supervised",
				RuntimeName = "echo",
			},
			RestartDelay = TimeSpan.FromMilliseconds(50),
		});

	private static ModelLoadOptions CreateLoadOptions() => new()
	{
		ModelPath = Path.Combine(
			Path.GetTempPath(),
			$"{Guid.NewGuid():N}.gguf"),
	};

	private static async Task<string> CollectAsync(IAsyncEnumerable<GenerationChunk> stream)
	{
		var text = new System.Text.StringBuilder();

		await foreach (var chunk in stream)
		{
			text.Append(chunk.Text);
		}

		return text.ToString();
	}

	private static async Task KillAndWaitAsync(int processId)
	{
		using var process = Process.GetProcessById(processId);
		process.Kill(entireProcessTree: true);
		await process.WaitForExitAsync();
	}

	private static async Task WaitUntilStoppedAsync(ModelHostSupervisor supervisor)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

		while (supervisor.IsRunning && DateTime.UtcNow < deadline)
		{
			await Task.Delay(50);
		}

		Assert.False(supervisor.IsRunning);
	}

	private static bool IsAlive(int processId)
	{
		try
		{
			using var process = Process.GetProcessById(processId);
			return !process.HasExited;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	private static string CreateRoot() => Path.Combine(
		Path.GetTempPath(),
		"astra-mh",
		Guid.NewGuid().ToString("N")[..8]);

	private static void DeleteRoot(string root)
	{
		if (Directory.Exists(root))
		{
			Directory.Delete(root, recursive: true);
		}
	}
}
