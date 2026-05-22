using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Rulesets;
using NotesApi.Services;

using Asp.Versioning;
namespace NotesApi.Controllers;

[ApiVersion("1.0")]
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

    [HttpPost("start")]
    public async Task<ActionResult<CombatStartResponse>> Start(Guid sessionId)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        var characters = await _db.Characters.Where(c => c.GameId == session.GameId).ToListAsync();
        var npcs = await _db.NpcsAndMonsters.Where(n => n.GameId == session.GameId).ToListAsync();
        var rolls = CombatInitiativeRoller
            .RollForGame(session.Game.Ruleset.DefinitionJson, characters, npcs)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Type)
            .ToList();

        var combatants = rolls.Select(r => new CombatantRequest
        {
            Type = r.Type == CombatantType.Character ? "Character" : "NpcOrMonster",
            Id = r.Id,
            Initiative = r.Score,
        });

        var entries = await ApplyInitiativeOrderAsync(session, combatants, preserveCurrentTurn: false);
        var initiativeDef = CombatInitiativeRoller.ResolveInitiativeDefinition(session.Game.Ruleset.DefinitionJson);

        return Ok(new CombatStartResponse
        {
            Initiative = entries ?? Enumerable.Empty<InitiativeEntryResponse>(),
            Rolls = rolls.Select(r => new InitiativeRollSummaryResponse
            {
                CombatantType = r.Type.ToString(),
                CombatantId = r.Id,
                CombatantName = r.Name,
                Score = r.Score,
                Summary = r.Summary,
            }),
            GuidanceText = initiativeDef.GuidanceText,
        });
    }

    [HttpPost]
    public async Task<ActionResult<IEnumerable<InitiativeEntryResponse>>> Setup(Guid sessionId, SetupCombatRequest request)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        var entries = await ApplyInitiativeOrderAsync(session, request.Combatants, preserveCurrentTurn: true);
        if (entries is null)
        {
            return BadRequest(new { errors = new[] { "One or more combatants were not found in this game." } });
        }

        return Ok(entries);
    }

    /// <summary>
    /// DM explicitly prompts a character to take their turn action, opening the action form on the player's device.
    /// </summary>
    [HttpPost("prompt-turn")]
    public async Task<IActionResult> PromptTurn(Guid sessionId, [FromBody] PromptTurnRequest request)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null) return NotFound();

        if (session.State != SessionMode.Combat)
            return BadRequest(new { errors = new[] { "Session is not in combat." } });

        var encounter = session.ActiveCombatEncounterId.HasValue
            ? await _db.Set<CombatEncounter>().FindAsync(session.ActiveCombatEncounterId.Value)
            : null;

        if (encounter is null)
            return BadRequest(new { errors = new[] { "No active combat encounter." } });

        // Validate the character is the current turn's combatant.
        var currentEntry = await _db.InitiativeEntries
            .Where(i => i.SessionId == sessionId && i.IsCurrentTurn)
            .FirstOrDefaultAsync();

        if (currentEntry is null
            || currentEntry.CombatantType != CombatantType.Character
            || currentEntry.CombatantId != request.CharacterId)
        {
            return BadRequest(new { errors = new[] { "It is not that character's turn." } });
        }

        encounter.PromptedTurnCharacterId = request.CharacterId;
        Touch(session);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("advance")]
    public async Task<ActionResult<IEnumerable<InitiativeEntryResponse>>> Advance(Guid sessionId)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        var entries = await _db.InitiativeEntries
            .Where(i => i.SessionId == sessionId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        if (entries.Count == 0)
        {
            return BadRequest(new { errors = new[] { "Set up initiative before advancing turns." } });
        }

        var encounter = session.ActiveCombatEncounterId.HasValue
            ? await _db.Set<NotesApi.Models.CombatEncounter>().FindAsync(session.ActiveCombatEncounterId.Value)
            : null;
        CombatTurnAdvanceService.AdvanceTurn(entries, encounter);
        Touch(session);
        await _db.SaveChangesAsync();

        return Ok(entries.Select(ControllerHelpers.ToInitiativeResponse));
    }

    private async Task<IEnumerable<InitiativeEntryResponse>?> ApplyInitiativeOrderAsync(
        GameSession session,
        IEnumerable<CombatantRequest> combatants,
        bool preserveCurrentTurn)
    {
        var sessionId = session.Id;
        var enteringCombat = session.State != SessionMode.Combat;
        var currentCombatantId = preserveCurrentTurn
            ? session.InitiativeEntries.FirstOrDefault(i => i.IsCurrentTurn)?.CombatantId
            : null;

        await _db.InitiativeEntries
            .Where(i => i.SessionId == sessionId)
            .ExecuteDeleteAsync();

        foreach (var e in _db.ChangeTracker.Entries<InitiativeEntry>().ToList())
        {
            e.State = EntityState.Detached;
        }

        var characters = await _db.Characters.Where(c => c.GameId == session.GameId).ToListAsync();
        var npcs = await _db.NpcsAndMonsters.Where(n => n.GameId == session.GameId).ToListAsync();
        var now = DateTime.UtcNow;
        var ordered = combatants
            .OrderByDescending(c => c.Initiative)
            .ThenBy(c => c.Type)
            .ToList();

        var hasCurrentCombatant = preserveCurrentTurn
            && currentCombatantId.HasValue
            && ordered.Any(c => c.Id == currentCombatantId.Value);
        var currentId = currentCombatantId.GetValueOrDefault();

        var newEntries = new List<InitiativeEntry>();
        for (var index = 0; index < ordered.Count; index++)
        {
            var combatant = ordered[index];
            if (!TryResolveCombatant(combatant, characters, npcs, out var type, out var name))
            {
                return null;
            }

            newEntries.Add(new InitiativeEntry
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                CombatantType = type,
                CombatantId = combatant.Id,
                CombatantName = name,
                SortOrder = index + 1,
                InitiativeScore = combatant.Initiative,
                IsCurrentTurn = hasCurrentCombatant ? combatant.Id == currentId : index == 0,
                CreatedAt = now,
            });
        }

        _db.InitiativeEntries.AddRange(newEntries);
        session.State = SessionMode.Combat;

        if (enteringCombat)
        {
            await CombatEncounterLifecycle.BeginEncounterAsync(_db, session);
        }
        else
        {
            await CombatEncounterLifecycle.EnsureEncounterForCombatAsync(_db, session);

            // Reordering initiative invalidates any active turn prompt so the player
            // is not shown an unexpected action form after the order changes.
            if (session.ActiveCombatEncounterId.HasValue)
            {
                var activeEncounter = await _db.Set<CombatEncounter>()
                    .FindAsync(session.ActiveCombatEncounterId.Value);
                if (activeEncounter is not null)
                {
                    activeEncounter.PromptedTurnCharacterId = null;
                }
            }
        }

        Touch(session);
        await _db.SaveChangesAsync();

        return newEntries.OrderBy(i => i.SortOrder).Select(ControllerHelpers.ToInitiativeResponse);
    }

    private async Task<GameSession?> GetOwnedSessionAsync(Guid sessionId)
    {
        var userId = this.UserId();
        return await _db.GameSessions
            .Include(s => s.Game).ThenInclude(g => g.Ruleset)
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
