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
	];
}
