# ADR-0002: Configuration and secrets

- Status: Accepted
- Date: 2026-11-18
- Accepted: 2026-11-18
- Decision owners: Project owner

## Context

Astra runs continuously, has user-scoped settings, must remain portable across Windows and
Linux, and must never store secrets in source files, ordinary JSON, SQLite, or logs.
Foundation needs one configuration model that later subsystems (models, memory, API,
permissions, resources) can extend without redesign.

## Decision

1. Use the standard .NET configuration stack through `IConfiguration` and the options
   pattern. Code depends on typed options, not on raw string keys.
2. Source precedence, lowest to highest:
   1. committed `appsettings.json` — product defaults only, no secrets;
   2. `appsettings.{Environment}.json` — non-secret environment defaults;
   3. user-scoped configuration file in the OS application-data directory — created in a
      later stage;
   4. environment variables with the `ASTRA_` prefix — operational overrides;
   5. command-line arguments — one-off overrides for tools and diagnostics.
3. Options are validated at startup (DataAnnotations or explicit validators). Invalid
   required configuration fails fast instead of degrading silently.
4. Secrets are never placed in configuration files, source control, SQLite, or logs. They
   are read from OS secret stores (Windows Credential Manager/DPAPI, Linux Secret Service
   or an equivalent) behind a dedicated abstraction introduced in Stage 2.
5. Persisted user settings that must be changed at runtime are stored with an explicit
   schema version and migrations; they are not edited by hand as JSON.
6. The host binds to loopback by default. LAN exposure requires authenticated access and
   is a separate, explicit configuration change (specification §15).
7. Development may use .NET user secrets; user secrets are not a production secret store.

## Alternatives considered

- **Single JSON file as the only source**: rejected — poor operational overrides, high risk
  of committing secrets.
- **Database-only configuration**: rejected — configuration must be readable before the
  database opens and must support recovery.
- **Custom configuration framework**: rejected — no requirement justifies it.

## Consequences

- Configuration keys and option types are stable contracts; changes require migration notes.
- Environment variables give CI and operators a secret-free override path.
- Secret access is abstracted, so tests can substitute an in-memory provider without
  weakening production behavior.

## Security implications

Separating configuration from secrets prevents accidental credential persistence. Startup
validation reduces misconfiguration-driven exposure (for example, binding to all
interfaces). Logs must not print option values that may contain sensitive paths or tokens.

## Validation plan

- Configuration binding tests with valid and invalid values, including fail-fast behavior.
- A test that no secret-like keys are read from committed JSON files.
- Review of the default bind address during API work.
