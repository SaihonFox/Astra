using Astra.Infrastructure.Database;
using Microsoft.Data.Sqlite;

namespace Astra.Infrastructure.Tests;

public sealed class DatabaseMigratorTests
{
	[Fact]
	public async Task MigrateCreatesSchemaAndEnablesWalAndForeignKeys()
	{
		using var database = new TemporaryDatabase();

		await database.Migrator.MigrateAsync();

		await using var connection = await database.Factory.OpenAsync();

		Assert.Equal("wal", await ScalarAsync(connection, "PRAGMA journal_mode;"));
		Assert.Equal(1L, await ScalarAsync(connection, "PRAGMA foreign_keys;"));
		Assert.Equal(
			1L,
			await ScalarAsync(
				connection,
				"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'settings';"));
		Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM schema_migrations;"));
	}

	[Fact]
	public async Task MigrateIsIdempotent()
	{
		using var database = new TemporaryDatabase();

		await database.Migrator.MigrateAsync();
		await database.Migrator.MigrateAsync();

		await using var connection = await database.Factory.OpenAsync();
		Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM schema_migrations;"));
	}

	[Fact]
	public async Task MigrateRejectsModifiedAppliedMigration()
	{
		using var database = new TemporaryDatabase(
			[new DatabaseMigration(1, "first", "CREATE TABLE first (id INTEGER);")]);
		await database.Migrator.MigrateAsync();

		var modified = new DatabaseMigrator(
			database.Factory,
			[new DatabaseMigration(1, "first", "CREATE TABLE changed (id INTEGER);")]);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => modified.MigrateAsync());
		Assert.Contains("modified", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task MigrateRejectsDatabaseWithNewerSchema()
	{
		using var database = new TemporaryDatabase(
			[
				new DatabaseMigration(1, "first", "CREATE TABLE first (id INTEGER);"),
				new DatabaseMigration(2, "second", "CREATE TABLE second (id INTEGER);"),
			]);
		await database.Migrator.MigrateAsync();

		var older = new DatabaseMigrator(
			database.Factory,
			[new DatabaseMigration(1, "first", "CREATE TABLE first (id INTEGER);")]);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => older.MigrateAsync());
		Assert.Contains("newer", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void ConstructorRejectsNonContiguousVersions()
	{
		using var database = new TemporaryDatabase();

		Assert.Throws<ArgumentException>(
			() => new DatabaseMigrator(
				database.Factory,
				[new DatabaseMigration(2, "second", "CREATE TABLE second (id INTEGER);")]));
	}

	private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		return await command.ExecuteScalarAsync();
	}
}
