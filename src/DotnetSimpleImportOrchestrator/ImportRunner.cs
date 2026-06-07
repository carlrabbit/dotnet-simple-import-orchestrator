using System.Text.Json.Nodes;
using DotnetSimpleImportOrchestrator.Abstractions;

namespace DotnetSimpleImportOrchestrator;

public sealed class ImportRunner : IImportRunner
{
    private readonly IReadOnlyDictionary<string, ImportSourceFactoryRegistration> _sourceFactories;
    private readonly IReadOnlyDictionary<string, ImportHandlerRegistration> _handlers;
    private readonly TimeProvider _timeProvider;

    public ImportRunner(
        IReadOnlyDictionary<string, ImportSourceFactoryRegistration> sourceFactories,
        IReadOnlyDictionary<string, ImportHandlerRegistration> handlers,
        TimeProvider? timeProvider = null)
    {
        _sourceFactories = sourceFactories;
        _handlers = handlers;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<ImportRunResult> RunOnceAsync(
        IReadOnlyList<IImportDefinition> imports,
        ImportRuntimeState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imports);
        ArgumentNullException.ThrowIfNull(state);

        IImportDefinition[] snapshot = imports.ToArray();
        ValidateSnapshot(snapshot);

        Dictionary<string, ImportState> updatedImports = CloneImports(state.Imports);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        List<ImportCheckResult> checks = [];
        List<IImportDefinition> dueImports = [];

        foreach (IImportDefinition import in snapshot)
        {
            ImportState importState = updatedImports.TryGetValue(import.Id, out ImportState? existingState)
                ? existingState
                : new ImportState();
            if (IsDue(import, importState, now))
            {
                dueImports.Add(import);
            }
            else
            {
                checks.Add(new ImportCheckResult
                {
                    ImportId = import.Id,
                    Outcome = ImportCheckOutcome.NotDue,
                    Message = "Import is not due."
                });
            }
        }

        foreach (IImportDefinition import in dueImports
            .OrderBy(static item => item.Priority)
            .ThenBy(static item => item.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            ImportState currentState = GetState(updatedImports, import.Id);
            DateTimeOffset checkedAt = _timeProvider.GetUtcNow();

            if (!_sourceFactories.TryGetValue(import.Id, out ImportSourceFactoryRegistration? sourceFactory))
            {
                throw new InvalidOperationException($"No source factory is registered for import '{import.Id}'.");
            }

            if (!_handlers.TryGetValue(import.Id, out ImportHandlerRegistration? handler))
            {
                throw new InvalidOperationException($"No handler is registered for import '{import.Id}'.");
            }

            IImportSource source;
            try
            {
                source = await sourceFactory.CreateAsync(import, currentState, _timeProvider, cancellationToken);
                if (source is null)
                {
                    throw new InvalidOperationException($"Source factory for import '{import.Id}' returned null.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                currentState = RecordError(currentState, exception, checkedAt);
                updatedImports[import.Id] = currentState;
                checks.Add(FailedCheck(import.Id, ImportCheckOutcome.SourceFailed, null, exception.Message, exception));
                continue;
            }

            ImportPollResult pollResult;
            try
            {
                pollResult = await source.PollAsync(
                    new ImportPollContext
                    {
                        ImportId = import.Id,
                        State = currentState,
                        TimeProvider = _timeProvider
                    },
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                currentState = RecordError(currentState, exception, checkedAt);
                updatedImports[import.Id] = currentState;
                checks.Add(FailedCheck(import.Id, ImportCheckOutcome.SourceFailed, null, exception.Message, exception));
                continue;
            }

            if (pollResult.Candidate is null)
            {
                currentState = currentState with
                {
                    LastCheckedAt = checkedAt,
                    LastError = null,
                    Cursor = MergeCursor(currentState.Cursor, pollResult.CursorUpdate)
                };
                updatedImports[import.Id] = currentState;
                checks.Add(new ImportCheckResult
                {
                    ImportId = import.Id,
                    Outcome = ImportCheckOutcome.NoCandidate,
                    Message = "No candidate was available."
                });
                continue;
            }

            ImportCandidate candidate = pollResult.Candidate;
            try
            {
                await using Stream payload = await candidate.OpenReadAsync(cancellationToken);
                ImportHandlingResult handlingResult = await handler.HandleAsync(
                    import,
                    candidate,
                    currentState,
                    payload,
                    cancellationToken);

                if (!handlingResult.Succeeded)
                {
                    string message = handlingResult.ErrorMessage ?? "Import handler reported failure.";
                    currentState = currentState with
                    {
                        LastCheckedAt = checkedAt,
                        LastError = new ImportError
                        {
                            Message = message,
                            ErrorType = "ImportHandlingFailure",
                            OccurredAt = checkedAt
                        }
                    };
                    updatedImports[import.Id] = currentState;
                    checks.Add(FailedCheck(import.Id, ImportCheckOutcome.HandlerFailed, candidate.SourceItemId, message));
                    continue;
                }

                currentState = currentState with
                {
                    LastCheckedAt = checkedAt,
                    LastSuccessfulImportAt = _timeProvider.GetUtcNow(),
                    LastError = null,
                    Cursor = MergeCursor(
                        MergeCursor(currentState.Cursor, pollResult.CursorUpdate),
                        handlingResult.CursorUpdate)
                };
                updatedImports[import.Id] = currentState;
                checks.Add(new ImportCheckResult
                {
                    ImportId = import.Id,
                    Outcome = ImportCheckOutcome.Imported,
                    SourceItemId = candidate.SourceItemId
                });

                return new ImportRunResult
                {
                    State = new ImportRuntimeState { Imports = updatedImports },
                    SuccessfulImportPerformed = true,
                    SuccessfulImportId = import.Id,
                    Checks = checks
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                currentState = RecordError(currentState, exception, checkedAt);
                updatedImports[import.Id] = currentState;
                checks.Add(FailedCheck(import.Id, ImportCheckOutcome.HandlerFailed, candidate.SourceItemId, exception.Message, exception));
            }
        }

        return new ImportRunResult
        {
            State = new ImportRuntimeState { Imports = updatedImports },
            SuccessfulImportPerformed = false,
            Checks = checks
        };
    }

    private void ValidateSnapshot(IReadOnlyList<IImportDefinition> imports)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int i = 0; i < imports.Count; i++)
        {
            IImportDefinition? import = imports[i];
            if (import is null)
            {
                throw new ArgumentException($"Import at index {i} is null.", nameof(imports));
            }

            if (string.IsNullOrWhiteSpace(import.Id))
            {
                throw new ArgumentException("Import ID must be non-empty.", nameof(imports));
            }

            if (!ids.Add(import.Id))
            {
                throw new ArgumentException($"Duplicate import ID '{import.Id}'.", nameof(imports));
            }

            if (import.Polling is null)
            {
                throw new ArgumentException($"Import '{import.Id}' has no polling options.", nameof(imports));
            }

            if (import.Polling.Interval <= TimeSpan.Zero)
            {
                throw new ArgumentException($"Import '{import.Id}' polling interval must be positive.", nameof(imports));
            }

            if (import.Configuration is null)
            {
                throw new ArgumentException($"Import '{import.Id}' configuration must be non-null.", nameof(imports));
            }
        }

        foreach (IImportDefinition import in imports)
        {
            if (!_sourceFactories.ContainsKey(import.Id))
            {
                throw new InvalidOperationException($"No source factory is registered for import '{import.Id}'.");
            }

            if (!_handlers.ContainsKey(import.Id))
            {
                throw new InvalidOperationException($"No handler is registered for import '{import.Id}'.");
            }
        }
    }

    private static bool IsDue(IImportDefinition import, ImportState state, DateTimeOffset now)
    {
        if (state.LastCheckedAt is null)
        {
            return true;
        }

        return state.LastCheckedAt.Value + import.Polling.Interval <= now;
    }

    private static ImportState GetState(Dictionary<string, ImportState> imports, string importId)
    {
        if (imports.TryGetValue(importId, out ImportState? state))
        {
            return state;
        }

        state = new ImportState();
        imports[importId] = state;
        return state;
    }

    private static Dictionary<string, ImportState> CloneImports(IReadOnlyDictionary<string, ImportState> imports) =>
        imports.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value with { Cursor = CloneJsonObject(pair.Value.Cursor) },
            StringComparer.Ordinal);

    private static ImportState RecordError(ImportState state, Exception exception, DateTimeOffset occurredAt) =>
        state with
        {
            LastCheckedAt = occurredAt,
            LastError = new ImportError
            {
                Message = exception.Message,
                ErrorType = exception.GetType().FullName ?? exception.GetType().Name,
                OccurredAt = occurredAt
            }
        };

    private static ImportCheckResult FailedCheck(
        string importId,
        ImportCheckOutcome outcome,
        string? sourceItemId,
        string message,
        Exception? exception = null) =>
        new()
        {
            ImportId = importId,
            Outcome = outcome,
            SourceItemId = sourceItemId,
            Message = message,
            Exception = exception
        };

    private static JsonObject MergeCursor(JsonObject existing, JsonObject updates)
    {
        JsonObject merged = CloneJsonObject(existing);
        foreach (KeyValuePair<string, JsonNode?> update in updates)
        {
            merged[update.Key] = update.Value?.DeepClone();
        }

        return merged;
    }

    private static JsonObject CloneJsonObject(JsonObject value) =>
        value.DeepClone().AsObject();
}
