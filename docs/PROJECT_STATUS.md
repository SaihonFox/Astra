# Project Status

**Current stage:** Stage 3 — Model Runtime (in progress)
**Specification:** approved by project owner on 2026-11-18
**Implementation:** foundation and core infrastructure complete; model registry, runtime contract and ModelHost IPC implemented; real inference pending
**Last updated:** 2026-11-19

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

### Stage 2 — Core Infrastructure
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
- ADR 0006 (persistence) and ADR 0007 (eventing) accepted by the owner on 2026-11-18.
- CI green on Ubuntu and Windows: run
  [44348269884](https://github.com/SaihonFox/Astra/actions/runs/44348269884).
- [Stage 2 report](STAGE_2_CORE_INFRASTRUCTURE_REPORT.md) signed off.

### Stage 3 — Model Runtime (in progress)
- ADR 0008 accepted by the owner on 2026-11-18: LLamaSharp as the llama.cpp integration
  inside a dedicated `Astra.ModelHost` process, CPU baseline and optional CUDA 12.
- Model registry contracts in `Astra.Core`: `ModelDescriptor`, `ModelFormat`,
  `ModelCapabilities`, `ModelTestStatus`, `ModelAvailability`, validator and
  `IModelRegistry`.
- Migration 2 creates the `models` table with strict constraints, uniqueness on
  identifier/path and a name index.
- `SqliteModelRegistry`: CRUD, duplicate protection, registry-managed timestamps and live
  file availability checks; registered in Host DI.
- Runtime contract in `Astra.Core.Inference`: `ILocalModelRuntime`, load options,
  generation and embedding requests, streamed chunks, shared validation rules.
- Deterministic fake runtime and contract tests: load/unload/dispose, single active model,
  streamed generation, max-token limit, cancellation, deterministic embeddings and input
  validation.
- ModelHost IPC (phase 3.4):
  - `Astra.ModelHost.Contracts`: proto3 contract `astra.modelhost.v1`, protocol version
    handshake, validated local endpoint types (`pipe:` / `unix:`).
  - `Astra.ModelHost`: Kestrel HTTP/2 listener on a named pipe (Windows) or Unix domain
    socket (Linux), gRPC service over `ILocalModelRuntime`, bounded message size, graceful
    shutdown. It currently runs a deterministic echo runtime instead of llama.cpp.
  - `Astra.ModelHost.Client`: `ModelHostClient` implementing `ILocalModelRuntime` with
    version and process-id checks plus error translation; `ModelHostProcess` for start,
    handshake wait, stale-socket cleanup and bounded stop/kill.
  - Security: pipe created with `PipeOptions.CurrentUserOnly`; socket inside a `0700`
    `ipc` directory; no TCP listener. Details in [MODEL_HOST_IPC.md](MODEL_HOST_IPC.md).
  - 19 IPC, runtime-selection and lifecycle tests against real child processes.
- LLamaSharp adapter (phase 3.5) confined to ModelHost:
  - LLamaSharp 0.30.0 with the CPU backend; only the build host's native runtime is copied.
  - model load/unload, streaming generation with temperature/seed/token limits and
    embeddings; operations are serialized because llama.cpp contexts are not thread-safe.
  - missing and corrupt GGUF files fail as managed errors while the process stays alive.
  - real inference validated on Linux CPU with Qwen3-4B-Q4_K_M (2.4 GB, context 512):
    load 7.5 s, 16 tokens in 3.9 s, 2560-dimension embeddings in 0.6 s.
  - `--runtime llama` is the default; `--runtime echo` remains for tests and diagnostics.
- Supervision and basic admission (phase 3.7):
  - `ModelHostSupervisor` (client library) starts ModelHost on demand, restarts it with
    bounded backoff after a crash and reloads the active model.
  - Verified by killing the child process: the next generation restarts, reloads and streams.
  - `--max-model-bytes` rejects oversized model files before native loading; context, token,
    prompt, embedding-batch and GPU-layer limits are enforced by shared validation.
- Hardware validation:
  - `RealModelHardwareTests` (trait `Hardware`) runs only when `ASTRA_TEST_MODEL` is set and
    is a no-op in CI; validated here on Linux CPU with Qwen3-4B-Q4_K_M.
  - CUDA 12 is an opt-in build (`-p:UseCuda=true`) and is not in the default build or lock
    file. It ships only the llama.cpp libraries, so the CUDA 12 toolkit must be installed
    for `cudart64_12.dll`/`cublas64_12.dll` and `CUDA_PATH` detection. Windows CPU and
    GTX 1650 checks are documented in
    [STAGE_3_HARDWARE_VALIDATION.md](STAGE_3_HARDWARE_VALIDATION.md).
  - Model load logs `GPU offload supported` and the requested GPU layer count, so hardware
    runs show whether CUDA was actually selected.
- 85 tests total (23 core, 34 infrastructure, 2 host, 26 ModelHost).
- Verified locally: fresh database applies migrations 1–2, exposes `models`, and `/health`
  is healthy.
- CI green on Ubuntu and Windows for the registry commit: run
  [44351727206](https://github.com/SaihonFox/Astra/actions/runs/44351727206).
- CI green on Ubuntu and Windows for the runtime-contract commit: run
  [44352946810](https://github.com/SaihonFox/Astra/actions/runs/44352946810).
- CI green on Ubuntu and Windows for the ModelHost IPC commit: run
  [44460539305](https://github.com/SaihonFox/Astra/actions/runs/44460539305).
  Ubuntu exercised Unix sockets, Windows exercised named pipes.
- CI green on Ubuntu and Windows for the LLamaSharp adapter: run
  [44462869583](https://github.com/SaihonFox/Astra/actions/runs/44462869583).
- CI green on Ubuntu and Windows for supervision, admission and the hardware runbook: run
  [44464487382](https://github.com/SaihonFox/Astra/actions/runs/44464487382).
- CI green on Ubuntu and Windows for GPU capability logging: run
  [44468595677](https://github.com/SaihonFox/Astra/actions/runs/44468595677).
- Known flake: Windows failed once in run
  [44464834270](https://github.com/SaihonFox/Astra/actions/runs/44464834270) on a docs-only
  commit, then passed with the same test code. The workflow now reports failed tests through
  annotations and the step summary so the next occurrence names the test.
- Not yet verified: CPU inference on Windows, CUDA offload on the GTX 1650, and Host-side
  wiring of the supervisor.

## Verification performed

Local environment, 2026-11-18, Ubuntu 26.04, SDK 10.0.115:

```
dotnet restore AstraAgent.slnx --locked-mode                  → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release          → 0 warnings, 0 errors
dotnet test AstraAgent.slnx --no-build -c Release             → 85 passed, 0 failed, 0 skipped
dotnet format AstraAgent.slnx --verify-no-changes             → exit 0
```

Runtime check with a temporary data root:

```
ASTRA_DATA_DIR=<temp> dotnet run --project src/Astra.Host
GET /health                                                   → 200 "Healthy"
database/astra.db                                             → journal_mode=wal,
                                                                schema_migrations + settings + models
logs/astra-<date>.jsonl                                       → compact JSON log lines
```

ModelHost check with a temporary data root:

```
Astra.ModelHost --endpoint unix:<temp>/modelhost.sock --data-root <temp>
listener                                                     → local Unix socket only, no TCP
ipc directory / socket mode                                  → 0700 / 0755 (owner-only connect)
handshake and RPCs                                           → covered by 17 process tests
```

A clean copy without `bin/obj` was restored with `--locked-mode` and passed build and tests.

GitHub Actions run 44345679716 — **success** (Stage 1 commit):

- `build-test (ubuntu-latest)`: all steps successful.
- `build-test (windows-latest)`: all steps successful.

## Current work

Stage 3 — Model Runtime, phase 3.6: validate CPU inference on Windows and CUDA offload on the
GTX 1650 using [STAGE_3_HARDWARE_VALIDATION.md](STAGE_3_HARDWARE_VALIDATION.md). These require
the owner's machine. Phase 3.8 will then collect evidence and the stage report. ModelHost is
not yet started by `Astra.Host`; it will be wired in Stage 4.

Real-model and GPU validation must run on the target Windows machine with the GTX 1650.
Policy Engine remains Stage 4.

## Not yet implemented

Agent Core, real llama.cpp inference inside ModelHost, resource admission, memory, tools,
policy engine, API surface, desktop UI, Android client, extensions and learning.

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
- Central log redaction is not implemented; required before tool output or user content is
  logged. Audit trail storage is also pending (Stage 4 with the Policy Engine).

## Reporting rule
Never mark an item completed without evidence. Record exact build/test commands and results.
