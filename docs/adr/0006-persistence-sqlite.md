# ADR-0006: Persistence baseline — SQLite, migrations, settings

- Status: Proposed
- Date: 2026-11-18
- Decision owners: Project owner

## Context

Astra needs durable local state: settings, task state, memory and audit data. The
specification mandates SQLite. Foundation has no persistence. Stage 2 must establish one
storage foundation without inventing the product data model, which belongs to later stages
(Memory, Agent Core, Security).

## Decision

1. Use SQLite through `Microsoft.Data.Sqlite` (MIT) with direct ADO.NET commands. No ORM is
   introduced at this stage; EF Core adds mapping and migration machinery that the current
   requirements do not justify.
2. `Astra.Host` is the only writer to the primary database in Stage 2. Other processes get
   their own storage or an API/IPC boundary; they do not open the host database directly.
3. Database file location: `<data root>/database/astra.db`, where the data root defaults to
   the OS local application-data directory plus `Astra`, and can be overridden with
   `ASTRA_DATA_DIR` or configuration key `Astra:DataDirectory`. Relative overrides are
   rejected.
4. Connection policy:
   - `PRAGMA journal_mode = WAL` once during initialization;
   - `PRAGMA foreign_keys = ON` and `PRAGMA synchronous = NORMAL` on each connection;
   - `busy_timeout = 5000` ms;
   - parameterized commands only.
5. Schema changes use an append-only ordered migration list. The runner:
   - creates `schema_migrations(version, name, checksum, applied_at)`;
   - applies pending migrations in one `BEGIN IMMEDIATE` transaction;
   - records a SHA-256 checksum of each migration's SQL and refuses to run if an applied
     migration was edited;
   - requires unique, contiguous versions starting at 1.
6. The first migration creates a `settings` table (`key`, `value`, `updated_at`, `STRICT`).
   Values are strings (JSON for structured values), limited to 64 KiB, and are never used
   for secrets.
7. Backup, encryption and restore are not implemented here. The single-file layout and
   checksum metadata keep them possible; backup must use SQLite's online backup mechanism,
   not a raw copy of a live WAL database.
8. The data directory is created with owner-only permissions on Unix. Windows ACL hardening
   is deferred to the security/storage stage.

## Alternatives considered

- **EF Core**: productive, but hides SQL and brings model/migration coupling before a domain
  model exists.
- **Dapper**: acceptable, but raw ADO.NET is sufficient for the current command set and adds
  no dependency.
- **LiteDB / JSON files**: rejected — not SQLite, weaker query and integrity story.
- **One database per module**: rejected — unnecessary operational complexity; module
  isolation is logical first.

## Consequences

- Later stages add tables through new migrations; applied migrations are immutable.
- Long-running writes need a busy strategy and short transactions.
- Schema evolution and checksum validation are testable independently of product features.

## Security implications

Parameterized SQL prevents injection. Settings are explicitly not a secret store (ADR 0002).
Owner-only directory permissions reduce local disclosure. SQLite is not a multi-user
security boundary; file access control is the boundary.

## Validation plan

- Integration tests against temporary databases: fresh migration, idempotent rerun, checksum
  mismatch rejection, settings persistence across connections.
- Host integration test asserts the database is created and `/health` reports it.
- WAL/foreign-key pragmas are asserted in tests where SQLite exposes them.
