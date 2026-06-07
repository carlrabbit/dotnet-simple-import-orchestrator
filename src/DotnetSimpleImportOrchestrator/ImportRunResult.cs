namespace DotnetSimpleImportOrchestrator;

public sealed record ImportRunResult
{
    public required ImportRuntimeState State { get; init; }

    public required bool SuccessfulImportPerformed { get; init; }

    public string? SuccessfulImportId { get; init; }

    public IReadOnlyList<ImportCheckResult> Checks { get; init; } = [];
}

public enum ImportCheckOutcome
{
    NotDue,
    NoCandidate,
    SourceFailed,
    HandlerFailed,
    Imported,
    Skipped
}

public sealed record ImportCheckResult
{
    public required string ImportId { get; init; }

    public required ImportCheckOutcome Outcome { get; init; }

    public string? SourceItemId { get; init; }

    public string? Message { get; init; }

    public Exception? Exception { get; init; }
}
