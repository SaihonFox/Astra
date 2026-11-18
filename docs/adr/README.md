# Architecture Decision Records

Create one Markdown file per significant decision, using sequential identifiers such as
`0001-solution-layout.md`.

An ADR should contain:
- Status: Proposed / Accepted / Superseded.
- Context.
- Decision.
- Alternatives considered.
- Consequences.
- Security implications.
- Validation plan.

Do not silently reverse an accepted decision. Create a superseding ADR and obtain owner
approval for changes that affect the product specification.

## Index

| ADR | Title | Status |
|---|---|---|
| [0001](0001-solution-layout.md) | Solution layout and dependency direction | Accepted |
| [0002](0002-configuration-and-secrets.md) | Configuration and secrets | Accepted |
| [0003](0003-logging-and-audit.md) | Logging and audit trail | Accepted |
| [0004](0004-testing-baseline.md) | Testing baseline | Accepted |
| [0005](0005-ipc-and-process-boundaries.md) | IPC and process boundaries | Accepted |
| [0006](0006-persistence-sqlite.md) | Persistence baseline — SQLite, migrations, settings | Proposed |
| [0007](0007-in-process-eventing.md) | In-process eventing | Proposed |
