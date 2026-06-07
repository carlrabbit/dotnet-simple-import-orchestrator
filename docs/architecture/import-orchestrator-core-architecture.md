# Architecture: Import orchestrator core

## Purpose

The library is a small in-house import orchestration component.

It owns:

- polling decisions;
- priority ordering;
- bounded runner passes;
- runtime state transitions;
- re-entrant execution based on JSON-compatible state;
- source factory, source, and handler contracts.

It does not own:

- source-specific configuration schemas;
- business parsing of CSV, XML, JSON, binary, or web payloads;
- real web-service integration;
- durable persistence of runtime state;
- hosted background scheduling;
- distributed locking;
- domain validation;
- plugin discovery.

## Core flow

```text
Current import definitions + runtime state
        ↓
ImportRunner snapshots definitions
        ↓
ImportRunner validates definitions and registrations
        ↓
ImportRunner filters due imports
        ↓
ImportRunner orders due imports by priority, then ID
        ↓
Typed source factory creates an IImportSource
        ↓
IImportSource returns no candidate or one ImportCandidate
        ↓
Typed handler receives a Stream
        ↓
ImportRunner updates runtime state
        ↓
Host application persists state
```

One runner pass stops after the first successful import. If no import succeeds, the pass ends after all due imports have been checked.

## Design boundaries

### Import definitions are orchestration metadata only

The core import definition contains only:

- stable import ID;
- priority;
- polling interval;
- user-owned typed configuration.

Do not put these concerns into the core import definition:

- source kind;
- payload format;
- source name;
- handler name;
- enabled flag;
- file path;
- URL;
- JSON object containing source settings.

Disabled imports are omitted from the list supplied to the runner.

### Source acquisition and payload interpretation are separate

Sources acquire candidate streams. Handlers interpret candidate streams.

The library does not create parser-specific abstractions such as:

```text
ICsvImporter
IXmlImporter
IWebImporter
```

The library also does not need core enums for `Csv`, `Xml`, `Json`, or `Binary` payload formats. If the user needs such information, it belongs in user configuration or candidate metadata.

### Persistence is external

The runner returns updated runtime state. The host persists it.

Do not introduce a state store abstraction for this task unless it is purely a test helper.

### Polling is host-driven

The core API exposes an explicit runner method. It does not require a background service.

A hosted service adapter may be added later, but it is not part of this architecture baseline.

## Public model direction

Names may be adjusted for idiomatic C#, but the following concepts must exist.

### `IImportConfiguration`

Marker interface implemented by user-owned configuration types.

```csharp
public interface IImportConfiguration
{
}
```

The library must not interpret implementation-specific configuration properties.

### `IImportDefinition`

Non-generic view for mixed import lists.

```csharp
public interface IImportDefinition
{
    string Id { get; }
    int Priority { get; }
    PollingOptions Polling { get; }
    IImportConfiguration Configuration { get; }
}
```

### `ImportDefinition<TConfiguration>`

Strongly typed import definition.

```csharp
public sealed record ImportDefinition<TConfiguration> : IImportDefinition
    where TConfiguration : IImportConfiguration
{
    public required string Id { get; init; }
    public int Priority { get; init; } = ImportPriorities.Normal;
    public required PollingOptions Polling { get; init; }
    public required TConfiguration Configuration { get; init; }

    IImportConfiguration IImportDefinition.Configuration => Configuration;
}
```

### `PollingOptions`

```csharp
public sealed record PollingOptions
{
    public required TimeSpan Interval { get; init; }
}
```

The interval must be positive.

### `ImportPriorities`

```csharp
public static class ImportPriorities
{
    public const int Highest = 0;
    public const int High = 100;
    public const int Normal = 500;
    public const int Low = 900;
}
```

Lower numbers run first.

### `ImportRuntimeState`

Represents all durable runtime state.

```csharp
public sealed record ImportRuntimeState
{
    public Dictionary<string, ImportState> Imports { get; init; } = [];
}
```

### `ImportState`

Represents runtime state for one import ID.

```csharp
public sealed record ImportState
{
    public DateTimeOffset? LastCheckedAt { get; init; }
    public DateTimeOffset? LastSuccessfulImportAt { get; init; }
    public ImportError? LastError { get; init; }
    public JsonObject Cursor { get; init; } = [];
}
```

`Cursor` remains JSON-extensible for source-specific progress state.

### `ImportError`

Persisted state may include an error summary, but must not serialize raw exceptions.

