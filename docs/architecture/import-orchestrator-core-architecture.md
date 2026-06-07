# Architecture: Import orchestrator core

## Purpose

The library is a small in-house import orchestration component.

It owns:

- import definitions;
- polling decisions;
- runtime state transitions;
- re-entrant execution based on JSON-compatible state;
- source and handler contracts.

It does not own:

- business parsing of CSV or XML;
- real web-service integration;
- durable persistence of runtime state;
- hosted background scheduling;
- distributed locking;
- domain validation.

## Core flow

```text
JSON import configuration + JSON runtime state
        ↓
ImportRunner decides which imports are due
        ↓
IImportSource produces ImportCandidate values
        ↓
IImportHandler receives a Stream
        ↓
ImportRunner produces updated ImportRuntimeState
        ↓
Host application persists state
```

## Design boundaries

### Source acquisition and payload interpretation are separate

The library may know that a payload is `Csv`, `Xml`, `Json`, or `Binary`, but user code interprets the stream.

Do not create parser-specific abstractions such as:

```text
ICsvImporter
IXmlImporter
IWebImporter
```

Prefer generic source and handler contracts.

### Persistence is external

The runner returns updated runtime state. The host persists it.

Do not introduce a state store abstraction in the first task unless it is only a test helper.

### Polling is host-driven

The core API should expose an explicit runner method. It should not require a background service.

A hosted service adapter may be added later, but it is not part of this task.

## Public model direction

Names may be adjusted for idiomatic C#, but the following concepts must exist.

### `ImportDefinition`

Represents configured import behavior.

Required conceptual fields:

```csharp
public sealed record ImportDefinition
{
    public required string Id { get; init; }
    public required string SourceName { get; init; }
    public required string HandlerName { get; init; }
    public ImportPayloadFormat Format { get; init; }
    public bool Enabled { get; init; } = true;
    public PollingOptions Polling { get; init; } = new();
    public JsonObject Source { get; init; } = [];
}
```

`Source` must remain JSON-extensible for source-specific configuration.

### `PollingOptions`

Represents simple host-driven polling rules.

Suggested shape:

```csharp
public sealed record PollingOptions
{
    public TimeSpan? Interval { get; init; }
}
```

If `Interval` is null, the import can be considered manually runnable or always due when explicitly invoked.

### `ImportRuntimeState`

Represents all durable runtime state.

Required conceptual fields:

```csharp
public sealed record ImportRuntimeState
{
    public Dictionary<string, ImportState> Imports { get; init; } = [];
}
```

### `ImportState`

Represents runtime state for one import ID.

Required conceptual fields:

```csharp
public sealed record ImportState
{
    public DateTimeOffset? LastPollStartedAt { get; init; }
    public DateTimeOffset? LastPollCompletedAt { get; init; }
    public DateTimeOffset? LastSuccessfulImportAt { get; init; }
    public ImportError? LastError { get; init; }
    public JsonObject Cursor { get; init; } = [];
}
```

`Cursor` must remain JSON-extensible for source-specific progress state.

### `ImportError`

Persisted state may include an error summary, but must not serialize raw exceptions.

Suggested shape:

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

Suggested shape:

```csharp
public interface IImportRunner
{
    ValueTask<ImportRunResult> RunDueImportsAsync(
        IReadOnlyList<ImportDefinition> definitions,
        ImportRuntimeState state,
        CancellationToken cancellationToken);
}
```

A later task may add ad-hoc stream execution. It is optional for the bootstrap task.

### `IImportSource`

Produces import candidates.

Suggested shape:

```csharp
public interface IImportSource
{
    ValueTask<IReadOnlyList<ImportCandidate>> PollAsync(
        ImportSourceContext context,
        CancellationToken cancellationToken);
}
```

### `ImportCandidate`

Represents one importable item.

Suggested shape:

```csharp
public sealed record ImportCandidate
{
    public required string SourceItemId { get; init; }
    public required ImportPayloadFormat Format { get; init; }
    public required Func<CancellationToken, ValueTask<Stream>> OpenReadAsync { get; init; }
    public JsonObject Metadata { get; init; } = [];
}
```

### `IImportHandler`

Handles one candidate stream.

Suggested shape:

```csharp
public interface IImportHandler
{
    ValueTask<ImportHandlingResult> HandleAsync(
        ImportHandlingContext context,
        Stream payload,
        CancellationToken cancellationToken);
}
```

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
- contains per-attempt results;
- exposes live exceptions in attempt results if relevant;
- does not require raw exception serialization into durable state.

## Source registry direction

For the first implementation, avoid plugin discovery.

Acceptable approaches:

- constructor-injected dictionaries keyed by source/handler name;
- small explicit registry interfaces;
- simple DI registration helpers if they remain minimal.

The implementation must make it possible to map an `ImportDefinition` to one source and one handler.

## File-backed source

The first implementation should provide a deterministic file-backed source useful for tests and demos.

It may be a simple source that returns configured file paths as candidates. It does not need production-grade directory polling yet.

A production directory polling source can be added later with file stability checks, marker files, checksums, and processed-file cursors.

## State transition rules

Minimum durable rule:

> An import item is considered successfully processed only after the handler returns success and the runner has produced updated runtime state.

For the first implementation, it is acceptable to update only import-level timestamps and merge handler cursor updates into the import cursor.

Do not attempt cross-process locking.

## Concurrency policy

Initial policy:

- no concurrent execution of the same import ID within one runner call;
- sequential processing is acceptable and preferred for the bootstrap task;
- cross-process concurrency is the host application's responsibility.

## JSON policy

Use `System.Text.Json`.

Recommended approach:

- typed records for stable fields;
- `JsonObject` for source-specific configuration and cursor state;
- no raw JSON strings for internal state unless needed for serialization tests.

## Acceptance criteria

- The architecture is represented by compiling public contracts.
- The runner can execute at least one deterministic file-backed import in tests.
- Runtime state is JSON-serializable.
- Source-specific configuration and cursor data can preserve arbitrary JSON object properties.
- The implementation does not force parser, web client, persistence, or hosted-service choices.
