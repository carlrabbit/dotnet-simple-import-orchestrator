using System.Text.Json.Nodes;
using DotnetSimpleImportOrchestrator.Abstractions;

namespace DotnetSimpleImportOrchestrator.Testing;

public sealed class FileBackedImportSource : IImportSource
{
    private readonly IReadOnlyList<string> _filePaths;

    public FileBackedImportSource(IReadOnlyList<string> filePaths)
    {
        _filePaths = filePaths;
    }

    public FileBackedImportSource(params string[] filePaths)
        : this((IReadOnlyList<string>)filePaths)
    {
    }

    public ValueTask<IReadOnlyList<ImportCandidate>> PollAsync(
        ImportSourceContext context,
        CancellationToken cancellationToken)
    {
        List<ImportCandidate> candidates = new(_filePaths.Count);
        foreach (string filePath in _filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            candidates.Add(new ImportCandidate
            {
                SourceItemId = filePath,
                Format = context.Definition.Format,
                Metadata = new JsonObject { ["path"] = filePath },
                OpenReadAsync = token =>
                {
                    Stream stream = File.OpenRead(filePath);
                    return ValueTask.FromResult(stream);
                }
            });
        }

        return ValueTask.FromResult<IReadOnlyList<ImportCandidate>>(candidates);
    }
}
