# Task 001: Project setup and initial implementation

## Status

Planned.

## Goal

Bootstrap `carlrabbit/dotnet-simple-import-orchestrator` as a small in-house .NET import orchestration library.

The implementation must create the repository foundation, root maintainer/agent documentation, a .NET 10 solution, the first library project, and a TUnit-based test project using Microsoft Testing Platform (MTP). The resulting repository must be buildable and testable, and it must contain enough implementation skeleton to make the intended library architecture explicit.

This task is not about delivering a production-complete importer. It is about creating the project structure, public contracts, minimal orchestration core, and documentation baseline that later tasks can extend.

## Repository inspection result

At planning time the repository was accessible but effectively empty:

- `README.md` was not present.
- `AGENTS.md` was not present.
- No existing `docs/` content was found by repository search.
- Repository default branch: `main`.
- Repository visibility: public.

Because no repository conventions exist yet, this task defines the initial conventions for the repository.

## Required authority documents

The implementation agent must read these files before implementing:

1. `docs/tasks/001-project-setup-and-initial-implementation.md` — this task package entry point.
2. `docs/specs/repository-introduction-and-agent-docs.md` — required root `README.md` and `AGENTS.md` content contract.
3. `docs/specs/dotnet-project-setup.md` — required .NET 10, MTP, and TUnit repository setup contract.
4. `docs/architecture/import-orchestrator-core-architecture.md` — initial library architecture and public contract direction.

Do not require the implementation agent to reconstruct planning context from chat history.

## Deliverables

The implementation agent must create or replace the following repository files.

### Root documentation

Create:

```text
AGENTS.md
README.md
```

Rules:

- `AGENTS.md` must be short and point agents to `README.md` as the repository introduction.
- `README.md` must not be user-facing product documentation.
- `README.md` must introduce the repository to AI agents and maintainers.
- `README.md` must state the tech stack: .NET 10, Microsoft Testing Platform, and TUnit.
- `README.md` must describe the library as an in-house import orchestration library.
- `README.md` must link to the task and architecture documents once those documents are committed.

See `docs/specs/repository-introduction-and-agent-docs.md`.

### .NET solution and projects

Create a .NET 10 solution with at least:

```text
src/DotnetSimpleImportOrchestrator/DotnetSimpleImportOrchestrator.csproj
tests/DotnetSimpleImportOrchestrator.Tests/DotnetSimpleImportOrchestrator.Tests.csproj
```

Expected solution naming:

```text
DotnetSimpleImportOrchestrator.slnx
```

If `.slnx` support is unavailable in the installed SDK, create `DotnetSimpleImportOrchestrator.sln` instead and document the reason in the implementation summary.

The library project must target `net10.0`.

The test project must target `net10.0`, use Microsoft Testing Platform, and use TUnit.

See `docs/specs/dotnet-project-setup.md`.

### Initial source structure

Create the initial library folders and types needed to make the architecture concrete. Suggested structure:

```text
src/DotnetSimpleImportOrchestrator/
  ImportDefinition.cs
  ImportRuntimeState.cs
  ImportRunner.cs
  Abstractions/
    IImportRunner.cs
    IImportSource.cs
    IImportHandler.cs
  FileSystem/
    DirectoryPollingImportSource.cs
  Testing/
    FileBackedImportSource.cs
```

The exact file names may vary, but the initial implementation must expose the same concepts:

- `ImportDefinition`
- `ImportRuntimeState`
- `ImportState`
- `IImportRunner`
- `IImportSource`
- `IImportHandler`
- `ImportCandidate`
- `ImportSourceContext`
- `ImportHandlingContext`
- `ImportHandlingResult`
- a minimal file-backed source useful for tests/demos

See `docs/architecture/import-orchestrator-core-architecture.md`.

## Scope

### In scope

