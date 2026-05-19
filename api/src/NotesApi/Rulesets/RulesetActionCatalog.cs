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

    public static RulesetItemDefinition? FindItem(string definitionJson, string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
        {
            return null;
        }

        var definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        return definition?.Items.FirstOrDefault(item => item.Key.Equals(itemKey, StringComparison.OrdinalIgnoreCase));
    }

    public static RulesetRollDefinition ResolveActionRoll(RulesetActionDefinition action, RulesetItemDefinition? item)
    {
        if (item?.AttackRoll is not null)
        {
            return MergeRoll(item.AttackRoll, item.Modifiers);
        }

        if (item is null)
        {
            return action.Roll;
        }

        return MergeRoll(action.Roll, item.Modifiers);
    }

    private static RulesetRollDefinition MergeRoll(RulesetRollDefinition roll, IEnumerable<RulesetModifierDefinition> extraModifiers)
    {
        var merged = new RulesetRollDefinition
        {
            Dice = roll.Dice,
            DicePoolMode = roll.DicePoolMode,
            Attribute = roll.Attribute,
            Skill = roll.Skill,
            SuccessRule = roll.SuccessRule,
            DifficultyClass = roll.DifficultyClass,
            Modifiers = roll.Modifiers.Concat(extraModifiers).ToList(),
        };

        return merged;
    }
}
