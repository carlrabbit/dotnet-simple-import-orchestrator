using System.Text.Json.Nodes;

namespace DotnetSimpleImportOrchestrator;

public sealed record ImportRuntimeState
{
    public Dictionary<string, ImportState> Imports { get; init; } = [];
}

public sealed record ImportState
{
    public DateTimeOffset? LastCheckedAt { get; init; }

    public DateTimeOffset? LastSuccessfulImportAt { get; init; }

    public ImportError? LastError { get; init; }

    public JsonObject Cursor { get; init; } = [];
}

public sealed record ImportError
{
    public required string Message { get; init; }

    public required string ErrorType { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
