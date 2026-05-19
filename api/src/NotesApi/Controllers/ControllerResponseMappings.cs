using Microsoft.AspNetCore.Mvc;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Controllers;

public static partial class ControllerHelpers
{
    /// <summary>
    /// Builds a GameResponse. When playerView is true, hidden NPCs are omitted entirely.
    /// NPCs default to hidden until the DM marks them visible for the session.
    /// </summary>
    public static GameResponse ToGameResponse(
        this ControllerBase controller,
        Game game,
        Dictionary<string, string>? npcVisibilities = null,
        bool playerView = false) => new()
    {
        Id = game.Id,
        Name = game.Name,
        Description = game.Description,
        RulesetCode = game.RulesetCode,
        RulesetName = game.Ruleset?.DisplayName ?? game.RulesetCode,
        InviteCode = game.InviteCode,
        InviteUrl = controller.JoinUrl($"/join/game/{game.InviteCode}"),
        CreatedAt = game.CreatedAt,
        UpdatedAt = game.UpdatedAt,
        Characters = game.Characters.OrderBy(c => c.Name).Select(ToCharacterResponse),
        NpcsAndMonsters = game.NpcsAndMonsters
            .OrderBy(n => n.Name)
            .Select(n => (npc: n, vis: ControllerHelpers.GetNpcVisibility(n.Id, npcVisibilities)))
            .Where(x => !playerView || ControllerHelpers.IsNpcVisible(x.npc.Id, npcVisibilities))
            .Select(x => ToNpcResponse(x.npc, x.vis)),
        Sessions = game.Sessions.OrderByDescending(s => s.StartedAt).Select(s => controller.ToSessionSummaryResponse(s)),
    };

    public static RulesetResponse ToRulesetResponse(Ruleset ruleset) => new()
    {
        Code = ruleset.Code,
        DisplayName = ruleset.DisplayName,
        Description = ruleset.Description,
        DiceNotation = ruleset.DiceNotation,
        IsPlaceholder = ruleset.IsPlaceholder,
        CharacterTemplateJson = ruleset.CharacterTemplateJson,
        DefinitionJson = ruleset.DefinitionJson,
    };

    public static RulesetDetailResponse ToRulesetDetailResponse(Ruleset ruleset)
    {
        var response = ToRulesetResponse(ruleset);
        return new RulesetDetailResponse
        {
            Code = response.Code,
            DisplayName = response.DisplayName,
            Description = response.Description,
            DiceNotation = response.DiceNotation,
            IsPlaceholder = response.IsPlaceholder,
            CharacterTemplateJson = response.CharacterTemplateJson,
            DefinitionJson = response.DefinitionJson,
        };
    }

    public static CharacterResponse ToCharacterResponse(Character character) => new()
    {
        Id = character.Id,
        Name = character.Name,
        PlayerName = character.PlayerName,
        MaxHealth = character.MaxHealth,
        Health = character.Health,
        Armor = character.Armor,
        InventoryJson = character.InventoryJson,
        RulesetDataJson = character.RulesetDataJson,
        ClassKey = character.ClassKey,
    };

    public static NpcResponse ToNpcResponse(NpcOrMonster npc, string visibility = ControllerHelpers.NpcVisibilityHidden) => new()
    {
        Id = npc.Id,
        Name = npc.Name,
        Kind = npc.Kind,
        MaxHealth = npc.MaxHealth,
        Health = npc.Health,
        Armor = npc.Armor,
        StatBlockJson = npc.StatBlockJson,
        Visibility = visibility,
    };

    public static SessionSummaryResponse ToSessionSummaryResponse(this ControllerBase controller, GameSession session) => new()
    {
        Id = session.Id,
        GameId = session.GameId,
        JoinCode = session.JoinCode,
        JoinUrl = controller.JoinUrl($"/join/{session.JoinCode}"),
        IsActive = session.IsActive,
        State = session.State.ToString(),
        Version = session.Version,
        StartedAt = session.StartedAt,
        EndedAt = session.EndedAt,
        UpdatedAt = session.UpdatedAt,
    };

    public static ActionQueueItemResponse ToActionResponse(
        ActionRequest action,
        bool isSkillCheckResponse = false,
        Dictionary<string, string>? npcVisibilities = null,
        bool playerView = false)
    {
        var actorName = action.ActorName;
        var targetName = action.TargetName;
        if (playerView)
        {
            if (action.ActorNpcId.HasValue && !ControllerHelpers.IsNpcVisible(action.ActorNpcId.Value, npcVisibilities))
            {
                actorName = "???";
            }

            if (action.TargetNpcId.HasValue && !ControllerHelpers.IsNpcVisible(action.TargetNpcId.Value, npcVisibilities))
            {
                targetName = null;
            }
        }

        return new ActionQueueItemResponse
    {
        Id = action.Id,
        Sequence = action.Sequence,
        IsSkillCheckResponse = isSkillCheckResponse,
        ActorName = actorName,
        ActorCharacterId = action.ActorCharacterId,
        ActorNpcId = playerView && action.ActorNpcId.HasValue && !ControllerHelpers.IsNpcVisible(action.ActorNpcId.Value, npcVisibilities)
            ? null
            : action.ActorNpcId,
        ActionKey = action.ActionKey,
        ActionText = action.ActionText,
        TargetNpcId = playerView && action.TargetNpcId.HasValue && !ControllerHelpers.IsNpcVisible(action.TargetNpcId.Value, npcVisibilities)
            ? null
            : action.TargetNpcId,
        TargetName = targetName,
        Description = action.Description,
        Status = action.Status.ToString(),
        ResolutionText = action.Resolution?.ResolutionText,
        RollSummary = action.Resolution?.RollSummary,
        AdditionalActions = action.Resolution?.AdditionalActions,
        Outcome = action.Resolution?.Outcome?.ToString(),
        StatChangesJson = action.Resolution?.StatChangesJson ?? "[]",
        FollowUpRolls = action.RollPrompts
            .Where(p => p.Status == RollPromptStatus.Completed)
            .OrderBy(p => p.CreatedAt)
            .Select(p => ToRollPromptResponse(p, action)),
        CombatEncounterId = action.CombatEncounterId,
        CombatEncounterSequence = action.CombatEncounter?.Sequence,
        SkillCheckBatchId = action.SkillCheckBatchId,
        SkillCheckGroupLabel = action.SkillCheckGroupLabel,
        SubmittedAt = action.SubmittedAt,
        PublishedAt = action.PublishedAt,
    };
    }

