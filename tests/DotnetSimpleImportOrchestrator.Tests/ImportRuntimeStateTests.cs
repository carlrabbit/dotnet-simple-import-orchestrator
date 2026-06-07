using System.Text.Json;
using System.Text.Json.Nodes;
using DotnetSimpleImportOrchestrator;

namespace DotnetSimpleImportOrchestrator.Tests;

public sealed class ImportRuntimeStateTests
{
    [Test]
    public async Task RuntimeStateJsonRoundTripPreservesImportStateAndCursorJson()
    {
        DateTimeOffset checkedAt = DateTimeOffset.Parse("2026-06-07T12:00:00Z");
        ImportRuntimeState state = new()
        {
            Imports =
            {
                ["orders"] = new ImportState
                {
                    LastCheckedAt = checkedAt,
                    LastSuccessfulImportAt = checkedAt.AddMinutes(1),
                    Cursor = new JsonObject
                    {
                        ["lastItem"] = "orders-001",
                        ["nested"] = new JsonObject { ["line"] = 42 }
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(state);
        ImportRuntimeState? roundTripped = JsonSerializer.Deserialize<ImportRuntimeState>(json);

        await Assert.That(roundTripped).IsNotNull();
        ImportState importState = roundTripped!.Imports["orders"];
        await Assert.That(importState.LastCheckedAt).IsEqualTo(checkedAt);
        await Assert.That(importState.Cursor["lastItem"]!.GetValue<string>()).IsEqualTo("orders-001");
        await Assert.That(importState.Cursor["nested"]!["line"]!.GetValue<int>()).IsEqualTo(42);
    }
}
