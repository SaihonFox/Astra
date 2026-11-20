# Stage 4 — Agent Core

- **Status:** Planned — awaiting owner approval of ADRs 0010–0013
- **Planned:** 2026-11-20
- **Depends on:** Stage 3 — Model Runtime (signed off 2026-11-20)
- **Report (to be written):** `docs/STAGE_4_AGENT_CORE_REPORT.md`

## Goal

Turn the Stage 1–3 foundation (host, persistence, eventing, model registry, local runtime) into
**one agent loop**: a request or event becomes an analyzed intent, an assembled context, a
selected local model, a bounded plan, an executed step, a validated result, a response and
persisted state that can be paused, resumed and recovered after a restart.

The stage delivers the *skeleton of the whole product*, not the product: there are no tools,
no memory/RAG, no UI and no autonomy yet. Everything must be model-independent and must keep
security and permissions outside the LLM.

## Scope

### In scope

1. ADRs 0010–0013 (task state, orchestration, context, policy/audit), approved before code.
2. `Astra.Agent` module: Request Analyzer, Intent Classifier, Context Manager, Model
   Orchestrator, Task Planner, Task Executor, Result Validator, Response Generator, Session
   Manager, Task State Engine, Policy Engine.
3. Persistence for sessions, messages, tasks, steps, checkpoints and audit events
   (migration 3, additive).
4. ModelHost wiring in `Astra.Host`: the supervisor starts the child process on demand, and a
   job object / watchdog prevents an orphaned ModelHost when the host crashes.
5. Pause, resume, cancel and restart recovery for tasks, with time/iteration/token/tool-call
   limits.
6. Policy decisions (allow / confirm / deny) for every action boundary and an append-only
   audit trail. In this stage the only real action is local inference; tool actions are
   represented by typed descriptors and covered by policy tests until Stage 5 provides the
   tools themselves.
7. Tests (unit, integration, architecture, security), documentation and a stage report.

### Out of scope

- Tool execution and OS integration — Stage 5. `IToolDispatcher` is a contract with a
  "no tools registered" implementation that denies any call.
- Memory, RAG and embeddings pipelines — Stage 6.
- Desktop UI and API surface — Stages 7 and 15. Stage 4 exposes no public HTTP agent endpoint;
  the pipeline is exercised through DI in tests and through the host's internal service.
- Background autonomy, scheduler and goal manager — Stage 12.
- Vision, voice, plugins, Android, learning — later stages.
- Multi-agent behaviour, delegation to other AI systems and cloud inference are permanently
  out of scope.

## Components and data flow

```
request / event
   │
   ▼
Request Analyzer ──► Intent Classifier ──► Session Manager (mode: Chat/Assist/…)
   │                                              │
   ▼                                              ▼
Context Manager ◄── Task State Engine ◄──── Task State Store (SQLite)
   │                    ▲      │
   ▼                    │      ▼
Model Orchestrator ──► Task Planner ──► Task Executor ──► Result Validator
   │  (selects model)        │               │  ▲              │
   ▼                         ▼               ▼  │              ▼
ILocalModelRuntime     policy check ◄── Tool Dispatcher      Response Generator
   │                                            (no tools)         │
   ▼                                                               ▼
ModelHost process  ◄── ModelHostSupervisor                    response + persisted state
                                     │
                                     ▼
                              Policy Engine + audit
```

Rules:

- The pipeline never calls native inference directly; it uses `ILocalModelRuntime`.
- The LLM output is untrusted: plans and tool arguments are parsed, validated against typed
  schemas and policy-checked before anything executes.
- External content is data. It enters the context in a marked "untrusted content" block and
  can never change policy, permissions or the task goal.
- Every state change is persisted before the next step starts; a crash loses at most the
  in-flight step.

## Proposed module layout