```csharp
public sealed record ImportError
{
    public required string Message { get; init; }
    public required string ErrorType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
}
```

### `IImportRunner`

Core orchestration entry point.

```csharp
public interface IImportRunner
{
    ValueTask<ImportRunResult> RunOnceAsync(
        IReadOnlyList<IImportDefinition> imports,
        ImportRuntimeState state,
        CancellationToken cancellationToken);
}
```

### `IImportSourceFactory<TConfiguration>`

Creates sources from strongly typed user configuration.

```csharp
public interface IImportSourceFactory<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    ValueTask<IImportSource> CreateAsync(
        ImportDefinition<TConfiguration> definition,
        ImportSourceFactoryContext<TConfiguration> context,
        CancellationToken cancellationToken);
}
```

Source factories are registered/resolved by import ID for this task.

### `IImportSource`

Checks whether source data is available.

```csharp
public interface IImportSource
{
    ValueTask<ImportPollResult> PollAsync(
        ImportPollContext context,
        CancellationToken cancellationToken);
}
```

### `ImportPollResult`

Represents no candidate or one candidate.

```csharp
public sealed record ImportPollResult
{
    public ImportCandidate? Candidate { get; init; }
    public JsonObject CursorUpdate { get; init; } = [];
}
```

### `ImportCandidate`

Represents one importable item.

```csharp
public sealed record ImportCandidate
{
    public required string SourceItemId { get; init; }
    public required Func<CancellationToken, ValueTask<Stream>> OpenReadAsync { get; init; }
    public JsonObject Metadata { get; init; } = [];
}
```

The candidate does not expose a core payload format.

### `IImportHandler<TConfiguration>`

Handles one candidate stream.

```csharp
public interface IImportHandler<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    ValueTask<ImportHandlingResult> HandleAsync(
        ImportHandlingContext<TConfiguration> context,
        Stream payload,
        CancellationToken cancellationToken);
}
```

Handlers are registered/resolved by import ID for this task.

### `ImportHandlingResult`

Represents the handler result.

Required behavior:

- success/failure flag;
- optional cursor update on success;
- failure message for controlled failures.

### `ImportRunResult`

Returned by the runner.

Required behavior:

- contains the updated `ImportRuntimeState`;
- states whether a successful import was performed;
- identifies the successful import ID when one succeeded;
- contains per-check results;
- exposes live exceptions in check results when relevant;
- does not require raw exception serialization into durable state.

## Registration direction

For this task, avoid plugin discovery.

Use an explicit registry or constructor-supplied mapping keyed by import ID.

The implementation must make it possible to map each import ID to:

- one typed source factory;
- one typed handler.

The registry may use non-generic internal adapters so the runner can work with `IImportDefinition` while user implementations remain strongly typed.

## State transition rules

Minimum durable rule:

> An import item is considered successfully processed only after the handler returns success and the runner has produced updated runtime state.

No candidate:

- update `LastCheckedAt`;
- clear or preserve `LastError` according to implementation policy, but tests must specify the behavior;
- continue to the next due import.

Source or handler failure:

- update `LastCheckedAt`;
- write an `ImportError` summary;
- keep raw exception only in the live result;
- continue to the next due import.

Successful import:

- merge cursor updates that are safe to commit;
- update `LastCheckedAt`;
- update `LastSuccessfulImportAt`;
- clear stale error state unless tests intentionally preserve it;
- stop the runner pass.

Do not attempt cross-process locking.

## Concurrency policy

Initial policy:

- one runner pass is sequential;
- no concurrent execution of the same import ID within one runner call;
- one runner pass performs at most one successful import;
- cross-process concurrency is the host application's responsibility.

## JSON policy

Use `System.Text.Json`.

Recommended approach:

- typed records for stable state fields;
- `JsonObject` for cursor state and source/candidate metadata;
- no raw JSON strings for internal state unless needed for tests.

The library does not need to deserialize mixed generic import definition arrays from JSON in this task. The host application owns construction of the current import list.

## Acceptance criteria

- The architecture is represented by compiling public contracts.
- The old non-generic core `ImportDefinition` shape is gone.
- The runner can execute a deterministic import using a user-owned configuration type, source factory, source, and handler.
- Runtime state is JSON-serializable.
- Source-specific cursor data can preserve arbitrary JSON object properties.
- One runner pass stops after the first successful import.
- Source-specific configuration is strongly typed and not interpreted by the core library.
- The implementation does not force parser, web client, persistence, hosted-service, or plugin-discovery choices.
