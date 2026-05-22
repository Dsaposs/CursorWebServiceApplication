namespace NotesApi.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NotesApi.Controllers;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Services;
using System.Security.Claims;

public class RollPromptFlowTests
{
    private const string RulesetJson = """
        {
          "schemaVersion": 1,
          "code": "test-rules",
          "displayName": "Test Rules",
          "description": "Rules for testing.",
          "diceNotation": "d20",
          "diceRollerKey": "d20-check",
          "dice": [{ "key": "d20", "label": "D20", "notation": "1d20" }],
          "character": {
            "vitals": {},
            "attributes": [{ "key": "strength", "label": "Strength", "default": 10 }],
            "gameValues": [],
            "classes": [{ "key": "fighter", "label": "Fighter", "availableSkills": ["athletics"], "startingSkillPoints": 2 }],
            "skills": [{ "key": "athletics", "label": "Athletics", "attribute": "strength", "default": 0 }]
          },
          "actions": [{
            "key": "strike",
            "label": "Strike",
            "allowedClasses": ["fighter"],
            "roll": {
              "dice": "d20",
              "attribute": "strength",
              "skill": "athletics",
              "successRule": "Meet or beat target AC."
            }
          }],
          "npcTemplates": []
        }
        """;

    private const string RollChainRulesetJson = """
        {
          "schemaVersion": 1,
          "code": "test-rules",
          "displayName": "Test Rules",
          "description": "Rules for testing.",
          "diceNotation": "d20",
          "diceRollerKey": "d20-check",
          "dice": [{ "key": "d20", "label": "D20", "notation": "1d20" }],
          "character": {
            "vitals": {},
            "attributes": [{ "key": "strength", "label": "Strength", "default": 10 }],
            "gameValues": [],
            "classes": [{ "key": "fighter", "label": "Fighter", "availableSkills": ["athletics"], "startingSkillPoints": 2 }],
            "skills": [{ "key": "athletics", "label": "Athletics", "attribute": "strength", "default": 0 }]
          },
          "actions": [{
            "key": "strike",
            "label": "Strike",
            "allowedClasses": ["fighter"],
            "attackType": "weaponAttack",
            "requiresTarget": true,
            "damageRoll": { "notation": "1d8", "description": "Slashing damage" },
            "roll": {
              "dice": "d20",
              "attribute": "strength",
              "skill": "athletics",
              "successRule": "Meet or beat target AC."
            },
            "rollChain": [
              {
                "step": "attack",
                "label": "Attack roll",
                "checkMode": "Action",
                "resultKind": "Total",
                "autoResolve": { "condition": "total >= target.armor", "fallback": "dm_input" },
                "onSuccess": "damage",
                "onFailure": "end"
              },
              {
                "step": "damage",
                "label": "Damage roll",
                "checkMode": "Custom",
                "resultKind": "Total",
                "diceSource": "actionDamage",
                "onComplete": "end"
              }
            ]
          }],
          "npcTemplates": []
        }
        """;

    [Fact]
    public async Task SubmitRollPrompt_CompletesPromptAndExposesRollToDmState()
    {
        await using var db = CreateDbContext();
        var (session, character, action, prompt, playerToken) = await SeedCombatActionWithPromptAsync(db);

        var submitController = CreateActionsController(db, playerToken: playerToken);
        var submitResult = await submitController.SubmitRollPrompt(prompt.Id, new SubmitRollPromptRequest
        {
            RollSummary = "1d20 (14) + 2 = 16",
        });

        var submitOk = Assert.IsType<OkObjectResult>(submitResult.Result);
        var submittedPrompt = Assert.IsType<RollPromptResponse>(submitOk.Value);
        Assert.Equal("Completed", submittedPrompt.Status);
        Assert.Equal("1d20 (14) + 2 = 16", submittedPrompt.RollSummary);

        var updatedAction = await db.ActionRequests.SingleAsync(a => a.Id == action.Id);
        Assert.Equal(ActionStatus.RollReceived, updatedAction.Status);

        var reloadedSession = await ReloadSessionAsync(db, session.Id);
        var dmRollPrompts = ControllerHelpers.SelectRollPrompts(reloadedSession).ToList();
        Assert.Contains(dmRollPrompts, p => p.Id == prompt.Id && p.Status == "Completed");

        var dmAction = ControllerHelpers.ToActionResponse(
            reloadedSession.Actions.Single(a => a.Id == action.Id));
        Assert.Contains(dmAction.FollowUpRolls, p => p.Id == prompt.Id && p.RollSummary == "1d20 (14) + 2 = 16");
    }

