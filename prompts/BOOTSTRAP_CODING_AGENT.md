# Bootstrap Prompt for a Coding Agent

You are contributing to Astra Agent, a single local-first autonomous AI agent.

Start by reading, in this order:

1. `AGENTS.md` — mandatory working rules.
2. `prompts/PROJECT_HANDOFF.md` — current state, evidence, next work, pitfalls.
3. `docs/FINAL_PRODUCT_SPECIFICATION.md` — approved specification.
4. `docs/ROADMAP.md` and `docs/PROJECT_STATUS.md`.
5. `docs/ARCHITECTURE.md`, `docs/MODEL_HOST_IPC.md` and the current stage document.
6. Relevant `skills/*/SKILL.md`.

The product specification is approved. Stages 0–2 are complete; Stage 3 (Model Runtime) is in
progress. Unless the owner assigns a different task, continue with the next incomplete phase
listed in `prompts/PROJECT_HANDOFF.md` section 6 — currently the Windows CPU/CUDA hardware
validation and the Stage 3 report.

Your first response must:

- Summarize the repository state from `git log -1`, `git status` and the docs.
- Identify the current roadmap stage and the next incomplete phase.
- Inspect the installed SDKs/toolchains and run the documented restore/build/test/format
  checks.
- List the minimum decisions needed for the next step.
- Propose a small implementation plan and acceptance criteria.

Do not create the entire product in one pass. Do not create empty placeholder modules. Do not
implement cloud AI integrations or multiple runtime agents. Astra is one agent using multiple
local models as interchangeable engines. Do not start Stage 4 implementation before Stage 3 is
signed off unless the owner explicitly asks.

External coding agents are allowed as development tools, but must not be included in Astra's
runtime architecture.

Before modifying architecture/security/data/API contracts, propose an ADR and get owner
approval. Execute the relevant checks and report exact commands/results. Do not claim unrun
tests passed. Do not commit model weights, secrets or user data, and do not log prompts or
file contents — central log redaction is not implemented yet.
