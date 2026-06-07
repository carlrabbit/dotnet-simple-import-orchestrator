namespace DotnetSimpleImportOrchestrator;

public interface IImportConfiguration
{
}

public interface IImportDefinition
{
    string Id { get; }

    int Priority { get; }

    PollingOptions Polling { get; }

    IImportConfiguration Configuration { get; }
}

public sealed record ImportDefinition<TConfiguration> : IImportDefinition
    where TConfiguration : IImportConfiguration
{
    public required string Id { get; init; }

    public int Priority { get; init; } = ImportPriorities.Normal;

    public required PollingOptions Polling { get; init; }

    public required TConfiguration Configuration { get; init; }

    IImportConfiguration IImportDefinition.Configuration => Configuration;
}

public static class ImportPriorities
{
    public const int Highest = 0;
    public const int High = 100;
    public const int Normal = 500;
    public const int Low = 900;
}

public sealed record PollingOptions
{
    public required TimeSpan Interval { get; init; }
}
