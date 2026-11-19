# Stage 3 — Model Runtime

- **Status:** In progress
- **Started:** 2026-11-18
- **Depends on:** Stage 2 — Core Infrastructure (completed)

## Goal

Run local GGUF models through llama.cpp safely and predictably, with a model registry,
a replaceable runtime boundary and a supervised ModelHost process. The Agent Core does not
yet decide which model to use; it will consume this runtime in Stage 4.

## Scope

### In scope

1. ADR 0008 — runtime strategy (LLamaSharp, ModelHost process, CPU baseline, optional CUDA).
2. Model registry:
   - schema and migration owned by the Models module;
   - descriptor fields from the specification: id, name, path, format, quantization, size,
     context, capabilities, source, license, RAM/VRAM requirements, test results,
     availability, preferred tasks, version;
   - CRUD contract and SQLite implementation.
3. Runtime contract `ILocalModelRuntime` and a deterministic fake implementation for tests.
4. `Astra.ModelHost` process with versioned gRPC IPC over Named Pipes on Windows and Unix
   Domain Sockets on Linux; only one local transport per process. See
   [MODEL_HOST_IPC.md](MODEL_HOST_IPC.md).
5. LLamaSharp adapter confined to ModelHost:
   - load/unload;
   - text generation with streaming and cancellation;
   - embeddings where supported;
   - backend and parameter configuration.
6. Basic resource admission: one active model, context/size limits, CPU/GPU configuration.
7. Tests, hardware validation notes and a stage report.

### Out of scope

- Model Orchestrator policy and automatic model choice — Stage 4 (Agent Core).
- Memory/RAG embeddings pipeline — Stage 6.
- Model download/update UI and catalog — later stage; downloads require approval.
- Training/LoRA — Stage 14.
- Vision models — Stage 10.

## Phases

| Phase | Deliverable | Status |
|---|---|---|
| 3.1 | ADR 0008 and environment probe | ADR accepted 2026-11-18; probe done (no GPU here) |
| 3.2 | Model registry (schema, store, tests) | Completed |
| 3.3 | Runtime contract and fake runtime | Completed |
| 3.4 | ModelHost process and IPC contract | Completed |
| 3.5 | LLamaSharp adapter and CPU backend | Completed on Linux CPU; Windows CPU validation pending |
| 3.6 | Optional CUDA 12 backend on target hardware | Pending owner hardware; runbook ready |
| 3.7 | Resource admission and supervision | Completed |
| 3.8 | Stage report and CI verification | Planned |

## Environment facts (2026-11-18)

- Current development environment: Ubuntu 26.04, 4 vCPU AMD EPYC, 7.6 GB RAM, **no NVIDIA
  GPU**. GPU-specific validation must run on the owner's Windows machine.
- Target hardware: Windows, GTX 1650 4 GB (Turing, compute capability 7.5), 16 GB RAM.
- LLamaSharp 0.30.0 targets `net10.0`; CPU and CUDA12 backends are MIT.
- Windows CPU and CUDA checks are described in
  [STAGE_3_HARDWARE_VALIDATION.md](STAGE_3_HARDWARE_VALIDATION.md). CUDA is opt-in with
  `-p:UseCuda=true` and is not part of the default build, lock file or CI. The opt-in restore
  and build were verified here; GPU execution was not, because this machine has no GPU.

## Acceptance criteria

1. Registry persists model metadata, rejects invalid descriptors and survives restart.
2. Migration is append-only and does not modify Stage 2 schema.
3. Fake runtime passes load/unload/generate/cancellation contract tests.
4. ModelHost starts on demand, negotiates protocol version and shuts down gracefully.
5. A real GGUF model can be loaded and generate text on CPU on Windows and Linux.
6. On the target GTX 1650, CUDA offload is either working with recorded settings or
   documented as unavailable with the CPU fallback.
7. The host remains responsive and recovers when ModelHost crashes or is killed.
8. CI is green on Ubuntu and Windows without model downloads or GPU requirements.
9. Documentation and status reflect actual behavior and limitations.

## Security notes