    public static IEnumerable<InitiativeEntryResponse> SelectInitiativeEntries(
        GameSession session,
        Dictionary<string, string>? npcVisibilities = null,
        bool playerView = false) =>
        session.InitiativeEntries
            .OrderBy(i => i.SortOrder)
            .Where(i => !playerView
                || i.CombatantType != CombatantType.NpcOrMonster
                || ControllerHelpers.IsNpcVisible(i.CombatantId, npcVisibilities))
            .Select(ToInitiativeResponse);

    public static CombatEncounterResponse ToCombatEncounterResponse(CombatEncounter encounter, Guid? activeEncounterId) => new()
    {
        Id = encounter.Id,
        Sequence = encounter.Sequence,
        StartedAt = encounter.StartedAt,
        EndedAt = encounter.EndedAt,
        IsActive = encounter.Id == activeEncounterId && encounter.EndedAt is null,
    };

    public static IEnumerable<CombatEncounterResponse> SelectCombatEncounters(GameSession session) =>
        session.CombatEncounters
            .OrderBy(e => e.Sequence)
            .Select(e => ToCombatEncounterResponse(e, session.ActiveCombatEncounterId));

    public static InitiativeEntryResponse ToInitiativeResponse(InitiativeEntry entry) => new()
    {
        Id = entry.Id,
        CombatantType = entry.CombatantType.ToString(),
        CombatantId = entry.CombatantId,
        CombatantName = entry.CombatantName,
        SortOrder = entry.SortOrder,
        IsCurrentTurn = entry.IsCurrentTurn,
    };

    public static IEnumerable<RollPromptResponse> SelectRollPrompts(GameSession session, Guid? playerCharacterId = null)
    {
        foreach (var action in session.Actions.Where(a => a.Status == ActionStatus.Pending).OrderBy(a => a.Sequence))
        {
            var prompts = action.RollPrompts.AsEnumerable();
            if (playerCharacterId.HasValue)
            {
                prompts = prompts.Where(p =>
                    p.TargetCharacterId == playerCharacterId.Value
                    && p.Status == RollPromptStatus.Pending);
            }
            else
            {
                prompts = prompts.Where(p => p.Status != RollPromptStatus.Cancelled);
            }

            foreach (var prompt in prompts.OrderBy(p => p.CreatedAt))
            {
                yield return ToRollPromptResponse(prompt, action);
            }
        }

        var sessionPrompts = session.SessionRollPrompts.AsEnumerable();
        if (playerCharacterId.HasValue)
        {
            sessionPrompts = sessionPrompts.Where(p =>
                p.TargetCharacterId == playerCharacterId.Value
                && p.Status == RollPromptStatus.Pending);
        }
        else
        {
            sessionPrompts = sessionPrompts.Where(p => p.Status == RollPromptStatus.Pending);
        }

        foreach (var prompt in sessionPrompts.OrderBy(p => p.CreatedAt))
        {
            yield return ToSessionRollPromptResponse(prompt);
        }
    }

    public static RollPromptResponse ToRollPromptResponse(ActionRollPrompt prompt, ActionRequest? action = null) => new()
    {
        Id = prompt.Id,
        IsSessionPrompt = false,
        ActionRequestId = prompt.ActionRequestId,
        ActionSequence = action?.Sequence ?? prompt.ActionRequest?.Sequence ?? 0,
        TargetCharacterId = prompt.TargetCharacterId,
        TargetCharacterName = prompt.TargetCharacter?.Name ?? string.Empty,
        PromptLabel = prompt.PromptLabel,
        CheckMode = prompt.CheckMode,
        ResultKind = prompt.ResultKind,
        ActionKey = prompt.ActionKey,
        SkillKey = prompt.SkillKey,
        AttributeKey = prompt.AttributeKey,
        CustomCheckText = prompt.CustomCheckText,
        Status = prompt.Status.ToString(),
        RollSummary = prompt.RollSummary,
        CreatedAt = prompt.CreatedAt,
        CompletedAt = prompt.CompletedAt,
    };

    public static RollPromptResponse ToSessionRollPromptResponse(SessionRollPrompt prompt) => new()
    {
        Id = prompt.Id,
        IsSessionPrompt = true,
        ActionRequestId = null,
        ActionSequence = null,
        TargetCharacterId = prompt.TargetCharacterId,
        TargetCharacterName = prompt.TargetCharacter?.Name ?? string.Empty,
        PromptLabel = prompt.PromptLabel,
        CheckMode = prompt.CheckMode,
        ResultKind = prompt.ResultKind,
        ActionKey = prompt.ActionKey,
        SkillKey = prompt.SkillKey,
        AttributeKey = prompt.AttributeKey,
        CustomCheckText = prompt.CustomCheckText,
        Status = prompt.Status.ToString(),
        RollSummary = prompt.RollSummary,
        ResultActionRequestId = prompt.ActionRequestId,
        CreatedAt = prompt.CreatedAt,
        CompletedAt = prompt.CompletedAt,
    };
}
