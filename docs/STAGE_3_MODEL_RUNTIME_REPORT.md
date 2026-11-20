# Stage 3 — Model Runtime Report

- **Status:** Draft — awaiting phase 3.6 hardware validation on the owner's Windows laptop
- **Draft prepared:** 2026-11-20
- **Environment:** Ubuntu 26.04, 4 vCPU, 7.6 GB RAM, no GPU, .NET SDK 10.0.115; GitHub Actions
  `ubuntu-latest` and `windows-latest`
- **Code commit verified:** `acfb463` (all inference, IPC and supervision code)
- **CI runs:** [44688730382](https://github.com/SaihonFox/Astra/actions/runs/44688730382)
  (code, Ubuntu and Windows green) and
  [44710009253](https://github.com/SaihonFox/Astra/actions/runs/44710009253) (documentation
  snapshot, Ubuntu and Windows green)

> **How to finish this report:** run section 1 (Windows CPU) and, optionally, section 2 (CUDA)
> of [STAGE_3_HARDWARE_VALIDATION.md](STAGE_3_HARDWARE_VALIDATION.md), fill the tables in
> [Hardware validation](#hardware-validation-to-be-completed-on-the-laptop), then change the
> status to `Signed off`, update `docs/STAGE_3_MODEL_RUNTIME.md` and
> `docs/PROJECT_STATUS.md`. Do not mark CUDA as working without a recorded run on the target
> machine; a documented CPU fallback also satisfies the criterion.

## Scope delivered

- **ADR 0008 — model runtime strategy** (accepted 2026-11-18): LLamaSharp inside a separate
  `Astra.ModelHost` process, gRPC over a named pipe (Windows) or Unix domain socket
  (Linux/macOS), CPU baseline, optional CUDA 12 backend. See
  [adr/0008-model-runtime-strategy.md](adr/0008-model-runtime-strategy.md).
- **Model registry** in SQLite (migration 2): descriptor fields from the specification,
  validation, CRUD contract `IModelRegistry`, `SqliteModelRegistry`, live file availability.
  The Stage 2 schema is unchanged; migration 2 is additive.
- **Runtime contract** `ILocalModelRuntime` in `Astra.Core.Inference` with
  `ModelLoadOptions`, `GenerationRequest`/`GenerationChunk`, `EmbeddingRequest`/`EmbeddingResult`
  and shared `LocalRuntimeValidation`. A deterministic fake runtime backs the contract tests.
- **ModelHost process** (`Astra.ModelHost`): Kestrel HTTP/2 on a local-only transport,
  versioned proto3 contract `astra.modelhost.v1`
  ([model_host.proto](../src/Astra.ModelHost.Contracts/Protos/model_host.proto)), protocol
  handshake, one model at a time, graceful shutdown. Details in
  [MODEL_HOST_IPC.md](MODEL_HOST_IPC.md).
- **Runtime adapter** `LlamaSharpModelRuntime` with LLamaSharp 0.30.0 CPU backend:
  - load/unload of GGUF files, optional context window and GPU layer count;
  - streaming text generation with temperature, optional seed, token limit and cancellation;
  - embeddings;
  - serialized load/generate/embed/unload because llama.cpp contexts are not thread-safe;
  - native llama.cpp log routing at warning/error only;
  - managed errors for missing, oversized and corrupt model files without killing the process.
- **Client library** (`Astra.ModelHost.Client`):
  - `ModelHostClient` implements `ILocalModelRuntime` over gRPC and translates status codes;
  - `ModelHostProcess` starts the child process on demand, waits for the handshake, verifies
    the process id and shuts down within bounded deadlines;
  - `ModelHostSupervisor` serializes calls, detects a dead process, restarts with bounded
    exponential backoff and reloads the last model;
  - `ModelHostUnavailableException` separates transport unavailability from model errors.
- **Resource admission:** one active model, `--max-model-bytes`, context/token/prompt/embedding
  and GPU-layer limits.
- **Optional CUDA 12 build:** `-p:UseCuda=true` adds `LLamaSharp.Backend.Cuda12` and writes its
  own lock file at `src/Astra.ModelHost/obj/packages.cuda.lock.json`, so the committed CPU lock
  file, the default build and CI are unaffected. The backend additionally requires the CUDA 12
  toolkit (`CUDA_PATH`, `cudart64_12.dll`, `cublas64_12.dll`); a driver alone is not enough.
- **Tests:** 85 total — 23 core, 34 infrastructure, 2 host, 26 ModelHost. The ModelHost suite
  covers endpoint parsing, handshake, load/generate/unload, second-load rejection, generation
  without a model, embeddings, cancellation, invalid paths, graceful shutdown, missing
  executable, duplicate instance, lifecycle, supervision and limits. A hardware-gated test
  (`Category=Hardware`, enabled only by `ASTRA_TEST_MODEL`) validates a real GGUF model and is a
  no-op in CI.

## Acceptance criteria

| # | Criterion | Status | Evidence |
|---|---|---|---|
| 1 | Registry persists metadata, rejects invalid descriptors, survives restart | Met | `SqliteModelRegistryTests` (12 tests), host integration test |
| 2 | Migration is additive and does not modify the Stage 2 schema | Met | `DatabaseMigratorTests`; migration 2 adds only `models` |
| 3 | Fake runtime passes load/unload/generate/cancellation contract tests | Met | `LocalModelRuntimeContractTests` (12 tests) |
| 4 | ModelHost starts on demand, negotiates protocol, shuts down gracefully | Met | 26 ModelHost tests; Unix socket here, named pipe in Windows CI |
| 5 | Real GGUF model loads and generates on CPU on Linux and Windows | **Linux met, Windows pending** | Linux: section below; Windows: hardware runbook, section 1 |
| 6 | CUDA offload works on the GTX 1650 or is documented unavailable with CPU fallback | **Pending** | Opt-in restore/build verified here; GPU execution requires the laptop |
| 7 | Host stays responsive and recovers when ModelHost crashes or is killed | Met at library level | `ModelHostSupervisorTests`: kill, restart, reload, unload semantics |
| 8 | CI green on Ubuntu and Windows without models or GPU | Met | Runs 44688730382 and 44710009253 |
| 9 | Documentation and status reflect behavior and limitations | Met | This report, stage plan, status, handoff, IPC and hardware docs |

Criterion 7 covers supervision as a library. `Astra.Host` does not start ModelHost yet; that
wiring, plus a job object/watchdog so ModelHost cannot outlive a crashed host, belongs to the
Stage 4 integration.

## Verification log

### Build, tests, formatting

Local Ubuntu 26.04, SDK 10.0.115:

```
dotnet restore AstraAgent.slnx --locked-mode                  → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release          → succeeded, 0 warnings
dotnet test AstraAgent.slnx --no-build -c Release             → 85 passed, 0 failed
dotnet format AstraAgent.slnx --verify-no-changes             → exit 0
```

The same sequence was repeated on 2026-11-20 against a fresh `git clone` of `acfb463` with no
`bin/obj`, matching the laptop workflow: locked restore, 0 warnings, 85 passed, format exit 0.

### Real model on Linux CPU

`Qwen3-4B-Q4_K_M.gguf` (2.4 GB, pre-existing local file, context 512, no GPU):

```
load                     → 7.5 s
generate, 16 tokens      → 3.9 s (" Paris. The capital of Germany is Berlin. ...")
embeddings, 1 input      → 0.6 s, 2560 dimensions
unload                   → graceful, process exits
```

The native log at load reports `GPU offload supported: False; requested GPU layers: 0.` as
expected for the CPU backend. No model file is downloaded or committed; CI uses the echo runtime
or invalid GGUF files.

### IPC and Windows-specific behavior

- Local transport is a Unix domain socket under `<data-root>/ipc`, directory mode `0700`, socket
  `0755`; no TCP listener exists.
- Windows CI exercises the named-pipe transport with the same 26 tests, including the
  duplicate-instance and lifecycle checks.
- A second process on the same instance is rejected during the handshake; stale Unix sockets are
  removed on start.

### CI history and the Windows flake

| Run | Commit | Result |
|---|---|---|
| [44351727206](https://github.com/SaihonFox/Astra/actions/runs/44351727206) | `964f983` | Registry, both green |
| [44352946810](https://github.com/SaihonFox/Astra/actions/runs/44352946810) | `051bdaa` | Runtime contract, both green |
| [44460539305](https://github.com/SaihonFox/Astra/actions/runs/44460539305) | `8a723d0` | ModelHost IPC, both green |
| [44462869583](https://github.com/SaihonFox/Astra/actions/runs/44462869583) | `45e27f7` | LLamaSharp adapter, both green |
| [44464487382](https://github.com/SaihonFox/Astra/actions/runs/44464487382) | `91489a1` | Supervision and admission, both green |
| [44468595677](https://github.com/SaihonFox/Astra/actions/runs/44468595677) | `b274875` | GPU capability logging, both green |
| [44686114240](https://github.com/SaihonFox/Astra/actions/runs/44686114240) | `0ea1c08` | Failure reporting and test serialization, both green |
| [44688730382](https://github.com/SaihonFox/Astra/actions/runs/44688730382) | `a8640f2` | Handshake fix, both green |
| [44710009253](https://github.com/SaihonFox/Astra/actions/runs/44710009253) | `f292389` | Documentation snapshot, both green |

Windows failed twice on commits with identical product code (runs
[44464834270](https://github.com/SaihonFox/Astra/actions/runs/44464834270) and
[44471882692](https://github.com/SaihonFox/Astra/actions/runs/44471882692)). The improved
failure reporting named the test in run
[44686500462](https://github.com/SaihonFox/Astra/actions/runs/44686500462):
`ModelHostLifecycleTests.StopAsyncShutsDownTheProcess` failed with
`RpcException(StatusCode.Cancelled)` from the startup handshake.

Root cause: `ModelHostClient.ConnectAsync` used a 2 s per-attempt handshake timeout. When that
attempt was canceled, gRPC surfaced `RpcException(Cancelled)` rather than
`OperationCanceledException`; the readiness loop treated it as fatal instead of retrying within
the 20 s startup deadline. A loaded Windows runner reproduces this while ModelHost is still
starting. `ConnectAsync` now maps a canceled attempt with an active caller token to
`TimeoutException` (retryable) and preserves caller cancellation as
`OperationCanceledException`. ModelHost test collections are also serialized
(`tests/Astra.ModelHost.Tests/xunit.runner.json`) to reduce process-start contention.

The workflow now captures `dotnet test` output, publishes every non-passed TRX result,
run-level errors and a 200-line log tail to the step summary, emits failed test names and
messages as error annotations, and uses `if: failure()` so the reporting step runs after any
earlier failure. This made the diagnosis possible without authenticated access to job logs.

## Hardware validation (to be completed on the laptop)

Follow [STAGE_3_HARDWARE_VALIDATION.md](STAGE_3_HARDWARE_VALIDATION.md). Record facts, not
impressions; leave a section explicitly marked "not run" if it was skipped.

### Windows CPU

Commands:

```powershell
git clone https://github.com/SaihonFox/Astra.git
cd Astra
dotnet restore AstraAgent.slnx --locked-mode
dotnet build AstraAgent.slnx --no-restore -c Release
dotnet test AstraAgent.slnx --no-build -c Release
dotnet format AstraAgent.slnx --verify-no-changes

$env:ASTRA_TEST_MODEL = "C:\models\<model>.gguf"
$env:ASTRA_TEST_GPU_LAYERS = "0"
dotnet test tests/Astra.ModelHost.Tests -c Release `
  --filter "Category=Hardware" --logger "console;verbosity=detailed"
```

| Item | Result |
|---|---|
| Windows version / build | `<fill in>` |
| CPU / RAM | `<fill in>` |
| .NET SDK | `<fill in>` |
| Model file / quantization / license / source | `<fill in>` |
| Context window | `<fill in>` |
| Load time | `<fill in>` |
| Generation (chunks, tokens, seconds) | `<fill in>` |
| Embedding dimensions and time | `<fill in>` |
| `GPU offload supported` line | expected `False`, layers `0` — `<fill in>` |
| Suite result (85 tests) | `<fill in>` |
| CI run for the validated commit | `<fill in>` |

Acceptance: the hardware test passes, the suite is green, and the log shows the CPU fallback
line. If embedding fails because the model lacks support, record it and note that generation
passed.

### CUDA 12 on the GTX 1650 (optional)

Prerequisites: NVIDIA driver plus the **CUDA 12 toolkit** (`CUDA_PATH`), free disk for ~500 MB
of packages and ~1.2 GB of build output, and a model that fits in 4 GB VRAM.

```powershell
$env:ASTRA_TEST_MODEL = "C:\models\<model>.gguf"
$env:ASTRA_TEST_GPU_LAYERS = "99"
dotnet test tests/Astra.ModelHost.Tests -c Release -p:UseCuda=true `
  --filter "Category=Hardware" --logger "console;verbosity=detailed"
# second terminal while it runs:
nvidia-smi
git status --short   # the committed packages.lock.json must be unchanged
```

| Item | Result |
|---|---|
| NVIDIA driver / CUDA toolkit version | `<fill in>` |
| Working `ASTRA_TEST_GPU_LAYERS` | `<fill in>` |
| `GPU offload supported` line | must be `True` for a successful GPU run — `<fill in>` |
| Load / generation time | `<fill in>` |
| VRAM used per `nvidia-smi` | `<fill in>` |
| `git status` after the CUDA build | expected: no lock-file change — `<fill in>` |
| OR: CUDA failure and CPU fallback evidence | `<fill in>` |

If CUDA cannot be used (driver/toolkit/package mismatch, OOM with the model), record the exact
error, rerun the CPU procedure and state "CUDA unavailable on this machine; CPU fallback
validated". That satisfies criterion 6. Do not claim CUDA works without the recorded evidence
above.

## Known limitations

- Windows CPU inference and CUDA offload are not validated yet; they are the only open Stage 3
  acceptance criteria.
- `Astra.Host` does not start ModelHost. Wiring the supervisor into the host, and preventing an
  orphaned ModelHost after a host crash with a job object/watchdog, is Stage 4 work.
- Central log redaction is not implemented. Prompts, completions, file contents and user data
  must not be logged; current runtime events contain model ids, counts and timings only.
- Model download, update and catalog UI are out of scope; no weights are committed.
- There is no Model Orchestrator: model selection, prompt/context assembly and fallback policy
  belong to Stage 4.
- The CUDA backend is opt-in and requires the toolkit; it is not part of CI, the default build or
  the committed lock file.
- Generation is serialized per ModelHost process; concurrent requests from the host will queue.
  Parallel inference needs a second process or a future batching design.
- A hard native crash is contained by the process boundary and recovered by the supervisor; not
  every native failure is recoverable in-process.

## Remaining work to close Stage 3

1. Run the Windows CPU validation and record the table above.
2. Attempt CUDA 12 on the GTX 1650; record success evidence or the documented fallback.
3. Fill this report, set the status to `Signed off` and update the phase table in
   [STAGE_3_MODEL_RUNTIME.md](STAGE_3_MODEL_RUNTIME.md).
4. Update `docs/PROJECT_STATUS.md`, `docs/ROADMAP.md` and `README.md` with the final status.
5. Push the final commit and record its green CI run on Ubuntu and Windows.
6. Obtain owner sign-off before starting Stage 4 — Agent Core, including the Policy Engine,
   which requires explicit approval.

## References

- [FINAL_PRODUCT_SPECIFICATION.md](FINAL_PRODUCT_SPECIFICATION.md)
- [ROADMAP.md](ROADMAP.md)
- [STAGE_3_MODEL_RUNTIME.md](STAGE_3_MODEL_RUNTIME.md) — plan, phases and verification log
- [STAGE_3_HARDWARE_VALIDATION.md](STAGE_3_HARDWARE_VALIDATION.md) — laptop procedures
- [MODEL_HOST_IPC.md](MODEL_HOST_IPC.md) — transport, limits, supervision
- [adr/0008-model-runtime-strategy.md](adr/0008-model-runtime-strategy.md)
- [PROJECT_STATUS.md](PROJECT_STATUS.md)
- [prompts/PROJECT_HANDOFF.md](../prompts/PROJECT_HANDOFF.md)
