using System.Text.Json;
using System.Text.Json.Nodes;
using NotesApi.Models;

namespace NotesApi.Rulesets;

public static class RulesetCharacterData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void ApplyNestedDeltas(Character character, string nestedKey, Dictionary<string, int> deltas)
    {
        if (deltas.Count == 0)
        {
            return;
        }

        var root = ParseRoot(character.RulesetDataJson);
        var section = GetIntDictionary(root, nestedKey);

        foreach (var kv in deltas)
        {
            section[kv.Key] = (section.TryGetValue(kv.Key, out var current) ? current : 0) + kv.Value;
        }

        SetIntDictionary(root, nestedKey, section);
        character.RulesetDataJson = root.ToJsonString(JsonOptions);
        character.UpdatedAt = DateTime.UtcNow;
    }

    public static void ApplyGameValues(
        Character character,
        Dictionary<string, int>? setGameValues,
        Dictionary<string, int>? gameValueDeltas)
    {
        if (setGameValues is not { Count: > 0 } && gameValueDeltas is not { Count: > 0 })
        {
            return;
        }

        var root = ParseRoot(character.RulesetDataJson);
        var gameValues = GetIntDictionary(root, "gameValues");

        foreach (var kv in setGameValues ?? [])
        {
            gameValues[kv.Key] = kv.Value;
        }

        foreach (var kv in gameValueDeltas ?? [])
        {
            gameValues[kv.Key] = (gameValues.TryGetValue(kv.Key, out var current) ? current : 0) + kv.Value;
        }

        SetIntDictionary(root, "gameValues", gameValues);
        character.RulesetDataJson = root.ToJsonString(JsonOptions);
        character.UpdatedAt = DateTime.UtcNow;
    }

    public static string MergeLegacyStats(string rulesetDataJson, string attributesJson, string skillsJson)
    {
        var root = ParseRoot(rulesetDataJson);
        MergeFlatIntoSection(root, "attributes", ParseFlatIntDictionary(attributesJson));
        MergeFlatIntoSection(root, "skills", ParseFlatIntDictionary(skillsJson));
        return root.ToJsonString(JsonOptions);
    }

    private static void MergeFlatIntoSection(JsonObject root, string nestedKey, Dictionary<string, int> flat)
    {
        if (flat.Count == 0)
        {
            return;
        }

        var section = GetIntDictionary(root, nestedKey);
        foreach (var kv in flat)
        {
            section[kv.Key] = kv.Value;
        }

        SetIntDictionary(root, nestedKey, section);
    }

    private static JsonObject ParseRoot(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static Dictionary<string, int> GetIntDictionary(JsonObject root, string key)
    {
        if (root[key] is not JsonObject section)
        {
            return new Dictionary<string, int>();
        }

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in section)
        {
            if (property.Value is JsonValue value && value.TryGetValue<int>(out var number))
            {
                result[property.Key] = number;
            }
        }

        return result;
    }

    private static void SetIntDictionary(JsonObject root, string key, Dictionary<string, int> values)
    {
        var section = new JsonObject();
        foreach (var kv in values)
        {
            section[kv.Key] = kv.Value;
        }

        root[key] = section;
    }

    private static Dictionary<string, int> ParseFlatIntDictionary(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(string.IsNullOrWhiteSpace(json) ? "{}" : json)
                   ?? new Dictionary<string, int>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>();
        }
    }
}
