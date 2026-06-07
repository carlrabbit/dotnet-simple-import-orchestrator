using DotnetSimpleImportOrchestrator.Abstractions;

namespace DotnetSimpleImportOrchestrator.Csv;

public interface ICsvFileImportSourceOptionsMapper<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    CsvFileImportSourceOptions Map(
        ImportDefinition<TConfiguration> definition,
        ImportSourceFactoryContext<TConfiguration> context);
}

public sealed class CsvFileImportSourceFactory<TConfiguration> : IImportSourceFactory<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    private readonly ICsvFileImportSourceOptionsMapper<TConfiguration> _mapper;

    public CsvFileImportSourceFactory(ICsvFileImportSourceOptionsMapper<TConfiguration> mapper)
    {
        _mapper = mapper;
    }

    public ValueTask<IImportSource> CreateAsync(
        ImportDefinition<TConfiguration> definition,
        ImportSourceFactoryContext<TConfiguration> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        CsvFileImportSourceOptions options = _mapper.Map(definition, context)
            ?? throw new InvalidOperationException("CSV file import source options mapper returned null.");
        CsvOptionsValidator.Validate(options);

        return ValueTask.FromResult<IImportSource>(new CsvFileImportSource(options));
    }
}
