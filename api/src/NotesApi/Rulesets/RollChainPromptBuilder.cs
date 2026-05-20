using NotesApi.Models;

namespace NotesApi.Rulesets;

public static class RollChainPromptBuilder
{
    public static ActionRollPrompt CreatePrompt(
        RulesetRollChainStepDefinition step,
        ActionRequest action,
        Character targetCharacter,
        string definitionJson,
        DateTime createdAt)
    {
        var checkMode = NormalizeCheckMode(step.CheckMode);
        var resultKind = string.Equals(step.ResultKind, "Total", StringComparison.OrdinalIgnoreCase)
            ? RollPromptResultKind.Total
            : RollPromptResultKind.PassFail;

        string? actionKey = null;
        string? customCheckText = step.CustomCheckText;

        if (checkMode == "Action")
        {
            actionKey = action.ActionKey;
        }
        else if (checkMode == "Custom" && string.Equals(step.DiceSource, "equippedWeapon", StringComparison.OrdinalIgnoreCase))
        {
            customCheckText = BuildWeaponDamageText(definitionJson, action, targetCharacter) ?? step.CustomCheckText ?? "Weapon damage";
        }

        return new ActionRollPrompt
        {
            Id = Guid.NewGuid(),
            ActionRequestId = action.Id,
            TargetCharacterId = targetCharacter.Id,
            TargetCharacter = targetCharacter,
            PromptLabel = step.Label,
            GuidanceText = step.GuidanceText,
            CheckMode = checkMode,
            ResultKind = resultKind,
            ActionKey = actionKey,
            CustomCheckText = customCheckText,
            ChainStepKey = step.Step,
            Status = RollPromptStatus.Pending,
            CreatedAt = createdAt,
        };
    }

    private static string NormalizeCheckMode(string raw) =>
        raw switch
        {
            var s when s.Equals("Skill", StringComparison.OrdinalIgnoreCase) => "Skill",
            var s when s.Equals("Attribute", StringComparison.OrdinalIgnoreCase) => "Attribute",
            var s when s.Equals("Custom", StringComparison.OrdinalIgnoreCase) => "Custom",
            _ => "Action",
        };

    private static string? BuildWeaponDamageText(string definitionJson, ActionRequest action, Character character)
    {
        var rulesetAction = RulesetActionCatalog.FindAction(definitionJson, action.ActionKey);
        if (rulesetAction is null)
        {
            return null;
        }

        var inventory = CharacterInventory.Parse(character.InventoryJson);
        RulesetItemDefinition? item = null;

        if (!string.IsNullOrWhiteSpace(rulesetAction.RequiredItemKey))
        {
            item = RulesetActionCatalog.FindItem(definitionJson, rulesetAction.RequiredItemKey);
        }
        else
        {
            foreach (var entry in inventory)
            {
                var candidate = RulesetActionCatalog.FindItem(definitionJson, entry.ItemKey);
                if (candidate?.DamageRoll is not null)
                {
                    item = candidate;
                    break;
                }
            }
        }

        if (item?.DamageRoll is null)
        {
            return "Roll damage dice";
        }

        var notation = item.DamageRoll.Notation;
        var description = item.DamageRoll.Description;
        return string.IsNullOrWhiteSpace(description)
            ? $"Roll {notation} damage"
            : $"{description} ({notation})";
    }
}
