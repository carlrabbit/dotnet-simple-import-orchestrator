# Dotnet Simple Import Orchestrator

## Purpose

This repository is maintainer- and agent-facing. It is not public product documentation.

Dotnet Simple Import Orchestrator is a small in-house .NET import orchestration library. It owns polling/import orchestration, runtime state, and re-entrant import execution. It does not own business parsing of CSV, XML, JSON, binary, or web payloads, and it does not own durable state persistence.

## Current status

The repository is in initial setup/bootstrap state until the first implementation task is complete. The current implementation establishes the solution structure, public contracts, a minimal runner, deterministic file-backed source helpers, and initial tests.

## Tech stack

- .NET 10
- C#
- `System.Text.Json` for JSON configuration and runtime state
- Microsoft Testing Platform (MTP)
- TUnit for unit tests

## Architecture overview

```text
JSON import configuration + JSON runtime state
        ↓
Import runner decides which imports are due
        ↓
Import source produces import candidates
        ↓
Import handler receives a Stream
        ↓
Import runner updates runtime state
        ↓
Host application persists state
```

Source acquisition and payload interpretation are separate concerns. The library may route a `Csv`, `Xml`, `Json`, or `Binary` payload, but user code interprets the stream and persists the returned runtime state.

## Repository layout

```text
AGENTS.md
README.md
DotnetSimpleImportOrchestrator.slnx
src/
  DotnetSimpleImportOrchestrator/
tests/
  DotnetSimpleImportOrchestrator.Tests/
docs/
  architecture/
  specs/
  tasks/
```

## Development workflow

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes
```

Use `dotnet test` when `--no-build` is not appropriate for the current local artifact state.

## Documentation map

- [AGENTS.md](AGENTS.md)
- [Task 001: Project setup and initial implementation](docs/tasks/001-project-setup-and-initial-implementation.md)
- [.NET project setup spec](docs/specs/dotnet-project-setup.md)
- [Import orchestrator core architecture](docs/architecture/import-orchestrator-core-architecture.md)
