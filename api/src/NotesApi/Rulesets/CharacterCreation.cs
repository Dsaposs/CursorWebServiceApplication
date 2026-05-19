using System.Text.Json;
using System.Text.Json.Nodes;

namespace NotesApi.Rulesets;

public static class CharacterCreation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public sealed record BuildResult(string RulesetDataJson, string InventoryJson);

    public static BuildResult Build(
        string definitionJson,
        string characterTemplateJson,
        string classKey,
        IReadOnlyDictionary<string, int>? skillAllocations,
        string? startingItemKey)
    {
        var definition = DeserializeDefinition(definitionJson)
            ?? throw new InvalidOperationException("Invalid ruleset definition.");

        var classDef = definition.Character.Classes.FirstOrDefault(c =>
            c.Key.Equals(classKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Selected class is not available for this ruleset.");

        ValidateSkillAllocations(classDef, skillAllocations);
        ValidateStartingItem(classDef, definition, startingItemKey);

        return new BuildResult(
            BuildRulesetDataJson(characterTemplateJson, classKey, skillAllocations),
            BuildInventoryJson(startingItemKey));
    }

    public static void ValidateSkillAllocations(
        RulesetClassDefinition classDef,
        IReadOnlyDictionary<string, int>? skillAllocations)
    {
        var budget = classDef.StartingSkillPoints;
        if (budget <= 0)
        {
            return;
        }

        skillAllocations ??= new Dictionary<string, int>();
        var allowed = classDef.AvailableSkills
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var spent = 0;
        foreach (var kv in skillAllocations)
        {
            if (!allowed.Contains(kv.Key))
            {
                throw new ArgumentException($"Skill '{kv.Key}' is not available for this class.");
            }

            if (kv.Value < 0)
            {
                throw new ArgumentException($"Skill '{kv.Key}' cannot be negative.");
            }

            if (classDef.MaxSkillRank is int maxRank && kv.Value > maxRank)
            {
                throw new ArgumentException($"Skill '{kv.Key}' cannot exceed {maxRank} at creation.");
            }

            spent += kv.Value;
        }

        if (spent != budget)
        {
            throw new ArgumentException($"Allocate exactly {budget} skill points (currently {spent}).");
        }
    }

    public static void ValidateStartingItem(
        RulesetClassDefinition classDef,
        RulesetDefinition definition,
        string? startingItemKey)
    {
        var options = classDef.StartingItemOptions.ToList();
        if (options.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(startingItemKey))
        {
            throw new ArgumentException("Choose a starting item for this class.");
        }

        if (!options.Any(key => key.Equals(startingItemKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Selected starting item is not available for this class.");
        }

        if (definition.Items.All(item => !item.Key.Equals(startingItemKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Selected starting item is not defined in this ruleset.");
        }
    }

    private static string BuildRulesetDataJson(
        string characterTemplateJson,
        string classKey,
        IReadOnlyDictionary<string, int>? skillAllocations)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(characterTemplateJson) ? "{}" : characterTemplateJson) as JsonObject
            ?? new JsonObject();

        if (!string.IsNullOrWhiteSpace(classKey))
        {
            root["classKey"] = classKey;
        }

        if (skillAllocations is { Count: > 0 })
        {
            var skills = root["skills"] as JsonObject ?? new JsonObject();
            foreach (var kv in skillAllocations)
            {
                skills[kv.Key] = kv.Value;
            }

            root["skills"] = skills;
        }

        return root.ToJsonString(JsonOptions);
    }

    private static string BuildInventoryJson(string? startingItemKey)
    {
        if (string.IsNullOrWhiteSpace(startingItemKey))
        {
            return "[]";
        }

        var inventory = new[]
        {
            new { itemKey = startingItemKey.Trim(), quantity = 1 },
        };

        return JsonSerializer.Serialize(inventory, JsonOptions);
    }

    private static RulesetDefinition? DeserializeDefinition(string definitionJson)
    {
        try
        {
            return JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
