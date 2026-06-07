using System.Text;
using System.Text.Json.Nodes;
using DotnetSimpleImportOrchestrator;
using DotnetSimpleImportOrchestrator.Abstractions;
using DotnetSimpleImportOrchestrator.Testing;

namespace DotnetSimpleImportOrchestrator.Tests;

public sealed class ImportRunnerTests
{
    [Test]
    public async Task SuccessfulImportRunPassesStreamToHandlerAndUpdatesState()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(filePath, "id,name\n1,Ada\n", Encoding.UTF8);

        try
        {
            CapturingHandler handler = new();
            ImportRunner runner = new(
                new Dictionary<string, IImportSource>
                {
                    ["files"] = new FileBackedImportSource(filePath)
                },
                new Dictionary<string, IImportHandler>
                {
                    ["capture"] = handler
                });

            ImportDefinition definition = new()
            {
                Id = "orders",
                SourceName = "files",
                HandlerName = "capture",
                Format = ImportPayloadFormat.Csv
            };

            ImportRunResult result = await runner.RunDueImportsAsync([definition], new ImportRuntimeState());

            await Assert.That(handler.Payloads).Count().IsEqualTo(1);
            await Assert.That(handler.Payloads[0]).Contains("Ada");
            await Assert.That(result.Attempts).Count().IsEqualTo(1);
            await Assert.That(result.Attempts[0].Succeeded).IsTrue();
            await Assert.That(result.State.Imports["orders"].LastSuccessfulImportAt).IsNotNull();
            await Assert.That(result.State.Imports["orders"].Cursor["lastSourceItemId"]!.GetValue<string>())
                .IsEqualTo(filePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task DisabledImportDoesNotExecute()
    {
        CapturingHandler handler = new();
        ImportRunner runner = new(
            new Dictionary<string, IImportSource>
            {
                ["files"] = new FileBackedImportSource("unused.csv")
            },
            new Dictionary<string, IImportHandler>
            {
                ["capture"] = handler
            });

        ImportDefinition definition = new()
        {
            Id = "orders",
            SourceName = "files",
            HandlerName = "capture",
            Format = ImportPayloadFormat.Csv,
            Enabled = false
        };

        ImportRunResult result = await runner.RunDueImportsAsync([definition], new ImportRuntimeState());

        await Assert.That(handler.Payloads).IsEmpty();
        await Assert.That(result.Attempts).Count().IsEqualTo(1);
        await Assert.That(result.Attempts[0].Skipped).IsTrue();
        await Assert.That(result.State.Imports.ContainsKey("orders")).IsFalse();
    }

    private sealed class CapturingHandler : IImportHandler
    {
        public List<string> Payloads { get; } = [];

        public async ValueTask<ImportHandlingResult> HandleAsync(
            ImportHandlingContext context,
            Stream payload,
            CancellationToken cancellationToken)
        {
            using StreamReader reader = new(payload, Encoding.UTF8);
            Payloads.Add(await reader.ReadToEndAsync(cancellationToken));

            return ImportHandlingResult.Success(new JsonObject
            {
                ["lastSourceItemId"] = context.Candidate.SourceItemId
            });
        }
    }
}
