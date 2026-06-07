namespace DotnetSimpleImportOrchestrator.Abstractions;

public interface IImportSource
{
    ValueTask<IReadOnlyList<ImportCandidate>> PollAsync(
        ImportSourceContext context,
        CancellationToken cancellationToken);
}
