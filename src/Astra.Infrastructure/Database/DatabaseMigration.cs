namespace Astra.Infrastructure.Database;

/// <summary>
/// One append-only schema migration.
/// </summary>
/// <param name="Version">Contiguous migration version starting at 1.</param>
/// <param name="Name">Human-readable migration name.</param>
/// <param name="Sql">SQL applied inside the migration transaction.</param>
public sealed record DatabaseMigration(int Version, string Name, string Sql);
