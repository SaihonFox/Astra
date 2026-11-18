# ADR-0003: Logging and audit trail

- Status: Accepted
- Date: 2026-11-18
- Accepted: 2026-11-18
- Decision owners: Project owner

## Context

Astra performs long-running work with tools, background jobs and autonomous actions. The
specification mandates Serilog and requires that logs never reveal secrets and that
sensitive actions are auditable. Diagnostic logging and the security audit trail have
different retention, integrity and access requirements.

## Decision

1. Application code depends on `Microsoft.Extensions.Logging.ILogger<T>`; Serilog is the
   provider configured at the composition root. This keeps libraries provider-neutral and
   allows tests to capture logs.
2. Serilog is adopted in Stage 2 (Core Infrastructure), when the first real subsystems
   produce logs. Stage 1 uses the built-in provider so the abstraction and sink layout are
   validated before adding package surface.
3. Diagnostic logs are structured. Default sinks: console in Development, rolling JSON files
   in the user log directory. Rotation and retention limits are configured, not unbounded.
4. Log events carry correlation fields where applicable: `EventId`, `TaskId`, `SessionId`,
   process name and schema version. Field names follow `docs/API.md` so logs and API events
   stay consistent.
5. Redaction is applied centrally (sink/enricher level) for secrets, tokens, credentials and
   configured sensitive paths. Untrusted external content is truncated and clearly marked
   as data, never promoted into instructions.
6. The audit trail is separate from diagnostics: append-only, tamper-evident, retained per
   policy, recording actor, action, permission level, target, decision and result. It is
   introduced together with the Policy Engine in a later stage.
7. Log levels are configurable per category. Production defaults avoid debug payload dumps.

## Alternatives considered

- **Serilog directly everywhere**: rejected — couples all code to one provider and makes
  test capture harder.
- **Built-in logging only**: rejected — the specification requires Serilog and structured
  file sinks with enrichment.
- **Audit records inside diagnostic logs**: rejected — integrity, retention and access
  requirements differ.

## Consequences

- Stage 1 has minimal logging surface; the provider swap in Stage 2 is localized.
- Library code remains testable with `NullLogger` or a test logger.
- Audit storage will need its own schema and integrity design.

## Security implications

Central redaction reduces accidental secret leakage. Treating external content as data in
logs supports prompt-injection defense. The audit trail must not be writable by the agent's
own unprivileged execution path.

## Validation plan

- A test logger captures structured events and correlation fields.
- Redaction tests with representative secret-shaped values before Serilog adoption.
- Security review of sinks, file permissions and retention at Stage 2.
