using System.Text.Json;
using System.Text.Json.Nodes;

namespace DotnetSimpleImportOrchestrator.Csv;

public static class CsvCandidateMetadata
{
    private const string RootName = "csvFileSource";
    private const string FileName = "file";
    private const string CsvName = "csv";
    private const string ProcessedFilesName = "processedFiles";

    public static JsonObject Create(CsvFileMetadata file, CsvPayloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(options);

        return new JsonObject
        {
            [RootName] = new JsonObject
            {
                [FileName] = JsonSerializer.SerializeToNode(file),
                [CsvName] = JsonSerializer.SerializeToNode(options)
            }
        };
    }

    public static CsvFileMetadata GetFileMetadata(JsonObject metadata)
    {
        if (TryGetFileMetadata(metadata, out CsvFileMetadata? file))
        {
            return file!;
        }

        throw new ArgumentException("CSV file metadata is missing or invalid.", nameof(metadata));
    }

    public static CsvPayloadOptions GetPayloadOptions(JsonObject metadata)
    {
        if (TryGetPayloadOptions(metadata, out CsvPayloadOptions? options))
        {
            return options!;
        }

        throw new ArgumentException("CSV payload options metadata is missing or invalid.", nameof(metadata));
    }

    public static bool TryGetFileMetadata(JsonObject metadata, out CsvFileMetadata? file)
    {
        file = null;
        JsonNode? node = metadata[RootName]?[FileName];
        if (node is null)
        {
            return false;
        }

        file = node.Deserialize<CsvFileMetadata>();
        return file is not null;
    }

    public static bool TryGetPayloadOptions(JsonObject metadata, out CsvPayloadOptions? options)
    {
        options = null;
        JsonNode? node = metadata[RootName]?[CsvName];
        if (node is null)
        {
            return false;
        }

        options = node.Deserialize<CsvPayloadOptions>();
        return options is not null;
    }

    internal static bool IsProcessed(JsonObject cursor, string sourceItemId)
    {
        return cursor[RootName]?[ProcessedFilesName]?[sourceItemId] is not null;
    }

    public static JsonObject CreateProcessedCursorUpdate(
        string sourceItemId,
        CsvFileMetadata file,
        DateTimeOffset processedAt)
    {
        return new JsonObject
        {
            [RootName] = new JsonObject
            {
                [ProcessedFilesName] = new JsonObject
                {
                    [sourceItemId] = new JsonObject
                    {
                        ["length"] = file.Length,
                        ["lastWriteTimeUtc"] = file.LastWriteTimeUtc,
                        ["processedAt"] = processedAt
                    }
                }
            }
        };
    }
}
