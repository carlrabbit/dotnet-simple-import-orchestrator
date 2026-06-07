namespace DotnetSimpleImportOrchestrator.Abstractions;

public interface IImportHandler
{
    ValueTask<ImportHandlingResult> HandleAsync(
        ImportHandlingContext context,
        Stream payload,
        CancellationToken cancellationToken);
}
