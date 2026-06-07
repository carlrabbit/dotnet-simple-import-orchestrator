# Spec: CSV source and CSV processor

## Purpose

This spec defines the CSV extension for `DotnetSimpleImportOrchestrator`.

The extension adds:

1. a generic CSV file import source;
2. a CSV file processor that turns a CSV stream into a normalized table result and captures unprocessable content.

The core import orchestration model is fixed and must not be changed except for minimal metadata helpers required by this extension.

## Design principle

The user owns `TConfiguration`. The CSV extension owns a fixed internal option structure.

The mapping is explicit:

```text
ImportDefinition<TConfiguration>
        ↓
ICsvFileImportSourceOptionsMapper<TConfiguration>
        ↓
CsvFileImportSourceOptions
```

The core library must not inspect the user-owned configuration. The CSV extension may inspect only its own library-owned CSV option records after mapping.

## Namespace

Use:

```csharp
namespace DotnetSimpleImportOrchestrator.Csv;
```

## Package dependencies

Add CsvHelper to the library project:

```xml
<PackageReference Include="CsvHelper" Version="..." />
```

Use a current stable CsvHelper version available in the implementation environment. Report the version in the implementation summary.

If the implementation supports legacy code page encoding names such as `windows-1252`, add:

```xml
<PackageReference Include="System.Text.Encoding.CodePages" Version="..." />
```

and register `CodePagesEncodingProvider.Instance` once before resolving non-UTF encodings. If the implementation chooses not to support legacy code pages in this task, it must still support at least UTF-8 and UTF-16 encodings and must report unsupported encodings through option validation.

## CSV file source factory

### `ICsvFileImportSourceOptionsMapper<TConfiguration>`

```csharp
public interface ICsvFileImportSourceOptionsMapper<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    CsvFileImportSourceOptions Map(
        ImportDefinition<TConfiguration> definition,
        ImportSourceFactoryContext<TConfiguration> context);
}
```

Rules:

- the mapper is implemented by the consuming application or by tests;
- the mapper converts user-owned configuration into `CsvFileImportSourceOptions`;
- the mapper may read `definition.Configuration`, `definition.Id`, and current import state from `context`;
- the mapper must not return null.

### `CsvFileImportSourceFactory<TConfiguration>`

```csharp
public sealed class CsvFileImportSourceFactory<TConfiguration>
    : IImportSourceFactory<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    public CsvFileImportSourceFactory(
        ICsvFileImportSourceOptionsMapper<TConfiguration> mapper);
}
```

Behavior:

- calls the mapper;
- validates the returned options;
- creates `CsvFileImportSource`;
- does not inspect user configuration directly.

## CSV file source options

### `CsvFileImportSourceOptions`

```csharp
public sealed record CsvFileImportSourceOptions
{
    public required string DirectoryPath { get; init; }

    public required string SearchPattern { get; init; }

    public bool Recursive { get; init; }

    public FileCandidateOrdering Ordering { get; init; } =
        FileCandidateOrdering.OldestFirst;

    public MissingDirectoryBehavior MissingDirectoryBehavior { get; init; } =
        MissingDirectoryBehavior.TreatAsNoCandidate;

    public required FileReadinessOptions Readiness { get; init; }

    public required CsvPayloadOptions Csv { get; init; }
}
```

Validation:

- `DirectoryPath` must be non-empty;
- `SearchPattern` must be non-empty;
- `Readiness` must be non-null;
- `Csv` must be non-null;
- enum values must be defined.

### `MissingDirectoryBehavior`

```csharp
public enum MissingDirectoryBehavior
{
    TreatAsNoCandidate,
    Fail
}
```

Rules:

- `TreatAsNoCandidate` returns `ImportPollResult.NoCandidate()` when the directory is missing;
- `Fail` causes source polling to fail with a clear exception.

### `FileCandidateOrdering`

```csharp
public enum FileCandidateOrdering
{
    OldestFirst,
    NewestFirst,
    NameAscending,
    NameDescending
}
```

Ordering rules:

- `OldestFirst`: ascending `LastWriteTimeUtc`, then normalized full path ordinal;
- `NewestFirst`: descending `LastWriteTimeUtc`, then normalized full path ordinal;
- `NameAscending`: normalized full path ordinal ascending;
- `NameDescending`: normalized full path ordinal descending.

