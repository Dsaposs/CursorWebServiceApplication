using System.Text.Json;
using NotesApi.DTOs;
using NotesApi.Rulesets;
using Xunit;

namespace NotesApi.Tests;

public class NpcTemplateApplicatorTests
{
    [Fact]
    public void TryBuildCreateRequest_AppliesTemplateVitalsAndStatBlock()
    {
        var definition = JsonSerializer.Deserialize<RulesetDefinition>(MinimalRulesetWithTemplate())!;
        var ok = NpcTemplateApplicator.TryBuildCreateRequest(
            definition,
            "hldWesOsterman",
            null,
            out var request,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("Wes Osterman", request.Name);
        Assert.Equal("NPC", request.Kind);
        Assert.Equal(3, request.MaxHealth);
        Assert.Equal(3, request.Health);
        Assert.Contains("survivorNpc", request.StatBlockJson);
        Assert.Contains("heavyMachinery", request.StatBlockJson);
    }

    [Fact]
    public void TryBuildCreateRequest_ReturnsErrorForUnknownTemplate()
    {
        var definition = JsonSerializer.Deserialize<RulesetDefinition>(MinimalRulesetWithTemplate())!;
        var ok = NpcTemplateApplicator.TryBuildCreateRequest(
            definition,
            "missing",
            null,
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Unknown NPC template", error);
    }

    private static string MinimalRulesetWithTemplate() =>
        """
        {
          "schemaVersion": 2,
          "diceRollerKey": "d6-pool",
          "code": "alien-rpg",
          "displayName": "Alien RPG",
          "description": "test",
          "diceNotation": "Nd6",
          "dice": [{ "key": "d6Pool", "label": "D6", "notation": "Nd6", "successTarget": 6 }],
          "character": {
            "vitals": {
              "health": { "defaultMax": 3 },
              "armor": { "default": 0 }
            },
            "attributes": [],
            "gameValues": [],
            "classes": [],
            "skills": []
          },
          "actions": [],
          "npcTemplates": [
            {
              "key": "hldWesOsterman",
              "label": "Wes Osterman",
              "kind": "NPC",
              "maxHealth": 3,
              "health": 3,
              "defaultStats": {
                "classKey": "survivorNpc",
                "attributes": { "strength": 3 },
                "skills": { "heavyMachinery": 2 }
              }
            }
          ],
          "rollMechanics": {
            "skillCheck": {
              "diceKey": "d6Pool",
              "poolMode": "attribute+skill",
              "modifiers": []
            }
          }
        }
        """;
}
