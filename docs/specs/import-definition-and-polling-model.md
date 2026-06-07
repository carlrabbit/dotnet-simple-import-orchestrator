# Spec: Import definition and polling model

## Purpose

This spec defines the revised import definition, source factory, handler, and polling model for `DotnetSimpleImportOrchestrator`.

This spec replaces the bootstrap model. There is no backwards compatibility requirement.

## Design principle

The orchestrator owns only orchestration metadata and runtime state transitions.

The consuming application owns:

- file paths;
- URL shapes;
- API clients;
- credentials;
- CSV/XML/JSON/domain parsing;
- source-specific cursor semantics;
- durable persistence;
- construction of the current import list.

The library must not infer source kind or payload format from an import definition.

## Import definition model

### `IImportConfiguration`

Marker interface for user-owned configuration.

```csharp
public interface IImportConfiguration
{
}
```

The library must not inspect, serialize, validate, or interpret implementation-specific properties on `IImportConfiguration` except to require a non-null configuration object on each definition.

### `IImportDefinition`

Non-generic orchestration view used by the runner to accept mixed configuration types.

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

Strongly typed user-facing definition.

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

Required semantics:

- `Id` is stable and addresses definition, factory registration, handler registration, and runtime state.
- `Priority` controls check order only.
- `Polling` controls whether the import is due.
- `Configuration` is user-owned and strongly typed.

Forbidden core definition properties:

- `SourceName`;
- `HandlerName`;
- `SourceKind`;
- `Format` / `ImportPayloadFormat`;
- `Enabled`;
- `JsonObject Source` or equivalent library-owned source configuration.

Disabled imports are represented by not passing them to the runner.

## Priority model

Use lower numeric values for higher priority.

```csharp
public static class ImportPriorities
{
    public const int Highest = 0;
    public const int High = 100;
    public const int Normal = 500;
    public const int Low = 900;
}
```

Rules:

- missing priority uses `ImportPriorities.Normal`;
- duplicate priorities are allowed;
- ordering is deterministic by priority ascending, then import ID using `StringComparer.Ordinal`;
- priority never makes an import due earlier;
- starvation of lower-priority imports is accepted for v1 when higher-priority imports continuously succeed.

## Polling options

```csharp
public sealed record PollingOptions
{
    public required TimeSpan Interval { get; init; }
}
```

Rules:

- `Interval` must be greater than `TimeSpan.Zero`;
- the runner uses the current import state to decide whether the import is due;
- if there is no prior state for an import, the import is due immediately;
- if an import has been checked before, it is due when `last check timestamp + interval <= now`.

The implementation may name the timestamp `LastCheckedAt`, or may derive it from existing started/completed polling timestamps, but the due calculation must be explicit and tested.

## Current import list

Every runner call receives the current expected imports.

```csharp
ValueTask<ImportRunResult> RunOnceAsync(
    IReadOnlyList<IImportDefinition> imports,
    ImportRuntimeState state,
    CancellationToken cancellationToken);
```

Rules:

- the runner snapshots the list at the start of the call;
- mutations by the caller during a runner call do not affect the current pass;
- added, removed, or changed definitions take effect on the next runner call;
- removed imports do not automatically delete existing runtime state;
- re-added imports use existing runtime state if the same ID appears again;
- duplicate IDs in the current list are invalid.

## Source factory model

Users provide source factories.

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

Suggested context:

```csharp
public sealed record ImportSourceFactoryContext<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    public required ImportDefinition<TConfiguration> Definition { get; init; }

    public required ImportState State { get; init; }

    public required TimeProvider TimeProvider { get; init; }
}
```

Rules:

- source factories are resolved by import ID;
- each import ID in a runner call must have a registered source factory;
- the factory receives the strongly typed definition;
- the factory may use user-owned configuration to construct an `IImportSource`;
- the core library must not provide plugin discovery for this task.

## Source model

The source checks whether an import candidate exists.

```csharp
public interface IImportSource
{
    ValueTask<ImportPollResult> PollAsync(
        ImportPollContext context,
        CancellationToken cancellationToken);
}
```

Suggested context:

```csharp
public sealed record ImportPollContext
{
    public required string ImportId { get; init; }

    public required ImportState State { get; init; }

    public required TimeProvider TimeProvider { get; init; }
}
```

The source may represent file, web, test, or any other acquisition mechanism. The library does not need to know which.

## Poll result

The source returns either no candidate or one candidate.

```csharp
public sealed record ImportPollResult
{
    public ImportCandidate? Candidate { get; init; }

    public JsonObject CursorUpdate { get; init; } = [];
}
```

Acceptable convenience factories:

```csharp
ImportPollResult.NoCandidate(JsonObject? cursorUpdate = null);
ImportPollResult.Candidate(ImportCandidate candidate, JsonObject? cursorUpdate = null);
```

Rules:

- no candidate is not a successful import;
- a source poll that returns no candidate still counts as a completed check for polling interval purposes;
- cursor updates from no-candidate polls may be merged into state only when they do not claim irreversible import completion;
- irreversible source progress should normally be committed only after handler success.

## Candidate model

