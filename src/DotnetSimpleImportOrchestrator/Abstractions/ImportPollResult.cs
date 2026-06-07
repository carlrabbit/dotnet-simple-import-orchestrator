using System.Text.Json.Nodes;

namespace DotnetSimpleImportOrchestrator.Abstractions;

public sealed record ImportPollResult
{
    public ImportCandidate? Candidate { get; init; }

    public JsonObject CursorUpdate { get; init; } = [];

    public static ImportPollResult NoCandidate(JsonObject? cursorUpdate = null) =>
        new() { CursorUpdate = cursorUpdate ?? [] };

    public static ImportPollResult CandidateResult(ImportCandidate candidate, JsonObject? cursorUpdate = null) =>
        new() { Candidate = candidate, CursorUpdate = cursorUpdate ?? [] };

}
