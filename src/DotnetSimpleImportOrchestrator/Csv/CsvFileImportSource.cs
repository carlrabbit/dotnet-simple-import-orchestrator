using DotnetSimpleImportOrchestrator.Abstractions;

namespace DotnetSimpleImportOrchestrator.Csv;

public sealed class CsvFileImportSource : IImportSource
{
    private readonly CsvFileImportSourceOptions _options;

    public CsvFileImportSource(CsvFileImportSourceOptions options)
    {
        CsvOptionsValidator.Validate(options);
        _options = options;
    }

    public async ValueTask<ImportPollResult> PollAsync(
        ImportPollContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_options.DirectoryPath))
        {
            if (_options.MissingDirectoryBehavior == MissingDirectoryBehavior.TreatAsNoCandidate)
            {
                return ImportPollResult.NoCandidate();
            }

            throw new DirectoryNotFoundException($"CSV import directory '{_options.DirectoryPath}' does not exist.");
        }

        IEnumerable<string> files = Directory.EnumerateFiles(
            _options.DirectoryPath,
            _options.SearchPattern,
            _options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        List<CsvFileMetadata> readyFiles = [];
        foreach (string filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fullPath = NormalizePath(filePath);
            if (CsvCandidateMetadata.IsProcessed(context.State.Cursor, fullPath))
            {
                continue;
            }

            if (await IsReadyAsync(fullPath, cancellationToken))
            {
                readyFiles.Add(CreateMetadata(fullPath));
            }
        }

        CsvFileMetadata? selected = Order(readyFiles).FirstOrDefault();
        if (selected is null)
        {
            return ImportPollResult.NoCandidate();
        }

        ImportCandidate candidate = new()
        {
            SourceItemId = selected.FullPath,
            Metadata = CsvCandidateMetadata.Create(selected, _options.Csv),
            OpenReadAsync = token =>
            {
                Stream stream = File.Open(selected.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return ValueTask.FromResult(stream);
            }
        };

        return ImportPollResult.CandidateResult(
            candidate,
            CsvCandidateMetadata.CreateProcessedCursorUpdate(
                selected.FullPath,
                selected,
                context.TimeProvider.GetUtcNow()));
    }

    private async ValueTask<bool> IsReadyAsync(string fullPath, CancellationToken cancellationToken)
    {
        switch (_options.Readiness.Strategy)
        {
            case FileReadinessStrategy.None:
                return true;
            case FileReadinessStrategy.StableSize:
                return await IsStableAsync(fullPath, cancellationToken);
            case FileReadinessStrategy.ExclusiveRead:
                return CanOpen(fullPath, FileAccess.Read);
            case FileReadinessStrategy.ExclusiveWrite:
                return CanOpen(fullPath, FileAccess.ReadWrite);
            case FileReadinessStrategy.MarkerFile:
                return File.Exists(fullPath + _options.Readiness.MarkerFileExtension);
            default:
                throw new ArgumentOutOfRangeException(nameof(_options), "Unsupported file readiness strategy.");
        }
    }

    private async ValueTask<bool> IsStableAsync(string fullPath, CancellationToken cancellationToken)
    {
        FileInfo before = new(fullPath);
        long length = before.Length;
        DateTime lastWrite = before.LastWriteTimeUtc;

        await Task.Delay(_options.Readiness.StableFor, cancellationToken);

        FileInfo after = new(fullPath);
        return after.Exists &&
            after.Length == length &&
            after.LastWriteTimeUtc == lastWrite;
    }

    private static bool CanOpen(string fullPath, FileAccess access)
    {
        try
        {
            using FileStream _ = File.Open(fullPath, FileMode.Open, access, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private IEnumerable<CsvFileMetadata> Order(IEnumerable<CsvFileMetadata> files)
    {
        return _options.Ordering switch
        {
            FileCandidateOrdering.OldestFirst => files
                .OrderBy(static file => file.LastWriteTimeUtc)
                .ThenBy(static file => file.FullPath, StringComparer.Ordinal),
            FileCandidateOrdering.NewestFirst => files
                .OrderByDescending(static file => file.LastWriteTimeUtc)
                .ThenBy(static file => file.FullPath, StringComparer.Ordinal),
            FileCandidateOrdering.NameAscending => files
                .OrderBy(static file => file.FullPath, StringComparer.Ordinal),
            FileCandidateOrdering.NameDescending => files
                .OrderByDescending(static file => file.FullPath, StringComparer.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(_options), "Unsupported file candidate ordering.")
        };
    }

    private static CsvFileMetadata CreateMetadata(string fullPath)
    {
        FileInfo info = new(fullPath);
        return new CsvFileMetadata
        {
            FullPath = NormalizePath(info.FullName),
            FileName = info.Name,
            Length = info.Length,
            LastWriteTimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)
        };
    }

    private static string NormalizePath(string filePath) =>
        Path.GetFullPath(filePath);
}
