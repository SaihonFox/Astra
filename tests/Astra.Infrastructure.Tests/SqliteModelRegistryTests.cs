using Astra.Core.Models;
using Astra.Infrastructure.Database;

namespace Astra.Infrastructure.Tests;

public sealed class SqliteModelRegistryTests
{
	private static readonly DateTimeOffset Now =
		new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task AddAndGetRoundTripAllFields()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();

		var clock = new TestClock(Now);
		var registry = new SqliteModelRegistry(database.Factory, clock);
		var testedAt = Now.AddHours(-1);

		var descriptor = CreateDescriptor() with
		{
			Quantization = "Q4_K_M",
			ContextWindow = 8192,
			Capabilities = ModelCapabilities.Chat | ModelCapabilities.Completion,
			Source = "https://example.invalid/model",
			License = "Apache-2.0",
			RequiredRamBytes = 6L * 1024 * 1024 * 1024,
			RequiredVramBytes = 2L * 1024 * 1024 * 1024,
			PreferredTasks = "chat, code",
			ModelVersion = "1.0",
			TestStatus = ModelTestStatus.Passed,
			TestedAt = testedAt,
			TestNotes = "Loaded and generated 32 tokens.",
			CreatedAt = DateTimeOffset.UnixEpoch,
			UpdatedAt = DateTimeOffset.UnixEpoch,
		};

		var stored = await registry.AddAsync(descriptor);

		Assert.Equal(Now, stored.CreatedAt);
		Assert.Equal(Now, stored.UpdatedAt);

		var loaded = await registry.GetAsync(descriptor.Id);

		Assert.NotNull(loaded);
		Assert.Equal(stored, loaded);
		Assert.Equal(testedAt, loaded.TestedAt);
		Assert.Equal(
			ModelCapabilities.Chat | ModelCapabilities.Completion,
			loaded.Capabilities);
	}

	[Fact]
	public async Task ListOrdersModelsByNameIgnoringCase()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();

		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));
		await registry.AddAsync(CreateDescriptor("zeta", "Zeta"));
		await registry.AddAsync(CreateDescriptor("alpha", "alpha"));
		await registry.AddAsync(CreateDescriptor("beta", "Beta"));

		var models = await registry.ListAsync();

		Assert.Equal(["alpha", "Beta", "Zeta"], models.Select(model => model.Name));
	}

	[Fact]
	public async Task AddRejectsDuplicateIdentifier()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();
		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));

		await registry.AddAsync(CreateDescriptor());

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => registry.AddAsync(CreateDescriptor(path: NewModelPath())));
		Assert.Contains("already registered", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task AddRejectsDuplicatePath()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();
		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));
		var path = NewModelPath();

		await registry.AddAsync(CreateDescriptor("first", "First", path));

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => registry.AddAsync(CreateDescriptor("second", "Second", path)));
		Assert.Contains("already used", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task UpdateReplacesMetadataAndPreservesCreationTime()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();

		var clock = new TestClock(Now);
		var registry = new SqliteModelRegistry(database.Factory, clock);
		var created = await registry.AddAsync(CreateDescriptor());

		clock.Advance(TimeSpan.FromMinutes(5));
		var updated = await registry.UpdateAsync(created with
		{
			Name = "Renamed",
			TestStatus = ModelTestStatus.Failed,
			TestNotes = "Out of memory.",
			CreatedAt = DateTimeOffset.UnixEpoch,
			UpdatedAt = DateTimeOffset.UnixEpoch,
		});

		Assert.Equal(Now, updated.CreatedAt);
		Assert.Equal(Now.AddMinutes(5), updated.UpdatedAt);
		Assert.Equal("Renamed", updated.Name);
		Assert.Equal(ModelTestStatus.Failed, updated.TestStatus);

		var loaded = await registry.GetAsync(created.Id);
		Assert.Equal(updated, loaded);
	}

	[Fact]
	public async Task UpdateRejectsUnknownModel()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();
		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));

		await Assert.ThrowsAsync<KeyNotFoundException>(
			() => registry.UpdateAsync(CreateDescriptor()));
	}

	[Fact]
	public async Task UpdateRejectsPathUsedByAnotherModel()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();
		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));

		var first = await registry.AddAsync(CreateDescriptor("first", "First"));
		var second = await registry.AddAsync(CreateDescriptor("second", "Second"));

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => registry.UpdateAsync(first with { Path = second.Path }));
	}

	[Fact]
	public async Task DeleteRemovesOnlyExistingEntries()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();
		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));

		await registry.AddAsync(CreateDescriptor());

		Assert.True(await registry.DeleteAsync("test-model"));
		Assert.False(await registry.DeleteAsync("test-model"));
		Assert.Null(await registry.GetAsync("test-model"));
	}

	[Fact]
	public async Task GetReturnsNullForUnknownModel()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();
		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));

		Assert.Null(await registry.GetAsync("unknown"));
	}

	[Fact]
	public async Task CheckAvailabilityDetectsMissingAndExistingFiles()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();
		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));

		var path = Path.Combine(database.Paths.Root, "test-model.gguf");
		await registry.AddAsync(CreateDescriptor(path: path));

		Assert.Equal(ModelAvailability.Missing, await registry.CheckAvailabilityAsync("test-model"));

		await File.WriteAllTextAsync(path, "not a real model");
		Assert.Equal(ModelAvailability.Available, await registry.CheckAvailabilityAsync("test-model"));

		await Assert.ThrowsAsync<KeyNotFoundException>(
			() => registry.CheckAvailabilityAsync("unknown"));
	}

	[Theory]
	[InlineData("Invalid")]
	[InlineData("-leading")]
	[InlineData("has space")]
	[InlineData("")]
	public async Task AddRejectsInvalidIdentifiers(string id)
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();
		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));

		await Assert.ThrowsAsync<ArgumentException>(
			() => registry.AddAsync(CreateDescriptor(id)));
	}

	[Fact]
	public async Task AddRejectsInvalidDescriptors()
	{
		using var database = new TemporaryDatabase();
		await database.Migrator.MigrateAsync();
		var registry = new SqliteModelRegistry(database.Factory, new TestClock(Now));
		var valid = CreateDescriptor();

		await Assert.ThrowsAsync<ArgumentException>(
			() => registry.AddAsync(valid with { Name = " " }));
		await Assert.ThrowsAsync<ArgumentException>(
			() => registry.AddAsync(valid with { Path = "relative/model.gguf" }));
		await Assert.ThrowsAsync<ArgumentException>(
			() => registry.AddAsync(valid with { Path = NewModelPath().Replace(".gguf", ".bin", StringComparison.Ordinal) }));
		await Assert.ThrowsAsync<ArgumentException>(
			() => registry.AddAsync(valid with { SizeBytes = -1 }));
		await Assert.ThrowsAsync<ArgumentException>(
			() => registry.AddAsync(valid with { ContextWindow = 0 }));
		await Assert.ThrowsAsync<ArgumentException>(
			() => registry.AddAsync(valid with { Capabilities = (ModelCapabilities)1024 }));
	}

	private static string NewModelPath() =>
		Path.Combine(
			Path.GetTempPath(),
			"astra-tests",
			"models",
			$"{Guid.NewGuid():N}.gguf");

	private static ModelDescriptor CreateDescriptor(
		string id = "test-model",
		string name = "Test Model",
		string? path = null) => new()
		{
			Id = id,
			Name = name,
			Path = path ?? NewModelPath(),
			SizeBytes = 4L * 1024 * 1024 * 1024,
		};
}
