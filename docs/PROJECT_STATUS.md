# Project Status

**Current stage:** Stage 1 — Foundation (in progress)
**Specification:** approved by project owner on 2026-11-18
**Implementation:** solution skeleton and build/test infrastructure exist; product subsystems not started
**Last updated:** 2026-11-18

## Completed

### Stage 0
- Product concept and scope.
- Single-agent / multiple-local-models architecture decision.
- Platform and client direction.
- Autonomy and initiative policy.
- Memory and personalization direction.
- Tool and OS integration scope.
- Development environment scope.
- Security and permission levels.
- LAN-only remote access.
- Research and self-improvement direction.
- Deployment and operations scope.
- Plugins and Skills direction.
- Storage and backup direction.
- Resource management direction.
- Preliminary technology stack.
- Final Product Specification approved.

### Stage 1 — Foundation (partial)
- Toolchain verified on 2026-11-18: .NET SDK 10.0.115, Ubuntu 26.04, NuGet reachable.
- ADRs 0001–0005 accepted by the owner on 2026-11-18 (solution layout,
  configuration/secrets, logging/audit, testing, IPC).
- MIT license selected; `LICENSE` and `LICENSE-DECISION.md` added. Initial dependency
  licenses checked (Microsoft — MIT, xUnit — Apache-2.0).
- `AstraAgent.slnx` created with real projects only: `Astra.Host`, `Astra.Host.Tests`.
- Shared build settings: nullable, analyzers, warnings-as-errors, deterministic build,
  central package management, package lock files.
- Minimal host with `/health` endpoint bound to loopback in development.
- Integration test for the health endpoint.
- CI workflow for Linux and Windows added (not yet executed on GitHub). Includes locked
  restore, Release build, format verification and tests.
- Line endings normalized to LF; `.editorconfig` corrected for YAML.
- Clean-copy verification: restore, build and tests succeed without `bin/obj` and without
  an existing `packages.lock.json` mutation.

## Verification performed

On 2026-11-18, Ubuntu 26.04, SDK 10.0.115:

```
dotnet restore AstraAgent.slnx --locked-mode                  → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release          → 0 warnings, 0 errors
dotnet test AstraAgent.slnx --no-build -c Release             → 1 passed, 0 failed, 0 skipped
dotnet format AstraAgent.slnx --verify-no-changes             → exit 0
```

A clean copy without `bin/obj` was restored with `--locked-mode` and passed build and tests.

Windows build and GitHub Actions execution are **not yet verified**.

## Current work

Finish Stage 1:
- Commit and push the repository, including `packages.lock.json` files.
- Run the CI workflow and record results for Ubuntu and Windows.
- Verify the Windows build; fix platform issues if any.
- Sign off [the Foundation report](STAGE_1_FOUNDATION_REPORT.md).

## Not yet implemented

All product subsystems: Agent Core, model runtime, memory, tools, policy engine, API
surface, desktop UI, Android client, extensions and learning.

## Risks / decisions to validate during implementation
- Exact .NET 10 SDK and Avalonia compatibility at implementation time.
- llama.cpp packaging and Windows/Linux runtime strategy.
- GPU backend support and actual GTX 1650 compatibility.
- Vector search library and multilingual embedding model selection.
- Sandbox implementation per OS.
- IPC protocol details per boundary (ADR 0005 defines the strategy, not the wire format).
- Android LAN discovery and secure pairing.
- Voice stack and local speech models.
- LoRA/QLoRA feasibility on target hardware.
- Extension signing/trust model.
- CI cost/availability and Windows runner coverage for the primary platform.

## Reporting rule
Never mark an item completed without evidence. Record exact build/test commands and results.
