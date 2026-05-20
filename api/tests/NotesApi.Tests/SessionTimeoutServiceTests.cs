namespace NotesApi.Tests;

using System.Reflection;
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
    public async Task TimeoutCheck_EndsBrowserIdleSessionsButKeepsRecentlySeenParticipantsActive()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        await SeedRulesetAndDmAsync(db);

        var idleSession = AddSessionGraph(
            db,
            name: "Browser Idle",
            joinCode: "browser-idle",
            updatedAt: now.AddMinutes(-20),
            startedAt: now.AddMinutes(-40),
            participantLastSeenAt: now.AddMinutes(-20),
            lastActionSubmittedAt: now.AddMinutes(-5));
        var activeBrowserSession = AddSessionGraph(
            db,
            name: "Browser Active",
            joinCode: "browser-active",
            updatedAt: now.AddMinutes(-20),
            startedAt: now.AddMinutes(-40),
            participantLastSeenAt: now.AddMinutes(-1),
            lastActionSubmittedAt: now.AddMinutes(-5));
        await db.SaveChangesAsync();

        await RunTimeoutCheckAsync(db);

        Assert.False(idleSession.IsActive);
        Assert.NotNull(idleSession.EndedAt);
        Assert.True(idleSession.Version > 0);
        Assert.True(activeBrowserSession.IsActive);
        Assert.Null(activeBrowserSession.EndedAt);
        Assert.Equal(0, activeBrowserSession.Version);
    }

    [Fact]
    public async Task TimeoutCheck_EndsActionIdleSessionsEvenWhenBrowsersAreActive()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        await SeedRulesetAndDmAsync(db);

        var actionIdleSession = AddSessionGraph(
            db,
            name: "Action Idle",
            joinCode: "action-idle",
            updatedAt: now.AddMinutes(-1),
            startedAt: now.AddMinutes(-40),
            participantLastSeenAt: now.AddMinutes(-1),
            lastActionSubmittedAt: now.AddMinutes(-31));
        var recentActionSession = AddSessionGraph(
            db,
            name: "Recent Action",
            joinCode: "recent-action",
            updatedAt: now.AddMinutes(-1),
            startedAt: now.AddMinutes(-40),
            participantLastSeenAt: now.AddMinutes(-1),
            lastActionSubmittedAt: now.AddMinutes(-5));
        var newEmptySession = AddSessionGraph(
            db,
            name: "New Empty Session",
            joinCode: "new-empty",
            updatedAt: now.AddMinutes(-1),
            startedAt: now.AddMinutes(-20),
            participantLastSeenAt: now.AddMinutes(-1),
            lastActionSubmittedAt: null);
        await db.SaveChangesAsync();

        await RunTimeoutCheckAsync(db);

        Assert.False(actionIdleSession.IsActive);
        Assert.NotNull(actionIdleSession.EndedAt);
        Assert.True(actionIdleSession.Version > 0);
        Assert.True(recentActionSession.IsActive);
        Assert.Null(recentActionSession.EndedAt);
        Assert.True(newEmptySession.IsActive);
        Assert.Null(newEmptySession.EndedAt);
    }

    private static async Task RunTimeoutCheckAsync(ApplicationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        await using var provider = services.BuildServiceProvider();
        var timeoutService = new SessionTimeoutService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SessionTimeoutService>.Instance);
        var method = typeof(SessionTimeoutService).GetMethod(
            "EndInactiveSessionsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(timeoutService, [CancellationToken.None]));
        await task;
    }

    private static GameSession AddSessionGraph(
        ApplicationDbContext db,
        string name,
        string joinCode,
        DateTime updatedAt,
        DateTime startedAt,
        DateTime participantLastSeenAt,
        DateTime? lastActionSubmittedAt)
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var createdAt = startedAt.AddMinutes(-5);
        var game = new Game
        {
            Id = gameId,
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = name,
            InviteCode = joinCode,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
        var character = new Character
        {
            Id = characterId,
            GameId = gameId,
            Name = $"{name} Character",
            PlayerName = "Player",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
        var participant = new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            CharacterId = characterId,
            DisplayName = character.Name,
            JoinToken = $"{joinCode}-token",
            CreatedAt = createdAt,
            LastSeenAt = participantLastSeenAt,
        };
        var session = new GameSession
        {
            Id = sessionId,
            GameId = gameId,
            JoinCode = joinCode,
            IsActive = true,
            State = SessionMode.Exploration,
            StartedAt = startedAt,
            UpdatedAt = updatedAt,
        };

        db.AddRange(game, character, participant, session);

        if (lastActionSubmittedAt is DateTime submittedAt)
        {
            session.Actions.Add(new ActionRequest
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ActorCharacterId = characterId,
                ActorName = character.Name,
                ActionText = "checks for danger",
                Sequence = 1,
                SubmittedAt = submittedAt,
            });
        }

        return session;
    }

    private static async Task SeedRulesetAndDmAsync(ApplicationDbContext db)
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

    private static ApplicationDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
