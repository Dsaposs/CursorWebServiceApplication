using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/sessions/{sessionId:guid}/combat")]
[Authorize]
public class CombatController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CombatController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<ActionResult<IEnumerable<InitiativeEntryResponse>>> Setup(Guid sessionId, SetupCombatRequest request)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        session.InitiativeEntries.Clear();
        var characters = await _db.Characters.Where(c => c.GameId == session.GameId).ToListAsync();
        var npcs = await _db.NpcsAndMonsters.Where(n => n.GameId == session.GameId).ToListAsync();
        var now = DateTime.UtcNow;
        var ordered = request.Combatants
            .OrderByDescending(c => c.Initiative)
            .ThenBy(c => c.Type)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var combatant = ordered[index];
            if (!TryResolveCombatant(combatant, characters, npcs, out var type, out var name))
            {
                return BadRequest(new { errors = new[] { $"Combatant {combatant.Id} was not found in this game." } });
            }

            session.InitiativeEntries.Add(new InitiativeEntry
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                CombatantType = type,
                CombatantId = combatant.Id,
                CombatantName = name,
                SortOrder = index + 1,
                IsCurrentTurn = index == 0,
                CreatedAt = now,
            });
        }

        session.State = SessionMode.Combat;
        Touch(session);
        await _db.SaveChangesAsync();

        return Ok(session.InitiativeEntries.OrderBy(i => i.SortOrder).Select(ControllerHelpers.ToInitiativeResponse));
    }

    [HttpPost("advance")]
    public async Task<ActionResult<IEnumerable<InitiativeEntryResponse>>> Advance(Guid sessionId)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        var entries = session.InitiativeEntries.OrderBy(i => i.SortOrder).ToList();
        if (entries.Count == 0)
        {
            return BadRequest(new { errors = new[] { "Set up initiative before advancing turns." } });
        }

        var currentIndex = entries.FindIndex(i => i.IsCurrentTurn);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        entries[currentIndex].IsCurrentTurn = false;
        entries[(currentIndex + 1) % entries.Count].IsCurrentTurn = true;
        Touch(session);
        await _db.SaveChangesAsync();

        return Ok(entries.Select(ControllerHelpers.ToInitiativeResponse));
    }

    private async Task<GameSession?> GetOwnedSessionAsync(Guid sessionId)
    {
        var userId = this.UserId();
        return await _db.GameSessions
            .Include(s => s.Game)
            .Include(s => s.InitiativeEntries)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Game.DmUserId == userId);
    }

    private static bool TryResolveCombatant(
        CombatantRequest request,
        IEnumerable<Character> characters,
        IEnumerable<NpcOrMonster> npcs,
        out CombatantType type,
        out string name)
    {
        if (request.Type.Equals("Character", StringComparison.OrdinalIgnoreCase))
        {
            var character = characters.FirstOrDefault(c => c.Id == request.Id);
            if (character is not null)
            {
                type = CombatantType.Character;
                name = character.Name;
                return true;
            }
        }

        if (request.Type.Equals("NpcOrMonster", StringComparison.OrdinalIgnoreCase) || request.Type.Equals("Npc", StringComparison.OrdinalIgnoreCase))
        {
            var npc = npcs.FirstOrDefault(n => n.Id == request.Id);
            if (npc is not null)
            {
                type = CombatantType.NpcOrMonster;
                name = npc.Name;
                return true;
            }
        }

        type = CombatantType.Character;
        name = string.Empty;
        return false;
    }

    private static void Touch(GameSession session)
    {
        session.Version++;
        session.UpdatedAt = DateTime.UtcNow;
    }
}
