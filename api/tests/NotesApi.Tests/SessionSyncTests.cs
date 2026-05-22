namespace NotesApi.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NotesApi.Controllers;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using System.Security.Claims;

public class SessionSyncTests
{
    [Fact]
    public async Task GetSessionVersion_ReturnsCurrentVersionForDm()
    {
        await using var db = CreateDbContext();
        var (session, _) = await SeedSessionAsync(db);

        var controller = CreateSessionsController(db, "dm-1");
        var result = await controller.GetSessionVersion(session.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SessionVersionResponse>(ok.Value);
        Assert.Equal(session.Version, response.Version);
    }

    [Fact]
    public async Task GetSessionLive_ReturnsIncrementalActionsWithoutRuleset()
    {
        await using var db = CreateDbContext();
        var (session, _) = await SeedSessionAsync(db);

        var controller = CreateSessionsController(db, "dm-1");
        var result = await controller.GetSessionLive(session.Id, sinceSequence: 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SessionLiveResponse>(ok.Value);
        Assert.NotNull(response.Game);
        Assert.Single(response.Actions);
        Assert.Equal("Published", response.Actions.First().Status);
    }

    [Fact]
    public async Task GetPlayerSessionVersion_RequiresParticipantToken()
    {
        await using var db = CreateDbContext();
        var (session, _) = await SeedSessionAsync(db);

        var controller = CreateSessionJoinController(db);
        var unauthorized = await controller.GetSessionVersion(session.JoinCode);
        Assert.IsType<UnauthorizedObjectResult>(unauthorized.Result);

        controller = CreateSessionJoinController(db, "player-token-1");
        var result = await controller.GetSessionVersion(session.JoinCode);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SessionVersionResponse>(ok.Value);
        Assert.Equal(session.Version, response.Version);
    }

    private static async Task<(GameSession Session, Game Game)> SeedSessionAsync(ApplicationDbContext db)
    {
        db.Rulesets.Add(new Ruleset
        {
            Code = "test-rules",
            DisplayName = "Test Rules",
            Description = "Test",
            DiceNotation = "d20",
            CharacterTemplateJson = "{}",
            DefinitionJson = "{}",
        });
        db.Users.Add(new ApplicationUser { Id = "dm-1", UserName = "dm-1@example.local", Email = "dm-1@example.local" });
        await db.SaveChangesAsync();

        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            Name = "Test Game",
            RulesetCode = "test-rules",
            InviteCode = "GAME01",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var character = new Character
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = "Hero",
            PlayerName = "Player",
            Health = 10,
            MaxHealth = 10,
            Armor = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = "JOIN01",
            IsActive = true,
            State = SessionMode.Exploration,
            Version = 3,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var pendingAction = new ActionRequest
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ActorCharacterId = character.Id,
            ActorName = character.Name,
            ActionText = "Look around",
            Status = ActionStatus.Pending,
            Sequence = 1,
            SubmittedAt = DateTime.UtcNow,
        };
        var publishedAction = new ActionRequest
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ActorCharacterId = character.Id,
            ActorName = character.Name,
            ActionText = "Open door",
            Status = ActionStatus.Published,
            Sequence = 2,
            SubmittedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow,
            ResolvedAt = DateTime.UtcNow,
        };

        db.AddRange(game, character, session, pendingAction, publishedAction);
        db.GameParticipants.Add(new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            CharacterId = character.Id,
            DisplayName = character.PlayerName,
            JoinToken = "player-token-1",
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return (session, game);
    }

    private static SessionsController CreateSessionsController(ApplicationDbContext db, string dmUserId)
    {
        var controller = new SessionsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, dmUserId) },
                        "TestAuth")),
                },
            },
        };
        return controller;
    }

    private static SessionJoinController CreateSessionJoinController(ApplicationDbContext db, string? playerToken = null)
    {
        var controller = new SessionJoinController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        if (playerToken is not null)
        {
            controller.ControllerContext.HttpContext.Request.Headers["X-Player-Token"] = playerToken;
        }

        return controller;
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
