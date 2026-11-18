# Architecture Overview

## Core principle

Astra is one agent. The architecture separates decision-making from execution and enforces policy at tool boundaries.

## Logical layers

1. **Experience Layer** — Avalonia Desktop, Android client, CLI, API.
2. **Agent Layer** — request understanding, context, planning, task state, response.
3. **Intelligence Layer** — local model orchestration, inference adapters, prompt/context construction.
4. **Knowledge Layer** — memory, project context, RAG, research, document indexing.
5. **Execution Layer** — tools, OS adapters, browser, code environment, deployment.
6. **Policy & Security Layer** — permissions, approvals, sandbox profiles, audit, secrets.
7. **Infrastructure Layer** — persistence, eventing, configuration, logging, IPC.

## Decision/execution separation

The LLM may propose plans and tool arguments. It does not receive direct OS authority. The Tool Dispatcher validates schemas, Policy Engine checks permissions, and the execution adapter runs the action under the appropriate identity and sandbox.

## Process boundaries

- `Astra.Host`: orchestration and long-lived state.
- `Astra.ModelHost`: local inference lifecycle.
- `Astra.Sandbox`: untrusted or risky tool execution.
- `Astra.ExtensionHost`: external extension processes.

Do not split modules into processes without a concrete isolation, lifecycle, or runtime requirement.

## Task state

Represent long-running work as persisted task state with checkpoints. Avoid relying on in-memory chat history as the only source of truth.

## Model independence

Conversation state, memory, task state, tool contracts, and project data must not depend on a specific model vendor or model identity.

## Eventing

Events are versioned and carry stable IDs, task correlation, timestamps, type, payload schema/version. Event delivery should be resilient to reconnects and process restarts.

## Infrastructure baseline

- Persistence: SQLite through `Microsoft.Data.Sqlite`, append-only checksum-verified
  migrations, WAL mode. The host is the only writer at this stage (ADR 0006).
- Settings: small non-secret values in SQLite; secrets use OS secret stores (ADR 0002).
- Eventing: typed in-process bus with the versioned envelope; not durable yet (ADR 0007).
- Logging: Serilog structured logs to console and rolling compact JSON files (ADR 0003).
- Data root: OS local application-data directory plus `Astra`, overridable via
  `ASTRA_DATA_DIR` or `Astra:DataDirectory`.
- Model registry: model descriptors and metadata in SQLite, live file availability checks
  (ADR 0006 schema, migration 2). Runtime for actual inference is not implemented yet.

## Architecture decisions

Record significant decisions in `docs/adr/` using the template. Revisit a decision only with evidence and a new ADR.
