# Spec: Repository introduction and agent documentation

## Purpose

This spec defines the required root `README.md` and `AGENTS.md` files for the initial repository setup.

The repository is for an in-house .NET import orchestration library. The root documentation is primarily for maintainers and AI implementation agents, not for external package consumers.

## Required files

Create:

```text
README.md
AGENTS.md
```

## `AGENTS.md` requirements

`AGENTS.md` must be short and authoritative.

Required content:

- State that agents should start with `README.md`.
- State that task-specific docs under `docs/tasks/` are authoritative for implementation work.
- State that agents should prefer small, focused changes.
- State that validation commands must be reported after implementation.

Recommended structure:

```markdown
# Agent instructions

Start with `README.md` for the repository overview.

For implementation tasks, read the relevant task document under `docs/tasks/` and the authority documents it lists. Do not infer missing requirements from unrelated documents.

Keep changes small and focused. Report validation commands and results when finishing work.
```

## `README.md` requirements

`README.md` must not read like public product documentation. It must introduce the repository to maintainers and AI agents.

Required sections:

1. `# Dotnet Simple Import Orchestrator`
2. `## Purpose`
3. `## Current status`
4. `## Tech stack`
5. `## Architecture overview`
6. `## Repository layout`
7. `## Development workflow`
8. `## Documentation map`

## Required content details

### Purpose

The README must describe the library as:

- a small in-house import orchestration library;
- responsible for polling/import orchestration, runtime state, and re-entrant import execution;
- not responsible for business parsing of CSV, XML, or web payloads;
- not responsible for durable state persistence.

### Current status

The README must state that the repository is in initial setup/bootstrap state until the first implementation task is complete.

### Tech stack

The README must explicitly list:

- .NET 10;
- C#;
- `System.Text.Json` for JSON configuration/state;
- Microsoft Testing Platform, abbreviated as MTP;
- TUnit for unit tests.

### Architecture overview

The README must summarize this flow:

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

The README must state that source acquisition and payload interpretation are separate concerns.

### Repository layout

The README must document the expected initial layout:

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

If `.sln` is used instead of `.slnx`, the README must reflect the actual solution file name.

### Development workflow

The README must list the standard local commands:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes
```

The README may note that `dotnet test` can be used when `--no-build` is not appropriate.

### Documentation map

The README must link to:

- `AGENTS.md`;
- `docs/tasks/001-project-setup-and-initial-implementation.md`;
- `docs/specs/dotnet-project-setup.md`;
- `docs/architecture/import-orchestrator-core-architecture.md`.

## Tone and audience

Use direct maintainer-facing language.

Avoid:

- marketing language;
- installation instructions for external users;
- package-consumer examples beyond a minimal architecture sketch;
- claims that the library is production-ready.

## Acceptance criteria

- `AGENTS.md` exists and points to `README.md`.
- `README.md` exists and is maintainer/agent-facing.
- `README.md` explicitly names .NET 10, MTP, and TUnit.
- `README.md` describes the import orchestration boundary clearly.
- `README.md` links to the relevant task/spec/architecture documents.
