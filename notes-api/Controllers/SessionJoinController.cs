using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Rulesets;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/session-join")]
public class SessionJoinController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SessionJoinController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{joinCode}")]
    public async Task<ActionResult<SessionJoinOptionsResponse>> GetSession(string joinCode)
    {
        var session = await _db.GameSessions
            .AsNoTracking()
            .Include(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(s => s.Game).ThenInclude(g => g.Characters)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode && s.IsActive);

        if (session is null)
        {
            return NotFound();
        }

        var claimedCharacterIds = await _db.GameParticipants
            .AsNoTracking()
            .Where(p => p.GameId == session.GameId && p.LastSeenAt >= session.StartedAt)
            .Select(p => p.CharacterId)
            .ToListAsync();

        return Ok(new SessionJoinOptionsResponse
        {
            Session = this.ToSessionSummaryResponse(session),
            Ruleset = ControllerHelpers.ToRulesetDetailResponse(session.Game.Ruleset),
            AvailableCharacters = session.Game.Characters
                .Where(c => !claimedCharacterIds.Contains(c.Id))
                .OrderBy(c => c.Name)
                .Select(ControllerHelpers.ToCharacterResponse),
        });
    }

    [HttpPost("{joinCode}")]
    public async Task<ActionResult<JoinGameResponse>> JoinSession(string joinCode, JoinGameRequest request)
    {
        var session = await _db.GameSessions
            .Include(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(s => s.Game).ThenInclude(g => g.Characters)
            .Include(s => s.Game).ThenInclude(g => g.NpcsAndMonsters)
            .Include(s => s.Game).ThenInclude(g => g.Sessions)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode && s.IsActive);

        if (session is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        Character? character;

        if (request.CharacterId.HasValue)
        {
            character = await _db.Characters.FirstOrDefaultAsync(c => c.GameId == session.GameId && c.Id == request.CharacterId.Value);
            if (character is null)
            {
                return BadRequest(new { errors = new[] { "Character was not found in this game." } });
            }

            var isInSession = await _db.GameParticipants.AnyAsync(p =>
                p.GameId == session.GameId
                && p.CharacterId == character.Id
                && p.LastSeenAt >= session.StartedAt);
            if (isInSession)
            {
                return BadRequest(new { errors = new[] { "That character is already in the session." } });
            }
        }
        else
        {
            var normalizedName = request.CharacterName.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return BadRequest(new { errors = new[] { "Choose an existing character or enter a new character name." } });
            }

            character = await _db.Characters.FirstOrDefaultAsync(c => c.GameId == session.GameId && c.Name == normalizedName);
            if (character is not null)
            {
                return BadRequest(new { errors = new[] { "That character already exists. Select it from the character list if it is available." } });
            }

            var classKey = request.ClassKey.Trim();
            if (!RulesetCharacterBuilder.ClassExists(session.Game.Ruleset.DefinitionJson, classKey))
            {
                return BadRequest(new { errors = new[] { "Selected class is not available for this ruleset." } });
            }

            character = new Character
            {
                Id = Guid.NewGuid(),
                GameId = session.GameId,
                Name = normalizedName,
                PlayerName = string.IsNullOrWhiteSpace(request.PlayerName) ? normalizedName : request.PlayerName.Trim(),
                Health = 10,
                MaxHealth = 10,
                Armor = 0,
                AttributesJson = "{}",
                SkillsJson = "{}",
                InventoryJson = "[]",
                RulesetDataJson = RulesetCharacterBuilder.BuildRulesetDataJson(session.Game.Ruleset.CharacterTemplateJson, classKey),
                ClassKey = classKey,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Characters.Add(character);
            await _db.SaveChangesAsync();
        }

        // Reuse an older game participant when the same character enters this
        // live session, but prevent another joiner from claiming active ones.
        var participant = await _db.GameParticipants
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.CharacterId == character.Id);

        if (participant is null)
        {
            participant = new GameParticipant
            {
                Id = Guid.NewGuid(),
                GameId = session.GameId,
                CharacterId = character.Id,
                DisplayName = character.Name,
                JoinToken = ControllerHelpers.NewToken(),
                CreatedAt = now,
                LastSeenAt = now,
            };
            _db.GameParticipants.Add(participant);
        }
        else
        {
            participant.LastSeenAt = now;
        }

        await _db.SaveChangesAsync();

        return Ok(new JoinGameResponse
        {
            ParticipantToken = participant.JoinToken,
            Character = ControllerHelpers.ToCharacterResponse(character),
            Game = this.ToGameResponse(session.Game),
        });
    }

    [HttpGet("{joinCode}/state")]
    public async Task<ActionResult<SessionStateResponse>> GetState(string joinCode, int sinceSequence = 0)
    {
        var session = await _db.GameSessions
            .Include(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(s => s.Game).ThenInclude(g => g.Characters)
            .Include(s => s.Game).ThenInclude(g => g.NpcsAndMonsters)
            .Include(s => s.Game).ThenInclude(g => g.Sessions)
            .Include(s => s.Actions).ThenInclude(a => a.Resolution)
            .Include(s => s.InitiativeEntries)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode && s.IsActive);

        if (session is null)
        {
            return NotFound();
        }

        var participant = await GetParticipantAsync(session.GameId);
        if (participant is null)
        {
            return Unauthorized(new { errors = new[] { "Join the session before reading player state." } });
        }

        var summary = this.ToSessionSummaryResponse(session);
        return Ok(new SessionStateResponse
        {
            Id = summary.Id,
            GameId = summary.GameId,
            JoinCode = summary.JoinCode,
            JoinUrl = summary.JoinUrl,
            IsActive = summary.IsActive,
            State = summary.State,
            Version = summary.Version,
            StartedAt = summary.StartedAt,
            EndedAt = summary.EndedAt,
            UpdatedAt = summary.UpdatedAt,
            Game = this.ToGameResponse(
                session.Game,
                ControllerHelpers.ParseNpcVisibilities(session.NpcVisibilitiesJson),
                playerView: true),
            Character = ControllerHelpers.ToCharacterResponse(participant.Character),
            Actions = session.Actions
                .Where(a => a.Sequence > sinceSequence)
                .OrderBy(a => a.Sequence)
                .Select(ControllerHelpers.ToActionResponse),
            Initiative = session.InitiativeEntries.OrderBy(i => i.SortOrder).Select(ControllerHelpers.ToInitiativeResponse),
        });
    }

    private async Task<GameParticipant?> GetParticipantAsync(Guid gameId)
    {
        var token = Request.Headers["X-Player-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var participant = await _db.GameParticipants
            .Include(p => p.Character)
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.JoinToken == token);

        if (participant is not null)
        {
            // Only persist LastSeenAt if it is more than 30 seconds stale to
            // avoid a DB write on every polling request.
            if ((DateTime.UtcNow - participant.LastSeenAt).TotalSeconds > 30)
            {
                participant.LastSeenAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        return participant;
    }
}