- Model paths are validated; models never execute as code.
- ModelHost has no network listener; IPC is local and access-controlled.
- Model metadata includes license/source; no weights in git.
- Cancellation and timeouts are mandatory for generation.
- GPU/native asset integrity is reviewed before distribution.

## Verification

Registry phase, 2026-11-18:

```
dotnet build -c Release                 → 0 warnings, 0 errors
dotnet test -c Release                  → 59 passed, 0 failed, 0 skipped
dotnet format --verify-no-changes       → exit 0
fresh database                          → migrations 1–2, models table, /health 200
CI run 44351727206 (commit 964f983)      → success on Ubuntu and Windows
CI run 44352946810 (commit 051bdaa)      → success on Ubuntu and Windows
```

LLamaSharp adapter, 2026-11-19:

- `LlamaSharpModelRuntime` inside `Astra.ModelHost`; LLamaSharp types are not referenced by
  `Astra.Core`, `Astra.Infrastructure`, `Astra.Host` or the IPC contracts.
- Load uses `LLamaWeights.LoadFromFileAsync` with an optional context override and GPU layer
  count; a missing file and a corrupt file fail as managed errors and keep the process alive.
- Generation streams tokens from `InteractiveExecutor` with temperature, optional seed and
  `MaxTokens`; the system prompt is prepended to the user prompt.
- Embeddings use `LLamaEmbedder` with embeddings enabled on a separate context.
- Load, generation, embedding and unload are serialized because llama.cpp contexts are not
  thread-safe; unload waits for an active stream instead of disposing it underneath.
- The runtime is selected with `--runtime llama` (default) or `--runtime echo` (tests and
  diagnostics). No model files are required by CI.
- Native llama.cpp messages are routed through Serilog at warning/error level; per-tensor
  information output is dropped.

Real-model validation, 2026-11-19, Linux CPU (4 vCPU, no GPU):

```
model                     Qwen3-4B-Q4_K_M.gguf (2.4 GB, pre-existing local file)
load, context 512         → 7.5 s
generate, 16 tokens       → 3.9 s (" Paris. The capital of Germany is Berlin. ...")
embeddings, 1 input       → 0.6 s, 2560 dimensions
unload                    → graceful, process exits
```

The same model was not loaded in CI; tests use the echo runtime or invalid GGUF files.

Supervision and admission, 2026-11-19:

- `ModelHostSupervisor` starts ModelHost on demand, serializes runtime calls, detects a dead
  process, restarts with bounded exponential backoff and reloads the last model.
- Tested by killing the child process: the next generation restarts it, reloads the model and
  returns output; an unloaded model is not restored.
- `--max-model-bytes` rejects oversized files before native loading; the process stays alive.
- Limit: if `Astra.Host` itself crashes, the child process is not killed by the OS. A job
  object/watchdog remains for the Host wiring phase.

ModelHost IPC phase, 2026-11-19:

```
dotnet build AstraAgent.slnx -c Release  → 0 warnings, 0 errors
dotnet test AstraAgent.slnx -c Release   → 85 passed, 0 failed, 0 skipped
dotnet format --verify-no-changes        → exit 0
```

- 26 ModelHost tests: endpoint parsing, handshake (protocol, runtime name, process id),
  load/generate/unload, second-load rejection, generation without a loaded model,
  embeddings, stream cancellation, invalid model path, graceful shutdown, missing
  executable, duplicate instance, lifecycle.
- Transport on this machine: Unix domain socket under the data root's `ipc` directory;
  socket created as `0755` inside a `0700` directory, so only the owner can connect.
- The process does not bind TCP; the only listener is the local endpoint.
- CI run 44460539305 (commit 8a723d0) → success on Ubuntu and Windows. The Windows job
  exercised the named-pipe transport and the same IPC tests; local verification here
  cannot.
- CI run 44462869583 (commit 45e27f7) → success on Ubuntu and Windows with LLamaSharp
  packages and native backend present.
- CI run 44464487382 (commit 91489a1) → success on Ubuntu and Windows with supervision,
  admission and the hardware-gated test present.