Default is `OldestFirst`.

## File readiness

### `FileReadinessOptions`

```csharp
public sealed record FileReadinessOptions
{
    public FileReadinessStrategy Strategy { get; init; } =
        FileReadinessStrategy.StableSize;

    public TimeSpan StableFor { get; init; } = TimeSpan.FromSeconds(5);

    public string? MarkerFileExtension { get; init; }
}
```

### `FileReadinessStrategy`

```csharp
public enum FileReadinessStrategy
{
    None,
    StableSize,
    ExclusiveRead,
    ExclusiveWrite,
    MarkerFile
}
```

Rules:

#### `None`

The file is considered ready as soon as it is discovered. Use for tests or controlled input folders.

#### `StableSize`

The file is ready when length and last-write timestamp remain unchanged for `StableFor`.

Implementation guidance:

```text
read length + last-write timestamp
wait StableFor with cancellation support
read length + last-write timestamp again
ready only if both values are unchanged
```

Validation:

- `StableFor` must be greater than `TimeSpan.Zero` when this strategy is used.

Tests may use very small intervals.

#### `ExclusiveRead`

The file is ready when the source can open the file for read access with exclusive sharing and close it again.

Suggested implementation:

```csharp
File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)
```

#### `ExclusiveWrite`

The file is ready when the source can open the file for read/write access with exclusive sharing and close it again.

Suggested implementation:

```csharp
File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
```

This may fail on read-only shares and should only be configured when appropriate.

#### `MarkerFile`

The data file is ready only when a marker file exists.

If the data file is:

```text
customers.csv
```

and `MarkerFileExtension` is `.done`, the marker file is:

```text
customers.csv.done
```

Validation:

- `MarkerFileExtension` must be non-empty when this strategy is used.

## CSV payload options

### `CsvPayloadOptions`

```csharp
public sealed record CsvPayloadOptions
{
    public required string EncodingName { get; init; }

    public string CultureName { get; init; } = "";

    public string Delimiter { get; init; } = ",";

    public char Quote { get; init; } = '"';

    public char Escape { get; init; } = '"';

    public bool HasHeaderRecord { get; init; } = true;

    public bool TrimFields { get; init; }

    public bool IgnoreBlankLines { get; init; } = true;

    public string? NewLine { get; init; }
}
```

Rules:

- `EncodingName` is required and resolved with `Encoding.GetEncoding` or equivalent;
- `CultureName` empty means invariant culture;
- `Delimiter` must be non-empty;
- `Quote` and `Escape` are single characters;
- `HasHeaderRecord` controls whether the first parsed record is treated as headers;
- `TrimFields` controls whether field values and header values are trimmed;
- `IgnoreBlankLines` is passed to CsvHelper;
- `NewLine` null lets CsvHelper use its default behavior.

There is no automatic delimiter, header, or encoding detection in this task.

## CSV candidate metadata

### Metadata shape

CSV candidates must include a metadata object with file and CSV sections.

Example:

```json
{
  "csvFileSource": {
    "file": {
      "fullPath": "C:/imports/customers/customers.csv",
      "fileName": "customers.csv",
      "length": 12093,
      "lastWriteTimeUtc": "2026-06-07T10:12:00Z"
    },
    "csv": {
      "encodingName": "utf-8",
      "cultureName": "",
      "delimiter": ";",
      "quote": "\"",
      "escape": "\"",
      "hasHeaderRecord": true,
      "trimFields": true,
      "ignoreBlankLines": true,
      "newLine": null
    }
  }
}
```

### `CsvCandidateMetadata`

Provide a helper to avoid fragile manual JSON parsing:

```csharp
public static class CsvCandidateMetadata
{
    public static JsonObject Create(
        CsvFileMetadata file,
        CsvPayloadOptions options);

    public static CsvFileMetadata GetFileMetadata(JsonObject metadata);

    public static CsvPayloadOptions GetPayloadOptions(JsonObject metadata);

    public static bool TryGetFileMetadata(
        JsonObject metadata,
        out CsvFileMetadata? file);

    public static bool TryGetPayloadOptions(
        JsonObject metadata,
        out CsvPayloadOptions? options);
}
```

