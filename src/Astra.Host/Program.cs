using System.Globalization;
using Astra.Core;
using Astra.Core.Models;
using Astra.Host.Health;
using Astra.Infrastructure;
using Astra.Infrastructure.Database;
using Astra.Infrastructure.Events;
using Astra.Infrastructure.Time;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var paths = AstraPaths.Resolve(builder.Configuration[AstraPaths.DataDirectoryConfigurationKey]);
paths.EnsureDirectories();

builder.Host.UseSerilog((context, _, configuration) => configuration
	.ReadFrom.Configuration(context.Configuration)
	.Enrich.FromLogContext()
	.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
	.WriteTo.File(
		new CompactJsonFormatter(),
		Path.Combine(paths.LogsDirectory, "astra-.jsonl"),
		rollingInterval: RollingInterval.Day,
		retainedFileCountLimit: 14,
		fileSizeLimitBytes: 64 * 1024 * 1024,
		rollOnFileSizeLimit: true));

builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
builder.Services.AddSingleton<SqliteConnectionFactory>();
builder.Services.AddSingleton<IReadOnlyList<DatabaseMigration>>(AstraMigrations.All);
builder.Services.AddSingleton<DatabaseMigrator>();
builder.Services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
builder.Services.AddSingleton<IModelRegistry, SqliteModelRegistry>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

await app.Services.GetRequiredService<DatabaseMigrator>().MigrateAsync();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program
{
}
