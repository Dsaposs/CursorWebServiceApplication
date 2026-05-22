using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.Models;

namespace NotesApi.Services;

public static class CombatTurnAdvanceService
{
    /// <summary>
    /// Clears turn prompts and optionally advances initiative after an action is resolved or rejected.
    /// Combat stat checks defer turn advance until the player's primary combat action is also finished.
    /// </summary>
    public static async Task CompleteResolvedActionAsync(
        ApplicationDbContext db,
        GameSession session,
        ActionRequest action)
    {
        await ClearPromptForActorAsync(db, session, action);

        if (await ShouldAdvanceTurnAfterActionAsync(db, action))
        {
            await TryAdvanceAfterActionAsync(db, session, action);
        }
    }

    /// <summary>
    /// Returns false when a combat stat check was resolved but the same actor still has a primary
    /// combat action awaiting DM resolution in the same encounter.
    /// </summary>
    public static async Task<bool> ShouldAdvanceTurnAfterActionAsync(
        ApplicationDbContext db,
        ActionRequest action)
    {
        if (!action.SkillCheckBatchId.HasValue || !action.CombatEncounterId.HasValue)
        {
            return true;
        }

        var awaitingResolution = new[]
        {
            ActionStatus.Pending,
            ActionStatus.DmReviewing,
            ActionStatus.AwaitingRoll,
            ActionStatus.RollReceived,
            ActionStatus.AwaitingReaction,
            ActionStatus.ReactionPending,
            ActionStatus.Resolving,
            ActionStatus.AwaitingFollowUpRoll,
        };

        return !await db.ActionRequests.AnyAsync(a =>
            a.SessionId == action.SessionId
            && a.Id != action.Id
            && a.CombatEncounterId == action.CombatEncounterId
            && a.ActorCharacterId == action.ActorCharacterId
            && !a.SkillCheckBatchId.HasValue
            && awaitingResolution.Contains(a.Status));
    }

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
    /// Clears an active turn prompt when the prompted character's action is resolved or rejected.
    /// </summary>
    public static async Task ClearPromptForActorAsync(
        ApplicationDbContext db,
        GameSession session,
        ActionRequest action)
    {
        if (!action.ActorCharacterId.HasValue || !session.ActiveCombatEncounterId.HasValue)
        {
            return;
        }

        var encounter = await db.Set<CombatEncounter>().FindAsync(session.ActiveCombatEncounterId.Value);
        if (encounter is null)
        {
            return;
        }

        if (encounter.PromptedTurnCharacterId == action.ActorCharacterId.Value)
        {
            encounter.PromptedTurnCharacterId = null;
        }
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
