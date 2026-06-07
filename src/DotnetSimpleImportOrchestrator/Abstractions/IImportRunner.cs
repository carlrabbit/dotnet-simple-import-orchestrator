namespace DotnetSimpleImportOrchestrator.Abstractions;

public interface IImportRunner
{
    ValueTask<ImportRunResult> RunDueImportsAsync(
        IReadOnlyList<ImportDefinition> definitions,
        ImportRuntimeState state,
        CancellationToken cancellationToken = default);
}
