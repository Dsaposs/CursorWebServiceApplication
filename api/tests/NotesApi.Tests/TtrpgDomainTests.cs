namespace NotesApi.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Controllers;
using NotesApi.Data;
using NotesApi.Models;
using NotesApi.DTOs;
using NotesApi.Rulesets;
using System.Security.Claims;

public class TtrpgDomainTests
{
    private const string ValidRulesetJson = """
        {
          "schemaVersion": 1,
          "code": "test-rules",
          "displayName": "Test Rules",
          "description": "Rules for testing.",
          "diceNotation": "d6 pool",
          "dice": [{ "key": "d6Pool", "label": "D6 Pool", "notation": "attribute + skill d6" }],
          "character": {
            "vitals": {},
            "attributes": [{ "key": "agility", "label": "Agility", "default": 2 }],
            "gameValues": [{ "key": "stress", "label": "Stress", "type": "number", "default": 0 }],
            "classes": [{ "key": "marine", "label": "Marine", "availableSkills": ["rangedCombat"], "startingSkillPoints": 10 }],
            "skills": [{ "key": "rangedCombat", "label": "Ranged Combat", "attribute": "agility", "default": 0 }]
          },
          "actions": [{
            "key": "shoot",
            "label": "Shoot",
            "allowedClasses": ["marine"],
            "roll": {
              "dice": "d6Pool",
              "attribute": "agility",
              "skill": "rangedCombat",
              "modifiers": [{ "source": "gameValue", "key": "stress", "dicePerPoint": 1 }],
              "successRule": "Each 6 is a success."
            }
          }],
          "npcTemplates": []
        }
        """;

    [Fact]
    public void RulesetValidator_AcceptsValidRulesetJson()
    {
        var validator = new RulesetDefinitionValidator();

        var result = validator.Validate(ValidRulesetJson);

        Assert.True(result.IsValid);
        Assert.Equal("test-rules", result.Definition?.Code);
        Assert.Contains("rangedCombat", result.CharacterTemplateJson);
    }

