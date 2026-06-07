using System.Text.Json.Nodes;

namespace DotnetSimpleImportOrchestrator.Abstractions;

public interface IImportSourceFactory<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    ValueTask<IImportSource> CreateAsync(
        ImportDefinition<TConfiguration> definition,
        ImportSourceFactoryContext<TConfiguration> context,
        CancellationToken cancellationToken);
}

public sealed record ImportSourceFactoryContext<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    public required ImportDefinition<TConfiguration> Definition { get; init; }

    public required ImportState State { get; init; }

    public required TimeProvider TimeProvider { get; init; }
}

public sealed record ImportPollContext
{
    public required string ImportId { get; init; }

    public required ImportState State { get; init; }

    public required TimeProvider TimeProvider { get; init; }
}

public sealed record ImportHandlingContext<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    public required ImportDefinition<TConfiguration> Definition { get; init; }

    public required string SourceItemId { get; init; }

    public required ImportState State { get; init; }

    public JsonObject Metadata { get; init; } = [];
}
