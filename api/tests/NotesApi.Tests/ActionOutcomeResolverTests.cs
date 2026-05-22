using NotesApi.Models;
using NotesApi.Rulesets;

namespace NotesApi.Tests;

public class ActionOutcomeResolverTests
{
    private const string DndRulesetJson = """
        {
          "schemaVersion": 1,
          "code": "dnd-5e",
          "displayName": "D&D 5e",
          "description": "Test",
          "diceNotation": "1d20",
          "diceRollerKey": "d20-check",
          "dice": [{ "key": "d20", "label": "D20", "notation": "1d20" }],
          "character": {
            "vitals": {},
            "attributes": [{ "key": "intelligence", "label": "Intelligence", "default": 10 }],
            "gameValues": [],
            "classes": [{ "key": "wizard", "label": "Wizard", "availableSkills": ["arcana"], "startingSkillPoints": 2 }],
            "skills": [{ "key": "arcana", "label": "Arcana", "attribute": "intelligence", "default": 0 }]
          },
          "actions": [{
            "key": "fireBolt",
            "label": "Fire Bolt",
            "allowedClasses": ["wizard"],
            "roll": {
              "dice": "d20",
              "attribute": "intelligence",
              "skill": "arcana",
              "modifiers": [],
              "successRule": "Ranged spell attack: roll 1d20 + Intelligence modifier + proficiency vs target AC. On a hit, deal 1d10 fire damage."
            },
            "context": "combat"
          }, {
            "key": "magicMissile",
            "label": "Magic Missile",
            "allowedClasses": ["wizard"],
            "roll": {
              "dice": "d20",
              "attribute": "intelligence",
              "skill": "arcana",
              "modifiers": [],
              "successRule": "Automatically hits. Three darts each deal 1d4 + 1 force damage (no attack roll required)."
            },
            "context": "combat"
          }],
          "rollMechanics": {
            "skillCheck": { "difficultyClass": 15 },
            "attributeCheck": { "difficultyClass": 15 }
          }
        }
        """;

    [Fact]
    public void Resolve_UsesTargetArmor_ForNpcAttackVsTargetAc()
    {
        var targetNpcId = Guid.NewGuid();
        var game = new Game
        {
            NpcsAndMonsters =
            [
                new NpcOrMonster { Id = targetNpcId, Name = "Susan", Armor = 16 },
            ],
        };
        var action = new ActionRequest
        {
            ActionKey = "fireBolt",
            TargetNpcId = targetNpcId,
            Description = "🎲 Roll: 1d20: [18] + 4 = 22",
        };

        var outcome = ActionOutcomeResolver.Resolve(DndRulesetJson, action, rollPrompts: null, game);

        Assert.Equal(ActionOutcome.Pass, outcome);
    }

    [Fact]
    public void Resolve_Fails_WhenRollIsBelowTargetArmor()
    {
        var targetNpcId = Guid.NewGuid();
        var game = new Game
        {
            NpcsAndMonsters =
            [
                new NpcOrMonster { Id = targetNpcId, Name = "Susan", Armor = 16 },
            ],
        };
        var action = new ActionRequest
        {
            ActionKey = "fireBolt",
            TargetNpcId = targetNpcId,
            Description = "🎲 Roll: 1d20: [8] + 4 = 12",
        };

        var outcome = ActionOutcomeResolver.Resolve(DndRulesetJson, action, rollPrompts: null, game);

        Assert.Equal(ActionOutcome.Fail, outcome);
    }

    [Fact]
    public void Resolve_PassesAutomaticHit_WithoutRollLine()
    {
        var action = new ActionRequest
        {
            ActionKey = "magicMissile",
            Description = null,
        };

        var outcome = ActionOutcomeResolver.Resolve(DndRulesetJson, action);

        Assert.Equal(ActionOutcome.Pass, outcome);
    }

    [Fact]
    public void Resolve_UsesPromptAutoResolveOutcome()
    {
        var action = new ActionRequest { ActionKey = "fireBolt" };
        var prompts = new[]
        {
            new ActionRollPrompt
            {
                Status = RollPromptStatus.Completed,
                ResultKind = RollPromptResultKind.PassFail,
                RollSummary = "1d20: [10] + 4 = 14",
                AutoResolveOutcome = RollChainOutcomes.Success,
                CompletedAt = DateTime.UtcNow,
            },
        };

        var outcome = ActionOutcomeResolver.Resolve(DndRulesetJson, action, prompts);

        Assert.Equal(ActionOutcome.Pass, outcome);
    }
}
