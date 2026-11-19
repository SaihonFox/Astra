# ADR-0005: IPC and process boundaries

- Status: Accepted
- Date: 2026-11-18
- Accepted: 2026-11-18
- Decision owners: Project owner

## Context

The specification defines four processes: `Astra.Host`, `Astra.ModelHost`, `Astra.Sandbox`
and `Astra.ExtensionHost`. They have different isolation and lifecycle needs. Choosing an
ad-hoc transport later would create incompatible protocols and security holes. Stage 1 does
not implement IPC, but the strategy must be fixed before those processes are built.

## Decision

1. In-process calls are the default. Modules inside `Astra.Host` communicate through typed
   interfaces and versioned events. A process boundary is introduced only for a concrete
   requirement: native inference lifecycle, untrusted execution, or third-party code.
2. Host ↔ `Astra.ModelHost` and Host ↔ `Astra.Sandbox`: gRPC over Named Pipes on Windows and
   Unix Domain Sockets on Linux. No TCP listener by default.
3. Host ↔ `Astra.ExtensionHost`: versioned JSON-RPC 2.0 over stdio or a local pipe, matching
   the external extension contract in the specification. Extensions run with minimal
   permissions in a separate process.
4. Every boundary:
   - negotiates a protocol/contract version during handshake and rejects incompatible peers;
   - uses typed messages with explicit schema versions;
   - propagates cancellation and enforces per-call and per-task timeouts;
   - enforces a maximum message size and validates decoded input;
   - relies on OS access control for peer identity (pipe ACLs, socket file permissions).
5. Process lifecycle is owned by the host: start on demand, health supervision, bounded
   restart with backoff, graceful shutdown, and persisted task state for recovery.
6. Internal IPC is never exposed on a network interface. The public API (ASP.NET Core) is a
   separate, authenticated, LAN-limited boundary.
7. Startup must tolerate an unavailable optional process: work is queued or paused, not
   lost.

## Alternatives considered

- **HTTP over localhost TCP**: rejected for internal boundaries — larger attack surface,
  unnecessary listener, weaker peer identity.
- **In-process only for everything**: rejected — native inference crashes and untrusted
  extensions require isolation.
- **Shared memory or custom binary framing**: rejected — higher maintenance and security
  review cost without a measured need.

## Consequences

- Two protocol families exist: gRPC for trusted internal processes, JSON-RPC for external
  extensions. Contract versioning is mandatory in both.
- Named Pipes/UDS platform code needs separate tests on Windows and Linux.
- The host becomes responsible for supervision and recovery.

## Security implications

Local-only transports with OS ACLs reduce exposure. Version negotiation and message limits
resist malformed input. Extension trust is separate from transport: an authenticated pipe
does not make extension code trusted. Docker is not treated as a security boundary.

## Validation plan

- A later ADR or design note details pipe ACLs, socket permissions and handshake fields.
  Satisfied for ModelHost by [MODEL_HOST_IPC.md](../MODEL_HOST_IPC.md).
- Cross-platform IPC tests are required before the first process is added.
- Threat review covers each new boundary and its failure modes.
