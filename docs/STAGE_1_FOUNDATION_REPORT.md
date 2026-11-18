# Stage 1 — Foundation Report

- **Status:** Signed off — 2026-11-18
- **Environment:** Ubuntu 26.04, .NET SDK 10.0.115, ASP.NET Core runtime 10.0.15; GitHub
  Actions Ubuntu and Windows runners
- **CI run:** [44345679716](https://github.com/SaihonFox/Astra/actions/runs/44345679716)
  (commit `105961f`) — success on both platforms

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
| Clean checkout builds reproducibly | Met | CI checkout + `dotnet restore --locked-mode` + Release build on fresh runners |
| Tests run | Met | Local and CI: 1 passed, 0 failed, 0 skipped on Ubuntu and Windows |
| CI checks build and tests | Met | Run 44345679716: all steps successful on both platforms |
| Documentation matches structure | Met | README, DEVELOPMENT, ROADMAP, PROJECT_STATUS and this report updated |
| No empty projects | Met | Only `Astra.Host` and `Astra.Host.Tests` exist |
| Architectural decisions recorded | Met | ADR 0001–0005 accepted 2026-11-18 |
| No unrecorded critical risks | Met | Risks recorded in `PROJECT_STATUS.md` |

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

Cross-target publish (supplementary, not a substitute for Windows execution):

```
dotnet publish src/Astra.Host -c Release -r win-x64 --self-contained false
→ Astra.Host.exe, Astra.Host.dll, runtimeconfig.json
```

CI (run 44345679716, commit `105961f`), both jobs successful:

| Job | Restore | Build | Format | Test |
|---|---|---|---|---|
| ubuntu-latest | success | success | success | success |
| windows-latest | success | success | success | success |

## Known limitations

- CI actions are pinned by version tags, not commit SHAs; supply-chain hardening is pending.
- Branch protection and required status checks are not configured.
- Foundation covers only the host boundary and `/health`; product behavior starts at Stage 2.

## Conclusion

All Stage 1 exit criteria are met with recorded evidence. Foundation is complete; Stage 2 —
Core Infrastructure may begin after its ADRs are drafted and accepted.
