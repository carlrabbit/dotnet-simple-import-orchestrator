# Dotnet Simple Import Orchestrator

## Purpose

This repository is maintainer- and agent-facing. It is not public product documentation.

Dotnet Simple Import Orchestrator is a small in-house .NET import orchestration library. It owns polling/import orchestration, priority ordering, runtime state transitions, and re-entrant import execution. It does not own source-specific configuration schemas, business parsing of payloads, real web-service integration, or durable state persistence.

## Current status

The repository is in early implementation state. The active design is the revised polling and import definition model from Task 002. Bootstrap-era contracts that placed source names, handler names, payload formats, enabled flags, or source JSON on the core import definition are superseded and should be rewritten, not extended.

## Tech stack

- .NET 10
- C#
- `System.Text.Json` for JSON-compatible runtime state
- Microsoft Testing Platform (MTP)
- TUnit for unit tests

## Architecture overview

```text
Current import definitions + runtime state
        ↓
Import runner snapshots definitions
        ↓
Import runner filters due imports
        ↓
Import runner orders by priority, then ID
        ↓
User-provided source factory creates source
        ↓
Source returns no candidate or one candidate stream
        ↓
User-provided handler processes the stream
        ↓
Import runner returns updated runtime state
        ↓
Host application persists state
```

The core import definition contains only import ID, priority, polling interval, and user-owned strongly typed configuration. The library does not know whether an import is backed by files, web services, CSV, XML, JSON, or another payload shape.

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

If the repository uses `DotnetSimpleImportOrchestrator.sln` instead of `.slnx`, prefer the actual file present in the repository.

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
- [Task 002: Rewrite polling model and import definition](docs/tasks/002-rewrite-polling-and-import-definition.md)
- [Import definition and polling model spec](docs/specs/import-definition-and-polling-model.md)
- [.NET project setup spec](docs/specs/dotnet-project-setup.md)
- [Import orchestrator core architecture](docs/architecture/import-orchestrator-core-architecture.md)
