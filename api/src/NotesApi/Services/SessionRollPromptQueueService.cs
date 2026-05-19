using NotesApi.Models;
using NotesApi.Rulesets;

namespace NotesApi.Services;

public static class SessionRollPromptQueueService
{
    public static string BuildActionText(SessionRollPrompt prompt, string definitionJson)
    {
        if (!string.IsNullOrWhiteSpace(prompt.PromptLabel))
        {
            return prompt.PromptLabel.Trim();
        }

        return prompt.CheckMode switch
        {
            "Action" => RulesetActionCatalog.FindAction(definitionJson, prompt.ActionKey)?.Label ?? "Action roll",
            "Skill" => FormatSkillCheckLabel(RulesetActionCatalog.FindSkill(definitionJson, prompt.SkillKey)?.Label),
            "Attribute" => FormatAttributeCheckLabel(RulesetActionCatalog.FindAttribute(definitionJson, prompt.AttributeKey)?.Label),
            _ => prompt.CustomCheckText?.Trim() ?? "Custom check",
        };
    }

    public static string BuildDescription(SessionRollPrompt prompt, string rollSummary)
    {
        var lines = new List<string> { $"🎲 Roll: {rollSummary.Trim()}" };
        if (!string.IsNullOrWhiteSpace(prompt.PromptLabel))
        {
            lines.Add(prompt.PromptLabel.Trim());
        }

        return string.Join('\n', lines);
    }

    public static ActionRequest CreatePendingAction(
        GameSession session,
        SessionRollPrompt prompt,
        string definitionJson,
        string rollSummary,
        DateTime now)
    {
        var nextSequence = session.Actions.Count == 0 ? 1 : session.Actions.Max(a => a.Sequence) + 1;
        var rollLine = rollSummary.Trim();

        return new ActionRequest
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ActorCharacterId = prompt.TargetCharacterId,
            ActorName = prompt.TargetCharacter.Name,
            ActionText = BuildActionText(prompt, definitionJson),
            Description = BuildDescription(prompt, rollLine),
            Status = ActionStatus.Pending,
            Sequence = nextSequence,
            CombatEncounterId = CombatEncounterLifecycle.ResolveActionEncounterId(session),
            SkillCheckBatchId = prompt.SkillCheckBatchId,
            SkillCheckGroupLabel = prompt.SkillCheckBatchId.HasValue
                ? BuildActionText(prompt, definitionJson)
                : null,
            SubmittedAt = now,
        };
    }

    private static string FormatSkillCheckLabel(string? label) =>
        string.IsNullOrWhiteSpace(label) ? "Skill check" : $"{label} check";

    private static string FormatAttributeCheckLabel(string? label) =>
        string.IsNullOrWhiteSpace(label) ? "Attribute check" : $"{label} check";
}
