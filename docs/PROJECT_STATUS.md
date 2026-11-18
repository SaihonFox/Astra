# Project Status

**Current stage:** Stage 2 — Core Infrastructure (in progress)
**Specification:** approved by project owner on 2026-11-18
**Implementation:** foundation plus core infrastructure (SQLite, migrations, settings, event bus, Serilog); product subsystems not started
**Last updated:** 2026-11-18

## Completed

### Stage 0
- Product concept and scope.
- Single-agent / multiple-local-models architecture decision.
- Platform and client direction.
- Autonomy and initiative policy.
- Memory and personalization direction.
- Tool and OS integration scope.
- Development environment scope.
- Security and permission levels.
- LAN-only remote access.
- Research and self-improvement direction.
- Deployment and operations scope.
- Plugins and Skills direction.
- Storage and backup direction.
- Resource management direction.
- Preliminary technology stack.
- Final Product Specification approved.

### Stage 1 — Foundation
- Toolchain verified on 2026-11-18: .NET SDK 10.0.115, Ubuntu 26.04, NuGet reachable.
- ADRs 0001–0005 accepted by the owner on 2026-11-18 (solution layout,
  configuration/secrets, logging/audit, testing, IPC).
- MIT license selected; `LICENSE` and `LICENSE-DECISION.md` added. Initial dependency
  licenses checked (Microsoft — MIT, xUnit — Apache-2.0).
- `AstraAgent.slnx` created with real projects only: `Astra.Host`, `Astra.Host.Tests`.
- Shared build settings: nullable, analyzers, warnings-as-errors, deterministic build,
  central package management, package lock files.
- Minimal host with `/health` endpoint bound to loopback in development.
- Integration test for the health endpoint.
- CI workflow for Ubuntu and Windows: locked restore, Release build, format verification,
  tests, TRX artifact upload.
- CI verified green on both platforms: run
  [44345679716](https://github.com/SaihonFox/Astra/actions/runs/44345679716).
- Line endings normalized to LF; `.editorconfig` corrected for YAML.
- Clean-copy verification: restore, build and tests succeed without `bin/obj`.
- [Foundation report](STAGE_1_FOUNDATION_REPORT.md) signed off.

### Stage 2 — Core Infrastructure (in progress)
- `Astra.Core` contracts: `IClock`, `IEventBus`, `EventEnvelope`, `ISettingsStore`.
- `Astra.Infrastructure`:
  - `AstraPaths` with data-root resolution, storage areas and owner-only Unix permissions;
  - SQLite connection factory with WAL, foreign keys, busy timeout and parameterized SQL;
  - checksum-verified append-only migration runner;
  - first migration creating the `settings` table;
  - SQLite settings store with key/value validation;
  - thread-safe in-process event bus with sequential, failure-isolated delivery;
  - system clock.
- Host composition: data directory and migrations at startup, database health check in
  `/health`, Serilog console + rolling compact JSON file logging.
- 25 tests across core, infrastructure and host.
- ADR 0006 (persistence) and ADR 0007 (eventing) drafted — **Proposed**, owner acceptance
  pending.
- Verified locally: database created with WAL, `settings` table and migration record;
  structured log file written; `/health` returns 200 only with a working database.
- Not yet verified: CI on the Stage 2 commit; Stage 2 report.

## Verification performed

Local environment, 2026-11-18, Ubuntu 26.04, SDK 10.0.115:

```
dotnet restore AstraAgent.slnx --locked-mode                  → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release          → 0 warnings, 0 errors
dotnet test AstraAgent.slnx --no-build -c Release             → 25 passed, 0 failed, 0 skipped
dotnet format AstraAgent.slnx --verify-no-changes             → exit 0
```

Runtime check with a temporary data root:

```
ASTRA_DATA_DIR=<temp> dotnet run --project src/Astra.Host
GET /health                                                   → 200 "Healthy"
database/astra.db                                             → journal_mode=wal,
                                                                schema_migrations + settings
logs/astra-<date>.jsonl                                       → compact JSON log lines
```

A clean copy without `bin/obj` was restored with `--locked-mode` and passed build and tests.

GitHub Actions run 44345679716 — **success** (Stage 1 commit):

- `build-test (ubuntu-latest)`: all steps successful.
- `build-test (windows-latest)`: all steps successful.

## Current work

Finish Stage 2:
- Owner acceptance of ADR 0006 and ADR 0007.
- Commit, push and verify CI on Ubuntu and Windows.
- Produce `docs/STAGE_2_CORE_INFRASTRUCTURE_REPORT.md`.
- Decide whether persisted settings get an API surface in a later stage.

Policy Engine is not part of Stage 2: the specification places it in Agent Core (Stage 4),
and changes to it require explicit owner approval.

## Not yet implemented

All product subsystems: Agent Core, model runtime, memory, tools, policy engine, API
surface, desktop UI, Android client, extensions and learning.

## Risks / decisions to validate during implementation
- Exact .NET 10 SDK and Avalonia compatibility at implementation time.
- llama.cpp packaging and Windows/Linux runtime strategy.
- GPU backend support and actual GTX 1650 compatibility.
- Vector search library and multilingual embedding model selection.
- Sandbox implementation per OS.
- IPC protocol details per boundary (ADR 0005 defines the strategy, not the wire format).
- Android LAN discovery and secure pairing.
- Voice stack and local speech models.
- LoRA/QLoRA feasibility on target hardware.
- Extension signing/trust model.
- CI actions are pinned by version tags, not commit SHAs; supply-chain hardening pending.
- Branch protection and required status checks are not configured yet.

## Reporting rule
Never mark an item completed without evidence. Record exact build/test commands and results.
