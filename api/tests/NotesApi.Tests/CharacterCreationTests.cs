namespace NotesApi.Tests;

using System.Text.Json;
using NotesApi.Rulesets;

public class CharacterCreationTests
{
    private const string DndLikeRulesetJson = """
        {
          "schemaVersion": 1,
          "code": "dnd-5e-test",
          "displayName": "D&D Test",
          "description": "D&D-like rules for character creation tests.",
          "diceNotation": "d20",
          "dice": [],
          "character": {
            "vitals": {},
            "attributes": [],
            "gameValues": [],
            "classes": [
              {
                "key": "fighter",
                "label": "Fighter",
                "availableSkills": ["athletics", "acrobatics", "perception"],
                "startingSkillPoints": 2,
                "maxSkillRank": 1,
                "startingItemOptions": ["longsword", "shortsword"]
              }
            ],
            "skills": [
              { "key": "athletics", "label": "Athletics", "attribute": "strength", "default": 0 },
              { "key": "acrobatics", "label": "Acrobatics", "attribute": "dexterity", "default": 0 },
              { "key": "perception", "label": "Perception", "attribute": "wisdom", "default": 0 },
              { "key": "stealth", "label": "Stealth", "attribute": "dexterity", "default": 0 }
            ]
          },
          "items": [
            { "key": "longsword", "label": "Longsword", "description": "A martial weapon.", "category": "weapon", "modifiers": [] },
            { "key": "shortsword", "label": "Shortsword", "description": "A finesse weapon.", "category": "weapon", "modifiers": [] }
          ],
          "actions": [],
          "npcTemplates": []
        }
        """;

    private const string CharacterTemplateJson = """
        {
          "attributes": { "strength": 10, "dexterity": 10, "wisdom": 10 },
          "skills": {
            "athletics": 0,
            "acrobatics": 0,
            "perception": 0,
            "stealth": 0
          }
        }
        """;

    [Fact]
    public void Build_StoresSelectedClassSkillAllocationsAndStartingItem()
    {
        var result = CharacterCreation.Build(
            DndLikeRulesetJson,
            CharacterTemplateJson,
            "fighter",
            new Dictionary<string, int>
            {
                ["athletics"] = 1,
                ["perception"] = 1,
            },
            "longsword");

        using var rulesetData = JsonDocument.Parse(result.RulesetDataJson);
        var root = rulesetData.RootElement;
        Assert.Equal("fighter", root.GetProperty("classKey").GetString());
        Assert.Equal(1, root.GetProperty("skills").GetProperty("athletics").GetInt32());
        Assert.Equal(1, root.GetProperty("skills").GetProperty("perception").GetInt32());
        Assert.Equal(0, root.GetProperty("skills").GetProperty("acrobatics").GetInt32());

        using var inventory = JsonDocument.Parse(result.InventoryJson);
        var startingItem = Assert.Single(inventory.RootElement.EnumerateArray());
        Assert.Equal("longsword", startingItem.GetProperty("itemKey").GetString());
        Assert.Equal(1, startingItem.GetProperty("quantity").GetInt32());
    }

    [Theory]
    [MemberData(nameof(InvalidCreationRequests))]
    public void Build_RejectsPartialOrInvalidDndCreationRequests(
        IReadOnlyDictionary<string, int>? skillAllocations,
        string? startingItemKey,
        string expectedMessage)
    {
        var exception = Assert.Throws<ArgumentException>(() => CharacterCreation.Build(
            DndLikeRulesetJson,
            CharacterTemplateJson,
            "fighter",
            skillAllocations,
            startingItemKey));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object?[]> InvalidCreationRequests()
    {
        yield return new object?[] { null, "longsword", "Allocate exactly 2 skill points" };
        yield return new object?[]
        {
            new Dictionary<string, int> { ["athletics"] = 1, ["perception"] = 1 },
            null,
            "Choose a starting item",
        };
        yield return new object?[]
        {
            new Dictionary<string, int> { ["athletics"] = 1, ["stealth"] = 1 },
            "longsword",
            "not available for this class",
        };
        yield return new object?[]
        {
            new Dictionary<string, int> { ["athletics"] = 2 },
            "longsword",
            "cannot exceed 1",
        };
        yield return new object?[]
        {
            new Dictionary<string, int> { ["athletics"] = 1, ["perception"] = 1 },
            "dagger",
            "not available for this class",
        };
    }
}
