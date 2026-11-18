using Astra.Infrastructure.Database;

namespace Astra.Infrastructure.Tests;

public sealed class SqliteSettingsStoreTests
{
	[Fact]
	public async Task SettingsPersistAcrossStoreInstancesAndCanBeDeleted()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();

		var first = new SqliteSettingsStore(database.Factory);
		await first.SetAsync("ui.theme", "dark");

		var second = new SqliteSettingsStore(database.Factory);
		Assert.Equal("dark", await second.GetAsync("ui.theme"));

		await second.SetAsync("ui.theme", "light");
		Assert.Equal("light", await second.GetAsync("ui.theme"));

		Assert.True(await second.DeleteAsync("ui.theme"));
		Assert.Null(await first.GetAsync("ui.theme"));
		Assert.False(await first.DeleteAsync("ui.theme"));
	}

	[Fact]
	public async Task GetReturnsNullForMissingKey()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();

		var store = new SqliteSettingsStore(database.Factory);

		Assert.Null(await store.GetAsync("missing"));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task StoreRejectsEmptyKeys(string key)
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();

		var store = new SqliteSettingsStore(database.Factory);

		await Assert.ThrowsAsync<ArgumentException>(() => store.GetAsync(key));
	}

	[Fact]
	public async Task StoreRejectsOversizedKeyAndValue()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();

		var store = new SqliteSettingsStore(database.Factory);

		await Assert.ThrowsAsync<ArgumentException>(
			() => store.GetAsync(new string('k', 201)));
		await Assert.ThrowsAsync<ArgumentException>(
			() => store.SetAsync("key", new string('v', (64 * 1024) + 1)));
	}
}