    [Fact]
    public async Task CreateRollPrompts_MovesActionToAwaitingRoll()
    {
        await using var db = CreateDbContext();
        var (session, character, action, _, dmUserId) = await SeedCombatActionAsync(db);

        var controller = CreateActionsController(db, dmUserId: dmUserId);
        var result = await controller.CreateRollPrompts(action.Id, new CreateRollPromptsRequest
        {
            Prompts =
            [
                new CreateRollPromptRequest
                {
                    TargetCharacterId = character.Id,
                    CheckMode = "Action",
                    ActionKey = "strike",
                    ResultKind = "PassFail",
                },
            ],
        });

        Assert.IsType<OkObjectResult>(result.Result);
        var updated = await db.ActionRequests.SingleAsync(a => a.Id == action.Id);
        Assert.Equal(ActionStatus.AwaitingRoll, updated.Status);
    }

    [Fact]
    public async Task StartRollChain_QueuesFollowUpDamagePromptOnHit()
    {
        await using var db = CreateDbContext();
        var (session, character, action, _, dmUserId) = await SeedCombatActionAsync(db, withRollChain: true, targetArmor: 12);

        var dmController = CreateActionsController(db, dmUserId: dmUserId);
        var startResult = await dmController.StartRollChain(action.Id);
        var startOk = Assert.IsType<OkObjectResult>(startResult.Result);
        var attackPrompt = Assert.IsType<RollPromptResponse>(startOk.Value);
        Assert.Equal("attack", attackPrompt.ChainStepKey);

        var playerController = CreateActionsController(db, playerToken: "player-token-1");
        var submitResult = await playerController.SubmitRollPrompt(attackPrompt.Id, new SubmitRollPromptRequest
        {
            RollSummary = "1d20: [18] + 4 = 22",
        });
        var submitOk = Assert.IsType<OkObjectResult>(submitResult.Result);
        var submittedAttack = Assert.IsType<RollPromptResponse>(submitOk.Value);
        Assert.Equal("success", submittedAttack.AutoResolveOutcome);

        var damagePrompt = await db.ActionRollPrompts
            .Where(p => p.ActionRequestId == action.Id && p.ChainStepKey == "damage")
            .SingleAsync();
        Assert.Equal(RollPromptStatus.Pending, damagePrompt.Status);
        Assert.NotNull(submittedAttack.NextPendingPrompt);
        Assert.Equal(damagePrompt.Id, submittedAttack.NextPendingPrompt!.Id);
        Assert.Equal("damage", submittedAttack.NextPendingPrompt.ChainStepKey);

        var updatedAction = await db.ActionRequests.SingleAsync(a => a.Id == action.Id);
        Assert.Equal(ActionStatus.AwaitingFollowUpRoll, updatedAction.Status);
    }

    [Fact]
    public async Task ResolveStatCheck_DoesNotAdvanceTurnWhilePrimaryCombatActionPending()
    {
        await using var db = CreateDbContext();
        var (session, character, combatAction, _, _) = await SeedCombatActionWithPromptAsync(db);

        var statCheckAction = new ActionRequest
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ActorCharacterId = character.Id,
            ActorName = character.Name,
            ActionText = "Perception check",
            Description = "🎲 Roll: 2 successes",
            Status = ActionStatus.Pending,
            Sequence = 2,
            CombatEncounterId = combatAction.CombatEncounterId,
            SkillCheckBatchId = Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow,
        };
        db.ActionRequests.Add(statCheckAction);
        await db.SaveChangesAsync();

        var dmController = CreateActionsController(db, dmUserId: "dm-1");
        var resolveResult = await dmController.Resolve(statCheckAction.Id, new ResolveActionRequest());
        Assert.IsType<OkObjectResult>(resolveResult.Result);