Exact method names may vary, but callers must have a strongly typed way to extract metadata produced by the CSV source.

### `CsvFileMetadata`

```csharp
public sealed record CsvFileMetadata
{
    public required string FullPath { get; init; }

    public required string FileName { get; init; }

    public required long Length { get; init; }

    public required DateTimeOffset LastWriteTimeUtc { get; init; }
}
```

## CSV file source behavior

`CsvFileImportSource` implements `IImportSource`.

Polling algorithm:

```text
PollAsync(context)
  validate options
  if directory missing:
      apply MissingDirectoryBehavior
  enumerate matching files
  remove files already marked processed in context.State.Cursor
  apply configured readiness strategy
  order ready candidates
  if no ready unprocessed file:
      return NoCandidate()
  create candidate for first ready file
  return Candidate(candidate, processed-file cursor update)
```

Rules:

- return at most one candidate per poll;
- `SourceItemId` is the normalized full path;
- `OpenReadAsync` opens a readable stream for the selected file;
- candidate metadata includes `CsvCandidateMetadata`;
- the source must not parse CSV rows;
- processed-file cursor update is returned with the candidate and committed by the core runner only after handler success.

## CSV processed-file cursor

Use a CSV-specific cursor section:

```json
{
  "csvFileSource": {
    "processedFiles": {
      "C:/imports/customers/customers.csv": {
        "length": 12093,
        "lastWriteTimeUtc": "2026-06-07T10:12:00Z",
        "processedAt": "2026-06-07T10:15:22Z"
      }
    }
  }
}
```

Rules:

- key is the candidate `SourceItemId`;
- `processedAt` uses the poll context `TimeProvider`;
- a file with the same source item ID is skipped if present in processed files;
- checksum-based identity is out of scope for this task.

## CSV file processor

### `CsvFileProcessor`

```csharp
public sealed class CsvFileProcessor
{
    public ValueTask<CsvFileProcessingResult> ProcessAsync(
        Stream stream,
        CsvFileProcessingContext context,
        CancellationToken cancellationToken = default);
}
```

The processor is independent from the runner. A user handler may call it after receiving an import stream and candidate metadata.

### `CsvFileProcessingContext`

```csharp
public sealed record CsvFileProcessingContext
{
    public required CsvPayloadOptions Options { get; init; }

    public JsonObject Metadata { get; init; } = [];
}
```

Rules:

- `Options` is required;
- `Metadata` is optional and usually comes from `ImportHandlingContext<TConfiguration>.Metadata`.

### `CsvFileProcessingResult`

```csharp
public sealed record CsvFileProcessingResult
{
    public required CsvTable Table { get; init; }

    public IReadOnlyList<CsvUnprocessableContent> UnprocessableContent { get; init; } = [];

    public bool HasUnprocessableContent => UnprocessableContent.Count > 0;
}
```

### `CsvTable`

```csharp
public sealed record CsvTable
{
    public IReadOnlyList<string> Headers { get; init; } = [];

    public IReadOnlyList<CsvRow> Rows { get; init; } = [];
}
```

`Headers` contains the final normalized column names. If no header record is configured, generated names are used.

### `CsvRow`

```csharp
public sealed record CsvRow
{
    public required int RowNumber { get; init; }

    public IReadOnlyList<string> Values { get; init; } = [];

    public IReadOnlyDictionary<string, string> Fields { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
```

Rules:

- `RowNumber` is the physical 1-based CSV row number;
- if `HasHeaderRecord` is true, the first data row normally has row number 2;
- `Values` is normalized to the final column count;
- `Fields` maps normalized header names to normalized values.

### `CsvUnprocessableContent`

```csharp
public sealed record CsvUnprocessableContent
{
    public int? RowNumber { get; init; }

    public required string RawContent { get; init; }

    public required string Reason { get; init; }

    public string? ErrorCode { get; init; }
}
```

Rules:

- malformed CSV content is captured here when possible;
- malformed records are excluded from `Rows`;
- raw content should contain CsvHelper raw record information when available;
- the processor continues after malformed content when CsvHelper can recover.

## Processor success semantics

The processor must always return a processing result for malformed CSV content. It must not throw merely because a CSV record is malformed.

Allowed exceptions:

- cancellation through `OperationCanceledException`;
- unreadable or disposed streams;
- environmental I/O failures;
- invalid processor options discovered before parsing.

