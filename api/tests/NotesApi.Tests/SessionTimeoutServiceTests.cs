namespace NotesApi.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotesApi.Data;
using NotesApi.Models;
using NotesApi.Services;

public class SessionTimeoutServiceTests
{
    [Fact]
    public async Task EndInactiveSessionsAsync_EndsBrowserIdleSessionsButKeepsRecentlyPolledGames()
    {
        await using var context = await CreateServiceContextAsync();
        var now = DateTime.UtcNow;
        var browserIdleSessionId = Guid.NewGuid();
        var recentlyPolledSessionId = Guid.NewGuid();

        await using (var scope = context.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await SeedRequiredDataAsync(db);

            var browserIdleGame = CreateGame("browser-idle-game", "browser-idle");
            var recentlyPolledGame = CreateGame("recently-polled-game", "recently-polled");
            db.AddRange(browserIdleGame, recentlyPolledGame);
            db.AddRange(
                CreateSession(browserIdleSessionId, browserIdleGame.Id, "browser-idle", now.AddMinutes(-20), now.AddMinutes(-16), version: 4),
                CreateSession(recentlyPolledSessionId, recentlyPolledGame.Id, "recently-polled", now.AddMinutes(-20), now.AddMinutes(-16), version: 7));
            AddParticipant(db, recentlyPolledGame.Id, "recent-player", now.AddMinutes(-5));
            await db.SaveChangesAsync();
        }

        await context.CreateService().EndInactiveSessionsAsync(CancellationToken.None);

        await using (var scope = context.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var browserIdleSession = await db.GameSessions.SingleAsync(s => s.Id == browserIdleSessionId);
            var recentlyPolledSession = await db.GameSessions.SingleAsync(s => s.Id == recentlyPolledSessionId);

            Assert.False(browserIdleSession.IsActive);
            Assert.NotNull(browserIdleSession.EndedAt);
            Assert.Equal(5, browserIdleSession.Version);
            Assert.True(browserIdleSession.UpdatedAt >= now);

            Assert.True(recentlyPolledSession.IsActive);
            Assert.Null(recentlyPolledSession.EndedAt);
            Assert.Equal(7, recentlyPolledSession.Version);
        }
    }

    [Fact]
    public async Task EndInactiveSessionsAsync_EndsActionIdleSessionsButKeepsSessionsWithRecentActions()
    {
        await using var context = await CreateServiceContextAsync();
        var now = DateTime.UtcNow;
        var actionIdleSessionId = Guid.NewGuid();
        var recentlyActedSessionId = Guid.NewGuid();

        await using (var scope = context.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await SeedRequiredDataAsync(db);

            var actionIdleGame = CreateGame("action-idle-game", "action-idle");
            var recentlyActedGame = CreateGame("recently-acted-game", "recently-acted");
            db.AddRange(actionIdleGame, recentlyActedGame);

            var actionIdleSession = CreateSession(actionIdleSessionId, actionIdleGame.Id, "action-idle", now.AddMinutes(-31), now.AddMinutes(-1), version: 1);
            var recentlyActedSession = CreateSession(recentlyActedSessionId, recentlyActedGame.Id, "recently-acted", now.AddMinutes(-31), now.AddMinutes(-1), version: 3);
            db.AddRange(actionIdleSession, recentlyActedSession);
            AddParticipant(db, actionIdleGame.Id, "open-browser", now.AddMinutes(-1));
            db.ActionRequests.Add(new ActionRequest
            {
                Id = Guid.NewGuid(),
                SessionId = recentlyActedSession.Id,
                ActorName = "Ripley",
                ActionText = "checks the hallway",
                Sequence = 1,
                SubmittedAt = now.AddMinutes(-5),
            });
            await db.SaveChangesAsync();
        }

        await context.CreateService().EndInactiveSessionsAsync(CancellationToken.None);

        await using (var scope = context.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var actionIdleSession = await db.GameSessions.SingleAsync(s => s.Id == actionIdleSessionId);
            var recentlyActedSession = await db.GameSessions.SingleAsync(s => s.Id == recentlyActedSessionId);

            Assert.False(actionIdleSession.IsActive);
            Assert.NotNull(actionIdleSession.EndedAt);
            Assert.Equal(2, actionIdleSession.Version);
            Assert.True(actionIdleSession.UpdatedAt >= now);

            Assert.True(recentlyActedSession.IsActive);
            Assert.Null(recentlyActedSession.EndedAt);
            Assert.Equal(3, recentlyActedSession.Version);
        }
    }

