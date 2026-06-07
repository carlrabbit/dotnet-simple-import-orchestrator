using System.Text.Json.Nodes;

namespace DotnetSimpleImportOrchestrator.Abstractions;

public sealed record ImportHandlingResult
{
    public bool Succeeded { get; init; }

    public JsonObject Cursor { get; init; } = [];

    public string? FailureMessage { get; init; }

    public static ImportHandlingResult Success(JsonObject? cursor = null) =>
        new() { Succeeded = true, Cursor = cursor ?? [] };

    public static ImportHandlingResult Failure(string message) =>
        new() { Succeeded = false, FailureMessage = message };
}