    [Fact]
    public void RulesetValidator_RejectsMissingReferences()
    {
        var invalidJson = ValidRulesetJson.Replace("\"skill\": \"rangedCombat\"", "\"skill\": \"missingSkill\"");
        var validator = new RulesetDefinitionValidator();

        var result = validator.Validate(invalidJson);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("missing skill", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RulesetsImport_UpsertsByCode()
    {
        await using var db = CreateDbContext();
        var controller = new RulesetsController(db, new RulesetDefinitionValidator());

        var first = await controller.Import(new ImportRulesetRequest { DefinitionJson = ValidRulesetJson });
        var second = await controller.Import(new ImportRulesetRequest { DefinitionJson = ValidRulesetJson.Replace("Test Rules", "Test Rules Updated") });

        var firstOk = Assert.IsType<OkObjectResult>(first.Result);
        var firstResponse = Assert.IsType<RulesetImportResponse>(firstOk.Value);
        var secondOk = Assert.IsType<OkObjectResult>(second.Result);
        var secondResponse = Assert.IsType<RulesetImportResponse>(secondOk.Value);
        var saved = await db.Rulesets.SingleAsync(r => r.Code == "test-rules");

        Assert.True(firstResponse.Created);
        Assert.False(secondResponse.Created);
        Assert.Equal("Test Rules Updated", saved.DisplayName);
    }

    [Fact]
    public void RulesetsImport_RequiresAdminRole()
    {
        var method = typeof(RulesetsController).GetMethod(nameof(RulesetsController.Import));
        var authorize = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false).Cast<AuthorizeAttribute>());

        Assert.Equal("Admin", authorize.Roles);
    }

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
    public async Task NpcUpdate_PreservesStructuredStatsAndBumpsOnlyActiveSessionVersion()
    {
        await using var db = CreateDbContext();
        await SeedRulesetAsync(db);
        await SeedUsersAsync(db);
        var timestamp = DateTime.UtcNow.AddMinutes(-5);
        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = "NPC Regression",
            InviteCode = "npc-regression",
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };
        var npc = new NpcOrMonster
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = "Drone",
            Kind = "Monster",
            MaxHealth = 8,
            Health = 4,
            Armor = 1,
            StatBlockJson = "{}",
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };
        var activeSession = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = "active-npcs",
            IsActive = true,
            State = SessionMode.Exploration,
            Version = 2,
            StartedAt = timestamp,
            UpdatedAt = timestamp,
        };
        var inactiveSession = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = "inactive-npcs",
            IsActive = false,
            State = SessionMode.Exploration,
            Version = 7,
            StartedAt = timestamp,
            EndedAt = timestamp,
            UpdatedAt = timestamp,
        };
        db.AddRange(game, npc, activeSession, inactiveSession);
        await db.SaveChangesAsync();
        var controller = CreateNpcsController(db, "dm-1");
        var statBlockJson = """
            {"attributes":{"strength":4},"skills":{"stealth":2},"inventory":[{"itemKey":"acid-claws","quantity":1}]}
            """;

        var result = await controller.Update(game.Id, npc.Id, new UpdateNpcRequest
        {
            Name = "Alpha Drone",
            Kind = "Monster",
            MaxHealth = 12,
            Health = 99,
            Armor = 3,
            StatBlockJson = statBlockJson,
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NpcResponse>(ok.Value);
        Assert.Equal("Alpha Drone", response.Name);
        Assert.Equal("Monster", response.Kind);
        Assert.Equal(12, response.MaxHealth);
        Assert.Equal(12, response.Health);
        Assert.Equal(3, response.Armor);
        Assert.Equal(statBlockJson, response.StatBlockJson);
        Assert.Equal(3, activeSession.Version);
        Assert.NotEqual(timestamp, activeSession.UpdatedAt);
        Assert.Equal(7, inactiveSession.Version);
        Assert.Equal(timestamp, inactiveSession.UpdatedAt);

        var savedNpc = await db.NpcsAndMonsters.SingleAsync(n => n.Id == npc.Id);
        Assert.Equal(statBlockJson, savedNpc.StatBlockJson);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("  The xenomorph is driven back.  ", "The xenomorph is driven back.")]
    public async Task ResolveAction_AllowsOptionalResolutionTextAndClearsLegacyAdditionalActions(string? resolutionText, string expectedResolutionText)
    {
        await using var db = CreateDbContext();
        await SeedRulesetAsync(db);
        await SeedUsersAsync(db);
        var timestamp = DateTime.UtcNow.AddMinutes(-5);
        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = $"Resolution Regression {Guid.NewGuid():N}",
            InviteCode = $"resolve-{Guid.NewGuid():N}"[..32],
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = $"resolve-{Guid.NewGuid():N}"[..32],
            IsActive = true,
            State = SessionMode.Exploration,
            StartedAt = timestamp,
            UpdatedAt = timestamp,
        };
        var actionId = Guid.NewGuid();
        var action = new ActionRequest
        {
            Id = actionId,
            SessionId = session.Id,
            ActorName = "Ripley",
            ActionText = "holds the door",
            Status = ActionStatus.Pending,
            Sequence = 1,
            SubmittedAt = timestamp,
            Resolution = new ActionResolution
            {
                Id = Guid.NewGuid(),
                ActionRequestId = actionId,
                ResolutionText = "Legacy draft",
                AdditionalActions = "Legacy follow-up prompt",
                PublishedAt = timestamp,
            },
        };
        db.AddRange(game, session, action);
        await db.SaveChangesAsync();
        var controller = CreateActionsController(db, "dm-1");

        var result = await controller.Resolve(action.Id, new ResolveActionRequest
        {
            ResolutionText = resolutionText,
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ActionQueueItemResponse>(ok.Value);
        var saved = await db.ActionRequests.Include(a => a.Resolution).SingleAsync(a => a.Id == action.Id);
        Assert.Equal("Published", response.Status);
        Assert.Equal(expectedResolutionText, response.ResolutionText);
        Assert.Null(response.AdditionalActions);
        Assert.Equal(ActionStatus.Published, saved.Status);
        Assert.Equal(expectedResolutionText, saved.Resolution!.ResolutionText);
        Assert.Null(saved.Resolution.AdditionalActions);
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

    private static NpcsController CreateNpcsController(ApplicationDbContext db, string userId)
    {
        return new NpcsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                        "TestAuth")),
                },
            },
        };
    }

    private static ActionsController CreateActionsController(ApplicationDbContext db, string userId)
    {
        return new ActionsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                        "TestAuth")),
                },
            },
        };
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
            DefinitionJson = ValidRulesetJson.Replace("test-rules", "alien-rpg").Replace("Test Rules", "Alien RPG"),
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