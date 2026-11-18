# Stage 2 — Core Infrastructure Report

- **Status:** Implementation complete, CI verified; sign-off pending owner acceptance of
  ADR 0006 and ADR 0007
- **Date:** 2026-11-18
- **Environment:** Ubuntu 26.04, .NET SDK 10.0.115; GitHub Actions Ubuntu and Windows
  runners
- **Code commit:** `d06ecf5`
- **CI run:** [44348269884](https://github.com/SaihonFox/Astra/actions/runs/44348269884) —
  success on both platforms

## Scope delivered

- `Astra.Core` contracts:
  - `IClock`;
  - `IEventBus`;
  - `EventEnvelope<TEvent>` and non-generic factory with validation;
  - `ISettingsStore`.
- `Astra.Infrastructure`:
  - `AstraPaths` with absolute-path validation, storage areas and owner-only Unix
    permissions;
  - `SqliteConnectionFactory` with WAL, foreign keys, busy timeout, synchronous mode and
    pooled connections;
  - checksum-verified, append-only migration runner with atomic application and
    newer-schema protection;
  - first migration creating the `settings` table;
  - `SqliteSettingsStore` with parameterized SQL and input limits;
  - thread-safe `InProcessEventBus` with deterministic order and isolated handler failures;
  - `SystemClock`.
- Host composition:
  - data directory creation and migrations at startup;
  - `DatabaseHealthCheck` wired into `/health`;
  - Serilog console and rolling compact JSON logs.
- ADR 0006 (persistence) and ADR 0007 (eventing).
- 25 tests: 5 core, 19 infrastructure, 1 host integration.

## Acceptance criteria

| Criterion | Status | Evidence |
|---|---|---|
| Fresh data directory creates a migrated SQLite DB with WAL | Met | Runtime check + `MigrateCreatesSchemaAndEnablesWalAndForeignKeys` |
| Migrations idempotent; edited migration rejected | Met | `MigrateIsIdempotent`, `MigrateRejectsModifiedAppliedMigration`, newer-schema test |
| Settings survive a new connection/store | Met | `SettingsPersistAcrossStoreInstancesAndCanBeDeleted` |
| Event bus delivers, unsubscribes, preserves order, isolates failures | Met | Five event-bus tests |
| `/health` returns 200 only with a working database | Met | Host integration test + runtime check |
| Serilog writes structured rolling logs without secrets | Met | Runtime check: `logs/astra-20261118.jsonl` compact JSON |
| Local build/test and CI on Ubuntu/Windows pass | Met | 0 warnings/errors; 25 passed; CI run 44348269884 |
| Documentation, ADRs and status reflect implementation | Met | This report, ADR 0006/0007, README, DEVELOPMENT, ROADMAP, PROJECT_STATUS |

## Verification log

Local, Ubuntu 26.04, SDK 10.0.115:

```
dotnet restore AstraAgent.slnx --locked-mode           → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release   → 0 warnings, 0 errors
dotnet test AstraAgent.slnx --no-build -c Release      → 25 passed, 0 failed, 0 skipped
dotnet format AstraAgent.slnx --verify-no-changes      → exit 0
CI=true dotnet build -c Release                        → 0 warnings, 0 errors
```

Clean-copy verification without `.git`/`bin`/`obj`:

```
dotnet restore --locked-mode                           → succeeded
dotnet build -c Release                                → 0 warnings, 0 errors
dotnet test -c Release                                 → 25 passed
```

Runtime check with a temporary data root:

```
GET /health                                            → 200 "Healthy"
database/astra.db                                      → journal_mode=wal
tables                                                 → schema_migrations, settings
schema_migrations                                      → version 1, create-settings
logs/astra-20261118.jsonl                              → compact JSON events
```

CI run 44348269884 (commit `d06ecf5`), both jobs successful: locked restore, Release build,
format verification, 25 tests.

## Known limitations

- ADR 0006 and ADR 0007 are still **Proposed**; owner acceptance is required for sign-off.
- Settings have no API or UI surface yet; this is intentional and belongs to a later stage.
- Event bus is not durable and has no outbox; cross-process events remain future work.
- Backup, encryption and restore are not implemented.
- No policy/permission logic: the specification places the Policy Engine in Agent Core
  (Stage 4), and changes to it require explicit owner approval.

## Conclusion

Core infrastructure is implemented, tested and green in CI on both target platforms. After
owner acceptance of ADR 0006/0007, Stage 2 can be signed off and Stage 3 — Model Runtime can
begin.
