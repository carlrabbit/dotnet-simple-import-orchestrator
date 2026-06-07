namespace DotnetSimpleImportOrchestrator.Csv;

public sealed record CsvFileImportSourceOptions
{
    public required string DirectoryPath { get; init; }

    public required string SearchPattern { get; init; }

    public bool Recursive { get; init; }

    public FileCandidateOrdering Ordering { get; init; } = FileCandidateOrdering.OldestFirst;

    public MissingDirectoryBehavior MissingDirectoryBehavior { get; init; } =
        MissingDirectoryBehavior.TreatAsNoCandidate;

    public required FileReadinessOptions Readiness { get; init; }

    public required CsvPayloadOptions Csv { get; init; }
}

public enum MissingDirectoryBehavior
{
    TreatAsNoCandidate,
    Fail
}

public enum FileCandidateOrdering
{
    OldestFirst,
    NewestFirst,
    NameAscending,
    NameDescending
}

public sealed record FileReadinessOptions
{
    public FileReadinessStrategy Strategy { get; init; } = FileReadinessStrategy.StableSize;

    public TimeSpan StableFor { get; init; } = TimeSpan.FromSeconds(5);

    public string? MarkerFileExtension { get; init; }
}

public enum FileReadinessStrategy
{
    None,
    StableSize,
    ExclusiveRead,
    ExclusiveWrite,
    MarkerFile
}

public sealed record CsvPayloadOptions
{
    public required string EncodingName { get; init; }

    public string CultureName { get; init; } = "";

    public string Delimiter { get; init; } = ",";

    public char Quote { get; init; } = '"';

    public char Escape { get; init; } = '"';

    public bool HasHeaderRecord { get; init; } = true;

    public bool TrimFields { get; init; }

    public bool IgnoreBlankLines { get; init; } = true;

    public string? NewLine { get; init; }
}

public static class CsvOptionsValidator
{
    public static void Validate(CsvFileImportSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
        {
            throw new ArgumentException("CSV source directory path must be non-empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.SearchPattern))
        {
            throw new ArgumentException("CSV source search pattern must be non-empty.", nameof(options));
        }

        if (!Enum.IsDefined(options.Ordering))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Ordering, "CSV source ordering is invalid.");
        }

        if (!Enum.IsDefined(options.MissingDirectoryBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MissingDirectoryBehavior,
                "CSV source missing-directory behavior is invalid.");
        }

        Validate(options.Readiness);
        Validate(options.Csv);
    }

    public static void Validate(FileReadinessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Enum.IsDefined(options.Strategy))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Strategy, "File readiness strategy is invalid.");
        }

        if (options.Strategy == FileReadinessStrategy.StableSize && options.StableFor <= TimeSpan.Zero)
        {
            throw new ArgumentException("Stable-size readiness interval must be positive.", nameof(options));
        }

        if (options.Strategy == FileReadinessStrategy.MarkerFile &&
            string.IsNullOrWhiteSpace(options.MarkerFileExtension))
        {
            throw new ArgumentException("Marker-file readiness requires a marker file extension.", nameof(options));
        }
    }

    public static void Validate(CsvPayloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.EncodingName))
        {
            throw new ArgumentException("CSV encoding name must be non-empty.", nameof(options));
        }

        try
        {
            _ = System.Text.Encoding.GetEncoding(options.EncodingName);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new ArgumentException($"CSV encoding '{options.EncodingName}' is not supported.", nameof(options), exception);
        }

        if (!string.IsNullOrWhiteSpace(options.CultureName))
        {
            try
            {
                _ = System.Globalization.CultureInfo.GetCultureInfo(options.CultureName);
            }
            catch (System.Globalization.CultureNotFoundException exception)
            {
                throw new ArgumentException($"CSV culture '{options.CultureName}' is not supported.", nameof(options), exception);
            }
        }

        if (string.IsNullOrEmpty(options.Delimiter))
        {
            throw new ArgumentException("CSV delimiter must be non-empty.", nameof(options));
        }
    }
}
