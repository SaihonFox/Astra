# ADR-0011: Model orchestration and selection

- Status: Proposed
- Date: 2026-11-20
- Decision owners: Project owner
- Depends on: ADR 0008 (local model runtime), ADR 0010 (task state)
- Related: Stage 4 plan ([STAGE_4_AGENT_CORE.md](../STAGE_4_AGENT_CORE.md))

## Context

Astra has one agent and several local models used as interchangeable compute engines. The Model
Registry (Stage 3) stores capabilities, context window, quantization, RAM/VRAM requirements,
preferred tasks, test status and availability. Stage 4 must choose a model per request and task
without turning models into agents, without cloud fallback and without loading heavy models
needlessly.

The runtime constraint from Stage 3: one loaded model per ModelHost process, generation is
serialized, and loading is expensive (seconds to tens of seconds).

## Options considered

### A. Rules with hard filters and a weighted score (recommended)

Filter by hard requirements, then rank the remainder with configurable weights.

Pros: deterministic, explainable, testable, no extra infrastructure, easy to tune.
Cons: weights need calibration on real hardware; a naive score can be gamed by stale metrics.

### B. LLM decides which model to use

Rejected: adds a model call before every task, is non-deterministic, and puts a resource
decision into the untrusted model. It also contradicts "security and policy outside the LLM".

### C. Static per-mode mapping

Pros: trivial.
Cons: ignores availability, context size and measured performance; breaks as soon as a model is
missing.

## Decision

1. **Two modes:** `Manual` (the user pins a model id) and `Automatic` (default). Manual still
   passes through the same hard filters; if the pinned model is unavailable the request fails
   with a typed error instead of silently switching.
2. **Hard filters, in order:** model availability (file exists and is readable), required
   capabilities for the intent (for example coding or reasoning), context window ≥ assembled
   prompt + reserved output, declared RAM/VRAM within the active resource profile, and not
   already reserved by another task.
3. **Ranking:** weighted score over preferred-task match, measured throughput, test status and
   recent success rate. Weights are configuration (`Astra:Orchestration`), not code.
4. **Explainability:** the selection result carries the model id, the score components, the
   filters that excluded each other candidate and the mode. It is persisted on the task step
   and audited; it never contains prompt text.
5. **Single active model:** the orchestrator serializes loads through the supervisor and does
   not switch models mid-task. A task keeps the model it started with.
6. **Fallback:** if the chosen model fails to load, the next eligible candidate is tried once.
   A second failure fails the task with a typed error. Every attempt is audited.
7. **No model available:** a typed `NoEligibleModelException` with the filter reasons; the
   pipeline returns an actionable message instead of calling an empty prompt.
8. **Local only:** the orchestrator can only choose from the local registry. There is no
   provider abstraction, no API key and no network path; adding one requires a new ADR and is
   contrary to the product specification.
9. **Load lifetime:** the model stays loaded between tasks and is unloaded on host shutdown or
   an explicit unload. Idle unloading belongs to the resource-management stage.

## Consequences

- Selection is deterministic for the same registry, metrics and configuration, which makes
  tests and incident analysis possible.
- Metrics start empty; the first runs fall back to static preferences until data exists.
- A pinned model can make a request fail; this is intentional and visible.
- One model at a time keeps RAM/VRAM predictable but serializes heavy work.

## Security implications

- The model cannot choose itself or escalate to another model; the decision is host code.
- Registry metadata is untrusted input: validate paths, sizes and capability strings; never
  execute model content.
- Filter reasons are audited without exposing user content.
- No cloud provider abstraction is introduced, so no accidental remote inference path exists.

## Validation plan

1. Unit tests for each filter and for the ranking order with fixed metrics.
2. Manual-mode tests: pinned available model is used; pinned missing model fails with a typed
   error and an audit row.
3. Fallback test: first model fails to load, the second is used once; both attempts audited.
4. Context-fit test: a prompt larger than the window is rejected before load.
5. Concurrency test: two tasks cannot load two models into one ModelHost.
6. Architecture test: `Astra.Agent` references `ILocalModelRuntime` and the registry contract,
   never LLamaSharp or a provider SDK.
