# Changelog

## Unreleased

### Added
- Stage 3 model registry:
  - `Astra.Core` model contracts and validation (`ModelDescriptor`, capabilities, test
    status, availability, `IModelRegistry`);
  - migration 2 creating the `models` table with constraints and a name index;
  - `SqliteModelRegistry` with CRUD, duplicate protection, managed timestamps and live
    availability checks, wired into Host DI;
  - tests for registry round-trip, ordering, duplicates, updates, deletion, availability
    and validation.
- Stage 3 runtime contract:
  - `ILocalModelRuntime`, load options, generation/embedding requests, streamed chunks and
    shared validation rules in `Astra.Core.Inference`;
  - deterministic fake runtime and contract tests (load state, single active model,
    streaming, token limit, cancellation, embeddings, input validation);
  - suite now 59 tests.
- `docs/STAGE_3_MODEL_RUNTIME.md` plan and proposed ADR 0008.
- Stage 2 core infrastructure:
  - `Astra.Core` contracts (`IClock`, `IEventBus`, `EventEnvelope`, `ISettingsStore`);
  - SQLite persistence with WAL, checksum-verified migrations and settings table;
  - in-process event bus with failure-isolated delivery;
  - `AstraPaths` data-root and storage-area resolution;
  - host startup migration, database health check and Serilog logging.
- ADRs 0006 (persistence) and 0007 (eventing), accepted 2026-11-18.
- Tests for core contracts, infrastructure and host database initialization (25 total).
- `docs/STAGE_2_CORE_INFRASTRUCTURE.md` plan.
- `docs/STAGE_2_CORE_INFRASTRUCTURE_REPORT.md` (signed off).

### Changed
- Stage 3 — Model Runtime in progress; model registry completed, runtime not started.
- Stage 2 — Core Infrastructure completed and signed off; ADRs 0006/0007 accepted.
- CI verified green on Ubuntu and Windows for the Stage 2 commit (run 44348269884).
- Foundation solution `AstraAgent.slnx`.
- `Astra.Host` composition root with `/health` endpoint.
- `Astra.Host.Tests` integration test project.
- Shared build configuration: nullable, analyzers, warnings-as-errors, deterministic build,
  central package management, package lock files.
- CI workflow for Ubuntu and Windows (locked restore, build, format check, tests).
- ADRs 0001–0005 (accepted): solution layout, configuration/secrets, logging/audit,
  testing, IPC and process boundaries.
- MIT `LICENSE` and `LICENSE-DECISION.md`; initial dependency licenses checked.
- `global.json`, `.gitattributes`.
- `docs/STAGE_1_FOUNDATION_REPORT.md` (signed off).

### Changed
- Stage 1 — Foundation completed and signed off.
- CI verified green on Ubuntu and Windows (run 44345679716).
- Line endings normalized to LF; `.editorconfig` now uses LF and spaces for YAML.
- Development guide and project status updated with verified commands and results.
- CI verifies formatting in addition to build and tests.
- ADRs 0001–0005 moved to Accepted after owner approval.

### Earlier
- Initial documentation and coding-agent bootstrap package.
- Approved Final Product Specification added.
- Agent instructions, skills, roadmap, architecture and security guidance added.
