using System.Text.Json;
using System.Text.Json.Nodes;
using DotnetSimpleImportOrchestrator;

namespace DotnetSimpleImportOrchestrator.Tests;

public sealed class ImportSerializationTests
{
    [Test]
    public async Task ImportDefinitionJsonRoundTripPreservesKeyFields()
    {
        ImportDefinition definition = new()
        {
            Id = "orders",
            SourceName = "files",
            HandlerName = "orders-handler",
            Format = ImportPayloadFormat.Csv,
            Polling = new PollingOptions { Interval = TimeSpan.FromMinutes(5) },
            Source = new JsonObject
            {
                ["directory"] = "/imports/orders",
                ["searchPattern"] = "*.csv"
            }
        };

        string json = JsonSerializer.Serialize(definition);
        ImportDefinition? roundTripped = JsonSerializer.Deserialize<ImportDefinition>(json);

        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped!.Id).IsEqualTo("orders");
        await Assert.That(roundTripped.SourceName).IsEqualTo("files");
        await Assert.That(roundTripped.HandlerName).IsEqualTo("orders-handler");
        await Assert.That(roundTripped.Format).IsEqualTo(ImportPayloadFormat.Csv);
        await Assert.That(roundTripped.Polling.Interval).IsEqualTo(TimeSpan.FromMinutes(5));
        await Assert.That(roundTripped.Source["searchPattern"]!.GetValue<string>()).IsEqualTo("*.csv");
    }

    [Test]
    public async Task ImportRuntimeStateJsonRoundTripPreservesCursorJson()
    {
        ImportRuntimeState state = new()
        {
            Imports =
            {
                ["orders"] = new ImportState
                {
                    Cursor = new JsonObject
                    {
                        ["lastFile"] = "orders-001.csv",
                        ["nested"] = new JsonObject { ["line"] = 42 }
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(state);
        ImportRuntimeState? roundTripped = JsonSerializer.Deserialize<ImportRuntimeState>(json);

        await Assert.That(roundTripped).IsNotNull();
        JsonObject cursor = roundTripped!.Imports["orders"].Cursor;
        await Assert.That(cursor["lastFile"]!.GetValue<string>()).IsEqualTo("orders-001.csv");
        await Assert.That(cursor["nested"]!["line"]!.GetValue<int>()).IsEqualTo(42);
    }
}
