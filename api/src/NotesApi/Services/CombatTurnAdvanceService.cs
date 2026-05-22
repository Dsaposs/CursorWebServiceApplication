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

        var encounter = session.ActiveCombatEncounterId.HasValue
            ? await db.Set<CombatEncounter>().FindAsync(session.ActiveCombatEncounterId.Value)
            : null;

        return AdvanceTurn(entries, encounter);
    }

    /// <summary>
    /// Advances the initiative to the next combatant. When the order wraps back to the first
    /// combatant the encounter's Round counter is incremented.
    /// </summary>
    public static bool AdvanceTurn(IList<InitiativeEntry> entries, CombatEncounter? encounter = null)
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
        var nextIndex = (currentIndex + 1) % entries.Count;
        entries[nextIndex].IsCurrentTurn = true;

        if (encounter is not null)
        {
            // Clear the turn prompt so the next player is not auto-shown the action form.
            encounter.PromptedTurnCharacterId = null;

            // Increment round counter when the order wraps back to the first position.
            if (nextIndex == 0)
            {
                encounter.Round++;
            }
        }

        return true;
    }
}
