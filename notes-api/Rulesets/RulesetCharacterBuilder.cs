using System.Text.Json;
using System.Text.Json.Nodes;

namespace NotesApi.Rulesets;

public static class RulesetCharacterBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static bool ClassExists(string definitionJson, string classKey)
    {
        if (string.IsNullOrWhiteSpace(classKey))
        {
            return true;
        }

        var definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        return definition?.Character.Classes.Any(c => c.Key.Equals(classKey, StringComparison.OrdinalIgnoreCase)) == true;
    }

    public static string BuildRulesetDataJson(string characterTemplateJson, string classKey)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(characterTemplateJson) ? "{}" : characterTemplateJson) as JsonObject
            ?? new JsonObject();

        if (!string.IsNullOrWhiteSpace(classKey))
        {
            root["classKey"] = classKey;
        }

        return root.ToJsonString(JsonOptions);
    }
}
