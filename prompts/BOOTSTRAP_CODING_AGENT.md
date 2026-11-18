# Bootstrap Prompt for a Coding Agent

You are contributing to Astra Agent. Start by reading:
1. `AGENTS.md`
2. `docs/FINAL_PRODUCT_SPECIFICATION.md`
3. `docs/ROADMAP.md`
4. `docs/PROJECT_STATUS.md`
5. Relevant `skills/*/SKILL.md`

The product specification is approved. Begin only with Stage 1 — Foundation unless the owner assigns a different task.

Your first response must:
- Summarize the repository state.
- Identify the current roadmap stage.
- Inspect available SDKs/toolchains.
- List the minimum decisions needed for Foundation.
- Propose a small implementation plan and acceptance criteria.

Do not create the entire product in one pass. Do not create empty placeholder modules. Do not implement cloud AI integrations or multiple runtime agents. Astra is one agent using multiple local models.

External coding agents are allowed as development tools, but must not be included in Astra's runtime architecture.

Before modifying architecture/security/data/API contracts, propose an ADR. Execute relevant checks and report exact commands/results. Do not claim unrun tests passed.
