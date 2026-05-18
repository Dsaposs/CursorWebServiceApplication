using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Controllers;

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
    public async Task<ActionResult<SessionSummaryResponse>> StartSession(Guid gameId, StartSessionRequest request)
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

        session.State = state;
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
            .Include(s => s.InitiativeEntries)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Game.DmUserId == userId);
    }

    private SessionStateResponse ToSessionState(GameSession session, Character? character, int sinceSequence)
    {
        var summary = this.ToSessionSummaryResponse(session);
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
            Game = this.ToGameResponse(session.Game),
            Character = character is null ? null : ControllerHelpers.ToCharacterResponse(character),
            Actions = session.Actions
                .Where(a => a.Sequence > sinceSequence)
                .OrderBy(a => a.Sequence)
                .Select(ControllerHelpers.ToActionResponse),
            Initiative = session.InitiativeEntries.OrderBy(i => i.SortOrder).Select(ControllerHelpers.ToInitiativeResponse),
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
