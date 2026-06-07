using System.Text.Json.Nodes;
using DotnetSimpleImportOrchestrator.Abstractions;

namespace DotnetSimpleImportOrchestrator;

public sealed class ImportRunner : IImportRunner
{
    private readonly IReadOnlyDictionary<string, IImportSource> _sources;
    private readonly IReadOnlyDictionary<string, IImportHandler> _handlers;
    private readonly TimeProvider _timeProvider;

    public ImportRunner(
        IReadOnlyDictionary<string, IImportSource> sources,
        IReadOnlyDictionary<string, IImportHandler> handlers,
        TimeProvider? timeProvider = null)
    {
        _sources = sources;
        _handlers = handlers;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<ImportRunResult> RunDueImportsAsync(
        IReadOnlyList<ImportDefinition> definitions,
        ImportRuntimeState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(state);

        Dictionary<string, ImportState> imports = CloneImports(state.Imports);
        List<ImportAttemptResult> attempts = [];

        foreach (ImportDefinition definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!definition.Enabled)
            {
                attempts.Add(new ImportAttemptResult
                {
                    ImportId = definition.Id,
                    Skipped = true,
                    Message = "Import is disabled."
                });
                continue;
            }

            ImportState currentState = GetState(imports, definition.Id);
            DateTimeOffset startedAt = _timeProvider.GetUtcNow();
            if (!IsDue(definition, currentState, startedAt))
            {
                attempts.Add(new ImportAttemptResult
                {
                    ImportId = definition.Id,
                    Skipped = true,
                    Message = "Import is not due."
                });
                continue;
            }

            currentState = currentState with { LastPollStartedAt = startedAt };
            imports[definition.Id] = currentState;

            if (!_sources.TryGetValue(definition.SourceName, out IImportSource? source))
            {
                currentState = RecordError(currentState, new InvalidOperationException(
                    $"No import source is registered for '{definition.SourceName}'."), startedAt);
                imports[definition.Id] = currentState with { LastPollCompletedAt = startedAt };
                attempts.Add(FailedAttempt(definition.Id, null, currentState.LastError!.Message));
                continue;
            }

            if (!_handlers.TryGetValue(definition.HandlerName, out IImportHandler? handler))
            {
                currentState = RecordError(currentState, new InvalidOperationException(
                    $"No import handler is registered for '{definition.HandlerName}'."), startedAt);
                imports[definition.Id] = currentState with { LastPollCompletedAt = startedAt };
                attempts.Add(FailedAttempt(definition.Id, null, currentState.LastError!.Message));
                continue;
            }

            IReadOnlyList<ImportCandidate> candidates;
            try
            {
                candidates = await source.PollAsync(new ImportSourceContext
                {
                    Definition = definition,
                    State = currentState,
                    StartedAt = startedAt
                }, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                currentState = RecordError(currentState, exception, startedAt);
                imports[definition.Id] = currentState with { LastPollCompletedAt = startedAt };
                attempts.Add(FailedAttempt(definition.Id, null, exception.Message, exception));
                continue;
            }

            foreach (ImportCandidate candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using Stream payload = await candidate.OpenReadAsync(cancellationToken);
                    ImportHandlingResult handlingResult = await handler.HandleAsync(new ImportHandlingContext
                    {
                        Definition = definition,
                        Candidate = candidate,
                        State = currentState,
                        StartedAt = startedAt
                    }, payload, cancellationToken);

                    if (!handlingResult.Succeeded)
                    {
                        string message = handlingResult.FailureMessage ?? "Import handler reported failure.";
                        currentState = currentState with
                        {
                            LastError = new ImportError
                            {
                                Message = message,
                                ErrorType = "ImportHandlingFailure",
                                OccurredAt = _timeProvider.GetUtcNow()
                            }
                        };
                        imports[definition.Id] = currentState;
                        attempts.Add(FailedAttempt(definition.Id, candidate.SourceItemId, message));
                        continue;
                    }

                    currentState = currentState with
                    {
                        LastSuccessfulImportAt = _timeProvider.GetUtcNow(),
                        LastError = null,
                        Cursor = MergeCursor(currentState.Cursor, handlingResult.Cursor)
                    };
                    imports[definition.Id] = currentState;
                    attempts.Add(new ImportAttemptResult
                    {
                        ImportId = definition.Id,
                        SourceItemId = candidate.SourceItemId,
                        Succeeded = true
                    });
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    currentState = RecordError(currentState, exception, _timeProvider.GetUtcNow());
                    imports[definition.Id] = currentState;
                    attempts.Add(FailedAttempt(definition.Id, candidate.SourceItemId, exception.Message, exception));
                }
            }

            imports[definition.Id] = currentState with { LastPollCompletedAt = _timeProvider.GetUtcNow() };
        }

        return new ImportRunResult
        {
            State = new ImportRuntimeState { Imports = imports },
            Attempts = attempts
        };
    }

    private static bool IsDue(ImportDefinition definition, ImportState state, DateTimeOffset now)
    {
        if (definition.Polling.Interval is null)
        {
            return true;
        }

        if (state.LastPollCompletedAt is null)
        {
            return true;
        }

        return state.LastPollCompletedAt.Value + definition.Polling.Interval <= now;
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
            static pair => pair.Value with { Cursor = CloneJsonObject(pair.Value.Cursor) });

    private static ImportState RecordError(ImportState state, Exception exception, DateTimeOffset occurredAt) =>
        state with
        {
            LastError = new ImportError
            {
                Message = exception.Message,
                ErrorType = exception.GetType().FullName ?? exception.GetType().Name,
                OccurredAt = occurredAt
            }
        };

    private static ImportAttemptResult FailedAttempt(
        string importId,
        string? sourceItemId,
        string message,
        Exception? exception = null) =>
        new()
        {
            ImportId = importId,
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
