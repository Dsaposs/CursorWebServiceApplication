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
using System.Text.Json;

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

    private const string RollChainRulesetJson = """
        {
          "schemaVersion": 1,
          "code": "chain-rules",
          "displayName": "Chain Rules",
          "description": "Rules for roll-chain testing.",
          "diceRollerKey": "d6-pool",
          "diceNotation": "d6 pool",
          "dice": [{ "key": "d6Pool", "label": "D6 Pool", "notation": "attribute + skill d6", "successTarget": 6 }],
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
              "successRule": "One or more successes."
            },
            "rollChain": [
              {
                "step": "attack",
                "label": "Attack roll",
                "checkMode": "Action",
                "resultKind": "PassFail",
                "autoResolve": { "condition": "successes >= 1", "fallback": "dm_input" },
                "onSuccess": "damage",
                "onFailure": "end"
              },
              {
                "step": "damage",
                "label": "Damage roll",
                "checkMode": "Custom",
                "resultKind": "Total",
                "customCheckText": "Roll damage",
                "onComplete": "end",
                "applyEffects": [{
                  "target": "action.target",
                  "stat": "health",
                  "operation": "subtract",
                  "value": "roll.total",
                  "minResult": 1
                }]
              }
            ]
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
    public async Task DmRollForAction_AutoResolvesBareTotalAgainstDc()
    {
        await using var db = CreateDbContext();
        await SeedRulesetAsync(db);
        await SeedUsersAsync(db);
        var now = DateTime.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = "DM Roll Regression",
            InviteCode = "dm-roll",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var character = new Character
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = "Ripley",
            PlayerName = "Dan",
            Health = 10,
            MaxHealth = 10,
            ClassKey = "marine",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = "dm-roll-session",
            IsActive = true,
            State = SessionMode.Exploration,
            StartedAt = now,
            UpdatedAt = now,
        };
        var action = new ActionRequest
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ActorCharacterId = character.Id,
            ActorName = character.Name,
            ActionKey = "shoot",
            ActionText = "Shoot",
            Status = ActionStatus.Pending,
            Sequence = 1,
            SubmittedAt = now,
        };
        db.AddRange(game, character, session, action);
        await db.SaveChangesAsync();
        var controller = CreateActionsController(db, "dm-1");

        var result = await controller.DmRollForAction(action.Id, new DmRollRequest
        {
            RollSummary = "14",
            Dc = 12,
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RollPromptResponse>(ok.Value);
        Assert.True(response.DmRolled);
        Assert.Equal("Completed", response.Status);
        Assert.Equal(RollChainOutcomes.Success, response.AutoResolveOutcome);

        var savedPrompt = await db.ActionRollPrompts.SingleAsync(p => p.ActionRequestId == action.Id);
        Assert.True(savedPrompt.DmRolled);
        Assert.Equal(RollPromptStatus.Completed, savedPrompt.Status);
        Assert.Equal(RollChainOutcomes.Success, savedPrompt.AutoResolveOutcome);
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
    public async Task CombatSetup_PreservesCurrentTurnAndPersistsInitiativeScores()
    {
        await using var db = CreateDbContext();
        await SeedRulesetAsync(db);
        await SeedUsersAsync(db);
        var now = DateTime.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "alien-rpg",
            Name = "Initiative Regression",
            InviteCode = "initiative-regression",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var firstCharacter = new Character
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = "Hicks",
            PlayerName = "Dan",
            Health = 10,
            MaxHealth = 10,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var currentCharacter = new Character
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = "Ripley",
            PlayerName = "Alex",
            Health = 10,
            MaxHealth = 10,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = "initiative-session",
            IsActive = true,
            State = SessionMode.Combat,
            Version = 4,
            StartedAt = now,
            UpdatedAt = now,
        };
        session.InitiativeEntries.Add(new InitiativeEntry
        {
            Id = Guid.NewGuid(),
            CombatantType = CombatantType.Character,
            CombatantId = firstCharacter.Id,
            CombatantName = firstCharacter.Name,
            SortOrder = 1,
            InitiativeScore = 8,
            CreatedAt = now,
        });
        session.InitiativeEntries.Add(new InitiativeEntry
        {
            Id = Guid.NewGuid(),
            CombatantType = CombatantType.Character,
            CombatantId = currentCharacter.Id,
            CombatantName = currentCharacter.Name,
            SortOrder = 2,
            InitiativeScore = 7,
            IsCurrentTurn = true,
            CreatedAt = now,
        });
        db.AddRange(game, firstCharacter, currentCharacter, session);
        await db.SaveChangesAsync();
        var controller = CreateCombatController(db, "dm-1");

        var result = await controller.Setup(session.Id, new SetupCombatRequest
        {
            Combatants = new[]
            {
                new CombatantRequest { Type = "Character", Id = firstCharacter.Id, Initiative = 20 },
                new CombatantRequest { Type = "Character", Id = currentCharacter.Id, Initiative = 5 },
            },
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var entries = Assert.IsAssignableFrom<IEnumerable<InitiativeEntryResponse>>(ok.Value).ToList();
        Assert.Equal(new[] { firstCharacter.Id, currentCharacter.Id }, entries.Select(e => e.CombatantId));
        Assert.Equal(new[] { 20, 5 }, entries.Select(e => e.InitiativeScore));
        Assert.False(entries[0].IsCurrentTurn);
        Assert.True(entries[1].IsCurrentTurn);

        var savedEntries = await db.InitiativeEntries
            .Where(i => i.SessionId == session.Id)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();
        Assert.Equal(new[] { 20, 5 }, savedEntries.Select(e => e.InitiativeScore));
        Assert.Equal(currentCharacter.Id, Assert.Single(savedEntries.Where(e => e.IsCurrentTurn)).CombatantId);
    }

    [Fact]
    public async Task RollChainOrchestrator_QueuesDamagePromptAndStoresPendingDamage()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var actor = new Character
        {
            Id = Guid.NewGuid(),
            Name = "Ripley",
            PlayerName = "Dan",
            Health = 10,
            MaxHealth = 10,
            ClassKey = "marine",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var target = new NpcOrMonster
        {
            Id = Guid.NewGuid(),
            Name = "Drone",
            Kind = "Monster",
            Health = 8,
            MaxHealth = 8,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "chain-rules",
            Name = "Roll Chain Regression",
            InviteCode = "roll-chain",
            Ruleset = new Ruleset
            {
                Code = "chain-rules",
                DisplayName = "Chain Rules",
                Description = "Roll chain test rules",
                DiceNotation = "d6 pool",
                DefinitionJson = RollChainRulesetJson,
            },
            CreatedAt = now,
            UpdatedAt = now,
        };
        game.Characters.Add(actor);
        game.NpcsAndMonsters.Add(target);
        var action = new ActionRequest
        {
            Id = Guid.NewGuid(),
            ActorCharacterId = actor.Id,
            ActorName = actor.Name,
            ActionKey = "shoot",
            ActionText = "Shoot",
            TargetNpcId = target.Id,
            TargetName = target.Name,
            Status = ActionStatus.Pending,
            Sequence = 1,
            SubmittedAt = now,
        };

        var attackPrompt = RollChainOrchestrator.TryCreateFirstPrompt(action, game, RollChainRulesetJson, now);

        Assert.NotNull(attackPrompt);
        Assert.Equal("attack", attackPrompt.ChainStepKey);
        Assert.Equal("Attack roll", attackPrompt.PromptLabel);

        var hitResult = await RollChainOrchestrator.ProcessCompletedPromptAsync(
            db,
            attackPrompt,
            action,
            game,
            RollChainRulesetJson,
            "1 success",
            RollResultParser.Serialize(new RollResultData { Successes = 1 }),
            now);

        Assert.Equal(RollChainOutcomes.Success, hitResult.AutoResolveOutcome);
        Assert.NotNull(hitResult.QueuedNextPrompt);
        Assert.Equal("damage", hitResult.QueuedNextPrompt.ChainStepKey);
        var chainState = RollChainCatalog.ParseState(action.RollChainStateJson);
        Assert.NotNull(chainState);
        Assert.Equal(1, chainState.StepIndex);
        Assert.Equal(RollChainOutcomes.Success, chainState.LastOutcome);

        var damageResult = await RollChainOrchestrator.ProcessCompletedPromptAsync(
            db,
            hitResult.QueuedNextPrompt,
            action,
            game,
            RollChainRulesetJson,
            "Damage total = 5",
            RollResultParser.Serialize(new RollResultData { Total = 5 }),
            now);

        var statChange = Assert.Single(damageResult.SuggestedStatChanges);
        Assert.Equal("NpcOrMonster", statChange.TargetType);
        Assert.Equal(target.Id, statChange.TargetId);
        Assert.Equal(-5, statChange.HealthDelta);

        var pending = JsonSerializer.Deserialize<List<StatChangeRequest>>(
            action.PendingChainEffectsJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var pendingChange = Assert.Single(pending!);
        Assert.Equal(target.Id, pendingChange.TargetId);
        Assert.Equal(-5, pendingChange.HealthDelta);
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
            ControllerContext = CreateAuthenticatedControllerContext(userId),
        };
    }

    private static CombatController CreateCombatController(ApplicationDbContext db, string userId)
    {
        return new CombatController(db)
        {
            ControllerContext = CreateAuthenticatedControllerContext(userId),
        };
    }

    private static ControllerContext CreateAuthenticatedControllerContext(string userId)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                    "TestAuth")),
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