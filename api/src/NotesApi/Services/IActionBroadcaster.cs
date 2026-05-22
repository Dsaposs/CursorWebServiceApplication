namespace NotesApi.Services;

/// <summary>
/// Broadcasts real-time action lifecycle events to session participants.
/// Phase 2: no-op implementation. Phase 3 replaces with a SignalR hub implementation.
/// </summary>
public interface IActionBroadcaster
{
    /// <summary>Send an event to all participants in the given session.</summary>
    Task BroadcastToSessionAsync(Guid sessionId, string eventName, object payload, CancellationToken ct = default);

    /// <summary>Send an event to the DM of the given session only.</summary>
    Task BroadcastToDmAsync(Guid sessionId, string dmUserId, string eventName, object payload, CancellationToken ct = default);

    /// <summary>Send an event to a specific participant by their join token.</summary>
    Task BroadcastToParticipantAsync(Guid sessionId, string participantToken, string eventName, object payload, CancellationToken ct = default);
}

/// <summary>
/// No-op broadcaster used until SignalR is wired in Phase 3.
/// </summary>
public sealed class NoOpActionBroadcaster : IActionBroadcaster
{
    public Task BroadcastToSessionAsync(Guid sessionId, string eventName, object payload, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task BroadcastToDmAsync(Guid sessionId, string dmUserId, string eventName, object payload, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task BroadcastToParticipantAsync(Guid sessionId, string participantToken, string eventName, object payload, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>Well-known event name constants matching the WebSocket Event Catalogue in DESIGN_V2.md §6i.</summary>
public static class ActionEvents
{
    public const string SessionModeChanged = "session.mode_changed";
    public const string TurnOpened = "turn.opened";
    public const string TurnSkipped = "turn.skipped";
    public const string ActionSubmitted = "action.submitted";
    public const string ActionDmReviewing = "action.dm_reviewing";
    public const string ActionRollRequested = "action.roll_requested";
    public const string ActionRollReceived = "action.roll_received";
    public const string ActionReactionRequested = "action.reaction_requested";
    public const string ActionReactionReceived = "action.reaction_received";
    public const string ActionFollowupRollRequested = "action.followup_roll_requested";
    public const string ActionFollowupRollReceived = "action.followup_roll_received";
    public const string ActionResolved = "action.resolved";
    public const string ActionRejected = "action.rejected";
    public const string CharacterStatsUpdated = "character.stats_updated";
    public const string NpcStatsUpdated = "npc.stats_updated";
}
