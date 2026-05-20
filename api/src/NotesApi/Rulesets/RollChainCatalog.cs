using System.Text.Json;

namespace NotesApi.Rulesets;

public static class RollChainCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<RulesetRollChainStepDefinition> GetChain(string definitionJson, string? actionKey)
    {
        var action = RulesetActionCatalog.FindAction(definitionJson, actionKey);
        return action?.RollChain.ToList() ?? new List<RulesetRollChainStepDefinition>();
    }

    public static RulesetRollChainStepDefinition? GetStep(
        string definitionJson,
        string? actionKey,
        string stepKey) =>
        GetChain(definitionJson, actionKey)
            .FirstOrDefault(s => s.Step.Equals(stepKey, StringComparison.OrdinalIgnoreCase));

    public static RulesetRollChainStepDefinition? GetStepAtIndex(
        string definitionJson,
        string? actionKey,
        int index)
    {
        var chain = GetChain(definitionJson, actionKey);
        return index >= 0 && index < chain.Count ? chain[index] : null;
    }

    public static int IndexOfStep(IReadOnlyList<RulesetRollChainStepDefinition> chain, string stepKey) =>
        chain.ToList().FindIndex(s => s.Step.Equals(stepKey, StringComparison.OrdinalIgnoreCase));

    public static RollChainState? ParseState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RollChainState>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string SerializeState(RollChainState state) =>
        JsonSerializer.Serialize(state, JsonOptions);
}
