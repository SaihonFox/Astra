using Astra.Infrastructure.Database;
using Microsoft.Data.Sqlite;

namespace Astra.Infrastructure.Tests;

internal sealed class TemporaryDatabase : IDisposable
{
	private readonly string _root;

	public TemporaryDatabase(IReadOnlyList<DatabaseMigration>? migrations = null)
	{
		_root = Path.Combine(Path.GetTempPath(), "astra-tests", Guid.NewGuid().ToString("N"));
		Paths = AstraPaths.Resolve(_root);
		Paths.EnsureDirectories();
		Factory = new SqliteConnectionFactory(Paths);
		Migrator = new DatabaseMigrator(Factory, migrations ?? AstraMigrations.All);
	}

	public AstraPaths Paths { get; }

	public SqliteConnectionFactory Factory { get; }

	public DatabaseMigrator Migrator { get; }

	public void Dispose()
	{
		SqliteConnection.ClearAllPools();

		if (Directory.Exists(_root))
		{
			Directory.Delete(_root, recursive: true);
		}
	}
}
