namespace DotnetSimpleImportOrchestrator.Abstractions;

public interface IImportSource
{
    ValueTask<ImportPollResult> PollAsync(
        ImportPollContext context,
        CancellationToken cancellationToken);
}
