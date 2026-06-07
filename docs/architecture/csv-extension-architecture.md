# Architecture: CSV extension

## Purpose

This document defines the CSV extension architecture for `DotnetSimpleImportOrchestrator`.

The core import orchestrator is fixed around typed import definitions, source factories, sources, handlers, bounded polling, and JSON-compatible runtime state. CSV functionality is an extension built on top of that core model. It is included in the same source project for this in-house library, but it must remain architecturally separate from the core orchestration contracts.

## Boundary

The CSV extension owns:

- CSV file discovery;
- file readiness checks;
- mapping user-owned import configuration to library-owned CSV file source options;
- CSV candidate metadata helpers;
- processed-file cursor shape for the CSV file source;
- CSV stream processing into a neutral normalized table;
- unprocessable CSV content capture.

The CSV extension does not own:

- the core import runner behavior;
- the shape of `ImportDefinition<TConfiguration>`;
- user-owned configuration schemas;
- business/domain import logic;
- durable state persistence;
- moving, deleting, or archiving files after import;
- automatic CSV delimiter, encoding, or header detection.

## Placement

Use a CSV-specific namespace under the existing source project:

```text
DotnetSimpleImportOrchestrator.Csv
```

Recommended source folder layout:

```text
src/DotnetSimpleImportOrchestrator/
  Csv/
    CsvFileImportSource.cs
    CsvFileImportSourceFactory.cs
    CsvFileImportSourceOptions.cs
    CsvFileProcessor.cs
    CsvCandidateMetadata.cs
    CsvModels.cs
```

Exact file names may vary, but the public concepts must match `docs/specs/csv-source-and-processor.md`.

## Extension flow

```text
ImportDefinition<TConfiguration>
        ↓
CsvFileImportSourceFactory<TConfiguration>
        ↓
ICsvFileImportSourceOptionsMapper<TConfiguration>
        ↓
CsvFileImportSourceOptions
        ↓
CsvFileImportSource
        ↓
ImportPollResult.Candidate with stream opener and CSV metadata
        ↓
User IImportHandler<TConfiguration>
        ↓
CsvFileProcessor.ProcessAsync(stream, context)
        ↓
CsvFileProcessingResult
```

The CSV file source is responsible for finding one ready unprocessed file candidate. The CSV processor is responsible for parsing a stream and metadata/options into a neutral table result.

## Core interaction

The CSV extension must use these existing core concepts:

- `ImportDefinition<TConfiguration>`;
- `IImportConfiguration`;
- `IImportSourceFactory<TConfiguration>`;
- `IImportSource`;
- `ImportPollContext`;
- `ImportPollResult`;
- `ImportCandidate`;
- `ImportHandlingContext<TConfiguration>` metadata;
- `ImportHandlingResult` cursor update behavior;
- `ImportState.Cursor` for processed-file tracking.

The CSV extension must not add CSV-specific properties to core import definitions or core candidates. CSV-specific information belongs in CSV option records and candidate metadata.

## CSV source role

`CsvFileImportSource` is a reusable import source. It performs file acquisition only.

It must:

- enumerate files from configured directory/pattern settings;
- exclude files already marked processed in the CSV cursor;
- apply configured readiness strategy;
- order candidates deterministically;
- return at most one candidate per poll;
- attach file metadata and CSV payload options to candidate metadata;
- return a cursor update that marks the candidate processed only when the core runner later commits successful handler execution.

It must not parse rows or create domain objects.

## CSV processor role

`CsvFileProcessor` is a reusable CSV stream processor. It can be called by a user handler, tests, support tools, or ad-hoc utilities.

It must:

- consume a readable stream;
- consume CSV payload options and optional metadata;
- use CsvHelper for CSV parsing;
- return a normalized table with headers and rows;
- pad short rows with empty strings;
- generate column names when headers are absent, empty, duplicate, or shorter than data rows;
- capture malformed/unprocessable content;
- not fail processing merely because CSV content is malformed.

The processor does not persist anything and does not mark imports successful. The handler decides how to use the processing result and whether to return `ImportHandlingResult` success.

## Dependency policy

The core orchestration model remains dependency-light. CsvHelper is allowed for the CSV extension because CSV parsing is extension functionality.

Add `CsvHelper` to the library project. If support for legacy code page encodings such as `windows-1252` is implemented, add `System.Text.Encoding.CodePages` and register `CodePagesEncodingProvider.Instance` in a controlled, idempotent way before resolving those encodings.

## State and cursor policy

CSV processed-file state belongs under a CSV-specific cursor object in `ImportState.Cursor`, for example:

```json
{
  "csvFileSource": {
    "processedFiles": {
      "C:/imports/customers/customers-2026-06-07.csv": {
        "length": 12093,
        "lastWriteTimeUtc": "2026-06-07T10:12:00Z",
        "processedAt": "2026-06-07T10:15:22Z"
      }
    }
  }
}
```

The source may return this cursor update with a candidate. The core runner commits it only after the handler succeeds. The source must not claim irreversible completion for candidates that were merely discovered or opened.

## Acceptance criteria

- CSV functionality is implemented under a CSV-specific namespace/folder.
- Core import definition and runner contracts are not changed for CSV.
- The CSV source uses user configuration only through a typed mapper.
- The CSV source returns one ready unprocessed file candidate per poll.
- The CSV processor returns normalized tabular output and unprocessable content.
- Malformed CSV content does not cause the processor to fail the import by exception.
