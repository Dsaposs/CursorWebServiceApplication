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
        InviteUrl = $"/join/game/{game.InviteCode}",
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
        StatusEffectsJson = character.StatusEffectsJson,
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
        StatusEffectsJson = npc.StatusEffectsJson,
        Visibility = visibility,
    };

    public static SessionSummaryResponse ToSessionSummaryResponse(this ControllerBase controller, GameSession session) => new()
    {
        Id = session.Id,
        GameId = session.GameId,
        JoinCode = session.JoinCode,
        JoinUrl = $"/join/{session.JoinCode}",
        IsActive = session.IsActive,
        State = session.State.ToString(),
        DiceRollMode = session.DiceRollMode.ToString(),
        ActiveTurnParticipantId = session.ActiveTurnParticipantId,
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
            FlavourText = action.FlavourText,
            TargetNpcId = playerView && action.TargetNpcId.HasValue && !ControllerHelpers.IsNpcVisible(action.TargetNpcId.Value, npcVisibilities)
                ? null
                : action.TargetNpcId,
            TargetName = targetName,
            Description = action.Description,
            Status = action.Status.ToString(),
            ResolutionText = action.Resolution?.ResolutionText,
            RollSummary = action.Resolution?.RollSummary,
            RollDataJson = action.RollDataJson,
            AdditionalActions = action.Resolution?.AdditionalActions,
            Outcome = action.Resolution?.Outcome?.ToString(),
            StatChangesJson = action.Resolution?.StatChangesJson ?? "[]",
            PendingChainEffectsJson = action.PendingChainEffectsJson ?? "[]",
            FollowUpRolls = action.RollPrompts
                .Where(p => p.Status == RollPromptStatus.Completed)
                .OrderBy(p => p.CreatedAt)
                .Select(p => ToRollPromptResponse(p, action)),
            CombatEncounterId = action.CombatEncounterId,
            CombatEncounterSequence = action.CombatEncounter?.Sequence,
            SkillCheckBatchId = action.SkillCheckBatchId,
            SkillCheckGroupLabel = action.SkillCheckGroupLabel,
            RollMode = action.RollMode,
            DmDifficultyModifier = action.DmDifficultyModifier,
            EffectiveDc = action.EffectiveDc,
            ParentActionId = action.ParentActionId,
            FollowUpType = action.FollowUpType,
            ChainStep = action.ChainStep,
            SessionModeAtSubmit = action.SessionModeAtSubmit,
            CombatRound = action.CombatRound,
            SubmittedAt = action.SubmittedAt,
            PublishedAt = action.PublishedAt,
            ResolvedAt = action.ResolvedAt,
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
        Round = encounter.Round,
        StartedAt = encounter.StartedAt,
        EndedAt = encounter.EndedAt,
        IsActive = encounter.Id == activeEncounterId && encounter.EndedAt is null,
        PromptedTurnCharacterId = encounter.PromptedTurnCharacterId,
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
        InitiativeScore = entry.InitiativeScore,
        IsCurrentTurn = entry.IsCurrentTurn,
    };

    public static IEnumerable<RollPromptResponse> SelectRollPrompts(GameSession session, Guid? playerCharacterId = null)
    {
        var activeStatuses = new[] { ActionStatus.Pending, ActionStatus.DmReviewing, ActionStatus.AwaitingRoll, ActionStatus.RollReceived, ActionStatus.AwaitingReaction, ActionStatus.ReactionPending, ActionStatus.Resolving, ActionStatus.AwaitingFollowUpRoll };
        foreach (var action in session.Actions.Where(a => activeStatuses.Contains(a.Status)).OrderBy(a => a.Sequence))
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
        GuidanceText = prompt.GuidanceText,
        CheckMode = prompt.CheckMode,
        ResultKind = prompt.ResultKind,
        ActionKey = prompt.ActionKey,
        SkillKey = prompt.SkillKey,
        AttributeKey = prompt.AttributeKey,
        CustomCheckText = prompt.CustomCheckText,
        Status = prompt.Status.ToString(),
        RollSummary = prompt.RollSummary,
        RollResultJson = prompt.RollResultJson,
        ChainStepKey = prompt.ChainStepKey,
        AutoResolveOutcome = prompt.AutoResolveOutcome,
        Dc = prompt.Dc,
        DmRolled = prompt.DmRolled,
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
        GuidanceText = prompt.GuidanceText,
        CheckMode = prompt.CheckMode,
        ResultKind = prompt.ResultKind,
        ActionKey = prompt.ActionKey,
        SkillKey = prompt.SkillKey,
        AttributeKey = prompt.AttributeKey,
        CustomCheckText = prompt.CustomCheckText,
        Status = prompt.Status.ToString(),
        RollSummary = prompt.RollSummary,
        RollResultJson = prompt.RollResultJson,
        ResultActionRequestId = prompt.ActionRequestId,
        CreatedAt = prompt.CreatedAt,
        CompletedAt = prompt.CompletedAt,
    };
}