    [Fact]
    public async Task EndInactiveSessionsAsync_WhenBothIdleCriteriaMatch_EndsSessionOnce()
    {
        await using var context = await CreateServiceContextAsync();
        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();

        await using (var scope = context.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await SeedRequiredDataAsync(db);

            var game = CreateGame("both-idle-game", "both-idle");
            db.Games.Add(game);
            db.GameSessions.Add(CreateSession(sessionId, game.Id, "both-idle", now.AddMinutes(-45), now.AddMinutes(-20), version: 9));
            await db.SaveChangesAsync();
        }

        await context.CreateService().EndInactiveSessionsAsync(CancellationToken.None);

        await using (var scope = context.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var session = await db.GameSessions.SingleAsync(s => s.Id == sessionId);

            Assert.False(session.IsActive);
            Assert.NotNull(session.EndedAt);
            Assert.Equal(10, session.Version);
            Assert.True(session.UpdatedAt >= now);
        }
    }

    private static async Task<TestServiceContext> CreateServiceContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection))
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new TestServiceContext(connection, services);
    }

    private static async Task SeedRequiredDataAsync(ApplicationDbContext db)
    {
        db.Rulesets.Add(new Ruleset
        {
            Code = "alien-rpg",
            DisplayName = "Alien RPG",
            Description = "Test ruleset",
            DiceNotation = "d6",
            CharacterTemplateJson = "{}",
            DefinitionJson = "{}",
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "dm-1",
            UserName = "dm-1@example.local",
            Email = "dm-1@example.local",
        });
        await db.SaveChangesAsync();
    }

    private static Game CreateGame(string name, string inviteCode)
    {
        var now = DateTime.UtcNow;
        return new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = name,
            InviteCode = inviteCode,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static GameSession CreateSession(
        Guid sessionId,
        Guid gameId,
        string joinCode,
        DateTime startedAt,
        DateTime updatedAt,
        int version)
    {
        return new GameSession
        {
            Id = sessionId,
            GameId = gameId,
            JoinCode = joinCode,
            IsActive = true,
            State = SessionMode.Exploration,
            Version = version,
            StartedAt = startedAt,
            UpdatedAt = updatedAt,
        };
    }

    private static void AddParticipant(ApplicationDbContext db, Guid gameId, string displayName, DateTime lastSeenAt)
    {
        var character = new Character
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Name = displayName,
            PlayerName = "Player",
            MaxHealth = 10,
            Health = 10,
            CreatedAt = lastSeenAt,
            UpdatedAt = lastSeenAt,
        };
        db.Characters.Add(character);
        db.GameParticipants.Add(new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            CharacterId = character.Id,
            DisplayName = displayName,
            JoinToken = $"{displayName}-token",
            CreatedAt = lastSeenAt,
            LastSeenAt = lastSeenAt,
        });
    }

    private sealed class TestServiceContext : IAsyncDisposable
    {
        public TestServiceContext(SqliteConnection connection, ServiceProvider services)
        {
            Connection = connection;
            Services = services;
        }

        private SqliteConnection Connection { get; }

        public ServiceProvider Services { get; }

        public SessionTimeoutService CreateService()
        {
            return new SessionTimeoutService(
                Services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<SessionTimeoutService>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
