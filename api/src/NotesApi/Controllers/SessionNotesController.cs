using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;

using Asp.Versioning;
namespace NotesApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api")]
public class SessionNotesController : ControllerBase
{
    private const int MaxNoteLength = 20000;
    private readonly ApplicationDbContext _db;

    public SessionNotesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("sessions/{sessionId:guid}/session-notes")]
    [Authorize]
    public async Task<ActionResult<SessionNotesContextResponse>> GetDmSessionNotes(Guid sessionId)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        return Ok(await BuildContextAsync(session, SessionNoteOwnerKinds.Dm, this.UserId(), allowEditWhenInactive: false));
    }

    [HttpPut("sessions/{sessionId:guid}/session-notes")]
    [Authorize]
    public async Task<ActionResult<SessionNoteResponse>> UpsertDmSessionNotes(Guid sessionId, UpsertSessionNoteRequest request)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        if (!session.IsActive)
        {
            return BadRequest(new { errors = new[] { "Session notes can only be edited while the session is active. Edit past notes from the game dashboard." } });
        }

        return await UpsertNoteAsync(session, SessionNoteOwnerKinds.Dm, this.UserId(), request.Content, canEdit: true);
    }

    [HttpGet("games/{gameId:guid}/session-notes")]
    [Authorize]
    public async Task<ActionResult<GameSessionNotesResponse>> GetDmGameNotes(Guid gameId)
    {
        var game = await _db.GetOwnedGameAsync(gameId, this.UserId());
        if (game is null)
        {
            return NotFound();
        }

        var ownerId = this.UserId();
        var sessions = await _db.GameSessions
            .AsNoTracking()
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        var notes = await _db.SessionNotes
            .AsNoTracking()
            .Where(n => n.Session.GameId == gameId
                && n.OwnerKind == SessionNoteOwnerKinds.Dm
                && n.OwnerId == ownerId)
            .ToListAsync();

        var notesBySession = notes.ToDictionary(n => n.SessionId);

        return Ok(new GameSessionNotesResponse
        {
            GameId = gameId,
            Notes = sessions
                .Where(s => notesBySession.ContainsKey(s.Id))
                .Select(s => ToNoteResponse(notesBySession[s.Id], s, canEdit: true)),
        });
    }

    [HttpPut("games/{gameId:guid}/sessions/{sessionId:guid}/session-notes")]
    [Authorize]
    public async Task<ActionResult<SessionNoteResponse>> UpsertDmGameSessionNote(
        Guid gameId,
        Guid sessionId,
        UpsertSessionNoteRequest request)
    {
        var game = await _db.GetOwnedGameAsync(gameId, this.UserId());
        if (game is null)
        {
            return NotFound();
        }

        var session = game.Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session is null)
        {
            return NotFound();
        }

        return await UpsertNoteAsync(session, SessionNoteOwnerKinds.Dm, this.UserId(), request.Content, canEdit: true);
    }

    [HttpGet("session-join/{joinCode}/session-notes")]
    public async Task<ActionResult<SessionNotesContextResponse>> GetPlayerSessionNotes(string joinCode)
    {
        var session = await GetSessionByJoinCodeAsync(joinCode);
        if (session is null)
        {
            return NotFound();
        }

        var participant = await GetParticipantAsync(session.GameId);
        if (participant is null)
        {
            return Unauthorized(new { errors = new[] { "Join the session before reading session notes." } });
        }

        return Ok(await BuildContextAsync(
            session,
            SessionNoteOwnerKinds.Player,
            participant.Id.ToString(),
            allowEditWhenInactive: false));
    }

    [HttpPut("session-join/{joinCode}/session-notes")]
    public async Task<ActionResult<SessionNoteResponse>> UpsertPlayerSessionNotes(string joinCode, UpsertSessionNoteRequest request)
    {
        var session = await GetSessionByJoinCodeAsync(joinCode);
        if (session is null)
        {
            return NotFound();
        }

        var participant = await GetParticipantAsync(session.GameId);
        if (participant is null)
        {
            return Unauthorized(new { errors = new[] { "Join the session before saving session notes." } });
        }

        if (!session.IsActive)
        {
            return BadRequest(new { errors = new[] { "Session notes can only be edited while the session is active." } });
        }

        return await UpsertNoteAsync(session, SessionNoteOwnerKinds.Player, participant.Id.ToString(), request.Content, canEdit: true);
    }

    private async Task<SessionNotesContextResponse> BuildContextAsync(
        GameSession session,
        string ownerKind,
        string ownerId,
        bool allowEditWhenInactive)
    {
        var currentNoteEntity = await _db.SessionNotes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.SessionId == session.Id && n.OwnerKind == ownerKind && n.OwnerId == ownerId);

        var previousSessions = await _db.GameSessions
            .AsNoTracking()
            .Where(s => s.GameId == session.GameId && s.Id != session.Id)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        var previousSessionIds = previousSessions.Select(s => s.Id).ToList();
        var previousNoteEntities = previousSessionIds.Count == 0
            ? []
            : await _db.SessionNotes
                .AsNoTracking()
                .Where(n => previousSessionIds.Contains(n.SessionId) && n.OwnerKind == ownerKind && n.OwnerId == ownerId)
                .ToListAsync();

        var previousBySession = previousNoteEntities.ToDictionary(n => n.SessionId);
        var previousNotes = previousSessions
            .Where(s => previousBySession.ContainsKey(s.Id))
            .Select(s => ToNoteResponse(previousBySession[s.Id], s, canEdit: allowEditWhenInactive))
            .ToList();

        var canEditCurrent = allowEditWhenInactive || session.IsActive;

        return new SessionNotesContextResponse
        {
            SessionId = session.Id,
            IsSessionActive = session.IsActive,
            CurrentNote = currentNoteEntity is null && !canEditCurrent
                ? null
                : currentNoteEntity is null
                    ? new SessionNoteResponse
                    {
                        Id = Guid.Empty,
                        SessionId = session.Id,
                        Content = string.Empty,
                        CreatedAt = session.StartedAt,
                        UpdatedAt = session.StartedAt,
                        SessionStartedAt = session.StartedAt,
                        SessionEndedAt = session.EndedAt,
                        SessionIsActive = session.IsActive,
                        CanEdit = canEditCurrent,
                    }
                    : ToNoteResponse(currentNoteEntity, session, canEditCurrent),
            PreviousNotes = previousNotes,
        };
    }

    private async Task<ActionResult<SessionNoteResponse>> UpsertNoteAsync(
        GameSession session,
        string ownerKind,
        string ownerId,
        string content,
        bool canEdit)
    {
        if (!canEdit)
        {
            return BadRequest(new { errors = new[] { "This session note cannot be edited." } });
        }

        var trimmed = content?.Trim() ?? string.Empty;
        if (trimmed.Length > MaxNoteLength)
        {
            return BadRequest(new { errors = new[] { $"Session notes cannot exceed {MaxNoteLength} characters." } });
        }

        var now = DateTime.UtcNow;
        var note = await _db.SessionNotes
            .FirstOrDefaultAsync(n => n.SessionId == session.Id && n.OwnerKind == ownerKind && n.OwnerId == ownerId);

        if (note is null)
        {
            note = new SessionNote
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                OwnerKind = ownerKind,
                OwnerId = ownerId,
                Content = trimmed,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.SessionNotes.Add(note);
        }
        else
        {
            note.Content = trimmed;
            note.UpdatedAt = now;
        }

        session.UpdatedAt = now;
        await _db.SaveChangesAsync();

        return Ok(ToNoteResponse(note, session, canEdit: true));
    }

    private static SessionNoteResponse ToNoteResponse(SessionNote note, GameSession session, bool canEdit) => new()
    {
        Id = note.Id,
        SessionId = note.SessionId,
        Content = note.Content,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt,
        SessionStartedAt = session.StartedAt,
        SessionEndedAt = session.EndedAt,
        SessionIsActive = session.IsActive,
        CanEdit = canEdit,
    };

    private async Task<GameSession?> GetOwnedSessionAsync(Guid sessionId)
    {
        var userId = this.UserId();
        return await _db.GameSessions
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Game.DmUserId == userId);
    }

    private async Task<GameSession?> GetSessionByJoinCodeAsync(string joinCode) =>
        await _db.GameSessions
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode);

    private async Task<GameParticipant?> GetParticipantAsync(Guid gameId)
    {
        var token = Request.Headers["X-Player-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return await _db.GameParticipants
            .Include(p => p.Character)
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.JoinToken == token);
    }
}
