using Microsoft.EntityFrameworkCore;
using NotesApi.Data;

namespace NotesApi.Services;

/// <summary>
/// Background service that automatically ends active sessions that have had
/// no meaningful activity for <see cref="InactivityTimeout"/>.
///
/// A session is considered inactive when both conditions hold:
///   1. The session's UpdatedAt timestamp is older than the cutoff (no DM actions).
///   2. No game participant in that session's game has been seen since the cutoff
///      (no players actively polling).
/// </summary>
public sealed class SessionTimeoutService : BackgroundService
{
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

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

            var cutoff = DateTime.UtcNow - InactivityTimeout;

            // Active sessions with no DM activity since the cutoff AND
            // no player recently polling (LastSeenAt updated every ≤30 s while a
            // player tab is open).
            var staleSessions = await db.GameSessions
                .Where(s => s.IsActive && s.UpdatedAt < cutoff)
                .Where(s => !db.GameParticipants
                    .Any(p => p.GameId == s.GameId && p.LastSeenAt >= cutoff))
                .ToListAsync(cancellationToken);

            if (staleSessions.Count == 0) return;

            var now = DateTime.UtcNow;
            foreach (var session in staleSessions)
            {
                session.IsActive = false;
                session.EndedAt = now;
                session.UpdatedAt = now;
                session.Version++;
            }

            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Session timeout: ended {Count} inactive session(s) (idle >{Minutes} min).",
                staleSessions.Count,
                InactivityTimeout.TotalMinutes);
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
