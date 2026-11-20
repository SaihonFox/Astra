# ADR-0001: Solution layout and dependency direction

- Status: Accepted
- Date: 2026-11-18
- Accepted: 2026-11-18
- Decision owners: Project owner

## Context

Astra is a modular monolith with a small number of process boundaries (`Astra.Host`,
`Astra.ModelHost`, `Astra.Sandbox`, `Astra.ExtensionHost`). The specification proposes a
broad source layout but explicitly forbids creating empty projects for future modules.
Stage 1 must establish a reproducible build and an explicit dependency direction before
product subsystems exist.

## Decision

1. One solution at the repository root: `AstraAgent.slnx`. Target framework `net11.0`,
   C# 15 (see ADR 0009; originally `net10.0`, C# 14).
2. Production projects live in `src/Astra.<Capability>/`; test projects live in
   `tests/<ProductionProject>.Tests/`.
3. Modules are added only when they contain a concrete implementation or a shared
   contract. No placeholder projects.
4. `Astra.Host` is the composition root and the only executable created at this stage.
   It may reference installed modules. Modules must not reference `Astra.Host`.
5. Modules do not reference each other's implementation assemblies. Shared contracts live
   in a dedicated contracts project (`Astra.Core` or a focused `Astra.<Area>.Contracts`)
   created together with the first shared contract. Dependencies must remain acyclic.
6. Common build settings (nullable, analyzers, warnings-as-errors, deterministic build,
   package lock files) live in `Directory.Build.props`; package versions are centralized in
   `Directory.Packages.props`.
7. Additional processes are created only when a concrete isolation, lifecycle, or runtime
   requirement exists, and only as described in the specification.
8. Avalonia Desktop (Stage 7) and the Android client (Stage 13) are not added to the
   solution before their stages begin.

## Alternatives considered

- **Create the full proposed folder/project tree now** (Core, Agent, Models, Intelligence,
  Coding, System, Tools, Projects, Learning, Extensions, Infrastructure): rejected because
  it produces empty projects, premature abstractions and unclear ownership.
- **Single project for everything**: rejected because it erases module boundaries that the
  policy, safety and testing model depends on.
- **Separate solution per process**: rejected as unnecessary complexity while only one
  process exists.

## Consequences

- The repository stays small and every project has a purpose.
- Module boundaries are established by convention first and by architecture tests once at
  least two modules exist.
- Refactoring into new projects is expected as capabilities arrive; moving code later is
  cheaper than maintaining empty modules.

## Security implications

`Astra.Host` will wire the Policy Engine and permission checks at the composition root.
Keeping execution adapters out of the decision layer preserves the rule that the model
proposes but never authorizes. Process isolation is added only with a real boundary and a
security review.

## Validation plan

- `dotnet build AstraAgent.slnx` and `dotnet test AstraAgent.slnx` succeed on Linux and
  Windows in CI.
- Structure review at the end of each stage confirms no empty or orphan projects.
- Architecture tests enforcing dependency direction are added when a second module lands.
