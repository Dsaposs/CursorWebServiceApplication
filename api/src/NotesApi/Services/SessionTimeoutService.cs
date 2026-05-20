using Microsoft.EntityFrameworkCore;
using NotesApi.Data;

namespace NotesApi.Services;

/// <summary>
/// Background service that automatically ends active sessions under two conditions:
///
///   1. Browser inactivity (15 min): No player has polled in 15 minutes AND the
///      session's UpdatedAt is older than 15 minutes (DM has made no changes).
///      This approximates "nobody has the session open" from the server's perspective.
///
///   2. Action inactivity (30 min): No new action has been submitted to the session
///      in 30 minutes, even if browsers are still open and polling.
/// </summary>
public sealed class SessionTimeoutService : BackgroundService
{
    private static readonly TimeSpan BrowserIdleTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ActionIdleTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionTimeoutService> _logger;

    public SessionTimeoutService(IServiceScopeFactory scopeFactory, ILogger<SessionTimeoutService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger the first check so startup queries don't overlap with seeding.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await EndInactiveSessionsAsync(stoppingToken);
            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task EndInactiveSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;
            var browserCutoff = now - BrowserIdleTimeout;
            var actionCutoff = now - ActionIdleTimeout;

            // Condition 1: Browser idle — no player polling AND no DM session updates in 15 min.
            // GameParticipant.LastSeenAt is updated every ~30 s while a player tab is open.
            // session.UpdatedAt is bumped whenever the DM makes a change (resolve, advance turn, etc.).
            var browserIdleSessions = await db.GameSessions
                .Where(s => s.IsActive && s.UpdatedAt < browserCutoff)
                .Where(s => !db.GameParticipants
                    .Any(p => p.GameId == s.GameId && p.LastSeenAt >= browserCutoff))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            // Condition 2: Action idle — no action has been submitted to the session in 30 min.
            // New sessions with no actions yet use StartedAt as the baseline to avoid
            // instantly timing out empty sessions before a first action is submitted.
            var actionIdleSessions = await db.GameSessions
                .Where(s => s.IsActive && s.StartedAt < actionCutoff)
                .Where(s => !db.ActionRequests
                    .Any(a => a.SessionId == s.Id && a.SubmittedAt >= actionCutoff))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            var toEndIds = browserIdleSessions
                .Union(actionIdleSessions)
                .Distinct()
                .ToHashSet();

            if (toEndIds.Count == 0) return;

            var sessionsToEnd = await db.GameSessions
                .Where(s => toEndIds.Contains(s.Id))
                .ToListAsync(cancellationToken);

            foreach (var session in sessionsToEnd)
            {
                var reason = browserIdleSessions.Contains(session.Id) && actionIdleSessions.Contains(session.Id)
                    ? "browser-idle and action-idle"
                    : browserIdleSessions.Contains(session.Id)
                        ? "browser-idle"
                        : "action-idle";

                session.IsActive = false;
                session.EndedAt = now;
                session.UpdatedAt = now;
                session.Version++;

                _logger.LogInformation(
                    "Session timeout: ended session {SessionId} ({Reason}).",
                    session.Id,
                    reason);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session timeout check failed.");
        }
    }
}
