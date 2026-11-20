# ADR-0004: Testing baseline

- Status: Accepted
- Date: 2026-11-18
- Accepted: 2026-11-18
- Decision owners: Project owner

## Context

The specification mandates xUnit and requires unit, integration, architecture and security
tests. Foundation must make tests runnable and meaningful without models, GPU, network
access or a populated database, and must make failures visible in CI.

## Decision

1. xUnit (v2) is the test framework. Initial versions came from the .NET 10 SDK xunit template
   and are unchanged by the .NET 11 move: `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4,
   `Microsoft.NET.Test.Sdk` 17.14.1. `Microsoft.AspNetCore.Mvc.Testing` is pinned to 11.0.0 to
   match the installed ASP.NET Core runtime (see ADR 0009).
2. Each production project gets a test project under `tests/<ProductionProject>.Tests`
   only when it has testable behavior; the test project references the production project.
3. Test categories:
   - unit tests for deterministic logic, with no network, model or GPU dependency;
   - integration tests for adapters, persistence and HTTP boundaries, using temporary
     directories/databases that are cleaned up;
   - architecture tests for dependency direction once at least two modules exist;
   - security tests for permission bypass, untrusted input and prompt-injection boundaries
     for every new tool, endpoint and persistence feature.
4. Tests run with `dotnet test AstraAgent.slnx`. CI runs Release builds with warnings as
   errors on Linux and Windows.
5. Tests must be deterministic and parallel-safe; shared mutable static state is not
   permitted in test code.
6. Coverage collection is added when a baseline exists and a meaningful gate can be
   defined. No arbitrary coverage percentage gate is introduced now.
7. Upgrading test packages requires a compatibility run on both CI platforms and is recorded
   as a dependency decision.

## Alternatives considered

- **NUnit or MSTest**: viable, but xUnit is mandated by the specification and the team
  baseline.
- **xUnit v3**: not selected yet; migration requires framework/runner validation and yields
  no Foundation benefit. Revisit with evidence.
- **Testcontainers/Docker-based tests now**: rejected — no persistence boundary exists yet
  and Docker is not a security boundary.

## Consequences

- The first test is an integration test of the host health endpoint, proving the HTTP
  boundary and composition root work.
- Integration tests need explicit isolation and cleanup discipline as subsystems arrive.
- Package versions stay on the SDK-validated set until an upgrade is tested.

## Security implications

Tests exercise policy and untrusted-input behavior without production credentials. Test
fixtures must not contain real secrets or user data.

## Validation plan

- `dotnet restore`, `dotnet build`, `dotnet test` succeed from a clean checkout on Linux and
  Windows in CI.
- The test count and result are recorded in the Foundation exit report.
