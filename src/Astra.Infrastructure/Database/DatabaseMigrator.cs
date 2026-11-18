using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Astra.Infrastructure.Database;

/// <summary>
/// Applies append-only schema migrations atomically and verifies their integrity.
/// </summary>
public sealed class DatabaseMigrator
{
	private const string MigrationsTableSql =
		"""
		CREATE TABLE IF NOT EXISTS schema_migrations (
			version INTEGER NOT NULL PRIMARY KEY,
			name TEXT NOT NULL,
			checksum TEXT NOT NULL,
			applied_at TEXT NOT NULL
		) STRICT;
		""";

	private readonly SqliteConnectionFactory _connectionFactory;
	private readonly IReadOnlyList<DatabaseMigration> _migrations;

	/// <summary>Initializes a migrator.</summary>
	/// <param name="connectionFactory">Connection factory.</param>
	/// <param name="migrations">Ordered migrations. Versions must be unique and contiguous from 1.</param>
	/// <exception cref="ArgumentException">The migration list is invalid.</exception>
	public DatabaseMigrator(
		SqliteConnectionFactory connectionFactory,
		IReadOnlyList<DatabaseMigration> migrations)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_migrations = ValidateMigrations(migrations);
	}

	/// <summary>Applies all pending migrations in a single immediate transaction.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// An applied migration was edited, or the database schema is newer than the application.
	/// </exception>
	public async Task MigrateAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

		await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", transaction: null, cancellationToken);

		await using var transaction = connection.BeginTransaction(deferred: false);

		await ExecuteAsync(connection, MigrationsTableSql, transaction, cancellationToken);

		var applied = await ReadAppliedAsync(connection, transaction, cancellationToken);
		var knownVersions = _migrations.Select(migration => migration.Version).ToHashSet();

		var unknownVersions = applied.Keys.Where(version => !knownVersions.Contains(version)).ToArray();
		if (unknownVersions.Length > 0)
		{
			throw new InvalidOperationException(
				$"The database schema is newer than this application: unknown migration(s) " +
				$"{string.Join(", ", unknownVersions)}.");
		}

		foreach (var migration in _migrations)
		{
			var checksum = ComputeChecksum(migration.Sql);

			if (applied.TryGetValue(migration.Version, out var appliedChecksum))
			{
				if (!string.Equals(appliedChecksum, checksum, StringComparison.Ordinal))
				{
					throw new InvalidOperationException(
						$"Migration {migration.Version} ({migration.Name}) was modified after it was applied.");
				}

				continue;
			}

			await ExecuteAsync(connection, migration.Sql, transaction, cancellationToken);
			await InsertAppliedAsync(connection, transaction, migration, checksum, cancellationToken);
		}

		await transaction.CommitAsync(cancellationToken);
	}

	private static DatabaseMigration[] ValidateMigrations(
		IReadOnlyList<DatabaseMigration> migrations)
	{
		ArgumentNullException.ThrowIfNull(migrations);

		var ordered = migrations.OrderBy(migration => migration.Version).ToArray();

		for (var index = 0; index < ordered.Length; index++)
		{
			var migration = ordered[index];

			if (migration.Version != index + 1)
			{
				throw new ArgumentException(
					"Migration versions must be unique and contiguous starting at 1.",
					nameof(migrations));
			}

			if (string.IsNullOrWhiteSpace(migration.Name))
			{
				throw new ArgumentException(
					$"Migration {migration.Version} has an empty name.",
					nameof(migrations));
			}

			if (string.IsNullOrWhiteSpace(migration.Sql))
			{
				throw new ArgumentException(
					$"Migration {migration.Version} has empty SQL.",
					nameof(migrations));
			}
		}

		return ordered;
	}

	private static async Task<Dictionary<int, string>> ReadAppliedAsync(
		SqliteConnection connection,
		SqliteTransaction transaction,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "SELECT version, checksum FROM schema_migrations;";

		var applied = new Dictionary<int, string>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			applied[reader.GetInt32(0)] = reader.GetString(1);
		}

		return applied;
	}

	private static async Task InsertAppliedAsync(
		SqliteConnection connection,
		SqliteTransaction transaction,
		DatabaseMigration migration,
		string checksum,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText =
			"""
			INSERT INTO schema_migrations (version, name, checksum, applied_at)
			VALUES ($version, $name, $checksum, $appliedAt);
			""";
		command.Parameters.AddWithValue("$version", migration.Version);
		command.Parameters.AddWithValue("$name", migration.Name);
		command.Parameters.AddWithValue("$checksum", checksum);
		command.Parameters.AddWithValue(
			"$appliedAt",
			DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task ExecuteAsync(
		SqliteConnection connection,
		string sql,
		SqliteTransaction? transaction,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = sql;
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static string ComputeChecksum(string sql) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
}
