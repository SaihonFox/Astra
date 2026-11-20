# Development Guide

## Prerequisites

- .NET 11 SDK. Verified locally with **11.0.100**; `global.json` pins the 11.0.1xx feature
  band with `rollForward: latestFeature` (ADR 0009).
- Git.
- Windows build environment (primary platform) and Linux build environment/CI.
- Kotlin/Android toolchain — only when the Android stage begins.
- No native build tools are needed: NUGet ships prebuilt llama.cpp libraries
  (`LLamaSharp.Backend.Cpu`). The optional CUDA 12 backend additionally requires the CUDA 12
  toolkit for `cudart64_12.dll`/`cublas64_12.dll` and `CUDA_PATH` detection.

Verify current stable versions before adding a dependency. Do not assume versions from old
notes are still current.

## Repository layout

```
AstraAgent.slnx            solution (projects added only when they have a purpose)
global.json                SDK band
Directory.Build.props      shared compiler, analyzer and build settings
Directory.Packages.props   central package versions
src/Astra.Core/            model-independent contracts (clock, events, settings)
src/Astra.Infrastructure/  SQLite, migrations, event bus, paths, system clock
src/Astra.Host/            composition root and ASP.NET Core boundary
src/Astra.ModelHost.Contracts/  gRPC contract, protocol version, endpoint types
src/Astra.ModelHost/       separate inference process (LLamaSharp, pipe/UDS)
src/Astra.ModelHost.Client/     ILocalModelRuntime client and process supervision
tests/Astra.Core.Tests/
tests/Astra.Infrastructure.Tests/
tests/Astra.Host.Tests/    integration tests for the host
tests/Astra.ModelHost.Tests/    IPC, lifecycle, limits, supervision, hardware-gated model test
docs/                      specification, architecture, ADRs, status, stage reports
skills/, prompts/          guidance for coding agents
.github/workflows/ci.yml   Ubuntu + Windows build/test
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

Verified with SDK 11.0.100 on Windows 10 22H2 and Ubuntu 26.04 (see the Stage 3 report):

```bash
dotnet restore AstraAgent.slnx --locked-mode
dotnet build AstraAgent.slnx --no-restore -c Release
dotnet test AstraAgent.slnx --no-build -c Release
dotnet format AstraAgent.slnx --verify-no-changes
ASTRA_DATA_DIR=/tmp/astra-data dotnet run --project src/Astra.Host
curl http://127.0.0.1:5298/health
```

Real-model validation is manual and hardware-gated:

```bash
ASTRA_TEST_MODEL=<absolute path to .gguf> \
  dotnet test tests/Astra.ModelHost.Tests -c Release --filter "Category=Hardware"
```

Add `ASTRA_TEST_GPU_LAYERS=99` and `-p:UseCuda=true` for the CUDA check. See
[STAGE_3_HARDWARE_VALIDATION.md](STAGE_3_HARDWARE_VALIDATION.md).

The host creates `<data root>/database/astra.db`, `<data root>/logs/astra-*.jsonl` and the
`ipc`/`models`/`knowledge`/`projects`/`extensions`/`cache`/`backups` areas. ModelHost adds
`logs/modelhost-*.jsonl` and, on Linux/macOS, the socket under `ipc/`. Without
`ASTRA_DATA_DIR` (or `Astra:DataDirectory`), the data root is the OS local application-data
directory plus `Astra`. Tests always use temporary directories.

CI runs with `--locked-mode` restore. Keep lock files committed.

## Workflow

1. Read `AGENTS.md`, `prompts/PROJECT_HANDOFF.md` and relevant docs.
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
