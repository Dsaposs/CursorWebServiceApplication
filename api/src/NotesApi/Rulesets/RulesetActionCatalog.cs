using System.Text.Json;

namespace NotesApi.Rulesets;

public static class RulesetActionCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RulesetActionDefinition? FindAction(string definitionJson, string? actionKey)
    {
        if (string.IsNullOrWhiteSpace(actionKey))
        {
            return null;
        }

        var definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        return definition?.Actions.FirstOrDefault(action => action.Key.Equals(actionKey, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAllowedForClass(RulesetActionDefinition action, string classKey)
    {
        var allowedClasses = action.AllowedClasses.ToList();
        return allowedClasses.Count == 0
            || allowedClasses.Any(key => key.Equals(classKey, StringComparison.OrdinalIgnoreCase));
    }

    public static RulesetSkillDefinition? FindSkill(string definitionJson, string? skillKey)
    {
        if (string.IsNullOrWhiteSpace(skillKey))
        {
            return null;
        }

        var definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        return definition?.Character.Skills.FirstOrDefault(skill => skill.Key.Equals(skillKey, StringComparison.OrdinalIgnoreCase));
    }

    public static RulesetAttributeDefinition? FindAttribute(string definitionJson, string? attributeKey)
    {
        if (string.IsNullOrWhiteSpace(attributeKey))
        {
            return null;
        }

        var definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        return definition?.Character.Attributes.FirstOrDefault(attr => attr.Key.Equals(attributeKey, StringComparison.OrdinalIgnoreCase));
    }
}
