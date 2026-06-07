namespace DotnetSimpleImportOrchestrator.Abstractions;

public interface IImportRunner
{
    ValueTask<ImportRunResult> RunOnceAsync(
        IReadOnlyList<IImportDefinition> imports,
        ImportRuntimeState state,
        CancellationToken cancellationToken = default);
}