```
src/Astra.Core/Agent/                contracts only (no infrastructure, no LLamaSharp)
  AgentRequest.cs, AgentResponse.cs, AgentMode.cs, AgentIntent.cs
  IAgentRuntime.cs                   entry point used by the host
  IContextAssembler.cs, ContextPackage.cs, ContextSection.cs
  IModelOrchestrator.cs, ModelSelection.cs, ModelSelectionPolicy.cs
  ITaskStateStore.cs, TaskState.cs, TaskStep.cs, TaskStatus.cs, TaskLimits.cs
  ITaskPlanner.cs, TaskPlan.cs, PlannedStep.cs
  IPolicyEngine.cs, PolicyDecision.cs, PolicyRequest.cs, PolicyLevel.cs
  IAuditSink.cs, AuditEvent.cs
  IToolDispatcher.cs, ToolCall.cs, ToolResult.cs   (contracts; Stage 5 implements tools)
src/Astra.Agent/                     implementation of the pipeline
  AgentRuntime.cs                    orchestrates the loop
  Analysis/RequestAnalyzer.cs, IntentClassifier.cs
  Context/ContextAssembler.cs
  Orchestration/ModelOrchestrator.cs
  Planning/TaskPlanner.cs
  Execution/TaskExecutor.cs, NoToolsDispatcher.cs
  Validation/ResultValidator.cs
  Responses/ResponseGenerator.cs
  Sessions/SessionManager.cs
  State/TaskStateEngine.cs
  Policy/PolicyEngine.cs, PolicyRules.cs
src/Astra.Infrastructure/Database/   migration 3 and stores
  Migrations/…_AgentCore.cs
  SqliteSessionStore.cs, SqliteTaskStateStore.cs, SqliteAuditSink.cs
src/Astra.Host/                      composition root
  Agent/AgentServiceCollectionExtensions.cs
  ModelHost/ModelHostServiceCollectionExtensions.cs, ModelHostWatchdog.cs
tests/Astra.Agent.Tests/             pipeline, policy, orchestration, state machine
tests/Astra.Infrastructure.Tests/    migration 3, stores, restart/resume
tests/Astra.Host.Tests/              DI wiring and end-to-end run with the echo runtime
```

`Astra.Agent` references `Astra.Core` only. Persistence implementations stay in
`Astra.Infrastructure`. `Astra.Host` composes both. No project references LLamaSharp except
`Astra.ModelHost`.

## Phases

| Phase | Deliverable | Exit check |
|---|---|---|
| 4.1 | ADRs 0010–0013 approved; contracts in `Astra.Core.Agent` | Owner approval recorded; `Astra.Core` still has no infrastructure dependency |
| 4.2 | ModelHost wiring in Host + job object/watchdog | Host integration test runs a generation through the supervisor with the echo runtime; killing the host does not leave ModelHost running |
| 4.3 | Migration 3 and stores (sessions, messages, tasks, steps, checkpoints, audit) | Store round-trip and restart tests; migration is additive |
| 4.4 | Pipeline skeleton: analyzer, classifier, context, orchestrator, planner, executor, validator, responder | End-to-end run with a deterministic fake runtime; model selection matrix tests |
| 4.5 | Task State Engine: transitions, limits, pause/resume/cancel, recovery | State-machine tests; resume after simulated crash; limit enforcement |
| 4.6 | Policy Engine and audit | Policy decision matrix; bypass tests; audit append-only and free of prompts/secrets |
| 4.7 | Tests, docs, report | Full suite green on Windows and Linux; report signed off |

## Data model (migration 3, additive)

| Table | Purpose | Key fields |
|---|---|---|
| `sessions` | conversation container | `id`, `title`, `mode`, `status`, `created_at`, `updated_at` |
| `messages` | user/assistant turns | `id`, `session_id`, `role`, `content`, `created_at`, `task_id` |
| `tasks` | persisted task state | `id`, `session_id`, `parent_task_id`, `goal`, `mode`, `status`, `priority`, `version`, `tokens_used`, `iterations`, `tool_calls`, `result_summary`, `error`, timestamps |
| `task_steps` | planned/executed steps | `id`, `task_id`, `sequence`, `kind`, `status`, `input_json`, `output_summary`, `error`, timestamps |
| `task_checkpoints` | resumable snapshots | `id`, `task_id`, `sequence`, `state_json`, `created_at` |
| `audit_events` | append-only action trail | `id`, `occurred_at`, `correlation_id`, `actor`, `action_type`, `target`, `decision`, `policy_level`, `reason` |

Rules: no secrets and no prompt/completion text in `audit_events`; message content is user data
and is never written to logs; all JSON columns carry a schema version; updates use optimistic
concurrency on `tasks.version`.

## Task lifecycle

```
Queued → Planning → Executing → Validating → Completed
   │         │           │           │
   ├─────────┴───────────┴───────────┴──► Failed
   ├────────────────────────────────────► Cancelled
   └────────────────────────────────────► Paused ──► Queued (resume)
```

Recovery: on host start, tasks in `Planning`, `Executing` or `Validating` are moved to
`Paused` with a recovery checkpoint and a reason; the owner or a later autonomy stage decides
whether to resume. Limits are checked before each step; exceeding a limit moves the task to
`Failed` with a typed reason.

## Model orchestration

1. Hard filters: availability (file exists), required capabilities, context window fits the
   assembled prompt, declared RAM/VRAM within the current profile, not already busy.
2. Ranking: weighted score over preferred tasks, measured throughput, quality/test status and
   recent success. Weights live in configuration, not code.
3. Manual mode: the user selects a model id; the same hard filters still apply.
4. Fallback: if the preferred model fails to load, the next eligible model is tried once; the
   failure is audited and reported.
