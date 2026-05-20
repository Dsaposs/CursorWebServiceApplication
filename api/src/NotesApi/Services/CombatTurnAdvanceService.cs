using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.Models;

namespace NotesApi.Services;

public static class CombatTurnAdvanceService
{
    /// <summary>
    /// After an action is resolved or rejected, advance initiative if the actor's turn has concluded.
    /// </summary>
    public static async Task<bool> TryAdvanceAfterActionAsync(
        ApplicationDbContext db,
        GameSession session,
        ActionRequest action)
    {
        if (session.State != SessionMode.Combat)
        {
            return false;
        }

        var entries = await db.InitiativeEntries
            .Where(i => i.SessionId == session.Id)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        if (entries.Count == 0)
        {
            return false;
        }

        var current = entries.FirstOrDefault(e => e.IsCurrentTurn);
        if (current is null)
        {
            return false;
        }

        var actorIsCurrent = action.ActorCharacterId.HasValue
            && current.CombatantType == CombatantType.Character
            && current.CombatantId == action.ActorCharacterId.Value;

        var npcIsCurrent = action.ActorNpcId.HasValue
            && current.CombatantType == CombatantType.NpcOrMonster
            && current.CombatantId == action.ActorNpcId.Value;

        if (!actorIsCurrent && !npcIsCurrent)
        {
            return false;
        }

        return AdvanceTurn(entries);
    }

    public static bool AdvanceTurn(IList<InitiativeEntry> entries)
    {
        if (entries.Count == 0)
        {
            return false;
        }

        var currentIndex = entries.ToList().FindIndex(e => e.IsCurrentTurn);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        entries[currentIndex].IsCurrentTurn = false;
        entries[(currentIndex + 1) % entries.Count].IsCurrentTurn = true;
        return true;
    }
}
