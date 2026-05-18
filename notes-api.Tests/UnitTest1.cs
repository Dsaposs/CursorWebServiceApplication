namespace NotesApi.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NotesApi.Controllers;
using NotesApi.Data;
using NotesApi.Models;

public class TtrpgDomainTests
{
    [Fact]
    public async Task OwnedGameLookup_OnlyReturnsGamesForTheDm()
    {
        await using var db = CreateDbContext();
        await SeedRulesetAsync(db);
        await SeedUsersAsync(db);
        var ownedGame = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = "Hadley's Hope",
            InviteCode = "invite-one",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Games.Add(ownedGame);
        db.Games.Add(new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-2",
            RulesetCode = "alien-rpg",
            Name = "Other Table",
            InviteCode = "invite-two",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var visible = await db.GetOwnedGameAsync(ownedGame.Id, "dm-1");
        var hidden = await db.GetOwnedGameAsync(ownedGame.Id, "dm-2");

        Assert.NotNull(visible);
        Assert.Null(hidden);
    }

    [Fact]
    public async Task PlayerJoinToken_IsScopedToOneGameCharacter()
    {
        await using var db = CreateDbContext();
        await SeedRulesetAsync(db);
        await SeedUsersAsync(db);
        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = "LV-426",
            InviteCode = "abc123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var character = new Character
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = "Ripley",
            PlayerName = "Dan",
            Health = 10,
            MaxHealth = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.AddRange(game, character, new GameParticipant
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            CharacterId = character.Id,
            DisplayName = character.Name,
            JoinToken = "player-token",
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var participant = await db.GameParticipants
            .Include(p => p.Character)
            .SingleAsync(p => p.GameId == game.Id && p.JoinToken == "player-token");

        Assert.Equal("Ripley", participant.Character.Name);
        Assert.Equal(game.Id, participant.GameId);
    }

    [Fact]
    public async Task CombatInitiative_CanTrackExactlyOneCurrentTurn()
    {
        await using var db = CreateDbContext();
        await SeedRulesetAsync(db);
        await SeedUsersAsync(db);
        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = "Colony",
            InviteCode = "invite",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = "session",
            IsActive = true,
            State = SessionMode.Combat,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        session.InitiativeEntries.Add(new InitiativeEntry
        {
            Id = Guid.NewGuid(),
            CombatantType = CombatantType.Character,
            CombatantId = Guid.NewGuid(),
            CombatantName = "Ripley",
            SortOrder = 1,
            IsCurrentTurn = true,
            CreatedAt = DateTime.UtcNow,
        });
        session.InitiativeEntries.Add(new InitiativeEntry
        {
            Id = Guid.NewGuid(),
            CombatantType = CombatantType.NpcOrMonster,
            CombatantId = Guid.NewGuid(),
            CombatantName = "Xenomorph",
            SortOrder = 2,
            CreatedAt = DateTime.UtcNow,
        });
        db.AddRange(game, session);
        await db.SaveChangesAsync();

        var currentTurns = await db.InitiativeEntries.CountAsync(i => i.SessionId == session.Id && i.IsCurrentTurn);

        Assert.Equal(1, currentTurns);
    }

    [Fact]
    public async Task GameName_IsUniqueAcrossAllGames()
    {
        await using var db = CreateDbContext();
        await SeedRulesetAsync(db);
        await SeedUsersAsync(db);
        db.Games.Add(new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = "Shared Name",
            InviteCode = "invite-one",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        db.Games.Add(new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-2",
            RulesetCode = "alien-rpg",
            Name = "Shared Name",
            InviteCode = "invite-two",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingGame_RemovesSessionAndCharacterData()
    {
        await using var db = CreateDbContext();
        await SeedRulesetAsync(db);
        await SeedUsersAsync(db);
        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = "Delete Me",
            InviteCode = "delete-me",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var character = new Character
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = "Hicks",
            PlayerName = "Dan",
            Health = 10,
            MaxHealth = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = "delete-session",
            IsActive = true,
            State = SessionMode.Exploration,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        session.Actions.Add(new ActionRequest
        {
            Id = Guid.NewGuid(),
            ActorCharacterId = character.Id,
            ActorName = character.Name,
            ActionText = "searches the room",
            Sequence = 1,
            SubmittedAt = DateTime.UtcNow,
        });
        db.AddRange(game, character, session);
        await db.SaveChangesAsync();

        db.Games.Remove(game);
        await db.SaveChangesAsync();

        Assert.Empty(db.Games);
        Assert.Empty(db.Characters);
        Assert.Empty(db.GameSessions);
        Assert.Empty(db.ActionRequests);
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

    private static async Task SeedRulesetAsync(ApplicationDbContext db)
    {
        db.Rulesets.Add(new Ruleset
        {
            Code = "alien-rpg",
            DisplayName = "Alien RPG",
            Description = "Test ruleset",
            DiceNotation = "d6",
            CharacterTemplateJson = "{}",
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(ApplicationDbContext db)
    {
        db.Users.AddRange(
            new ApplicationUser { Id = "dm-1", UserName = "dm-1@example.local", Email = "dm-1@example.local" },
            new ApplicationUser { Id = "dm-2", UserName = "dm-2@example.local", Email = "dm-2@example.local" });
        await db.SaveChangesAsync();
    }
}