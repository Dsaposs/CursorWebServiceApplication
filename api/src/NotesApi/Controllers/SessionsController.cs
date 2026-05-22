using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Services;

using Asp.Versioning;
namespace NotesApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SessionsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPost("games/{gameId:guid}/sessions")]
    public async Task<ActionResult<SessionSummaryResponse>> StartSession(Guid gameId)
    {
        var game = await _db.GetOwnedGameAsync(gameId, this.UserId());
        if (game is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = await NewUniqueSessionCodeAsync(),
            IsActive = true,
            State = SessionMode.Exploration,
            Version = 1,
            StartedAt = now,
            UpdatedAt = now,
        };

        _db.GameSessions.Add(session);
        game.UpdatedAt = now;
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDmSession), new { sessionId = session.Id }, this.ToSessionSummaryResponse(session));
    }

    [HttpGet("sessions/{sessionId:guid}/dm")]
    public async Task<ActionResult<SessionStateResponse>> GetDmSession(Guid sessionId, int sinceSequence = 0)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        return session is null ? NotFound() : Ok(ToSessionState(session, null, sinceSequence));
    }

    [HttpPost("sessions/{sessionId:guid}/stop")]
    public async Task<ActionResult<SessionSummaryResponse>> StopSession(Guid sessionId)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        session.IsActive = false;
        session.EndedAt = DateTime.UtcNow;
        Touch(session);
        await _db.SaveChangesAsync();

        return Ok(this.ToSessionSummaryResponse(session));
    }

    [HttpPost("sessions/{sessionId:guid}/npc-visibility")]
    public async Task<ActionResult> SetNpcVisibility(Guid sessionId, SetNpcVisibilityRequest request)
    {
        var userId = this.UserId();
        var session = await _db.GameSessions
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Game.DmUserId == userId);

        if (session is null)
        {
            return NotFound();
        }

        if (!new[] { ControllerHelpers.NpcVisibilityVisible, ControllerHelpers.NpcVisibilityHidden }.Contains(request.Visibility))
        {
            return BadRequest(new { errors = new[] { "Visibility must be Visible or Hidden." } });
        }

        var visibilities = ControllerHelpers.ParseNpcVisibilities(session.NpcVisibilitiesJson);
        var key = request.NpcId.ToString();

        if (request.Visibility == ControllerHelpers.NpcVisibilityHidden)
            visibilities.Remove(key);
        else
            visibilities[key] = ControllerHelpers.NpcVisibilityVisible;

        session.NpcVisibilitiesJson = System.Text.Json.JsonSerializer.Serialize(visibilities);
        Touch(session);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("sessions/{sessionId:guid}/roll-prompts")]
    public async Task<ActionResult<IEnumerable<RollPromptResponse>>> CreateSessionRollPrompts(
        Guid sessionId,
        CreateSessionRollPromptsRequest request)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        if (!session.IsActive)
        {
            return BadRequest(new { errors = new[] { "Roll prompts can only be sent during an active session." } });
        }

        var prompts = request.Prompts?.ToList() ?? [];
        if (prompts.Count == 0)
        {
            return BadRequest(new { errors = new[] { "At least one roll prompt is required." } });
        }

        var created = new List<SessionRollPrompt>();
        var now = DateTime.UtcNow;
        var batchId = Guid.NewGuid();

        foreach (var item in prompts)
        {
            var character = session.Game.Characters.FirstOrDefault(c => c.Id == item.TargetCharacterId);
            if (character is null)
            {
                return BadRequest(new { errors = new[] { "Target character was not found in this game." } });
            }

            if (!RollPromptValidator.TryNormalizeCheckMode(item.CheckMode, out var checkMode))
            {
                return BadRequest(new { errors = new[] { "CheckMode must be Action, Skill, Attribute, or Custom." } });
            }

            if (!RollPromptValidator.TryNormalizeResultKind(item.ResultKind, out var resultKind))
            {
                return BadRequest(new { errors = new[] { "ResultKind must be PassFail or Total." } });
            }

            var validationError = RollPromptValidator.ValidateCheck(
                checkMode,
                item,
                session.Game.Ruleset.DefinitionJson,
                character.ClassKey);
            if (validationError is not null)
            {
                return BadRequest(new { errors = new[] { validationError } });
            }

            var prompt = new SessionRollPrompt
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                TargetCharacterId = character.Id,
                TargetCharacter = character,
                PromptLabel = string.IsNullOrWhiteSpace(item.PromptLabel) ? null : item.PromptLabel.Trim(),
                GuidanceText = string.IsNullOrWhiteSpace(item.GuidanceText) ? null : item.GuidanceText.Trim(),
                CheckMode = checkMode,
                ResultKind = resultKind,
                ActionKey = string.IsNullOrWhiteSpace(item.ActionKey) ? null : item.ActionKey.Trim(),
                SkillKey = string.IsNullOrWhiteSpace(item.SkillKey) ? null : item.SkillKey.Trim(),
                AttributeKey = string.IsNullOrWhiteSpace(item.AttributeKey) ? null : item.AttributeKey.Trim(),
                CustomCheckText = string.IsNullOrWhiteSpace(item.CustomCheckText) ? null : item.CustomCheckText.Trim(),
                Status = RollPromptStatus.Pending,
                SkillCheckBatchId = batchId,
                CreatedAt = now,
            };
            _db.SessionRollPrompts.Add(prompt);
            created.Add(prompt);
        }

        Touch(session);
        await _db.SaveChangesAsync();

        return Ok(created.Select(ControllerHelpers.ToSessionRollPromptResponse));
    }

    [HttpPost("sessions/{sessionId:guid}/state")]
    public async Task<ActionResult<SessionSummaryResponse>> ChangeState(Guid sessionId, ChangeSessionStateRequest request)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        if (!Enum.TryParse<SessionMode>(request.State, ignoreCase: true, out var state))
        {
            return BadRequest(new { errors = new[] { "State must be Exploration or Combat." } });
        }

        if (state == SessionMode.Combat)
        {
            await CombatEncounterLifecycle.EnsureEncounterForCombatAsync(_db, session);
            session.State = SessionMode.Combat;
        }
        else
        {
            await CombatEncounterLifecycle.EndActiveEncounterAsync(_db, session);
            session.State = SessionMode.Exploration;
        }

        Touch(session);
        await _db.SaveChangesAsync();

        return Ok(this.ToSessionSummaryResponse(session));
    }

    private async Task<GameSession?> GetOwnedSessionAsync(Guid sessionId)
    {
        var userId = this.UserId();
        return await _db.GameSessions
            .Include(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(s => s.Game).ThenInclude(g => g.Characters)
            .Include(s => s.Game).ThenInclude(g => g.NpcsAndMonsters)
            .Include(s => s.Actions).ThenInclude(a => a.Resolution)
            .Include(s => s.Actions).ThenInclude(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .Include(s => s.Actions).ThenInclude(a => a.CombatEncounter)
            .Include(s => s.CombatEncounters)
            .Include(s => s.InitiativeEntries)
            .Include(s => s.SessionRollPrompts).ThenInclude(p => p.TargetCharacter)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Game.DmUserId == userId);
    }

    private SessionStateResponse ToSessionState(GameSession session, Character? character, int sinceSequence)
    {
        var summary = this.ToSessionSummaryResponse(session);
        var skillCheckActionIds = session.SessionRollPrompts
            .Where(p => p.ActionRequestId.HasValue)
            .Select(p => p.ActionRequestId!.Value)
            .ToHashSet();

        return new SessionStateResponse
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
            Game = this.ToGameResponse(session.Game, ControllerHelpers.ParseNpcVisibilities(session.NpcVisibilitiesJson)),
            Character = character is null ? null : ControllerHelpers.ToCharacterResponse(character),
            Actions = session.Actions
                .Where(a => a.Sequence > sinceSequence)
                .OrderBy(a => a.Sequence)
                .Select(a => ControllerHelpers.ToActionResponse(a, skillCheckActionIds.Contains(a.Id))),
            Initiative = session.InitiativeEntries.OrderBy(i => i.SortOrder).Select(ControllerHelpers.ToInitiativeResponse),
            RollPrompts = ControllerHelpers.SelectRollPrompts(session),
            CombatEncounters = ControllerHelpers.SelectCombatEncounters(session),
        };
    }

    private async Task<string> NewUniqueSessionCodeAsync()
    {
        string code;
        do
        {
            code = ControllerHelpers.NewCode();
        }
        while (await _db.GameSessions.AnyAsync(s => s.JoinCode == code));

        return code;
    }

    private static void Touch(GameSession session)
    {
        session.Version++;
        session.UpdatedAt = DateTime.UtcNow;
    }
}
