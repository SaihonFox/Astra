# ADR-0007: In-process eventing

- Status: Accepted
- Date: 2026-11-18
- Accepted: 2026-11-18
- Decision owners: Project owner

## Context

Agent subsystems must react to actions, task progress, background work and system events
without direct coupling. The specification defines a versioned event envelope with
`EventId`, `TaskId`, `Timestamp`, `EventType`, `Payload` and `SchemaVersion`. No external
broker or cross-process messaging is needed in Stage 2.

## Decision

1. Events are immutable records wrapped in `EventEnvelope<TEvent>` with:
   - `EventId` (UUID v7, sortable),
   - optional `TaskId`,
   - UTC `Timestamp`,
   - non-empty `EventType`,
   - positive integer `SchemaVersion`,
   - non-null payload.
2. `IEventBus` provides typed publish/subscribe inside a single process. No reflection-based
   dispatch, no wildcard subscriptions and no hidden global state in consumers.
3. Delivery is sequential in subscription order for determinism. A failing subscriber does
   not prevent other subscribers from receiving the event; failures are aggregated and
   surfaced to the publisher after delivery completes.
4. Subscriptions are disposable and thread-safe. Publishing uses a snapshot, so subscribing
   or unsubscribing during delivery does not affect the current publish.
5. Events are not persisted and there is no outbox yet. Persistence, replay and
   cross-process delivery are added only with a concrete requirement (task recovery, IPC);
   that decision requires a new or superseding ADR.
6. Breaking payload changes increment `SchemaVersion`. Serialization at boundaries (API,
   IPC, logs) must include the event type and version; consumers reject unknown versions.
7. Event payloads may contain untrusted data. Events never carry authorization or permission
   grants.

## Alternatives considered

- **MediatR**: extra dependency and request/response semantics that do not match broadcast
  events.
- **`System.Threading.Channels` queue per subscriber**: useful for backpressure and slow
  consumers, but premature before background workers exist.
- **Reactive Extensions**: heavier model and harder-to-audit scheduling.
- **External broker / message bus**: rejected — Astra is one local agent; no service mesh
  requirement.

## Consequences

- Handlers run on the publisher's async flow; a slow handler delays publishing. Long work
  must be queued by the consumer in a later stage.
- Exception aggregation keeps one bad handler from silently starving others.
- A future durable/outbox implementation can keep the same envelope and `IEventBus` contract.

## Security implications

No ambient authority is transferred through events. Consumers must still validate input and
pass policy checks before executing privileged actions. Event payloads must not contain
secrets; logging of payloads follows ADR 0003 redaction rules.

## Validation plan

- Unit tests: delivery to multiple subscribers, unsubscribe, ordering, handler isolation and
  aggregated failures, cancellation propagation.
- Envelope tests: validation of type, version, payload, UTC normalization and sortable IDs.
- Architecture review before cross-process events are introduced.