All CSV syntax/content problems should be captured in `UnprocessableContent` and excluded from normalized rows when possible.

## Normalization rules

### Header handling

If `HasHeaderRecord` is true:

- the first parsed record is treated as the header row;
- data rows start after the header row;
- physical row numbers are preserved.

If `HasHeaderRecord` is false:

- no input row is treated as headers;
- generated headers are created from the maximum data row width.

### Final column count

```text
final column count = max(configured/parsed header field count, maximum parsed data row field count)
```

### Generated header names

Generated names use one-based final column position:

```text
Column1
Column2
Column3
...
```

### Empty and duplicate headers

A parsed header is usable only if it is non-empty and unique after optional trimming.

If a header is empty or duplicate, replace it with the generated name for that column position.

Example:

```text
Name;Name;;Country
```

becomes:

```text
Name
Column2
Column3
Country
```

### Header shorter than data rows

If headers are shorter than data rows, append generated header names.

Example:

```text
Name;Age
Alice;42;DE
```

produces headers:

```text
Name
Age
Column3
```

### Data rows shorter than headers

If a row has fewer fields than the final column count, append empty string values.

Example:

```text
Name;Age;Country
Alice;42
```

produces row values:

```text
Alice
42
""
```

### Data rows longer than headers

If a row has more fields than the parsed header row, generated headers are added as needed.

### Blank lines

If `IgnoreBlankLines` is true, blank lines are not rows and are not unprocessable content.

If `IgnoreBlankLines` is false, CsvHelper behavior applies, and the processor must normalize any parsed blank row according to the same column rules.

## CsvHelper configuration

The implementation should configure CsvHelper from `CsvPayloadOptions`:

- culture from `CultureName` or invariant culture;
- delimiter from `Delimiter`;
- quote from `Quote`;
- escape from `Escape`;
- `HasHeaderRecord` from options;
- `IgnoreBlankLines` from options;
- trim behavior from `TrimFields`;
- newline from `NewLine` when not null.

Malformed content handling must be configured so that CsvHelper exceptions for bad records are captured into `CsvUnprocessableContent` where possible and processing continues where possible.

## Example usage from handler

```csharp
public sealed class CustomerCsvImportHandler
    : IImportHandler<CustomerCsvImportConfiguration>
{
    private readonly CsvFileProcessor _processor = new();

    public async ValueTask<ImportHandlingResult> HandleAsync(
        ImportHandlingContext<CustomerCsvImportConfiguration> context,
        Stream payload,
        CancellationToken cancellationToken)
    {
        CsvPayloadOptions options =
            CsvCandidateMetadata.GetPayloadOptions(context.Metadata);

        CsvFileProcessingResult result = await _processor.ProcessAsync(
            payload,
            new CsvFileProcessingContext
            {
                Options = options,
                Metadata = context.Metadata
            },
            cancellationToken);

        // User-owned domain import logic consumes result.Table and result.UnprocessableContent.
        return ImportHandlingResult.Success();
    }
}
```

Exact convenience APIs may vary, but this usage must remain possible.

## Out of scope

- automatic delimiter detection;
- automatic encoding detection;
- schema inference;
- row-to-domain-object mapping;
- business validation;
- moving, deleting, archiving, or quarantining files;
- checksum-based file identity;
- filesystem watcher/event-driven source;
- background service scheduling;
- exposing CSV-specific properties on core import definitions.

## Acceptance criteria

- CSV extension compiles without changing core orchestration contracts.
- CsvHelper is used by `CsvFileProcessor`.
- `CsvFileImportSourceFactory<TConfiguration>` maps user configuration through `ICsvFileImportSourceOptionsMapper<TConfiguration>`.
- `CsvFileImportSource` returns one ready unprocessed candidate per poll.
- File readiness supports `None`, `StableSize`, `ExclusiveRead`, `ExclusiveWrite`, and `MarkerFile`.
- Candidate metadata can be created and read through typed helpers.
- `CsvFileProcessor` returns normalized headers, rows, fields, and unprocessable content.
- Malformed CSV content is captured and does not make processing fail by exception.
- Tests cover source discovery, readiness, cursor skip/commit shape, metadata, processing normalization, encoding, and malformed content capture.
