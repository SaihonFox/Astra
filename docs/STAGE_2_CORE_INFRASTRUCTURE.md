# Stage 2 — Core Infrastructure

- **Status:** In progress
- **Started:** 2026-11-18
- **Depends on:** Stage 1 — Foundation (completed)

## Goal

Provide reusable infrastructure that product subsystems need, without implementing Agent
Core, memory, tools or policy behavior. Everything here must be model-independent,
testable, and replaceable at its boundary.

## Scope

### In scope

1. Core contracts project (`Astra.Core`):
   - `IClock`;
   - `IEventBus` and `EventEnvelope<TEvent>`;
   - `ISettingsStore`.
2. Infrastructure project (`Astra.Infrastructure`):
   - `AstraPaths` — data root and storage area resolution;
   - SQLite connection factory and migration runner;
   - first migration: `settings` table;
   - SQLite settings store;
   - in-process event bus;
   - system clock.
3. Host composition:
   - data directory creation and database migration at startup;
   - database health check included in `/health`;
   - Serilog structured logging to console and rolling JSON files.
4. Tests for every component, including fresh-database and restart-persistence scenarios.
5. ADR 0006 (persistence) and ADR 0007 (eventing).

### Out of scope

- Policy Engine and permission enforcement — Stage 4 (Agent Core), requires owner approval.
- Task state, memory schema, knowledge base — later stages.
- IPC processes and transports — ADR 0005, not Stage 2.
- Public API endpoints beyond `/health`.
- Backup, encryption and restore.
- Desktop and Android.

## Components and files

```
src/Astra.Core/
  IClock.cs
  IEventBus.cs
  EventEnvelope.cs
  ISettingsStore.cs
src/Astra.Infrastructure/
  AstraPaths.cs
  Database/SqliteConnectionFactory.cs
  Database/DatabaseMigration.cs
  Database/DatabaseMigrator.cs
  Database/AstraMigrations.cs
  Database/SqliteSettingsStore.cs
  Events/InProcessEventBus.cs
  Time/SystemClock.cs
src/Astra.Host/
  Program.cs                  (composition and startup)
  Health/DatabaseHealthCheck.cs
tests/Astra.Core.Tests/
tests/Astra.Infrastructure.Tests/
tests/Astra.Host.Tests/
```

## Acceptance criteria

1. A fresh data directory produces a migrated SQLite database with WAL enabled.
2. Migrations are idempotent; editing an applied migration is rejected by checksum.
3. Settings survive a new connection and a new store instance.
4. Event bus delivers to all subscribers, supports unsubscribe, preserves order, and isolates
   handler failures.
5. `/health` returns 200 only when the database responds.
6. Serilog writes structured rolling logs under the data root; no secrets are logged.
7. `dotnet build` and `dotnet test` pass locally; CI passes on Ubuntu and Windows.
8. Documentation, ADRs and `PROJECT_STATUS.md` reflect the actual implementation.

## Security notes

- Settings are not a secret store.
- SQL is parameterized; migration SQL is checked for integrity.
- Data directory is owner-only on Unix.
- Event payloads are untrusted data and never grant permissions.

## Completion report

Produced after CI verification, in `docs/STAGE_2_CORE_INFRASTRUCTURE_REPORT.md`.
