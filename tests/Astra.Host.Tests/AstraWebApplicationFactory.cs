using Astra.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Astra.Host.Tests;

public sealed class AstraWebApplicationFactory : WebApplicationFactory<Program>
{
	public string DataDirectory { get; } =
		Path.Combine(Path.GetTempPath(), "astra-host-tests", Guid.NewGuid().ToString("N"));

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.UseSetting(AstraPaths.DataDirectoryConfigurationKey, DataDirectory);
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		SqliteConnection.ClearAllPools();

		if (Directory.Exists(DataDirectory))
		{
			Directory.Delete(DataDirectory, recursive: true);
		}
	}
}
