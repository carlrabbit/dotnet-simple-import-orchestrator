using System.Text.Json.Nodes;

namespace DotnetSimpleImportOrchestrator;

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

public sealed record PollingOptions
{
    public TimeSpan? Interval { get; init; }
}
