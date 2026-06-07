namespace DotnetSimpleImportOrchestrator;

public sealed record ImportRunResult
{
    public required ImportRuntimeState State { get; init; }

    public IReadOnlyList<ImportAttemptResult> Attempts { get; init; } = [];
}

public sealed record ImportAttemptResult
{
    public required string ImportId { get; init; }

    public string? SourceItemId { get; init; }

    public bool Succeeded { get; init; }

    public bool Skipped { get; init; }

    public string? Message { get; init; }

    public Exception? Exception { get; init; }
}
