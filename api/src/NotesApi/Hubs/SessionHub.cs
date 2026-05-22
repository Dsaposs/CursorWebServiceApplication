using Microsoft.AspNetCore.SignalR;
using NotesApi.Data;
using NotesApi.Services;
using Microsoft.EntityFrameworkCore;

namespace NotesApi.Hubs;

/// <summary>
/// SignalR hub for real-time session events.
/// Clients join a session group on connect by passing their join code and optional player token.
/// DM clients authenticate via JWT.
///
/// Client groups:
/// - "session:{sessionId}" — all participants including DM
/// - "dm:{sessionId}" — DM only
/// - "player:{participantToken}" — individual player
/// </summary>
public class SessionHub : Hub
{
    private readonly ApplicationDbContext _db;

    public SessionHub(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Player calls this after connecting, providing their session join code and participant token.</summary>
    public async Task JoinSessionAsPlayer(string joinCode, string participantToken)
    {
        var session = await _db.GameSessions
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode && s.IsActive);

        if (session is null) return;

        var participant = await _db.GameParticipants
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.JoinToken == participantToken);

        if (participant is null) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{session.Id}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"player:{participantToken}");
    }

    /// <summary>DM calls this after connecting (JWT is validated by hub auth middleware).</summary>
    public async Task JoinSessionAsDm(string joinCode)
    {
        if (Context.User?.Identity?.IsAuthenticated != true) return;

        var session = await _db.GameSessions
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode && s.IsActive);

        if (session is null) return;

        var userId = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId != session.Game.DmUserId) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{session.Id}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dm:{session.Id}:{userId}");
    }

    /// <summary>Client leaves a session group.</summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session:{sessionId}");
    }
}

/// <summary>
/// SignalR implementation of IActionBroadcaster — replaces the Phase 2 no-op.
/// Registered in DI when SignalR hub is available.
/// </summary>
public class SignalRActionBroadcaster : IActionBroadcaster
{
    private readonly IHubContext<SessionHub> _hub;

    public SignalRActionBroadcaster(IHubContext<SessionHub> hub)
    {
        _hub = hub;
    }

    public Task BroadcastToSessionAsync(Guid sessionId, string eventName, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group($"session:{sessionId}").SendAsync(eventName, payload, ct);

    public Task BroadcastToDmAsync(Guid sessionId, string dmUserId, string eventName, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group($"dm:{sessionId}:{dmUserId}").SendAsync(eventName, payload, ct);

    public Task BroadcastToParticipantAsync(Guid sessionId, string participantToken, string eventName, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group($"player:{participantToken}").SendAsync(eventName, payload, ct);
}
