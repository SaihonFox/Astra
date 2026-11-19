# ADR-0008: Local model runtime strategy

- Status: Accepted
- Date: 2026-11-18
- Accepted: 2026-11-18
- Decision owners: Project owner

## Context

Astra must run local GGUF models through llama.cpp with no cloud inference. The primary
machine is Windows with a GTX 1650 (4 GB VRAM), 16 GB RAM; Linux is also supported. Native
inference must not crash or block the host, and the runtime boundary must stay replaceable.

The specification mandates llama.cpp/GGUF but does not prescribe the integration mechanism.

## Options considered

### A. LLamaSharp in a dedicated `Astra.ModelHost` process (recommended)

Facts verified on 2026-11-18 from NuGet:

- `LLamaSharp` **0.30.0**, MIT, ships a `net10.0` target.
- `LLamaSharp.Backend.Cpu` 0.30.0, MIT — about 42 MB of native libraries.
- `LLamaSharp.Backend.Cuda12` 0.30.0, MIT — meta-package with separate Windows and Linux
  CUDA runtime packages.
- `LLamaSharp.Backend.Vulkan` 0.30.0, MIT.
- Depends on `Microsoft.Extensions.AI.Abstractions` (abstractions only; no provider, no
  network).

Pros: managed API for loading, context, sampling, grammar, embeddings; GGUF; active .NET
binding; native crashes isolated in ModelHost; no separate server protocol.

Cons: native asset size; CUDA runtime packaging complexity; LLamaSharp/llama.cpp version
coupling; API surface follows upstream changes.

### B. `llama-server` executable with an HTTP API

Pros: upstream process, simple supervision, stable JSON/OpenAI-style API, process isolation.

Cons: localhost HTTP listener and its attack surface; binary build/distribution per OS and
GPU; server API churn; extra serialization; weaker .NET integration for embeddings/grammar;
process lifecycle still needs a manager.

### C. Custom P/Invoke wrapper

Rejected: high unsafe-code and maintenance cost, duplicating LLamaSharp without a measured
need.

## Decision

1. Use **LLamaSharp** as the llama.cpp integration, behind an `ILocalModelRuntime`
   abstraction owned by Astra. No LLamaSharp types leak into Agent Core, memory or tools.
2. Run native inference in **`Astra.ModelHost`**, a separate process, per ADR 0005. The host
   (or a ModelHost client) communicates over Named Pipes/Unix Domain Sockets with a versioned
   gRPC contract. Native failures must not terminate `Astra.Host`.
3. Baseline backend is **CPU** so CI and first runs work without CUDA. **CUDA 12** is an
   optional per-platform backend for the target NVIDIA machine; Vulkan remains a fallback
   candidate. Backend selection is configuration, not hard-coded.
4. Model weights are never stored in the repository. Models are registered by absolute path
   with license/source metadata; downloading is a separate, user-approved flow.
5. The Model Registry is the source of truth for model metadata and lives in the Host
   SQLite database. ModelHost receives a resolved model path and load parameters; it does
   not own registry policy.
6. One active loaded model per ModelHost at a time at this stage. Concurrent models are a
   later resource-management decision.
7. Loading, unloading and generation honor cancellation and enforce timeouts. Memory/VRAM
   admission checks are added before automatic model selection.
8. Real-model integration tests run only on hardware that opts in; CI uses a deterministic
   fake runtime. No multi-gigabyte downloads in CI.

## Consequences

- LLamaSharp and native assets stay confined to ModelHost and its adapter.
- The process boundary and IPC must be built before real inference is exposed to Host.
- CUDA deployment needs explicit packaging and validation on the GTX 1650; CPU remains the
  compatibility baseline.
- The `ILocalModelRuntime` contract is the stable seam for a future alternative runtime.

## Security implications

Model files and metadata are untrusted input: validate paths, sizes and formats; never
execute model content. ModelHost runs with least privilege and no network listener. IPC
requires OS access control and version negotiation. GGUF license/source metadata is
recorded. No model download without explicit approval and integrity checks.

## Validation plan

1. Registry round-trip and migration tests (model-independent, in CI).
2. Fake runtime contract tests (load/unload/generate/cancel) in CI.
3. ModelHost IPC smoke test on Windows and Linux.
4. Manual load/generation test with a small GGUF model on the target GTX 1650 and on CPU;
   record tokens/second, RAM/VRAM and failure behavior.
5. Security review of native assets, paths and IPC before enabling GPU backends by default.
