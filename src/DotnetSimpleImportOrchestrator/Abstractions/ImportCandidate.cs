using System.Text.Json.Nodes;

namespace DotnetSimpleImportOrchestrator.Abstractions;

public sealed record ImportCandidate
{
    public required string SourceItemId { get; init; }

    public required Func<CancellationToken, ValueTask<Stream>> OpenReadAsync { get; init; }

    public JsonObject Metadata { get; init; } = [];
}
