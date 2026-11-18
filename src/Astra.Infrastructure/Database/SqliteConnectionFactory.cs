using Microsoft.Data.Sqlite;

namespace Astra.Infrastructure.Database;

/// <summary>
/// Creates configured SQLite connections and applies per-connection pragmas.
/// </summary>
public sealed class SqliteConnectionFactory
{
	private const string SessionPragmas =
		"PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000; PRAGMA synchronous = NORMAL;";

	private readonly string _connectionString;

	/// <summary>Initializes a factory for the database under the supplied paths.</summary>
	/// <param name="paths">Resolved Astra storage paths.</param>
	public SqliteConnectionFactory(AstraPaths paths)
	{
		ArgumentNullException.ThrowIfNull(paths);
		paths.EnsureDirectories();

		_connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = paths.DatabaseFile,
			Mode = SqliteOpenMode.ReadWriteCreate,
			Pooling = true,
			DefaultTimeout = 5,
		}.ToString();
	}

	/// <summary>Opens a connection and applies session pragmas.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
	{
		var connection = new SqliteConnection(_connectionString);

		try
		{
			await connection.OpenAsync(cancellationToken);

			await using var command = connection.CreateCommand();
			command.CommandText = SessionPragmas;
			await command.ExecuteNonQueryAsync(cancellationToken);

			return connection;
		}
		catch
		{
			await connection.DisposeAsync();
			throw;
		}
	}
}
