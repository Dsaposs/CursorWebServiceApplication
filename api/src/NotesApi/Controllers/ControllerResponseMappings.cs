using Microsoft.AspNetCore.Mvc;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Controllers;

public static partial class ControllerHelpers
{
    /// <summary>
    /// Builds a GameResponse. When playerView is true, Hidden NPCs are excluded
    /// and Obscured NPCs appear anonymised (no name, no stats).
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
            .Select(n => (npc: n, vis: npcVisibilities?.GetValueOrDefault(n.Id.ToString(), "Visible") ?? "Visible"))
            .Where(x => !playerView || x.vis != "Hidden")
            .Select(x => playerView && x.vis == "Obscured"
                ? new NpcResponse { Id = x.npc.Id, Name = "Unknown", Kind = "???", Visibility = "Obscured" }
                : ToNpcResponse(x.npc, x.vis)),
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
        AttributesJson = character.AttributesJson,
        SkillsJson = character.SkillsJson,
        InventoryJson = character.InventoryJson,
        RulesetDataJson = character.RulesetDataJson,
        ClassKey = character.ClassKey,
    };

    public static NpcResponse ToNpcResponse(NpcOrMonster npc, string visibility = "Visible") => new()
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

    public static ActionQueueItemResponse ToActionResponse(ActionRequest action) => new()
    {
        Id = action.Id,
        Sequence = action.Sequence,
        ActorName = action.ActorName,
        ActionKey = action.ActionKey,
        ActionText = action.ActionText,
        TargetName = action.TargetName,
        Description = action.Description,
        Status = action.Status.ToString(),
        ResolutionText = action.Resolution?.ResolutionText,
        RollSummary = action.Resolution?.RollSummary,
        AdditionalActions = action.Resolution?.AdditionalActions,
        StatChangesJson = action.Resolution?.StatChangesJson ?? "[]",
        SubmittedAt = action.SubmittedAt,
        PublishedAt = action.PublishedAt,
    };

    public static InitiativeEntryResponse ToInitiativeResponse(InitiativeEntry entry) => new()
    {
        Id = entry.Id,
        CombatantType = entry.CombatantType.ToString(),
        CombatantId = entry.CombatantId,
        CombatantName = entry.CombatantName,
        SortOrder = entry.SortOrder,
        IsCurrentTurn = entry.IsCurrentTurn,
    };
}
