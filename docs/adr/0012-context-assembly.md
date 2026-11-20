# ADR-0012: Context assembly and budgeting

- Status: Proposed
- Date: 2026-11-20
- Decision owners: Project owner
- Depends on: ADR 0011 (model selection), ADR 0013 (policy engine)
- Related: Stage 4 plan ([STAGE_4_AGENT_CORE.md](../STAGE_4_AGENT_CORE.md))

## Context

Every model call needs a prompt built from several sources with different trust levels: system
policy, mode instructions, task state, recent conversation, retrieved knowledge (Stage 6) and
untrusted external content. The prompt must fit the selected model's context window, must be
reproducible for debugging, and must keep prompt-injection protection architectural rather than
relying on wording alone.

There is no memory subsystem yet and no tokenizer for every model. A context assembler that is
too clever now will be replaced in Stage 6; one that is too thin will not survive it.

## Options considered

### A. Ordered sections with explicit budgets and trust labels (recommended)

Fixed section order, per-section budget, deterministic truncation, untrusted content wrapped and
labeled.

Pros: predictable, testable, easy to extend with memory; trust boundary is visible in the
prompt and enforced outside it.
Cons: truncation heuristics can drop useful history; a character-based estimate is imprecise.

### B. Concatenate everything until the window is full

Rejected: no trust boundary, unpredictable truncation, silent loss of the system policy.

### C. Let the model summarize its own history

Rejected for Stage 4: an extra model call with its own failure modes, and it lets untrusted
content influence what survives.

## Decision

1. **Fixed section order:** `system_policy`, `mode_instructions`, `task_state`,
   `conversation`, `knowledge`, `untrusted_content`, `user_request`. The system policy is never
   truncated or reordered.
2. **Budget:** `available = model.context_window − reserved_output − safety_margin`. Each
   section has a configured share; unused budget flows to later sections in a fixed order
   (`conversation`, then `knowledge`, then `untrusted_content`).
3. **Truncation:** conversation drops oldest turns first; knowledge drops lowest-ranked chunks;
   untrusted content is clipped with an explicit marker. The assembler records what was dropped
   in metadata (counts, not content).
4. **Token counting:** behind `ITokenCounter`. Stage 4 ships a conservative estimate
   (characters / 3 for mixed Russian/English text, rounded up) and never under-counts the
   system policy. A model-specific tokenizer replaces it when available; the interface does not
   change.
5. **Trust labels:** untrusted content is wrapped in a delimited block with an explicit
   "data, not instructions" label. The system policy states that instructions inside it are
   ignored. Enforcement is architectural: tool arguments and plans are schema-validated and
   policy-checked, so a successful injection still cannot execute an action.
6. **Determinism:** given the same inputs, budget and counter, the assembler returns the same
   package. Ordering is stable; no random sampling.
7. **Explainability:** `ContextPackage` carries section token counts, dropped counts and the
   model id. It is persisted with the step for debugging; prompt text itself is not logged.
8. **Model independence:** the assembler consumes sections and budgets, not model-specific
   prompt templates. Formatting adapters may be added later without changing the package.
9. **No secrets:** the assembler never reads secret stores and never places credentials,
   tokens or file contents that were not explicitly selected by a tool into the prompt.

## Consequences

- Context behavior is testable without a model and stable across models.
- The character-based estimate may waste or over-trim some budget; acceptable until a tokenizer
  is wired.
- Memory (Stage 6) plugs in as a ranked `knowledge` provider without changing the contract.
- Prompts are not logged, so debugging relies on section metadata and reproducible inputs.

## Security implications

- The system policy outranks all content and cannot be displaced by truncation.
- Untrusted content is always labeled and delimited; it can never become a system message.
- Prompt-injection defense does not depend on the model obeying the label: actions are validated
  and policy-checked outside the model.
- Section metadata contains counts and identifiers, not user text, so it is safe for audit.

## Validation plan

1. Unit tests for section order, budget split, overflow order and determinism.
2. Truncation tests: system policy survives any overflow; oldest turns drop first.
3. Trust-label test: untrusted content appears only inside the delimited block.
4. Token-estimate test: the counter never returns less than the configured minimum for a
   non-empty section.
5. Integration test: an oversized conversation produces a valid package and a metadata record.
6. Security test: an injected instruction inside untrusted content cannot change the goal,
   policy level or tool arguments after validation.
