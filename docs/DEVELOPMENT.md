# Development Guide

## Prerequisites

- .NET 10 SDK. Verified locally with **10.0.115**; `global.json` pins the 10.0.1xx feature
  band with `rollForward: latestFeature`.
- Git.
- Windows build environment (primary platform) and Linux build environment/CI.
- Kotlin/Android toolchain — only when the Android stage begins.
- Native build prerequisites for llama.cpp — only when the Model Runtime stage begins.

Verify current stable versions before adding a dependency. Do not assume versions from old
notes are still current.

## Repository layout

```
AstraAgent.slnx            solution (projects added only when they have a purpose)
global.json                SDK band
Directory.Build.props      shared compiler, analyzer and build settings
Directory.Packages.props   central package versions
src/Astra.Host/            composition root and ASP.NET Core boundary (Stage 1)
tests/Astra.Host.Tests/    integration tests for the host
docs/                      specification, architecture, ADRs, status
skills/, prompts/          guidance for coding agents
.github/workflows/ci.yml   Linux + Windows build/test
```

## Repository conventions

- Solution format: `.slnx`.
- Nullable reference types, analyzers and warnings-as-errors are enabled repository-wide.
- Async APIs for I/O; propagate `CancellationToken`.
- UI work must not block the UI thread.
- Keep dependency direction explicit (ADR 0001).
- Prefer small, testable modules; no empty projects or premature abstractions.
- Package versions are centralized in `Directory.Packages.props`; restore uses package lock
  files. After changing a package version run `dotnet restore` and commit the updated
  `packages.lock.json` files.
- Files use LF line endings (`.editorconfig`, `.gitattributes`).
- Do not commit secrets. Configuration rules are in ADR 0002.

## Commands

Verified with SDK 10.0.115 on Ubuntu 26.04:

```bash
dotnet restore AstraAgent.slnx
dotnet build AstraAgent.slnx --no-restore -c Release
dotnet test AstraAgent.slnx --no-build -c Release
dotnet run --project src/Astra.Host
curl http://127.0.0.1:5298/health
```

CI runs with `--locked-mode` restore. Keep lock files committed.

## Workflow

1. Read `AGENTS.md` and relevant docs.
2. Inspect repository state.
3. Create a small implementation plan.
4. Add/update an ADR for architectural decisions.
5. Implement a focused change.
6. Run build and tests.
7. Report exact commands and results.
8. Update docs and project status.

## Quality gates

A change should include:
- Purpose and scope.
- Design summary.
- Tests added/updated.
- Actual test output summary.
- Security implications.
- Platform compatibility.
- Known limitations.

Do not copy commands into a report unless they were actually executed.
