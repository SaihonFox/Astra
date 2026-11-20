# Stage 3 — Model Runtime Report

- **Status:** Signed off by the project owner on 2026-11-20
- **Draft prepared:** 2026-11-20; hardware validation added and signed off 2026-11-20
- **Environment:** Windows 10 22H2 (19045), Ryzen 5 4600H, 7.4 GB RAM, GTX 1650 4 GB,
  .NET SDK 11.0.100 (ADR 0009); earlier evidence was produced on Ubuntu 26.04, 4 vCPU,
  7.6 GB RAM, no GPU, .NET SDK 10.0.115; GitHub Actions `ubuntu-latest` and `windows-latest`
- **Code commit verified:** `acfb463` for the inference, IPC and supervision code; the working
  copy additionally contains the .NET 11 baseline (ADR 0009) and this hardware validation, and
  is not yet committed or pushed (the folder is not a git clone).
- **CI runs:** [44688730382](https://github.com/SaihonFox/Astra/actions/runs/44688730382)
  (code, Ubuntu and Windows green) and
  [44710009253](https://github.com/SaihonFox/Astra/actions/runs/44710009253) (documentation
  snapshot, Ubuntu and Windows green); the run for this change set is pending a push.

> **Status note:** the hardware validation below was executed on the owner's Windows laptop on
> 2026-11-20. CUDA 12 is recorded as a documented CPU fallback, which satisfies criterion 6.
> The owner signed Stage 3 off on 2026-11-20. The CI run for this change set is still pending a
> push, because the working copy is not a git clone.

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
| 5 | Real GGUF model loads and generates on CPU on Linux and Windows | Met | Linux: section below; Windows: hardware section (`Qwen3-0.6B-Q8_0`, load 1.2 s, 8 tokens in 0.6 s, 1024-dim embeddings) |
| 6 | CUDA offload works on the GTX 1650 or is documented unavailable with CPU fallback | Met via documented fallback | CUDA 12 build restored and ran; driver 457.49 / CUDA 11.1 and no toolkit → `GPU offload supported: False`, explicit "no usable GPU" warning, CPU fallback passed |
| 7 | Host stays responsive and recovers when ModelHost crashes or is killed | Met at library level | `ModelHostSupervisorTests`: kill, restart, reload, unload semantics |
| 8 | CI green on Ubuntu and Windows without models or GPU | Met | Runs 44688730382 and 44710009253 |
| 9 | Documentation and status reflect behavior and limitations | Met | This report, stage plan, status, handoff, IPC and hardware docs |

Criterion 7 covers supervision as a library. `Astra.Host` does not start ModelHost yet; that
wiring, plus a job object/watchdog so ModelHost cannot outlive a crashed host, belongs to the
Stage 4 integration.

## Verification log

### Build, tests, formatting

Windows 10 22H2, Ryzen 5 4600H, 7.4 GB RAM, SDK 11.0.100 (`net11.0`, C# 15, ADR 0009):

```
dotnet restore AstraAgent.slnx --locked-mode                  → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release          → succeeded, 0 warnings
dotnet test AstraAgent.slnx --no-build -c Release             → 85 passed, 0 failed
dotnet format AstraAgent.slnx --verify-no-changes             → exit 0
```

Host smoke test on Windows with a temporary data root: `GET /health` returned 200 "Healthy";
`database/`, `ipc/` and `logs/` were created.

Local Ubuntu 26.04, SDK 10.0.115 (historical, before the .NET 11 baseline change):

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

## Hardware validation (Windows laptop)

Performed on 2026-11-20 following
[STAGE_3_HARDWARE_VALIDATION.md](STAGE_3_HARDWARE_VALIDATION.md).

### Windows CPU

Commands:

```powershell
dotnet restore AstraAgent.slnx --locked-mode
dotnet build AstraAgent.slnx --no-restore -c Release
dotnet test AstraAgent.slnx --no-build -c Release
dotnet format AstraAgent.slnx --verify-no-changes

$env:ASTRA_TEST_MODEL = "C:\models\Qwen3-0.6B-Q8_0.gguf"
$env:ASTRA_TEST_GPU_LAYERS = "0"
dotnet test tests/Astra.ModelHost.Tests -c Release --no-build `
  --filter "Category=Hardware" --logger "console;verbosity=detailed"
```

| Item | Result |
|---|---|
| Windows version / build | Windows 10 22H2, 10.0.19045 |
| CPU / RAM | AMD Ryzen 5 4600H (6C/12T) / 7.4 GB usable |
| GPU | NVIDIA GeForce GTX 1650 4 GB, driver 457.49 (CUDA 11.1) |
| .NET SDK | 11.0.100 (`net11.0`, C# 15, ADR 0009) |
| Model file / quantization / license / source | `Qwen3-0.6B-Q8_0.gguf`, 639,446,688 bytes (Q8_0), Apache-2.0, `Qwen/Qwen3-0.6B-GGUF` on Hugging Face |
| Context window | 512 |
| Load time | 1.2 s |
| Generation (chunks, tokens, seconds) | 8 chunks, 37 characters, 0.6 s |
| Embedding dimensions and time | 1024 dimensions, 0.2 s |
| `GPU offload supported` line | `GPU offload supported: False; requested GPU layers: 0.` |
| Suite result (85 tests) | 85 passed, 0 failed, 0 skipped |
| CI run for the validated commit | not available — the working copy is not a git clone; CI runs on the next push |

The hardware test `RealModelHardwareTests.RealModelLoadsGeneratesAndEmbeds` passed in 3 s and
reported `load: 1.2 s, gpu layers: 0`, `generate: 8 chunks, 37 characters, 0.6 s`,
`embed: 1024 dimensions, 0.2 s`. llama.cpp logged two benign warnings: Qwen3 marks `</s>` as
a control-looking token, and embeddings required overriding non-output input tokens. Both are
model-metadata notes, not failures.

### CUDA 12 on the GTX 1650 (optional)

Result: **CUDA 12 backend restored and built, but no usable GPU was found at runtime; the run
fell back to CPU and the test passed. Criterion 6 is met through the documented fallback.**

Command:

```powershell
$env:ASTRA_TEST_MODEL = "C:\models\Qwen3-0.6B-Q8_0.gguf"
$env:ASTRA_TEST_GPU_LAYERS = "99"
dotnet test tests/Astra.ModelHost.Tests -c Release -p:UseCuda=true `
  --filter "Category=Hardware" --logger "console;verbosity=detailed"
```

Runtime log:

```
[INF] GPU offload supported: False; requested GPU layers: 99.
[WRN] GPU layers were requested but the native backend has no usable GPU; inference will use the CPU.
load: 1.7 s, gpu layers: 99
generate: 8 chunks, 37 characters, 0.5 s
embed: 1024 dimensions, 0.2 s
```

| Item | Result |
|---|---|
| NVIDIA driver / CUDA toolkit version | driver 457.49 (CUDA 11.1 runtime); no CUDA 12 toolkit (`CUDA_PATH` unset, `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA` absent) |
| Working `ASTRA_TEST_GPU_LAYERS` | none — 99 was requested and ignored with an explicit warning |
| `GPU offload supported` line | `False`; warning that the native backend has no usable GPU |
| Load / generation time | 1.7 s / 0.5 s for 8 tokens (CPU execution) |
| VRAM used per `nvidia-smi` | 134 MiB idle; no ModelHost allocation observed |
| `git status` after the CUDA build | committed `src/Astra.ModelHost/packages.lock.json` still lists only `LLamaSharp.Backend.Cpu`; the CUDA restore writes `obj/packages.cuda.lock.json` (ignored). Verified by inspecting both files — the working copy has no `.git`. |
| OR: CUDA failure and CPU fallback evidence | as above: opt-in restore/build succeeded, runtime reported no usable GPU and used the CPU |

The CUDA 12 backend needs a driver from the 525/527 branch or newer plus the CUDA 12 toolkit
for `cudart64_12.dll` and `cublas64_12.dll`. This machine has neither, so LLamaSharp selected
the CPU path. Installing a newer driver and the toolkit requires administrator rights and a
reboot and is left to the owner as a separate task.

After the CUDA run the ModelHost `bin`/`obj` were removed, the solution was restored with
`--locked-mode` and rebuilt: the output contains no CUDA native libraries and all 85 tests
pass, so the repository stays on the CPU baseline.

## Known limitations

- CUDA offload is not usable on the GTX 1650: the opt-in CUDA 12 build restores and runs, but
  with driver 457.49 and no CUDA 12 toolkit LLamaSharp reports no usable GPU and falls back to
  the CPU. GPU acceleration remains an owner task that needs a driver update and the toolkit.
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

Done on 2026-11-20: Windows CPU validation recorded; CUDA documented as unavailable with the
CPU fallback; report filled; .NET 11 baseline verified.

1. Commit the change set and record its green CI run on Ubuntu and Windows (requires a git
   clone; the current working copy has no `.git`).
2. Set the phase table in [STAGE_3_MODEL_RUNTIME.md](STAGE_3_MODEL_RUNTIME.md) to complete.
3. Update `docs/PROJECT_STATUS.md`, `docs/ROADMAP.md` and `README.md` with the final status.
4. Obtain owner sign-off before starting Stage 4 — Agent Core, including the Policy Engine,
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
