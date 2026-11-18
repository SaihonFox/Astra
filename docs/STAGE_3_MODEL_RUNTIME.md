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
4. `Astra.ModelHost` process skeleton with versioned IPC (gRPC over pipes/UDS).
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
| 3.1 | ADR 0008 and environment probe | ADR Proposed; probe done (no GPU here) |
| 3.2 | Model registry (schema, store, tests) | Completed |
| 3.3 | Runtime contract and fake runtime | Planned |
| 3.4 | ModelHost process and IPC contract | Planned |
| 3.5 | LLamaSharp adapter and CPU backend | Planned |
| 3.6 | Optional CUDA 12 backend on target hardware | Planned |
| 3.7 | Resource admission and diagnostics | Planned |
| 3.8 | Stage report and CI verification | Planned |

## Environment facts (2026-11-18)

- Current development environment: Ubuntu 26.04, 4 vCPU AMD EPYC, 7.6 GB RAM, **no NVIDIA
  GPU**. GPU-specific validation must run on the owner's Windows machine.
- Target hardware: Windows, GTX 1650 4 GB (Turing, compute capability 7.5), 16 GB RAM.
- LLamaSharp 0.30.0 targets `net10.0`; CPU and CUDA12 backends are MIT.

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
