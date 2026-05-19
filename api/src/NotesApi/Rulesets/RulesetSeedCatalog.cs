namespace NotesApi.Rulesets;

/// <summary>Built-in rulesets imported from JSON on application startup.</summary>
public static class RulesetSeedCatalog
{
    public static readonly IReadOnlyList<string> Codes =
    [
        "alien-rpg",
        "dnd-5e",
        "die-rpg",
        "pathfinder-2e",
    ];

    public static bool IsPlaceholder(string code) =>
        code.Equals("pathfinder-2e", StringComparison.OrdinalIgnoreCase);
}
