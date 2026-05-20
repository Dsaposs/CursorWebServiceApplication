using System.Text.Json;
using System.Text.RegularExpressions;
using NotesApi.Models;

namespace NotesApi.Rulesets;

public static class CombatInitiativeRoller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex DiceNotationRegex = new(@"(\d+)d(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public record InitiativeRollResult(
        CombatantType Type,
        Guid Id,
        string Name,
        int Score,
        string Summary);

    public static RulesetInitiativeDefinition ResolveInitiativeDefinition(string definitionJson)
    {
        var definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        if (definition?.Initiative is { } configured
            && !string.IsNullOrWhiteSpace(configured.Attribute)
            && !string.IsNullOrWhiteSpace(configured.Skill))
        {
            return configured;
        }

        return definition?.DiceRollerKey switch
        {
            "d-class-check" => new RulesetInitiativeDefinition
            {
                Attribute = "body",
                Skill = "move",
                TieBreakerDice = "1d8",
                GuidanceText = "Initiative = Body + Move + 1d8.",
            },
            _ => new RulesetInitiativeDefinition
            {
                Attribute = "agility",
                Skill = "mobility",
                TieBreakerDice = "1d6",
                GuidanceText = "Initiative = Agility + Mobility + 1d6.",
            },
        };
    }

    public static IEnumerable<InitiativeRollResult> RollForGame(
        string definitionJson,
        IEnumerable<Character> characters,
        IEnumerable<NpcOrMonster> npcs)
    {
        var initiativeDef = ResolveInitiativeDefinition(definitionJson);
        var results = new List<InitiativeRollResult>();

        foreach (var character in characters)
        {
            var (score, summary) = RollForCombatant(initiativeDef, character.RulesetDataJson);
            results.Add(new InitiativeRollResult(
                CombatantType.Character,
                character.Id,
                character.Name,
                score,
                summary));
        }

        foreach (var npc in npcs)
        {
            var (score, summary) = RollForCombatant(initiativeDef, npc.StatBlockJson);
            results.Add(new InitiativeRollResult(
                CombatantType.NpcOrMonster,
                npc.Id,
                npc.Name,
                score,
                summary));
        }

        return results;
    }

    public static (int Score, string Summary) RollForCombatant(
        RulesetInitiativeDefinition initiativeDef,
        string statsJson)
    {
        var attributes = ReadIntSection(statsJson, "attributes");
        var skills = ReadIntSection(statsJson, "skills");

        var attr = attributes.GetValueOrDefault(initiativeDef.Attribute);
        var skill = skills.GetValueOrDefault(initiativeDef.Skill);
        var tie = RollDiceNotation(initiativeDef.TieBreakerDice);
        var total = attr + skill + tie;

        var summary = $"{initiativeDef.Attribute} {attr} + {initiativeDef.Skill} {skill} + {initiativeDef.TieBreakerDice} [{tie}] = {total}";
        return (total, summary);
    }

    private static int RollDiceNotation(string notation)
    {
        var match = DiceNotationRegex.Match(notation ?? "1d6");
        if (!match.Success)
        {
            return Random.Shared.Next(1, 7);
        }

        var count = int.Parse(match.Groups[1].Value);
        var sides = int.Parse(match.Groups[2].Value);
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            total += Random.Shared.Next(1, sides + 1);
        }

        return total;
    }

    private static Dictionary<string, int> ReadIntSection(string json, string sectionKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (!doc.RootElement.TryGetProperty(sectionKey, out var section))
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in section.EnumerateObject())
            {
                if (property.Value.TryGetInt32(out var value))
                {
                    result[property.Name] = value;
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
