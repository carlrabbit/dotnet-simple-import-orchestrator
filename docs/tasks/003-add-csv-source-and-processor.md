# Task 003: Add CSV source and CSV processor

## Status

Planned.

## Goal

Add CSV functionality as an extension area inside the existing in-house library.

The core import orchestration implementation is considered fixed for this task. Do not redesign `ImportDefinition<TConfiguration>`, polling, source factory registration, handler registration, candidate handling, or bounded runner semantics.

This task adds:

1. a generic CSV file import source that maps user-owned `TConfiguration` to library-owned CSV source options;
2. a CSV file processor that consumes a CSV stream plus CSV metadata/options and returns a normalized table plus unprocessable content.

CsvHelper is allowed and should be used for CSV parsing.

## Repository inspection result

At planning time the repository contained:

- `README.md` describing the revised Task 002 core model and documentation map;
- `AGENTS.md` pointing implementation agents to `README.md`;
- `docs/tasks/002-rewrite-polling-and-import-definition.md` defining the current core contract;
- `docs/specs/import-definition-and-polling-model.md` defining `ImportDefinition<TConfiguration>`, `IImportSourceFactory<TConfiguration>`, `IImportSource`, `IImportHandler<TConfiguration>`, bounded polling, and candidate metadata;
- `docs/architecture/import-orchestrator-core-architecture.md` defining the core boundary;
- `src/DotnetSimpleImportOrchestrator/DotnetSimpleImportOrchestrator.csproj` targeting `net10.0` with nullable, implicit usings, warnings as errors, and packable enabled;
- `src/DotnetSimpleImportOrchestrator/ImportRunner.cs` implementing the bounded runner and cursor merge behavior;
- `src/DotnetSimpleImportOrchestrator/ImportRegistrations.cs` implementing typed source factory and handler registration adapters.

This task is additive. It must not require the implementation agent to reconstruct CSV requirements from chat history.

## Required authority documents

The implementation agent must read these files before implementing:

1. `README.md` — repository overview, tech stack, documentation map, and validation commands.
2. `AGENTS.md` — agent entry-point instructions.
3. `docs/tasks/003-add-csv-source-and-processor.md` — this task package entry point.
4. `docs/specs/csv-source-and-processor.md` — authoritative CSV source and processor contract.
5. `docs/architecture/csv-extension-architecture.md` — CSV extension boundary and core integration architecture.
6. `docs/specs/import-definition-and-polling-model.md` — existing core contract that must not be redesigned.
7. `docs/architecture/import-orchestrator-core-architecture.md` — existing core architecture boundary.

Do not require the implementation agent to read all prior task documents except where listed above.

## Deliverables

### Documentation deliverables

Add or replace these Markdown files from this task package:

```text
README.md
docs/tasks/003-add-csv-source-and-processor.md
docs/specs/csv-source-and-processor.md
docs/architecture/csv-extension-architecture.md
```

Do not modify `AGENTS.md` unless implementation discovers it is inconsistent with the repository state.

### Source deliverables

Add a CSV extension namespace/folder under the existing library project.

Expected concepts:

```text
DotnetSimpleImportOrchestrator.Csv

CsvFileImportSource
CsvFileImportSourceFactory<TConfiguration>
ICsvFileImportSourceOptionsMapper<TConfiguration>
CsvFileImportSourceOptions
FileReadinessOptions
FileReadinessStrategy
FileCandidateOrdering
MissingDirectoryBehavior
CsvPayloadOptions
CsvCandidateMetadata
CsvFileMetadata
CsvFileProcessor
CsvFileProcessingContext
CsvFileProcessingResult
CsvTable
CsvRow
CsvUnprocessableContent
```

Exact file names may vary, but public concepts and behavior must match `docs/specs/csv-source-and-processor.md`.

### Project deliverables

Add CsvHelper to the library project.

If legacy code page encoding support is implemented, also add `System.Text.Encoding.CodePages` and register the provider idempotently.

Do not add a separate project or package for this task. The CSV extension lives in the existing source project for this in-house library.

### Test deliverables

Add TUnit tests for the CSV source and CSV processor.

Minimum source tests:

1. source factory calls mapper and creates a source from typed user configuration;
2. missing directory returns no candidate when configured as `TreatAsNoCandidate`;
3. missing directory fails when configured as `Fail`;
4. `None` readiness accepts a discovered file;
5. `MarkerFile` readiness accepts only files with the configured marker file;
6. `StableSize` readiness accepts files unchanged for the configured interval;
7. exclusive readiness ignores locked files and accepts openable files where the platform permits the check;
8. processed files in the CSV cursor are skipped;
9. candidate source item ID is the normalized full path;
10. candidate metadata contains typed file metadata and CSV payload options;
11. candidate cursor update uses the `csvFileSource.processedFiles` shape and is safe for commit after handler success;
12. ordering is deterministic for at least `OldestFirst` and `NameAscending`.

Minimum processor tests:

1. parses headers and rows with configured delimiter;
2. generates headers when no header row is configured;
3. pads short rows with empty strings;
4. adds generated headers for rows wider than the parsed header row;
5. normalizes empty and duplicate headers to generated `ColumnN` names;
6. preserves physical row numbers;
7. respects trim behavior;
8. respects blank-line behavior;
9. respects encoding at least for UTF-8; include a legacy encoding test if code page support is added;
10. captures malformed/unprocessable content;
11. does not throw merely because CSV content is malformed;
12. returns `Fields` dictionaries that map normalized headers to normalized values.

## Scope

### In scope

- CSV extension namespace/folder in the existing source project.
- CsvHelper dependency.
- Generic CSV source factory and typed options mapper.
- CSV file source options and validation.
- File readiness strategies: `None`, `StableSize`, `ExclusiveRead`, `ExclusiveWrite`, `MarkerFile`.
- Missing directory behavior.
- File candidate ordering.
- CSV candidate metadata helpers.
- Processed-file cursor shape.
- CSV file processor producing normalized table output.
- Unprocessable content capture for malformed CSV content.
- TUnit tests for source behavior, processor behavior, metadata, and options validation.

### Out of scope

- Changes to core `ImportDefinition<TConfiguration>` shape.
- Changes to bounded runner semantics.
- Separate NuGet package or separate project for CSV.
- XML source or processor.
- Web source implementation.
- Automatic CSV delimiter/header/encoding detection.
- Row-to-domain-object mapping.
- Business validation.
- Moving, deleting, archiving, or quarantining processed files.
- Checksum-based file identity.
- Filesystem watcher/event-driven polling.
- Hosted background service.
- Durable state persistence service.

## Focus areas

The implementation can be done in independently executable focus areas.

### Focus area 1: documentation and package dependency

Add/replace documentation files from this package and add CsvHelper to the library project.

Acceptance criteria:

- README links Task 003, the CSV spec, and the CSV extension architecture document.
- CsvHelper is referenced by the library project.
- The implementation summary reports the CsvHelper version.
- If `System.Text.Encoding.CodePages` is added, the implementation summary reports the version and registration approach.

### Focus area 2: CSV options and metadata model

Implement CSV option and metadata records/helpers.

Acceptance criteria:

- `CsvFileImportSourceOptions`, `FileReadinessOptions`, `CsvPayloadOptions`, `CsvFileMetadata`, and metadata helpers compile.
- Options are validated with clear exceptions.
- Candidate metadata round-trips through `System.Text.Json.Nodes.JsonObject` and typed helper methods.

### Focus area 3: CSV file source factory and mapper boundary

Implement the generic source factory and mapper interface.

Acceptance criteria:

- `CsvFileImportSourceFactory<TConfiguration>` implements `IImportSourceFactory<TConfiguration>`.
- The factory calls `ICsvFileImportSourceOptionsMapper<TConfiguration>`.
- The factory does not inspect user-owned `TConfiguration` directly.
- Tests prove typed user configuration can be mapped to CSV source options.

### Focus area 4: CSV file source

Implement `CsvFileImportSource`.

Acceptance criteria:

- Source discovers matching files from configured directory, pattern, and recursion settings.
- Source applies missing-directory behavior.
- Source applies readiness strategy.
- Source skips files already present in the CSV processed-file cursor.
- Source orders ready files deterministically.
- Source returns at most one candidate per poll.
- Candidate opens a readable stream.
- Candidate metadata and cursor update match the CSV spec.

### Focus area 5: CSV file processor

Implement `CsvFileProcessor` using CsvHelper.

Acceptance criteria:

- Processor reads stream using configured encoding.
- Processor configures CsvHelper from `CsvPayloadOptions`.
- Processor returns normalized headers, rows, values, and fields.
- Processor pads short rows with empty strings.
- Processor generates `ColumnN` names for missing, empty, duplicate, or extra columns.
- Processor captures malformed content in `CsvUnprocessableContent`.
- Processor does not throw merely because CSV content is malformed.
- Processor still honors cancellation and may throw for unreadable streams or invalid options.

### Focus area 6: integration-style tests

Add at least one end-to-end test that uses the existing runner with the CSV source and a handler that calls `CsvFileProcessor`.

Acceptance criteria:

- Runner receives `ImportDefinition<TConfiguration>`.
- Source factory maps user configuration to CSV options.
- Source returns a candidate.
- Handler calls the processor.
- Handler returns success.
- Runner commits the CSV processed-file cursor update only after handler success.

## Validation expectations

Use tiered validation.

### Tier 1: local fast validation

Run after source and package changes:

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

- Keep the core orchestration contracts unchanged unless a compile fix is strictly required.
- Do not add CSV-specific properties to core import definitions.
- Do not add CSV-specific payload format enums to core candidates.
- Keep CSV metadata under candidate `Metadata`.
- Keep processed-file state under `ImportState.Cursor`.
- Use `CancellationToken` on async APIs.
- Use clear exceptions for invalid options.
- Preserve malformed CSV content as unprocessable content when possible.
- Do not swallow cancellation.
- Do not move, delete, or archive source files.
- Keep durable persistence external.

## Expected implementation summary

When finished, report:

- Markdown files added or replaced;
- source files added;
- project package references added and versions;
- readiness strategies implemented;
- CSV processor behavior implemented;
- tests added;
- validation commands run and results;
- any deviations from this task package.
