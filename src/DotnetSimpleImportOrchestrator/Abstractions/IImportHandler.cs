namespace DotnetSimpleImportOrchestrator.Abstractions;

public interface IImportHandler<TConfiguration>
    where TConfiguration : IImportConfiguration
{
    ValueTask<ImportHandlingResult> HandleAsync(
        ImportHandlingContext<TConfiguration> context,
        Stream payload,
        CancellationToken cancellationToken);
}
