using NotesApi.DTOs;
using NotesApi.Rulesets;

namespace NotesApi.Services;

public static class RollPromptValidator
{
    public static bool TryNormalizeCheckMode(string raw, out string checkMode)
    {
        if (string.Equals(raw, "Action", StringComparison.OrdinalIgnoreCase))
        {
            checkMode = "Action";
            return true;
        }

        if (string.Equals(raw, "Skill", StringComparison.OrdinalIgnoreCase))
        {
            checkMode = "Skill";
            return true;
        }

        if (string.Equals(raw, "Attribute", StringComparison.OrdinalIgnoreCase))
        {
            checkMode = "Attribute";
            return true;
        }

        if (string.Equals(raw, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            checkMode = "Custom";
            return true;
        }

        checkMode = string.Empty;
        return false;
    }

    public static string? ValidateCheck(
        string checkMode,
        CreateRollPromptRequest item,
        string definitionJson,
        string classKey)
    {
        return checkMode switch
        {
            "Action" when string.IsNullOrWhiteSpace(item.ActionKey) =>
                "An action must be selected for an action roll prompt.",
            "Action" => ValidateActionKey(item.ActionKey!, definitionJson, classKey),
            "Skill" when string.IsNullOrWhiteSpace(item.SkillKey) =>
                "A skill must be selected for a skill roll prompt.",
            "Skill" when RulesetActionCatalog.FindSkill(definitionJson, item.SkillKey!) is null =>
                "Selected skill is not available for this ruleset.",
            "Attribute" when string.IsNullOrWhiteSpace(item.AttributeKey) =>
                "An attribute must be selected for an attribute roll prompt.",
            "Attribute" when RulesetActionCatalog.FindAttribute(definitionJson, item.AttributeKey!) is null =>
                "Selected attribute is not available for this ruleset.",
            "Custom" when string.IsNullOrWhiteSpace(item.CustomCheckText) =>
                "Describe the custom check for a custom roll prompt.",
            _ => null,
        };
    }

    private static string? ValidateActionKey(string actionKey, string definitionJson, string classKey)
    {
        var rulesetAction = RulesetActionCatalog.FindAction(definitionJson, actionKey);
        if (rulesetAction is null)
        {
            return "Selected action is not available for this ruleset.";
        }

        if (!RulesetActionCatalog.IsAllowedForClass(rulesetAction, classKey))
        {
            return "Selected action is not available for this character's class.";
        }

        return null;
    }
}
