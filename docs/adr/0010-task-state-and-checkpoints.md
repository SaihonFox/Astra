# ADR-0010: Task state, sessions and checkpoints

- Status: Proposed
- Date: 2026-11-20
- Decision owners: Project owner
- Depends on: ADR 0006 (persistence), ADR 0007 (in-process eventing)
- Related: Stage 4 plan ([STAGE_4_AGENT_CORE.md](../STAGE_4_AGENT_CORE.md))

## Context

Stage 3 persists model metadata only. Stage 4 introduces long-running work that must survive
crashes, restarts and user pauses: one agent with one task state, bounded by time, iteration,
token and tool-call limits. Chat history alone is not a source of truth, and task state must not
depend on a model or a provider.

The host is currently the only writer of the SQLite database (ADR 0006). ModelHost is a
separate process and must never write application state.

## Options considered

### A. In-memory task state with periodic serialization (recommended)

Keep the live task object in memory, persist every state transition and a checkpoint at step
boundaries. Rebuild state from the database on restart.

Pros: simple mental model, low write volume, natural resume point, testable state machine.
Cons: the in-flight step is lost on a hard crash and must be retried; needs an explicit
"recovering" state.

### B. Event-sourced task log

Persist every state-changing event and derive state by replay.

Pros: complete history, strong audit story.
Cons: replay cost, schema evolution for every event, two sources of truth with the audit trail,
more code than Stage 4 needs.

### C. Serialize the whole task object on every change

Pros: trivial recovery.
Cons: write amplification, no queryable progress, JSON blobs hide invariants from SQL.

## Decision

1. **Tables (migration 3, additive):** `sessions`, `messages`, `tasks`, `task_steps`,
   `task_checkpoints`, `audit_events`. No Stage 1–3 table is altered.
2. **Task row is the authority** for status, limits, counters and result summary. Steps and
   checkpoints are child rows ordered by sequence.
3. **Explicit state machine:** `Queued → Planning → Executing → Validating → Completed`, with
   `Failed`, `Cancelled`, `Paused` and `Queued` (resume) transitions. Invalid transitions are
   rejected in code and tested.
4. **Optimistic concurrency:** `tasks.version` increments on every update; writes use
   `UPDATE … WHERE id = @id AND version = @expected`. A mismatch is a typed conflict, not a
   silent overwrite.
5. **Checkpoints** are written at every step boundary and before a pause. A checkpoint stores a
   versioned JSON snapshot (`schema_version`) sufficient to resume without replaying history.
6. **Recovery on host start:** tasks in `Planning`, `Executing` or `Validating` become `Paused`
   with `recovery` reason and a checkpoint. Resume is explicit; no automatic re-execution of an
   in-flight step.
7. **Limits** (`TaskLimits`) are checked before each step: wall-clock deadline, iterations,
   tokens, tool calls. Exceeding a limit produces `Failed` with a typed reason.
8. **Sessions and messages** store conversation content locally as user data. Content is never
   written to logs and never to `audit_events`.
9. **Single writer:** only the host process writes. ModelHost and future sandbox processes
   receive no database access.
10. **Events:** state transitions publish the existing versioned `EventEnvelope` on the
    in-process bus; eventing stays non-durable (ADR 0007). The database is the durable record.

## Consequences

- Resume semantics are explicit and testable; a hard crash loses at most one step.
- Counters and limits live in one place, so a resumed task cannot exceed its budget.
- Migration 3 adds tables only; rollback is "stop using the new tables", data is not dropped.
- Event consumers cannot assume delivery; they must read the database for durable state.

## Security implications

- Task and message content is sensitive user data: stored under the data root with existing
  owner-only Unix permissions, never logged, never sent outside the machine.
- `audit_events` records decisions and metadata only — no prompts, completions, file contents
  or secrets.
- Optimistic concurrency prevents a stale or duplicate executor from overwriting state.
- No database access from inference or sandbox processes; no network exposure.

## Validation plan

1. Migration 3 applies on a fresh database and on a Stage 3 database; schema is additive.
2. Store round-trip tests for sessions, tasks, steps, checkpoints and audit rows.
3. State-machine tests for every valid and invalid transition.
4. Restart test: start a task, simulate a crash, reopen the database, assert `Paused` with a
   recovery checkpoint, resume and complete.
5. Concurrency test: two writers with the same version — exactly one succeeds.
6. Limit tests: deadline, iterations, tokens and tool calls produce typed failures.
7. Architecture test: `Astra.Core.Agent` has no persistence or inference dependency.
