namespace Astra.Infrastructure.Database;

/// <summary>
/// Built-in schema migrations. Applied migrations are immutable; add a new version instead.
/// </summary>
public static class AstraMigrations
{
	/// <summary>Gets all built-in migrations ordered by version.</summary>
	public static IReadOnlyList<DatabaseMigration> All { get; } =
	[
		new DatabaseMigration(
			1,
			"create-settings",
			"""
			CREATE TABLE settings (
				key TEXT NOT NULL PRIMARY KEY,
				value TEXT NOT NULL,
				updated_at TEXT NOT NULL
			) STRICT;
			"""),
		new DatabaseMigration(
			2,
			"create-models",
			"""
			CREATE TABLE models (
				id TEXT NOT NULL PRIMARY KEY,
				name TEXT NOT NULL,
				path TEXT NOT NULL UNIQUE,
				format TEXT NOT NULL,
				quantization TEXT NULL,
				size_bytes INTEGER NOT NULL,
				context_window INTEGER NULL,
				capabilities INTEGER NOT NULL,
				source TEXT NULL,
				license TEXT NULL,
				required_ram_bytes INTEGER NULL,
				required_vram_bytes INTEGER NULL,
				preferred_tasks TEXT NULL,
				model_version TEXT NULL,
				test_status TEXT NOT NULL,
				tested_at TEXT NULL,
				test_notes TEXT NULL,
				created_at TEXT NOT NULL,
				updated_at TEXT NOT NULL,
				CHECK (length(id) > 0),
				CHECK (length(name) > 0),
				CHECK (length(path) > 0),
				CHECK (format IN ('gguf')),
				CHECK (size_bytes >= 0),
				CHECK (context_window IS NULL OR context_window > 0),
				CHECK (capabilities >= 0),
				CHECK (required_ram_bytes IS NULL OR required_ram_bytes >= 0),
				CHECK (required_vram_bytes IS NULL OR required_vram_bytes >= 0),
				CHECK (test_status IN ('untested', 'passed', 'failed'))
			) STRICT;

			CREATE INDEX ix_models_name ON models (name COLLATE NOCASE);
			"""),
	];
}
