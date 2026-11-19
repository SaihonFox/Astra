using Astra.Infrastructure;
using Astra.ModelHost.Client;

namespace Astra.ModelHost.Tests;

public sealed class ModelHostLifecycleTests
{
	[Fact]
	public async Task StopAsyncShutsDownTheProcess()
	{
		var root = CreateRoot();

		var process = await ModelHostProcess.StartAsync(
			AstraPaths.Resolve(root),
			ModelHostAssemblyPath(),
			"lifecycle",
			runtimeName: "echo");

		try
		{
			Assert.True(process.IsRunning);

			await process.StopAsync();

			Assert.False(process.IsRunning);
		}
		finally
		{
			await process.DisposeAsync();
			DeleteRoot(root);
		}
	}

	[Fact]
	public async Task StartAsyncRejectsMissingExecutable()
	{
		var root = CreateRoot();
		var missing = Path.Combine(root, "missing", "Astra.ModelHost.dll");

		try
		{
			var exception = await Assert.ThrowsAsync<InvalidOperationException>(
				() => ModelHostProcess.StartAsync(
					AstraPaths.Resolve(root),
					missing,
					"missing",
					runtimeName: "echo"));

			Assert.Contains("was not found", exception.Message, StringComparison.OrdinalIgnoreCase);
			Assert.False(File.Exists(missing));
		}
		finally
		{
			DeleteRoot(root);
		}
	}

	[Fact]
	public async Task StartAsyncRejectsSecondInstanceOnTheSameEndpoint()
	{
		var root = CreateRoot();
		var paths = AstraPaths.Resolve(root);

		var process = await ModelHostProcess.StartAsync(
			paths,
			ModelHostAssemblyPath(),
			"duplicate",
			runtimeName: "echo");

		try
		{
			var exception = await Assert.ThrowsAsync<InvalidOperationException>(
				() => ModelHostProcess.StartAsync(
					paths,
					ModelHostAssemblyPath(),
					"duplicate",
					runtimeName: "echo"));

			Assert.Contains("already", exception.Message, StringComparison.OrdinalIgnoreCase);
			Assert.True(process.IsRunning);
		}
		finally
		{
			await process.DisposeAsync();
			DeleteRoot(root);
		}
	}

	private static string CreateRoot() => Path.Combine(
		Path.GetTempPath(),
		"astra-mh",
		Guid.NewGuid().ToString("N")[..8]);

	private static string ModelHostAssemblyPath() => Path.Combine(
		AppContext.BaseDirectory,
		"modelhost",
		"Astra.ModelHost.dll");

	private static void DeleteRoot(string root)
	{
		if (Directory.Exists(root))
		{
			Directory.Delete(root, recursive: true);
		}
	}
}