        var currentTurn = await db.InitiativeEntries.SingleAsync(i => i.SessionId == session.Id && i.IsCurrentTurn);
        Assert.Equal(character.Id, currentTurn.CombatantId);

        var unresolvedCombatAction = await db.ActionRequests.SingleAsync(a => a.Id == combatAction.Id);
        Assert.Equal(ActionStatus.AwaitingRoll, unresolvedCombatAction.Status);
    }

    [Fact]
    public async Task ResolvePrimaryCombatAction_AdvancesTurnAfterStatCheckPublished()
    {
        await using var db = CreateDbContext();
        var (session, character, combatAction, _, _) = await SeedCombatActionWithPromptAsync(db);

        var statCheckAction = new ActionRequest
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ActorCharacterId = character.Id,
            ActorName = character.Name,
            ActionText = "Perception check",
            Status = ActionStatus.Published,
            Sequence = 2,
            CombatEncounterId = combatAction.CombatEncounterId,
            SkillCheckBatchId = Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow,
            ResolvedAt = DateTime.UtcNow,
        };
        db.ActionRequests.Add(statCheckAction);
        await db.SaveChangesAsync();

        db.InitiativeEntries.Add(new InitiativeEntry
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            CombatantType = CombatantType.NpcOrMonster,
            CombatantId = Guid.NewGuid(),
            CombatantName = "Goblin",
            SortOrder = 1,
            IsCurrentTurn = false,
        });
        combatAction.Status = ActionStatus.RollReceived;
        await db.SaveChangesAsync();

        var dmController = CreateActionsController(db, dmUserId: "dm-1");
        var resolveResult = await dmController.Resolve(combatAction.Id, new ResolveActionRequest());
        Assert.IsType<OkObjectResult>(resolveResult.Result);

        var currentTurn = await db.InitiativeEntries.SingleAsync(i => i.SessionId == session.Id && i.IsCurrentTurn);
        Assert.NotEqual(character.Id, currentTurn.CombatantId);
    }

    [Fact]
    public async Task SubmitCombatAction_AutoStartsRollChainForPlayer()
    {
        await using var db = CreateDbContext();
        var (session, character, _, game, _) = await SeedCombatActionAsync(
            db,
            withRollChain: true,
            targetArmor: 12,
            createAction: false,
            promptPlayerTurn: true);

        var targetNpcId = Guid.NewGuid();
        db.NpcsAndMonsters.Add(new NpcOrMonster
        {
            Id = targetNpcId,
            GameId = game.Id,
            Name = "Goblin",
            Kind = "Monster",
            Health = 7,
            MaxHealth = 7,
            Armor = 12,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var playerController = CreateActionsController(db, playerToken: "player-token-1");
        var submitResult = await playerController.SubmitAction(session.JoinCode, new SubmitActionRequest
        {
            ActionKey = "strike",
            TargetNpcId = targetNpcId,
            TargetName = "Goblin",
        });

        var created = Assert.IsType<CreatedAtActionResult>(submitResult.Result);
        var actionResponse = Assert.IsType<ActionQueueItemResponse>(created.Value);
        Assert.Equal("AwaitingRoll", actionResponse.Status);

        var action = await db.ActionRequests.SingleAsync(a => a.Id == actionResponse.Id);
        Assert.Equal(ActionStatus.AwaitingRoll, action.Status);
        Assert.NotNull(action.RollChainStateJson);

        var attackPrompt = await db.ActionRollPrompts
            .SingleAsync(p => p.ActionRequestId == action.Id);
        Assert.Equal("attack", attackPrompt.ChainStepKey);
        Assert.Equal(RollPromptStatus.Pending, attackPrompt.Status);
        Assert.Equal(character.Id, attackPrompt.TargetCharacterId);

        var encounter = await db.Set<CombatEncounter>().SingleAsync(e => e.SessionId == session.Id);
        Assert.Null(encounter.PromptedTurnCharacterId);
    }

    private static async Task<(GameSession Session, Character Character, ActionRequest Action, ActionRollPrompt Prompt, string PlayerToken)> SeedCombatActionWithPromptAsync(ApplicationDbContext db)
    {
        var (session, character, action, _, _) = await SeedCombatActionAsync(db);
        var prompt = new ActionRollPrompt
        {
            Id = Guid.NewGuid(),
            ActionRequestId = action.Id,
            TargetCharacterId = character.Id,
            TargetCharacter = character,
            CheckMode = "Action",
            ResultKind = RollPromptResultKind.PassFail,
            ActionKey = "strike",
            Status = RollPromptStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        action.Status = ActionStatus.AwaitingRoll;
        db.ActionRollPrompts.Add(prompt);
        await db.SaveChangesAsync();

        var participant = await db.GameParticipants.SingleAsync(p => p.CharacterId == character.Id);
        return (session, character, action, prompt, participant.JoinToken);
    }

    private static async Task<(GameSession Session, Character Character, ActionRequest Action, Game Game, string DmUserId)> SeedCombatActionAsync(
        ApplicationDbContext db,
        bool withRollChain = false,
        int targetArmor = 0,
        bool createAction = true,
        bool promptPlayerTurn = false)
    {
        var definitionJson = withRollChain ? RollChainRulesetJson : RulesetJson;

        db.Rulesets.Add(new Ruleset
        {
            Code = "test-rules",
            DisplayName = "Test Rules",
            Description = "Test",
            DiceNotation = "d20",
            CharacterTemplateJson = "{}",
            DefinitionJson = definitionJson,
        });
        db.Users.Add(new ApplicationUser { Id = "dm-1", UserName = "dm-1@example.local", Email = "dm-1@example.local" });
        await db.SaveChangesAsync();

        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = "dm-1",
            RulesetCode = "test-rules",
            Name = "Test Table",
            InviteCode = "test-game",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var character = new Character
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = "Fighter",
            PlayerName = "Alex",
            ClassKey = "fighter",
            Health = 10,
            MaxHealth = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            JoinCode = "combat-session",
            IsActive = true,
            State = SessionMode.Combat,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var encounter = new CombatEncounter
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Sequence = 1,
            Round = 1,
            StartedAt = DateTime.UtcNow,
        };
        session.CombatEncounters.Add(encounter);
        session.InitiativeEntries.Add(new InitiativeEntry
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            CombatantType = CombatantType.Character,
            CombatantId = character.Id,
            CombatantName = character.Name,
            SortOrder = 0,
            IsCurrentTurn = true,
        });

        var action = new ActionRequest
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ActorCharacterId = character.Id,
            ActorName = character.Name,
            ActionKey = "strike",
            ActionText = "Strike",
            TargetNpcId = targetArmor > 0 ? Guid.NewGuid() : null,
            TargetName = targetArmor > 0 ? "Goblin" : null,
            Status = ActionStatus.Pending,
            Sequence = 1,
            SubmittedAt = DateTime.UtcNow,
        };

        if (createAction)
        {
            db.AddRange(game, character, session, encounter, action);
        }
        else
        {
            db.AddRange(game, character, session, encounter);
        }
        if (targetArmor > 0 && action.TargetNpcId.HasValue)
        {
            db.NpcsAndMonsters.Add(new NpcOrMonster
            {
                Id = action.TargetNpcId.Value,
                GameId = game.Id,
                Name = "Goblin",
                Kind = "Monster",
                Health = 7,
                MaxHealth = 7,
                Armor = targetArmor,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
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

        action.CombatEncounterId = encounter.Id;
        session.ActiveCombatEncounterId = encounter.Id;
        if (promptPlayerTurn)
        {
            encounter.PromptedTurnCharacterId = character.Id;
        }
        await db.SaveChangesAsync();

        return (session, character, action, game, "dm-1");
    }

    private static Task<GameSession> ReloadSessionAsync(ApplicationDbContext db, Guid sessionId) =>
        db.GameSessions
            .Include(s => s.Actions).ThenInclude(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .Include(s => s.SessionRollPrompts).ThenInclude(p => p.TargetCharacter)
            .SingleAsync(s => s.Id == sessionId);

    private static ActionsController CreateActionsController(
        ApplicationDbContext db,
        string? dmUserId = null,
        string? playerToken = null)
    {
        var controller = new ActionsController(db, new NoOpActionBroadcaster())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        if (dmUserId is not null)
        {
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, dmUserId) },
                    "TestAuth"));
        }

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
