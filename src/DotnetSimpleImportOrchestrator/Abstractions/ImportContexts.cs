namespace DotnetSimpleImportOrchestrator.Abstractions;

public sealed record ImportSourceContext
{
    public required ImportDefinition Definition { get; init; }

    public required ImportState State { get; init; }

    public required DateTimeOffset StartedAt { get; init; }
}

public sealed record ImportHandlingContext
{
    public required ImportDefinition Definition { get; init; }

    public required ImportCandidate Candidate { get; init; }

    public required ImportState State { get; init; }

    public required DateTimeOffset StartedAt { get; init; }
}
