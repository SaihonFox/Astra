using Astra.Infrastructure;
using Astra.ModelHost.Client;

namespace Astra.ModelHost.Tests;

/// <summary>
/// Starts ModelHost with the LLamaSharp runtime. Loads no model, so CI needs no model
/// downloads and no GPU; the native CPU backend is exercised only for failure paths.
/// </summary>
public sealed class LlamaModelHostFixture : IAsyncLifetime
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

		Process = await ModelHostProcess.StartAsync(
			AstraPaths.Resolve(_root),
			assemblyPath,
			new ModelHostProcessOptions { InstanceName = "llama" });
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
