# Task 002: Rewrite polling model and import definition

## Status

Planned.

## Goal

Rewrite the import orchestration contract around the revised polling model and the revised import definition model.

This task intentionally does **not** preserve backwards compatibility with the bootstrap public contracts. Any affected contract, implementation, test, and documentation must be rewritten rather than extended with compatibility shims.

The revised model is:

- each runner call receives the current list of expected imports;
- each import has a stable unique ID, a priority, a polling interval, and user-owned configuration;
- import definitions are strongly typed as `ImportDefinition<TConfiguration>` where `TConfiguration : IImportConfiguration`;
- the library does not know source kind, payload format, source names, handler names, file paths, URLs, parser semantics, or web-service details;
- users provide source factories and handlers;
- the runner checks due imports in deterministic priority order;
- a bounded runner pass stops after the first successful import is performed and committed to the returned state;
- if no import succeeds, the pass returns after all due imports have been checked.

## Repository inspection result

At planning time the repository contained the bootstrap documentation and repository introduction:

- `README.md` exists and introduces the repository as maintainer- and agent-facing.
- `AGENTS.md` exists and points agents to `README.md`.
- `docs/tasks/001-project-setup-and-initial-implementation.md` exists and defines the bootstrap task.
- `docs/specs/dotnet-project-setup.md` exists and still contains bootstrap-era test expectations that mention the old `ImportDefinition` shape and disabled imports.
- `docs/architecture/import-orchestrator-core-architecture.md` exists and still defines the old non-generic `ImportDefinition` with source/handler names, payload format, enabled flag, and JSON source configuration.

This task supersedes the affected parts of Task 001 and replaces the affected specs/docs. Do not implement by preserving the old model.

## Required authority documents

The implementation agent must read these files before implementing:

1. `README.md` — repository overview, tech stack, and validation commands.
2. `AGENTS.md` — agent entry-point instructions.
3. `docs/tasks/002-rewrite-polling-and-import-definition.md` — this task package entry point.
4. `docs/specs/import-definition-and-polling-model.md` — authoritative revised public contract and polling behavior.
5. `docs/architecture/import-orchestrator-core-architecture.md` — replacement architecture document for the core import orchestrator.
6. `docs/specs/dotnet-project-setup.md` — replacement project/test expectations after this rewrite.

Do not require the implementation agent to reconstruct requirements from chat history or from the superseded bootstrap design.

## Replacement scope

The following concepts from the bootstrap design are replaced:

- non-generic `ImportDefinition` as the primary user-facing definition type;
- `SourceName` on import definitions;
- `HandlerName` on import definitions;
- `ImportPayloadFormat` on core import definitions and core candidates;
- `Enabled` on import definitions;
- `JsonObject Source` as library-owned source configuration;
- source/handler name registry as the primary resolution mechanism;
- runner behavior that attempts to run all due imports in one pass;
- tests that expect disabled imports or library-owned source configuration.

The implementation may keep type names only when they still match the revised contract. Otherwise, replace them.

## Deliverables

The implementation agent must update source, tests, and documentation to match the revised contract.

### Source deliverables

Rewrite the core library so it exposes these concepts:

```text
IImportConfiguration
IImportDefinition
ImportDefinition<TConfiguration>
ImportPriorities
PollingOptions
IImportSourceFactory<TConfiguration>
IImportSource
IImportHandler<TConfiguration>
ImportRunner / IImportRunner
ImportRuntimeState
ImportState
ImportPollResult
ImportCandidate
ImportHandlingResult
ImportRunResult
ImportCheckResult
```

Exact file names may vary, but the public concepts and behavior must match `docs/specs/import-definition-and-polling-model.md`.

### Documentation deliverables

Replace or add these Markdown files from this package:

```text
README.md
docs/tasks/002-rewrite-polling-and-import-definition.md
docs/specs/import-definition-and-polling-model.md
docs/specs/dotnet-project-setup.md
docs/architecture/import-orchestrator-core-architecture.md
```

Do not modify `AGENTS.md` unless implementation discovers it is inconsistent with the repository state. The current `AGENTS.md` already points to `README.md`.

### Test deliverables

Rewrite tests affected by the old model.

Minimum tests:

1. `ImportDefinition<TConfiguration>` exposes ID, priority, polling interval, and strongly typed configuration.
2. Import IDs must be non-empty and unique within one runner call.
3. Polling intervals must be positive.
4. Missing priority uses `ImportPriorities.Normal`.
5. Due imports are ordered by priority ascending, then import ID ordinal ascending.
6. Not-due imports are not polled.
7. A runner pass stops after the first successful import.
8. If a high-priority import has no candidate, the runner continues to the next due import.
9. If a source or handler fails, the runner records the failure and continues to lower-priority due imports.
10. The import list is snapshotted at the start of a runner call.
11. Runtime state round-trips through `System.Text.Json` and preserves `ImportState.Cursor` JSON.
12. Removed imports do not automatically delete existing state.

Do not keep the old disabled-import test. Disabled imports are represented by omitting the import from the supplied list.

## Focus areas

The implementation can be done in independently executable focus areas.

### Focus area 1: replace public import definition contracts

Implement the revised definition model:

- `IImportConfiguration` marker interface.
- `IImportDefinition` non-generic orchestration view.
- `ImportDefinition<TConfiguration>` with `Id`, `Priority`, `Polling`, and `Configuration` only.
- `PollingOptions` with required positive `Interval`.
- `ImportPriorities` constants.

Acceptance criteria:

- Core contracts compile.
- No core `ImportDefinition` property exists for source kind, source name, handler name, payload format, enabled flag, or JSON source configuration.
- `ImportDefinition<TConfiguration>` can be used in a mixed `IReadOnlyList<IImportDefinition>`.

### Focus area 2: replace source and handler resolution

Replace source/handler-name routing with user-provided factories and typed handlers.

Acceptance criteria:

- A source factory can be registered/resolved for each import ID.
- A handler can be registered/resolved for each import ID.
- Generic factory and handler implementations receive the strongly typed `ImportDefinition<TConfiguration>`.
- The library does not inspect user-owned configuration except to require it to be non-null.

### Focus area 3: implement bounded priority polling

Implement the revised runner pass:

1. Snapshot the supplied import list at the start of the call.
2. Validate import IDs, polling intervals, configuration values, and registrations.
3. Filter out not-due imports.
4. Sort due imports by priority ascending, then ID ordinal ascending.
5. Check each due import sequentially.
6. Continue on no candidate, source failure, or handler failure.
7. Stop after the first candidate is successfully handled and committed to the returned state.
8. Return updated state and per-check results.

Acceptance criteria:

- Runner behavior matches `docs/specs/import-definition-and-polling-model.md`.
- One runner pass performs at most one successful import.
- State is updated for checks, failures, and successful imports as specified.

### Focus area 4: rewrite runtime state and result tests

Adjust state and result tests to the revised model.

Acceptance criteria:

- Runtime state remains JSON-serializable through `System.Text.Json`.
- Cursor state remains JSON-extensible through `JsonObject` or equivalent `System.Text.Json.Nodes` APIs.
- Live results expose check outcomes without requiring raw exception serialization into durable state.

### Focus area 5: update repository docs

Replace affected docs with the documents in this task package and align the root README.

Acceptance criteria:

- README no longer says the core library knows payload formats such as CSV/XML/JSON/Binary.
- README links Task 002 and the revised import definition/polling spec.
- The core architecture document no longer contains the old `ImportDefinition` shape.
- The project setup spec no longer requires disabled-import tests.

## Validation expectations

Use tiered validation.

### Tier 1: local fast validation

Run after contract and runner changes:

```bash
dotnet restore
dotnet build --no-restore
```

### Tier 2: test validation

Run after test updates:

```bash
dotnet test --no-build
```

If `dotnet test --no-build` cannot run because build artifacts are missing, run:

```bash
dotnet test
```

### Tier 3: repository hygiene validation

Before finishing, attempt:

```bash
dotnet format --verify-no-changes
```

If format verification is unavailable in the environment, report that explicitly.

## Implementation constraints

- Do not preserve backwards compatibility with the old bootstrap contracts.
- Do not introduce compatibility adapters for the old non-generic `ImportDefinition` shape.
- Keep the library dependency-light; prefer BCL and `System.Text.Json`.
- Do not add CSV, XML, HTTP, scheduling, persistence, logging, or plugin-discovery dependencies for this task.
- Use `TimeProvider` where current time is needed.
- Use `CancellationToken` on asynchronous public APIs.
- Keep polling host-driven; do not introduce a background service as the core abstraction.
- Keep durable state persistence external; the runner returns updated state and the caller persists it.
- Cross-process locking remains the host application's responsibility.

## Expected implementation summary

When finished, report:

- public contracts replaced;
- files created, modified, or deleted;
- tests added/rewritten;
- validation commands run and their results;
- any deviations from this task package.
