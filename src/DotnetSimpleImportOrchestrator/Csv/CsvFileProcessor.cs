using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace DotnetSimpleImportOrchestrator.Csv;

public sealed class CsvFileProcessor
{
    public async ValueTask<CsvFileProcessingResult> ProcessAsync(
        Stream stream,
        CsvFileProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Options);

        CsvOptionsValidator.Validate(context.Options);

        Encoding encoding = Encoding.GetEncoding(context.Options.EncodingName);
        CultureInfo culture = string.IsNullOrWhiteSpace(context.Options.CultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(context.Options.CultureName);

        List<ParsedRecord> records = [];
        List<CsvUnprocessableContent> unprocessableContent = [];
        HashSet<int> badRows = [];

        using StreamReader reader = new(stream, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        CsvConfiguration configuration = CreateConfiguration(context.Options, culture, unprocessableContent, badRows);
        using CsvParser parser = new(reader, configuration);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool read;
            try
            {
                read = await parser.ReadAsync();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                unprocessableContent.Add(new CsvUnprocessableContent
                {
                    RowNumber = parser.Row > 0 ? parser.Row : null,
                    RawContent = parser.RawRecord ?? string.Empty,
                    Reason = exception.Message,
                    ErrorCode = exception.GetType().Name
                });
                break;
            }

            if (!read)
            {
                break;
            }

            if (badRows.Contains(parser.Row))
            {
                continue;
            }

            string[] record = parser.Record ?? [];
            records.Add(new ParsedRecord(parser.Row, record.Select(value => NormalizeValue(value, context.Options)).ToArray()));
        }

        CsvTable table = BuildTable(records, context.Options);
        return new CsvFileProcessingResult
        {
            Table = table,
            UnprocessableContent = unprocessableContent
        };
    }

    private static CsvConfiguration CreateConfiguration(
        CsvPayloadOptions options,
        CultureInfo culture,
        List<CsvUnprocessableContent> unprocessableContent,
        HashSet<int> badRows)
    {
        CsvConfiguration configuration = new(culture)
        {
            Delimiter = options.Delimiter,
            Quote = options.Quote,
            Escape = options.Escape,
            HasHeaderRecord = options.HasHeaderRecord,
            IgnoreBlankLines = options.IgnoreBlankLines,
            TrimOptions = options.TrimFields ? TrimOptions.Trim : TrimOptions.None,
            BadDataFound = args =>
            {
                if (args.Context.Parser is not null)
                {
                    badRows.Add(args.Context.Parser.Row);
                }

                unprocessableContent.Add(new CsvUnprocessableContent
                {
                    RowNumber = args.Context.Parser?.Row,
                    RawContent = args.RawRecord,
                    Reason = "Malformed CSV content.",
                    ErrorCode = "BadData"
                });
            },
            MissingFieldFound = null,
            HeaderValidated = null,
            ReadingExceptionOccurred = args =>
            {
                unprocessableContent.Add(new CsvUnprocessableContent
                {
                    RowNumber = args.Exception.Context?.Parser?.Row,
                    RawContent = args.Exception.Context?.Parser?.RawRecord ?? string.Empty,
                    Reason = args.Exception.Message,
                    ErrorCode = args.Exception.GetType().Name
                });
                return false;
            }
        };

        if (options.NewLine is not null)
        {
            configuration.NewLine = options.NewLine;
        }

        return configuration;
    }

    private static CsvTable BuildTable(IReadOnlyList<ParsedRecord> records, CsvPayloadOptions options)
    {
        string[] parsedHeaders = [];
        IReadOnlyList<ParsedRecord> dataRecords = records;

        if (options.HasHeaderRecord && records.Count > 0)
        {
            parsedHeaders = records[0].Values;
            dataRecords = records.Skip(1).ToArray();
        }

        int finalColumnCount = Math.Max(
            parsedHeaders.Length,
            dataRecords.Count == 0 ? 0 : dataRecords.Max(static record => record.Values.Length));

        if (!options.HasHeaderRecord)
        {
            parsedHeaders = [];
        }

        string[] headers = NormalizeHeaders(parsedHeaders, finalColumnCount, options);
        List<CsvRow> rows = new(dataRecords.Count);
        foreach (ParsedRecord record in dataRecords)
        {
            string[] values = NormalizeValues(record.Values, finalColumnCount);
            rows.Add(new CsvRow
            {
                RowNumber = record.RowNumber,
                Values = values,
                Fields = headers.Zip(values, static (header, value) => new KeyValuePair<string, string>(header, value))
                    .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)
            });
        }

        return new CsvTable
        {
            Headers = headers,
            Rows = rows
        };
    }

    private static string[] NormalizeHeaders(string[] parsedHeaders, int finalColumnCount, CsvPayloadOptions options)
    {
        string[] headers = new string[finalColumnCount];
        HashSet<string> used = new(StringComparer.Ordinal);
        for (int i = 0; i < finalColumnCount; i++)
        {
            string? parsed = i < parsedHeaders.Length ? NormalizeValue(parsedHeaders[i], options) : null;
            string generated = $"Column{i + 1}";
            string header = string.IsNullOrEmpty(parsed) || !used.Add(parsed)
                ? generated
                : parsed;

            if (header == generated)
            {
                used.Add(header);
            }

            headers[i] = header;
        }

        return headers;
    }

    private static string[] NormalizeValues(string[] values, int finalColumnCount)
    {
        string[] normalized = new string[finalColumnCount];
        for (int i = 0; i < finalColumnCount; i++)
        {
            normalized[i] = i < values.Length ? values[i] : string.Empty;
        }

        return normalized;
    }

    private static string NormalizeValue(string? value, CsvPayloadOptions options)
    {
        string normalized = value ?? string.Empty;
        return options.TrimFields ? normalized.Trim() : normalized;
    }

    private sealed record ParsedRecord(int RowNumber, string[] Values);
}
