# Project Status

**Current stage:** Stage 4 — Agent Core (planning; implementation awaits ADR approval)
**Specification:** approved by project owner on 2026-11-18
**Implementation:** foundation, core infrastructure and Stage 3 Model Runtime complete and
signed off; Stage 4 Agent Core is in planning and needs ADR approval before implementation
**Last updated:** 2026-11-20
**Handoff:** [prompts/PROJECT_HANDOFF.md](../prompts/PROJECT_HANDOFF.md) — full state, evidence, next steps and pitfalls for a new agent or machine

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

### Stage 3 — Model Runtime (completed, signed off 2026-11-20)
- ADR 0009 accepted by the owner on 2026-11-20: platform baseline moved from .NET 10 to
  **.NET 11** (`net11.0`, C# 15, SDK 11.0.100, `Microsoft.Data.Sqlite` 11.0.0,
  `Microsoft.AspNetCore.Mvc.Testing` 11.0.0, lock files regenerated). Verified on Windows
  with SDK 11.0.100: locked restore, build with 0 warnings, 85 tests, format exit 0.
- CI green on Ubuntu and Windows for the .NET 11 baseline and Stage 3 close-out commit
  `cb1efc4`: run [44717828284](https://github.com/SaihonFox/Astra/actions/runs/44717828284).
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
  - Windows CPU validated on the owner's laptop (Windows 10 22H2, Ryzen 5 4600H, 7.4 GB RAM,
    GTX 1650): `Qwen3-0.6B-Q8_0.gguf` (Apache-2.0), context 512, load 1.2 s, 8 tokens in
    0.6 s, 1024-dimension embeddings in 0.2 s; log shows
    `GPU offload supported: False; requested GPU layers: 0.`
  - CUDA 12 offload does not work on that machine: the opt-in CUDA build restores and runs, but
    the runtime reports `GPU offload supported: False` and
    `GPU layers were requested but the native backend has no usable GPU`, then uses the CPU.
    Driver 457.49 (CUDA 11.1) and no CUDA 12 toolkit (`CUDA_PATH` unset) explain it. The CPU
    fallback satisfies acceptance criterion 6; GPU acceleration needs a driver update and the
    toolkit.
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
- CI green on Ubuntu and Windows for the project handoff and refreshed status docs: run
  [44471553581](https://github.com/SaihonFox/Astra/actions/runs/44471553581).
- CI green on Ubuntu and Windows after fixing failure reporting and serializing ModelHost
  tests: run [44686114240](https://github.com/SaihonFox/Astra/actions/runs/44686114240).
- CI green on Ubuntu and Windows after the retryable-handshake fix: run
  [44688730382](https://github.com/SaihonFox/Astra/actions/runs/44688730382).
- Windows flake identified and fixed. Runs
  [44464834270](https://github.com/SaihonFox/Astra/actions/runs/44464834270) and
  [44471882692](https://github.com/SaihonFox/Astra/actions/runs/44471882692) failed on commits
  with identical product code. With the new failure reporting, run
  [44686500462](https://github.com/SaihonFox/Astra/actions/runs/44686500462) named
  `ModelHostLifecycleTests.StopAsyncShutsDownTheProcess` with `RpcException(Cancelled)` from
  the startup handshake. Root cause: a `GetInfo` handshake attempt canceled by its 2 s timeout
  surfaced as `RpcException(StatusCode.Cancelled)` instead of
  `OperationCanceledException`, and the readiness loop treated it as fatal instead of retrying
  within the 20 s startup deadline. Under load, a Windows runner hits this while ModelHost is
  still starting. `ModelHostClient.ConnectAsync` now maps a canceled handshake attempt to
  `TimeoutException` (retryable) when the caller's token is active, and to
  `OperationCanceledException` when the caller cancels. ModelHost test collections are also
  serialized to reduce process-start contention, and the workflow publishes failed test names,
  TRX outcomes and a log tail without requiring authenticated job-log access.
- [Stage 3 report](STAGE_3_MODEL_RUNTIME_REPORT.md) signed off by the owner on 2026-11-20.
- Not yet verified: Host-side wiring of the supervisor (Stage 4). CUDA offload is documented as
  unavailable with the validated CPU fallback.

## Verification performed

Windows 10 22H2 (19045), Ryzen 5 4600H, 7.4 GB RAM, SDK 11.0.100, 2026-11-20:

```
dotnet restore AstraAgent.slnx --locked-mode                  → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release          → 0 warnings, 0 errors
dotnet test AstraAgent.slnx --no-build -c Release             → 85 passed, 0 failed, 0 skipped
dotnet format AstraAgent.slnx --verify-no-changes             → exit 0
```

Host smoke test with a temporary data root on Windows: `GET /health` returned 200 "Healthy";
the database, `ipc` and log areas were created as expected.

Local environment, 2026-11-19, Ubuntu 26.04, SDK 10.0.115:

```
dotnet restore AstraAgent.slnx --locked-mode                  → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release          → 0 warnings, 0 errors
dotnet test AstraAgent.slnx --no-build -c Release             → 85 passed, 0 failed, 0 skipped
dotnet format AstraAgent.slnx --verify-no-changes             → exit 0
```

The documented laptop sequence was repeated on 2026-11-20 against a fresh clone of `acfb463`
(no `bin/obj`): locked restore, build with 0 warnings, 85 tests and format all passed.

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
handshake and RPCs                                           → covered by 26 process tests
```

A clean copy without `bin/obj` was restored with `--locked-mode` and passed build and tests.

GitHub Actions run 44345679716 — **success** (Stage 1 commit):

- `build-test (ubuntu-latest)`: all steps successful.
- `build-test (windows-latest)`: all steps successful.

## Current work

Stage 4 — Agent Core is in planning. [STAGE_4_AGENT_CORE.md](STAGE_4_AGENT_CORE.md) defines
the components, phases, data model, tests and exit criteria. Four ADRs are proposed and need
owner approval before implementation:

- ADR 0010 — task state, sessions and checkpoints;
- ADR 0011 — model orchestration and selection;
- ADR 0012 — context assembly and budgeting;
- ADR 0013 — Policy Engine, permissions and audit (security-critical).

No Stage 4 code has been written. ModelHost is still not started by `Astra.Host`; wiring the
supervisor together with a job object/watchdog is part of Stage 4 phase 4.2.

Stage 3 is signed off, and the change set is committed as `cb1efc4` with green CI on Ubuntu
and Windows ([run 44717828284](https://github.com/SaihonFox/Astra/actions/runs/44717828284)).
A new agent should start from
[prompts/PROJECT_HANDOFF.md](../prompts/PROJECT_HANDOFF.md).

## Not yet implemented

Agent Core, model orchestration and context assembly, memory, tools, Policy Engine and audit,
API surface, desktop UI, Android client, extensions and learning. `Astra.Host` does not start
ModelHost yet; supervision is a tested client library waiting for Stage 4 wiring. CUDA offload
on the GTX 1650 is not validated (CPU fallback documented); Windows CPU inference is validated.

## Risks / decisions to validate during implementation
- .NET 11 SDK and Avalonia compatibility at implementation time (baseline change: ADR 0009).
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