- Repository bootstrap.
- Root `README.md` and `AGENTS.md`.
- Initial `docs/` folders if needed by the implementation package.
- .NET 10 solution and project structure.
- MTP + TUnit unit test setup.
- Minimal public contracts for the import orchestration library.
- Minimal `ImportRunner` behavior sufficient to validate the architecture.
- Minimal file-backed source implementation for deterministic tests.
- Unit tests for configuration/state serialization and one successful import run.

### Out of scope

- Production-ready CSV parsing.
- Production-ready XML parsing.
- Real web-service client implementation.
- Hosted background service implementation.
- Database, blob, Redis, or file-based state persistence service.
- Distributed locking.
- NuGet package publishing workflow.
- CI workflow unless the implementation agent decides it is trivial and non-disruptive.
- User-facing product documentation.

## Focus areas

The implementation can be done in independently executable focus areas.

### Focus area 1: repository documentation bootstrap

Create root `README.md` and `AGENTS.md` according to `docs/specs/repository-introduction-and-agent-docs.md`.

Acceptance criteria:

- `AGENTS.md` exists and points to `README.md`.
- `README.md` clearly says it is maintainer/agent-facing, not end-user-facing.
- `README.md` names .NET 10, MTP, and TUnit.
- `README.md` describes the import orchestrator design at a high level.

### Focus area 2: solution and project setup

Create the solution, library project, and test project according to `docs/specs/dotnet-project-setup.md`.

Acceptance criteria:

- Repository restores successfully.
- Repository builds successfully.
- Test project is included in the solution.
- Test project runs through Microsoft Testing Platform and TUnit.

### Focus area 3: initial public contracts

Create the initial public API skeleton described in `docs/architecture/import-orchestrator-core-architecture.md`.

Acceptance criteria:

- The core abstractions compile.
- Configuration and runtime state are represented with typed records and JSON-extensible cursor/source sections.
- Public contracts do not force the user to use a specific parser, HTTP client, or persistence mechanism.

### Focus area 4: minimal orchestration behavior

Implement a minimal `ImportRunner` that can:

- accept import definitions and runtime state;
- determine whether enabled imports are due;
- ask the configured source for candidates;
- pass each candidate stream to the configured handler;
- produce updated runtime state after successful handling;
- return attempt results for success/failure visibility.

Acceptance criteria:

- A deterministic unit test can run one import from a file-backed source.
- The handler receives a readable stream.
- The returned state records successful completion for the import ID.
- Exceptions are represented in the live result and summarized in persisted state without serializing raw exception objects.

### Focus area 5: validation tests

Add initial TUnit tests.

Minimum tests:

- JSON round-trip for `ImportDefinition`.
- JSON round-trip for `ImportRuntimeState` with an extensible cursor object.
- A successful import run using a file-backed test source and test handler.
- A disabled import does not execute.

## Validation expectations

Use tiered validation.

### Tier 1: local fast validation

Run after each focus area if feasible:

```bash
dotnet restore
dotnet build --no-restore
```

### Tier 2: test validation

Run when the test project exists:

```bash
dotnet test --no-build
```

If `dotnet test --no-build` is not reliable because build artifacts are missing, run:

```bash
dotnet test
```

### Tier 3: repository hygiene validation

Before finishing:

```bash
dotnet format --verify-no-changes
```

If `dotnet format` is unavailable or cannot run in the environment, state that explicitly in the implementation summary.

## Implementation constraints

- Keep the first implementation small.
- Prefer BCL types and `System.Text.Json`.
- Do not introduce CSV, XML, HTTP, persistence, logging, or scheduling dependencies unless required to compile the minimal implementation.
- Use `TimeProvider` where current time is needed.
- Use `CancellationToken` on asynchronous public APIs.
- Keep persistence external: the runner returns updated state; the application persists it.
- Keep polling host-driven: do not make a background service the core abstraction.
- Avoid plugin discovery. A simple explicit registry or constructor-supplied mapping is sufficient.

## Expected implementation summary

When the implementation agent finishes, it should report:

- files created or modified;
- solution/project names;
- target frameworks;
- test framework packages used;
- validation commands run and their result;
- any deviations from this task package, especially `.slnx` fallback or unavailable validation commands.