5. One loaded model at a time (Stage 3 runtime constraint). Selection never silently switches
   the model mid-task.

## Context assembly

- Sections, in fixed order: system policy, mode instructions, task state, recent conversation,
  retrieved knowledge (empty until Stage 6), untrusted external content.
- A token budget derived from the selected model's context window is split per section with
  reserved output space; overflow drops oldest conversation turns first and never drops the
  system policy.
- Untrusted content is wrapped and labeled; the system policy states that content inside it is
  data, not instructions.
- The assembler is deterministic and unit-tested with a token counter abstraction.

## Policy Engine (security-critical)

- Levels L0–L4 from the specification; each level maps to `Allow`, `RequireConfirmation` or
  `Deny` per action type and mode.
- Decisions are made by the Policy Engine, never by the LLM. The LLM may only propose an
  action with typed arguments.
- `RequireConfirmation` produces a pending confirmation with a correlation id; nothing executes
  until a user decision arrives. No auto-confirm path exists.
- Every decision is audited with action type, target, level, decision and reason — no payload
  contents.
- Changing policy rules, the permission model or the core requires owner approval and a new
  ADR; the engine is deny-by-default for unknown action types.
- Stage 4 exercises policy with typed action descriptors and a fake tool dispatcher; real
  tools arrive in Stage 5 and must reuse the same engine.

## Tests

- **Unit:** intent classification, planner bounds, orchestrator filters and ranking, context
  budgeting and section order, policy decision matrix, task state transitions, limit checks.
- **Integration:** migration 3 and store round-trips; restart/resume; pause/cancel; end-to-end
  run with a fake runtime; host DI wiring with the echo ModelHost; audit rows for each decision.
- **Architecture:** `Astra.Agent` has no reference to LLamaSharp or `Astra.ModelHost.*`;
  `Astra.Core` references nothing; no cloud AI packages anywhere.
- **Security:** policy bypass attempts are denied; untrusted content cannot change the goal or
  permissions; audit and logs contain no prompt text, completion text or secrets; unknown
  action types are denied.

## Exit criteria

1. ADRs 0010–0013 approved and recorded.
2. A request runs through the full pipeline with a deterministic runtime and produces a
   persisted task, steps, checkpoint and response.
3. Tasks survive a restart: `Paused` recovery state is produced and resume completes the task.
4. Pause, resume and cancel work and are covered by tests.
5. Limits (time, iterations, tokens, tool calls) are enforced and produce typed failures.
6. Model selection follows the documented filters/ranking and never switches models mid-task
   without an explicit decision.
7. Every action boundary goes through the Policy Engine; unknown action types are denied.
8. Audit trail is append-only and contains no prompts, completions or secrets.
9. ModelHost is started by the host on demand and cannot outlive a crashed host.
10. Full suite green on Windows and Linux without models, GPU or network; `dotnet format`
    clean; documentation and stage report updated.

## Risks and open questions

- **Scope creep:** the pipeline can absorb everything. The plan keeps tools, memory, UI and
  autonomy out; a new requirement means a new stage or an ADR.
- **Token counting:** exact tokenization depends on the model. Stage 4 uses a conservative
  character-based estimate behind `ITokenCounter`, replaced by the model tokenizer when
  available.
- **Confirmation flow without UI:** Stage 4 can record pending confirmations but cannot show
  them to a user. Until Stage 7, `RequireConfirmation` resolves to `Deny` in headless runs.
- **Policy Engine approval:** this is the security-critical part of the stage and must be
  reviewed by the owner before implementation.
- **ModelHost orphan prevention:** the job object is Windows-specific; Linux needs a
  `PR_SET_PDEATHSIG`/process-group equivalent. Both must be tested by killing the parent.
- **Prompt-injection testing:** requires a corpus of adversarial documents; add a small
  fixture set in Stage 4 and expand it with memory/tools.

## Verification plan

```powershell
dotnet restore AstraAgent.slnx --locked-mode
dotnet build AstraAgent.slnx --no-restore -c Release
dotnet test AstraAgent.slnx --no-build -c Release
dotnet format AstraAgent.slnx --verify-no-changes
```

CI stays model-free: the pipeline uses a deterministic fake runtime and the echo ModelHost.
Real-model validation remains the hardware-gated test from Stage 3.

## References

- [FINAL_PRODUCT_SPECIFICATION.md](FINAL_PRODUCT_SPECIFICATION.md) — section 4 (Agent Core),
  section 16 (security), section 18 (resources)
- [ROADMAP.md](ROADMAP.md)
- [ARCHITECTURE.md](ARCHITECTURE.md)
- [SECURITY.md](SECURITY.md)
- [STAGE_3_MODEL_RUNTIME_REPORT.md](STAGE_3_MODEL_RUNTIME_REPORT.md)
- ADRs 0001–0013