```csharp
public sealed record ImportCandidate
{
    public required string SourceItemId { get; init; }

    public required Func<CancellationToken, ValueTask<Stream>> OpenReadAsync { get; init; }

    public JsonObject Metadata { get; init; } = [];
}
```

Rules:

- `SourceItemId` identifies the candidate within the source's own semantics;
- `OpenReadAsync` returns a readable stream;
- the core candidate does not expose payload format;
- `Metadata` is optional and source-owned.

## Handler model

Users provide typed handlers.

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

Suggested context:

```csharp
public sealed record ImportHandlingContext<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    public required ImportDefinition<TConfiguration> Definition { get; init; }

    public required string SourceItemId { get; init; }

    public required ImportState State { get; init; }

    public JsonObject Metadata { get; init; } = [];
}
```

Handler rules:

- handlers are resolved by import ID;
- each import ID in a runner call must have a registered handler;
- handlers receive the strongly typed definition and payload stream;
- handlers own parsing and business persistence;
- the core library does not provide CSV, XML, JSON, or web response parsing abstractions.

## Handling result

`ImportHandlingResult` must represent:

- success/failure;
- optional cursor update on success;
- controlled failure message.

A suggested shape:

```csharp
public sealed record ImportHandlingResult
{
    public required bool Succeeded { get; init; }

    public JsonObject CursorUpdate { get; init; } = [];

    public string? ErrorMessage { get; init; }
}
```

Rules:

- only success can produce a successful import;
- controlled failure does not stop the runner pass;
- thrown exceptions are captured in live check results and summarized into persisted state.

## Runner behavior

The runner performs one bounded polling pass.

Algorithm:

```text
RunOnce(imports, state)
  snapshot imports
  validate imports and registrations
  filter to due imports
  sort due imports by priority, then ID
  for each import:
      create source through the registered typed source factory
      poll source
      if no candidate:
          update check state
          continue
      open candidate stream
      invoke registered typed handler
      if handler succeeds:
          merge successful cursor updates
          update success state
          return result with SuccessfulImportPerformed = true
      else:
          update failure state
          continue
  return result with SuccessfulImportPerformed = false
```

Rules:

- a runner pass may check many imports;
- a runner pass performs at most one successful import;
- the runner stops only after a candidate was handled successfully and the returned runtime state reflects the successful import;
- source failures and handler failures are recorded and do not stop the pass by default;
- cancellation may stop the pass by throwing or returning a canceled task according to normal .NET cancellation conventions.

## Check outcomes

Result objects must make runner behavior inspectable without relying on logs.

Suggested enum:

```csharp
public enum ImportCheckOutcome
{
    NotDue,
    NoCandidate,
    SourceFailed,
    HandlerFailed,
    Imported,
    Skipped
}
```

Suggested check result:

```csharp
public sealed record ImportCheckResult
{
    public required string ImportId { get; init; }

    public required ImportCheckOutcome Outcome { get; init; }

    public string? SourceItemId { get; init; }

    public string? Message { get; init; }

    public Exception? Exception { get; init; }
}
```

Suggested run result:

```csharp
public sealed record ImportRunResult
{
    public required ImportRuntimeState State { get; init; }

    public required bool SuccessfulImportPerformed { get; init; }

    public string? SuccessfulImportId { get; init; }

    public IReadOnlyList<ImportCheckResult> Checks { get; init; } = [];
}
```

## Runtime state

Runtime state remains JSON-compatible and externally persisted.

```csharp
public sealed record ImportRuntimeState
{
    public Dictionary<string, ImportState> Imports { get; init; } = [];
}
```

Suggested import state:

```csharp
public sealed record ImportState
{
    public DateTimeOffset? LastCheckedAt { get; init; }

    public DateTimeOffset? LastSuccessfulImportAt { get; init; }

    public ImportError? LastError { get; init; }

    public JsonObject Cursor { get; init; } = [];
}
```

The implementation may retain `LastPollStartedAt` and `LastPollCompletedAt` if useful, but due calculation and tests must be clear.

Persisted errors must not serialize raw exception objects.

```csharp
public sealed record ImportError
{
    public required string Message { get; init; }

    public required string ErrorType { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
```

## Validation rules

The runner must reject invalid input before polling:

- `imports` is not null;
- `state` is not null;
- every import definition is non-null;
- every import ID is non-empty;
- every import ID is unique in the current call;
- every `PollingOptions` value is non-null;
- every polling interval is positive;
- every configuration object is non-null;
- every import ID has a registered source factory;
- every import ID has a registered handler.

Use idiomatic .NET exceptions for programmer errors, such as `ArgumentException` or `InvalidOperationException`. The exact exception type is less important than consistent tests and useful messages.

## Out of scope

- Backwards compatibility with the old definition shape.
- JSON polymorphic loading of mixed `ImportDefinition<TConfiguration>` arrays inside the library.
- Background service scheduling.
- State store abstraction.
- CSV/XML/JSON parsing.
- Real HTTP/web-service implementation.
- Distributed locks.
- Fairness or anti-starvation scheduling.
- Plugin discovery.
