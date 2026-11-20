# ADR-0013: Policy Engine, permissions and audit

- Status: **Proposed — security-critical, requires explicit owner approval**
- Date: 2026-11-20
- Decision owners: Project owner
- Depends on: ADR 0003 (logging and audit), ADR 0010 (task state)
- Related: Stage 4 plan ([STAGE_4_AGENT_CORE.md](../STAGE_4_AGENT_CORE.md)), [SECURITY.md](../SECURITY.md)

## Context

The specification requires five permission levels (L0 read, L1 temporary, L2 file/project
changes, L3 system changes and software installation, L4 critical operations) with per-level
allow / confirm / deny behavior, audit of every action, and prompt-injection protection that
does not rely on the model. The LLM must never execute system actions directly.

Stage 4 introduces the first action boundaries: local inference, task state changes, context
assembly and (as typed descriptors) future tool calls. Stage 5 will add real tools. The policy
model must therefore be designed now, before any tool exists, and must be deny-by-default.

## Options considered

### A. Capability-based decisions in host code with an append-only audit (recommended)

A typed `PolicyRequest` describes the action; rules map (action type, level, mode, target,
context) to `Allow` / `RequireConfirmation` / `Deny`; every decision is audited.

Pros: deterministic, testable, independent of the model, easy to reason about and to review.
Cons: rules must be maintained as tools grow; a missing rule fails closed (intended).

### B. Prompt-based permission instructions

Rejected: prompt injection defeats it, and it contradicts the specification's requirement that
protection be architectural.

### C. OS-level permissions only

Rejected as the sole mechanism: the OS cannot express "this task may read this project but not
write it" or "confirm before installing software"; it is a second layer, not a replacement.

## Decision

1. **Single decision point.** All actions pass through `IPolicyEngine.EvaluateAsync`. There is
   no bypass path; tool dispatchers, the executor and future API endpoints call it.
2. **Levels and outcomes.** L0–L4 map to `Allow`, `RequireConfirmation` or `Deny` per action
   type, mode and target scope. Defaults are deny-by-default; unknown action types are `Deny`
   and audited.
3. **Typed requests.** `PolicyRequest` carries action type, level, target (path/URL/resource),
   mode, task id, correlation id and a non-sensitive summary. It never carries secrets or full
   payloads.
4. **Confirmation flow.** `RequireConfirmation` creates a pending confirmation with a
   correlation id and expiry; the action does not run until a user decision is recorded. There
   is no auto-confirm, no timeout-approval and no "remember for everything" shortcut. Until a
   UI exists (Stage 7), headless runs treat `RequireConfirmation` as `Deny`.
5. **Scoped temporary permissions (L1).** Grants are explicit, time-bounded, scoped to an
   action type and target, and revocable; they are stored outside the LLM's reach.
6. **Audit.** Every decision and every executed action writes an append-only `audit_events` row:
   timestamp, correlation id, actor (user/agent/tool), action type, target, decision, level and
   a short reason. No prompt text, completion text, file contents or secrets. Audit rows are
   never updated or deleted by the application.
7. **Prompt-injection boundary.** The Policy Engine evaluates the *action*, not the prose that
   requested it. Untrusted content cannot raise a level, pre-approve an action or change a
   policy rule.
8. **Change control.** Policy rules, the permission model and the engine are security-critical:
   changes require owner approval and a new ADR. Rules are versioned; the active rule set
   version is recorded with each decision.
9. **Core vs. tools.** Stage 4 implements the engine, the rules for inference/task/state
   actions, the confirmation store and the audit sink. Stage 5 tools must register their action
   types and levels and must not implement their own permission checks.
10. **No secrets in policy data.** Secret access is a separate capability resolved through the
    OS secret store (ADR 0002); the policy engine decides whether an action may *request* a
    secret, never stores the value.

## Consequences

- Every new capability must declare its action type and level; forgetting to do so fails closed.
- The confirmation UX becomes a Stage 7 dependency; Stage 4 is safe but less convenient.
- Audit grows monotonically; retention/rotation is a later operations decision.
- Rules are code plus configuration, so review and tests are the main safety mechanism.

## Security implications

- Deny-by-default and no bypass path are the core protections.
- Confirmation cannot be satisfied by the model; only an authenticated user decision counts.
- Audit is tamper-evident within the application (append-only); database-level tamper resistance
  and signing are future work.
- A compromised model can at most propose actions that policy rejects; it cannot grant itself
  permissions.
- No secrets are written to audit or logs; secret values never enter policy requests.

## Validation plan

1. Decision-matrix tests for every level × action type × mode, including unknown types.
2. Bypass tests: executor and fake tool dispatcher refuse to act without an `Allow` decision.
3. Confirmation tests: pending confirmation blocks execution; denial and expiry deny; no
   auto-approve path exists.
4. Temporary-permission tests: scope, expiry and revocation.
5. Audit tests: one row per decision, append-only, no prompt/completion/secret content.
6. Prompt-injection tests: injected instructions cannot raise a level or approve an action.
7. Review by the owner before any Stage 5 tool is added.
