using System.Text.Json.Nodes;
using DotnetSimpleImportOrchestrator.Abstractions;

namespace DotnetSimpleImportOrchestrator.FileSystem;

public sealed class DirectoryPollingImportSource : IImportSource
{
    public ValueTask<IReadOnlyList<ImportCandidate>> PollAsync(
        ImportSourceContext context,
        CancellationToken cancellationToken)
    {
        string? directory = context.Definition.Source["directory"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return ValueTask.FromResult<IReadOnlyList<ImportCandidate>>([]);
        }

        string searchPattern = context.Definition.Source["searchPattern"]?.GetValue<string>() ?? "*";
        string[] files = Directory.GetFiles(directory, searchPattern);
        Array.Sort(files, StringComparer.Ordinal);

        List<ImportCandidate> candidates = new(files.Length);
        foreach (string filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            candidates.Add(new ImportCandidate
            {
                SourceItemId = filePath,
                Format = context.Definition.Format,
                Metadata = new JsonObject { ["path"] = filePath },
                OpenReadAsync = _ =>
                {
                    Stream stream = File.OpenRead(filePath);
                    return ValueTask.FromResult(stream);
                }
            });
        }

        return ValueTask.FromResult<IReadOnlyList<ImportCandidate>>(candidates);
    }
}
