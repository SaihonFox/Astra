# Stage 1 — Foundation Report

- **Status:** Draft — pending CI execution on GitHub
- **Date:** 2026-11-18
- **Environment:** Ubuntu 26.04, .NET SDK 10.0.115, ASP.NET Core runtime 10.0.15

## Scope delivered

- Minimal solution `AstraAgent.slnx` with two purposeful projects:
  - `src/Astra.Host` — composition root and HTTP boundary with `/health`;
  - `tests/Astra.Host.Tests` — integration test of the health endpoint.
- Repository-wide build policy: `net10.0`, C# 14, nullable, analyzers,
  warnings-as-errors, deterministic build, central package management, lock files.
- Configuration baseline: loopback-only development endpoint, restricted `AllowedHosts`.
- CI workflow for Linux and Windows: locked restore, Release build, format check, tests,
  TRX artifact upload.
- ADR 0001–0005 accepted by the owner.
- MIT license selected and recorded.
- Documentation aligned with the actual structure and verified commands.

## Exit criteria

| Criterion | Status | Evidence |
|---|---|---|
| Clean checkout builds reproducibly | Verified locally | Copy without `bin/obj`, `dotnet restore --locked-mode`, Release build: 0 warnings, 0 errors |
| Tests run | Verified locally | 1 passed, 0 failed, 0 skipped |
| CI checks build and tests | Pending | Workflow present; not executed on GitHub yet |
| Documentation matches structure | Verified | README, DEVELOPMENT, ROADMAP, PROJECT_STATUS updated |
| No empty projects | Verified | Only `Astra.Host` and `Astra.Host.Tests` exist |
| Architectural decisions recorded | Verified | ADR 0001–0005 accepted 2026-11-18 |
| No unrecorded critical risks | Partially verified | Risks recorded in `PROJECT_STATUS.md`; Windows runtime unverified |

## Verification log

Executed on 2026-11-18 in `/root/astra` and in a clean copy without build artifacts:

```
dotnet restore AstraAgent.slnx --locked-mode           → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release   → 0 warnings, 0 errors
dotnet test AstraAgent.slnx --no-build -c Release      → 1 passed, 0 failed, 0 skipped
dotnet format AstraAgent.slnx --verify-no-changes      → exit 0
CI=true dotnet build AstraAgent.slnx -c Release        → 0 warnings, 0 errors
dotnet run --project src/Astra.Host                    → GET /health = 200 "Healthy"
```

Clean-copy verification:

```
tar (excluding .git/bin/obj/TestResults) → /tmp/opencode/astra-clean
dotnet restore --locked-mode                           → succeeded
dotnet build -c Release                                → 0 warnings, 0 errors
dotnet test -c Release                                 → 1 passed
```

## Pending before sign-off

1. Commit and push the repository, including `packages.lock.json` files.
2. Run `.github/workflows/ci.yml`; record the run URL and results for `ubuntu-latest` and
   `windows-latest`.
3. Verify the Windows build result; fix platform issues if any.
4. Mark Stage 1 completed in `PROJECT_STATUS.md` and `ROADMAP.md`, then start Stage 2.

## Known limitations

- Windows execution was not verified from the current Linux environment.
- GitHub Actions were not executed because no GitHub CLI/credentials are available here.
- CI actions are pinned by version tags, not commit SHAs; supply-chain hardening is pending.
- The repository has no committed history for these changes yet.

## Conclusion

Foundation scope is implemented and verified on Linux. Stage 1 cannot be signed off until
the CI run confirms the Windows build and test path.
