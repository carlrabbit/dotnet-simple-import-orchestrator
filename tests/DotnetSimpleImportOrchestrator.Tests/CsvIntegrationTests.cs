using DotnetSimpleImportOrchestrator;
using DotnetSimpleImportOrchestrator.Abstractions;
using DotnetSimpleImportOrchestrator.Csv;

namespace DotnetSimpleImportOrchestrator.Tests;

public sealed class CsvIntegrationTests
{
    [Test]
    public async Task RunnerCommitsCsvProcessedFileCursorAfterHandlerSuccess()
    {
        string directory = CsvSourceTests.CreateTempDirectory();
        try
        {
            string file = CsvSourceTests.WriteFile(directory, "orders.csv", "Name\nAda\n");
            CsvHandler handler = new();
            ImportDefinition<CsvConfiguration> definition = new()
            {
                Id = "csv",
                Polling = new PollingOptions { Interval = TimeSpan.FromMinutes(1) },
                Configuration = new CsvConfiguration(directory)
            };
            ImportRunner runner = new(
                new Dictionary<string, ImportSourceFactoryRegistration>
                {
                    ["csv"] = ImportSourceFactoryRegistration.Create(
                        new CsvFileImportSourceFactory<CsvConfiguration>(new Mapper()))
                },
                new Dictionary<string, ImportHandlerRegistration>
                {
                    ["csv"] = ImportHandlerRegistration.Create(handler)
                });

            ImportRunResult result = await runner.RunOnceAsync([definition], new ImportRuntimeState());

            await Assert.That(result.SuccessfulImportPerformed).IsTrue();
            await Assert.That(handler.Names).IsEquivalentTo(["Ada"]);
            await Assert.That(result.State.Imports["csv"].Cursor["csvFileSource"]!["processedFiles"]![Path.GetFullPath(file)])
                .IsNotNull();
        }
        finally
        {
            CsvSourceTests.DeleteDirectory(directory);
        }
    }

    private sealed record CsvConfiguration(string Directory) : IImportConfiguration;

    private sealed class Mapper : ICsvFileImportSourceOptionsMapper<CsvConfiguration>
    {
        public CsvFileImportSourceOptions Map(
            ImportDefinition<CsvConfiguration> definition,
            ImportSourceFactoryContext<CsvConfiguration> context) =>
            CsvSourceTests.DefaultOptions(definition.Configuration.Directory);
    }

    private sealed class CsvHandler : IImportHandler<CsvConfiguration>
    {
        public List<string> Names { get; } = [];

        public async ValueTask<ImportHandlingResult> HandleAsync(
            ImportHandlingContext<CsvConfiguration> context,
            Stream payload,
            CancellationToken cancellationToken)
        {
            CsvFileProcessingResult result = await new CsvFileProcessor().ProcessAsync(
                payload,
                new CsvFileProcessingContext
                {
                    Options = CsvCandidateMetadata.GetPayloadOptions(context.Metadata),
                    Metadata = context.Metadata
                },
                cancellationToken);

            Names.AddRange(result.Table.Rows.Select(row => row.Fields["Name"]));
            return ImportHandlingResult.Success();
        }
    }
}
