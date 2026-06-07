using System.Text.Json.Nodes;

namespace DotnetSimpleImportOrchestrator.Csv;

public sealed record CsvFileMetadata
{
    public required string FullPath { get; init; }

    public required string FileName { get; init; }

    public required long Length { get; init; }

    public required DateTimeOffset LastWriteTimeUtc { get; init; }
}

public sealed record CsvFileProcessingContext
{
    public required CsvPayloadOptions Options { get; init; }

    public JsonObject Metadata { get; init; } = [];
}

public sealed record CsvFileProcessingResult
{
    public required CsvTable Table { get; init; }

    public IReadOnlyList<CsvUnprocessableContent> UnprocessableContent { get; init; } = [];

    public bool HasUnprocessableContent => UnprocessableContent.Count > 0;
}

public sealed record CsvTable
{
    public IReadOnlyList<string> Headers { get; init; } = [];

    public IReadOnlyList<CsvRow> Rows { get; init; } = [];
}

public sealed record CsvRow
{
    public required int RowNumber { get; init; }

    public IReadOnlyList<string> Values { get; init; } = [];

    public IReadOnlyDictionary<string, string> Fields { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record CsvUnprocessableContent
{
    public int? RowNumber { get; init; }

    public required string RawContent { get; init; }

    public required string Reason { get; init; }

    public string? ErrorCode { get; init; }
}
