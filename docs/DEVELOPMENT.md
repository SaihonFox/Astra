# Development Guide

## Prerequisites

Verify current stable versions before implementation:
- .NET 10 SDK.
- Git.
- Windows build environment.
- Linux build environment/CI.
- Kotlin/Android toolchain when Android stage begins.
- Native build prerequisites for llama.cpp only when Model Runtime stage begins.

Do not assume versions from old notes are still current.

## Repository conventions

- Solution format: `.slnx`.
- Nullable enabled.
- Async APIs for I/O.
- `CancellationToken` propagated.
- UI work must not block UI thread.
- Keep dependency direction explicit.
- Prefer small, testable modules.
- Avoid premature abstraction and empty projects.
- Use current stable package versions, pinned in project files/central package management after compatibility review.

## Workflow

1. Read `AGENTS.md` and relevant docs.
2. Inspect repository state.
3. Create a small implementation plan.
4. Add/update ADR for architectural decisions.
5. Implement a focused change.
6. Run build and tests.
7. Report exact commands and results.
8. Update docs and project status.

## Quality gates

A pull request/change should include:
- Purpose and scope.
- Design summary.
- Tests added/updated.
- Actual test output summary.
- Security implications.
- Platform compatibility.
- Known limitations.

## Commands

Commands must be adapted to the actual solution and SDK after they exist. Do not copy placeholder commands as evidence of successful tests.
