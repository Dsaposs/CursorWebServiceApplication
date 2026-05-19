using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.Models;

namespace NotesApi.Services;

public static class CombatEncounterLifecycle
{
    public static async Task<CombatEncounter> BeginEncounterAsync(ApplicationDbContext db, GameSession session)
    {
        await EndActiveEncounterAsync(db, session);

        var nextSequence = await db.CombatEncounters
            .Where(e => e.SessionId == session.Id)
            .MaxAsync(e => (int?)e.Sequence) ?? 0;

        var now = DateTime.UtcNow;
        var encounter = new CombatEncounter
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Sequence = nextSequence + 1,
            StartedAt = now,
        };

        db.CombatEncounters.Add(encounter);
        session.ActiveCombatEncounterId = encounter.Id;
        return encounter;
    }

    public static async Task EndActiveEncounterAsync(ApplicationDbContext db, GameSession session)
    {
        if (session.ActiveCombatEncounterId is not Guid encounterId)
        {
            return;
        }

        var encounter = await db.CombatEncounters.FirstOrDefaultAsync(e => e.Id == encounterId);
        if (encounter is not null && encounter.EndedAt is null)
        {
            encounter.EndedAt = DateTime.UtcNow;
        }

        session.ActiveCombatEncounterId = null;
    }

    public static async Task EnsureEncounterForCombatAsync(ApplicationDbContext db, GameSession session)
    {
        if (session.State != SessionMode.Combat)
        {
            return;
        }

        if (session.ActiveCombatEncounterId is Guid encounterId)
        {
            var active = await db.CombatEncounters.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == encounterId && e.EndedAt == null);
            if (active is not null)
            {
                return;
            }
        }

        await BeginEncounterAsync(db, session);
    }

    public static Guid? ResolveActionEncounterId(GameSession session) =>
        session.State == SessionMode.Combat ? session.ActiveCombatEncounterId : null;
}
