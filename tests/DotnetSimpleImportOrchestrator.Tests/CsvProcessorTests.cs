using System.Text;
using DotnetSimpleImportOrchestrator.Csv;

namespace DotnetSimpleImportOrchestrator.Tests;

public sealed class CsvProcessorTests
{
    [Test]
    public async Task ParsesHeadersAndRowsWithConfiguredDelimiter()
    {
        CsvFileProcessingResult result = await ProcessAsync("Name;Age\nAda;42\n", Options() with { Delimiter = ";" });

        await Assert.That(result.Table.Headers).IsEquivalentTo(["Name", "Age"]);
        await Assert.That(result.Table.Rows[0].Fields["Name"]).IsEqualTo("Ada");
        await Assert.That(result.Table.Rows[0].Fields["Age"]).IsEqualTo("42");
    }

    [Test]
    public async Task GeneratesHeadersWhenNoHeaderRowIsConfigured()
    {
        CsvFileProcessingResult result = await ProcessAsync("Ada,42\nGrace,43\n", Options() with { HasHeaderRecord = false });

        await Assert.That(result.Table.Headers).IsEquivalentTo(["Column1", "Column2"]);
        await Assert.That(result.Table.Rows[0].RowNumber).IsEqualTo(1);
        await Assert.That(result.Table.Rows[0].Fields["Column1"]).IsEqualTo("Ada");
    }

    [Test]
    public async Task PadsShortRowsWithEmptyStrings()
    {
        CsvFileProcessingResult result = await ProcessAsync("Name,Age,Country\nAda,42\n", Options());

        await Assert.That(result.Table.Rows[0].Values).IsEquivalentTo(["Ada", "42", ""]);
        await Assert.That(result.Table.Rows[0].Fields["Country"]).IsEqualTo("");
    }

    [Test]
    public async Task AddsGeneratedHeadersForRowsWiderThanHeader()
    {
        CsvFileProcessingResult result = await ProcessAsync("Name,Age\nAda,42,DE\n", Options());

        await Assert.That(result.Table.Headers).IsEquivalentTo(["Name", "Age", "Column3"]);
        await Assert.That(result.Table.Rows[0].Fields["Column3"]).IsEqualTo("DE");
    }

    [Test]
    public async Task NormalizesEmptyAndDuplicateHeaders()
    {
        CsvFileProcessingResult result = await ProcessAsync("Name,Name,,Country\nAda,42,,DE\n", Options());

        await Assert.That(result.Table.Headers).IsEquivalentTo(["Name", "Column2", "Column3", "Country"]);
    }

    [Test]
    public async Task PreservesPhysicalRowNumbers()
    {
        CsvFileProcessingResult result = await ProcessAsync("Name\nAda\nGrace\n", Options());

        await Assert.That(result.Table.Rows[0].RowNumber).IsEqualTo(2);
        await Assert.That(result.Table.Rows[1].RowNumber).IsEqualTo(3);
    }

    [Test]
    public async Task RespectsTrimBehavior()
    {
        CsvFileProcessingResult trimmed = await ProcessAsync(" Name \n Ada \n", Options() with { TrimFields = true });
        CsvFileProcessingResult untrimmed = await ProcessAsync(" Name \n Ada \n", Options() with { TrimFields = false });

        await Assert.That(trimmed.Table.Headers[0]).IsEqualTo("Name");
        await Assert.That(trimmed.Table.Rows[0].Fields["Name"]).IsEqualTo("Ada");
        await Assert.That(untrimmed.Table.Headers[0]).IsEqualTo(" Name ");
    }

    [Test]
    public async Task RespectsBlankLineBehavior()
    {
        CsvFileProcessingResult ignored = await ProcessAsync("Name\n\nAda\n", Options() with { IgnoreBlankLines = true });
        CsvFileProcessingResult kept = await ProcessAsync("Name\n\nAda\n", Options() with { IgnoreBlankLines = false });

        await Assert.That(ignored.Table.Rows).Count().IsEqualTo(1);
        await Assert.That(kept.Table.Rows).Count().IsEqualTo(2);
    }

    [Test]
    public async Task RespectsUtf8Encoding()
    {
        CsvFileProcessingResult result = await ProcessAsync("Name\nMüller\n", Options() with { EncodingName = "utf-8" });

        await Assert.That(result.Table.Rows[0].Fields["Name"]).IsEqualTo("Müller");
    }

    [Test]
    public async Task CapturesMalformedContentWithoutThrowing()
    {
        CsvFileProcessingResult result = await ProcessAsync("Name\n\"unterminated\n", Options());

        await Assert.That(result.HasUnprocessableContent).IsTrue();
        await Assert.That(result.UnprocessableContent[0].RawContent).IsNotEmpty();
    }

    [Test]
    public async Task FieldsMapNormalizedHeadersToNormalizedValues()
    {
        CsvFileProcessingResult result = await ProcessAsync(" Name ,Age\n Ada , 42\n", Options() with { TrimFields = true });

        await Assert.That(result.Table.Rows[0].Fields["Name"]).IsEqualTo("Ada");
        await Assert.That(result.Table.Rows[0].Fields["Age"]).IsEqualTo("42");
    }

    private static async ValueTask<CsvFileProcessingResult> ProcessAsync(string contents, CsvPayloadOptions options)
    {
        await using MemoryStream stream = new(Encoding.UTF8.GetBytes(contents));
        return await new CsvFileProcessor().ProcessAsync(
            stream,
            new CsvFileProcessingContext { Options = options },
            CancellationToken.None);
    }

    private static CsvPayloadOptions Options() => new()
    {
        EncodingName = "utf-8"
    };
}
