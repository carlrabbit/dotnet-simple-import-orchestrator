using System.Text.Json.Nodes;

namespace DotnetSimpleImportOrchestrator.Abstractions;

public sealed record ImportHandlingResult
{
    public required bool Succeeded { get; init; }

    public JsonObject CursorUpdate { get; init; } = [];

    public string? ErrorMessage { get; init; }

    public static ImportHandlingResult Success(JsonObject? cursorUpdate = null) =>
        new() { Succeeded = true, CursorUpdate = cursorUpdate ?? [] };

    public static ImportHandlingResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
