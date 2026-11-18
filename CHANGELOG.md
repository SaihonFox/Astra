# Changelog

## Unreleased

### Added
- Foundation solution `AstraAgent.slnx`.
- `Astra.Host` composition root with `/health` endpoint.
- `Astra.Host.Tests` integration test project.
- Shared build configuration: nullable, analyzers, warnings-as-errors, deterministic build,
  central package management, package lock files.
- CI workflow for Linux and Windows (build + test).
- ADRs 0001–0005 (accepted): solution layout, configuration/secrets, logging/audit,
  testing, IPC and process boundaries.
- MIT `LICENSE` and `LICENSE-DECISION.md`; initial dependency licenses checked.
- `global.json`, `.gitattributes`.
- `docs/STAGE_1_FOUNDATION_REPORT.md` (draft, pending CI).

### Changed
- Line endings normalized to LF; `.editorconfig` now uses LF and spaces for YAML.
- Development guide and project status updated with verified commands and results.
- CI now verifies formatting in addition to build and tests.
- ADRs 0001–0005 moved to Accepted after owner approval.

### Earlier
- Initial documentation and coding-agent bootstrap package.
- Approved Final Product Specification added.
- Agent instructions, skills, roadmap, architecture and security guidance added.
