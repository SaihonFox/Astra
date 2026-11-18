---
name: dotnet-engineering
description: Implement maintainable C# and .NET components for Astra.
---

# .NET Engineering Skill

- Target the repository's selected .NET 10 SDK and verify it is installed.
- Use `.slnx`; enable nullable reference types and analyzers.
- Prefer async I/O and propagate `CancellationToken`.
- Keep UI code non-blocking.
- Avoid `async void` except UI event handlers.
- Use dependency injection at composition roots, not as a substitute for clear design.
- Avoid service locator patterns and unnecessary abstractions.
- Validate external input and tool arguments.
- Handle errors explicitly; never swallow exceptions.
- Add unit/integration tests for observable behavior.
- Keep package versions pinned and review licenses/transitive dependencies.
- Run actual build and test commands; report exact results.
