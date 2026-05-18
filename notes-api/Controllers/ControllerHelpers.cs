using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Controllers;

public static class ControllerHelpers
{
    public static string UserId(this ControllerBase controller) =>
        controller.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User id missing.");

    public static string NewCode() => Convert.ToHexString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty)[..12].ToLowerInvariant();

    public static string NewToken() => Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();

    public static string JoinUrl(this ControllerBase controller, string path) => path;

    public static GameResponse ToGameResponse(this ControllerBase controller, Game game) => new()
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
        NpcsAndMonsters = game.NpcsAndMonsters.OrderBy(n => n.Name).Select(ToNpcResponse),
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
    };

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
    };

    public static NpcResponse ToNpcResponse(NpcOrMonster npc) => new()
    {
        Id = npc.Id,
        Name = npc.Name,
        Kind = npc.Kind,
        MaxHealth = npc.MaxHealth,
        Health = npc.Health,
        Armor = npc.Armor,
        StatBlockJson = npc.StatBlockJson,
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

    public static async Task<Game?> GetOwnedGameAsync(this ApplicationDbContext db, Guid gameId, string userId) =>
        await db.Games
            .Include(g => g.Ruleset)
            .Include(g => g.Characters)
            .Include(g => g.NpcsAndMonsters)
            .Include(g => g.Sessions)
            .FirstOrDefaultAsync(g => g.Id == gameId && g.DmUserId == userId);
}
