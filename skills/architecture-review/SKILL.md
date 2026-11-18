---
name: architecture-review
description: Review or change Astra architecture while preserving the approved single-agent, local-first design.
---

# Architecture Review Skill

1. Read `AGENTS.md`, the final specification, and current architecture docs.
2. Identify the affected layer and dependency direction.
3. Keep decision-making separate from execution.
4. Keep model adapters replaceable; memory and task state must not depend on a model.
5. Keep the product as one agent. Internal roles are modes, not independent agents.
6. Do not introduce cloud AI providers, multi-agent orchestration, or mandatory microservices.
7. For significant decisions, write an ADR with alternatives and consequences.
8. Identify failure modes, lifecycle, cancellation, persistence, and recovery.
9. Include tests that enforce architecture boundaries.
10. Update architecture and status documentation.
