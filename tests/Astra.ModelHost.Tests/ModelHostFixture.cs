using Astra.Infrastructure;
using Astra.ModelHost.Client;

namespace Astra.ModelHost.Tests;

public sealed class ModelHostFixture : IAsyncLifetime
{
	private string _root = string.Empty;

	public ModelHostProcess Process { get; private set; } = null!;

	public ModelHostClient Client => Process.Client;

	public async Task InitializeAsync()
	{
		_root = Path.Combine(
			Path.GetTempPath(),
			"astra-mh",
			Guid.NewGuid().ToString("N")[..8]);

		var assemblyPath = Path.Combine(
			AppContext.BaseDirectory,
			"modelhost",
			"Astra.ModelHost.dll");

		Assert.True(
			File.Exists(assemblyPath),
			$"ModelHost assembly was not copied to '{assemblyPath}'.");

		Process = await ModelHostProcess.StartAsync(
			AstraPaths.Resolve(_root),
			assemblyPath,
			"test",
			runtimeName: "echo");
	}

	public async Task DisposeAsync()
	{
		await Process.DisposeAsync();

		if (Directory.Exists(_root))
		{
			Directory.Delete(_root, recursive: true);
		}
	}
}
