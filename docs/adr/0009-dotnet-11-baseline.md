# ADR-0009: .NET 11 platform baseline

- Status: Accepted
- Date: 2026-11-20
- Accepted: 2026-11-20 (owner instruction)
- Decision owners: Project owner
- Supersedes the toolchain part of [ADR-0001](0001-solution-layout.md) and the SDK
  reference in [ADR-0004](0004-testing-baseline.md)

## Context

The Stage 0 specification and ADR-0001 selected C# 14 / .NET 10 as the implementation
platform. Development and the Stage 1–3 evidence were produced with .NET SDK 10.0.115.
On 2026-11-20 the owner instructed that the project must not stay on .NET 10 and must move
to .NET 11. .NET 11 is the current stable release (SDK 11.0.100, GA runtime and packages),
so the pinned SDK band has to change and the package baseline has to follow.

## Decision

1. The solution targets **`net11.0`** with **C# 15** (`LangVersion` 15.0), configured once in
   `Directory.Build.props`.
2. `global.json` pins SDK **11.0.100** with `rollForward: latestFeature`, so any 11.0.1xx
   patch/feature band is accepted and no .NET 10 SDK is required.
3. Framework-aligned packages move to the 11.0.0 line:
   `Microsoft.Data.Sqlite` 11.0.0 and `Microsoft.AspNetCore.Mvc.Testing` 11.0.0. Other
   dependencies keep their current versions; they are verified against `net11.0` by the build
   and test suite.
4. Package lock files are regenerated for `net11.0` and committed. CI restores with
   `--locked-mode` and sets up `dotnet-version: 11.0.x`.
5. The Stage 0 specification technology line is updated to C# 15 / .NET 11. This is a
   toolchain refresh requested by the owner, not a change of product concept, architecture or
   security model.

## Alternatives considered

- **Keep .NET 10 and add SDK 10 side-by-side.** Rejected by the owner: .NET 10 must not be
  used.
- **Multi-target `net10.0;net11.0`.** Rejected: doubles build time and lock-file surface with
  no consumer that needs .NET 10.
- **Pin an 11.0.1xx preview SDK.** Rejected: 11.0.100 is stable; previews add risk for a
  security-sensitive project.

## Consequences

- A .NET 11 SDK (11.0.1xx) is now the only prerequisite; the documented commands are
  unchanged apart from the SDK version.
- `LangVersion` 15.0 keeps language features aligned with the runtime baseline.
- Any machine or CI image with only .NET 10 fails fast at SDK resolution, which is intended.
- Historical Stage 1–3 evidence produced with SDK 10.0.115 stays valid as a record of what was
  run at the time; it is not rewritten. New verification is recorded on .NET 11.
- Avalonia, ASP.NET Core, SQLite and LLamaSharp compatibility must be re-verified when those
  layers are added or upgraded; LLamaSharp 0.30.0 ships a `net10.0` target and loads on
  `net11.0` (verified by the Stage 3 test suite and a real-model run).

## Security implications

- No change to the security model: same analyzers, nullable reference types,
  warnings-as-errors, locked restore and dependency policy.
- Moving to a supported runtime reduces the risk of running an out-of-support toolchain.
- Package changes are limited to framework-aligned Microsoft packages; licences (MIT) and
  transitive dependencies were checked by the locked restore.

## Validation plan

1. `dotnet restore AstraAgent.slnx` regenerates lock files for `net11.0`.
2. `dotnet build AstraAgent.slnx --no-restore -c Release` — zero warnings, zero errors.
3. `dotnet test AstraAgent.slnx --no-build -c Release` — full suite green.
4. `dotnet format AstraAgent.slnx --verify-no-changes` — exit 0.
5. `dotnet restore AstraAgent.slnx --locked-mode` — proves the committed lock files match.
6. Stage 3 hardware validation is repeated on the .NET 11 build (Windows CPU, real GGUF).
