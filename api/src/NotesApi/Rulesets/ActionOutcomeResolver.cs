using System.Text.Json;
using System.Text.RegularExpressions;
using NotesApi.Models;

namespace NotesApi.Rulesets;

/// <summary>
/// Derives Pass/Fail from the player's initial roll line in an action description.
/// </summary>
public static class ActionOutcomeResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Regex SuccessCountRegex = new(
        @"(\d+)\s+success(?:es)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DcRegex = new(
        @"\bDC\s+(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AcRegex = new(
        @"\bAC\s+(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NaturalDieRegex = new(
        @"\[(\d+)\]",
        RegexOptions.CultureInvariant);

    private static readonly Regex TotalAfterEqualsRegex = new(
        @"=\s*(\d+)",
        RegexOptions.CultureInvariant);

    private static readonly Regex ModifierRegex = new(
        @"\+\s*(\d+)",
        RegexOptions.CultureInvariant);

    public static ActionOutcome? Resolve(string definitionJson, string? actionKey, string? description)
    {
        var rollLine = ExtractRollLine(description);
        if (string.IsNullOrWhiteSpace(rollLine))
        {
            return null;
        }

        var definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        if (definition is null)
        {
            return null;
        }

        var rollerKey = ResolveRollerKey(definition);
        var rulesetAction = RulesetActionCatalog.FindAction(definitionJson, actionKey);
        var successRule = rulesetAction?.Roll.SuccessRule;

        return rollerKey switch
        {
            "d6-pool" => ResolveD6Pool(rollLine, successRule ?? string.Empty),
            "d20-check" => ResolveD20Check(rollLine, rulesetAction?.Roll, definition.RollMechanics),
            _ => null,
        };
    }

    private static string ResolveRollerKey(RulesetDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.DiceRollerKey))
        {
            return definition.DiceRollerKey;
        }

        return definition.Dice.Any(d => d.SuccessTarget.HasValue) ? "d6-pool" : "d20-check";
    }

    private static string? ExtractRollLine(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        foreach (var line in description.Split('\n'))
        {
            var index = line.IndexOf("🎲 Roll:", StringComparison.Ordinal);
            if (index >= 0)
            {
                return line[(index + "🎲 Roll:".Length)..].Trim();
            }
        }

        return null;
    }

    private static ActionOutcome? ResolveD6Pool(string rollLine, string successRule)
    {
        if (!rollLine.Contains("success", StringComparison.OrdinalIgnoreCase)
            && !rollLine.Contains("d6", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var successes = ParseSuccessCount(rollLine);
        var minSuccesses = MinSuccessesFromRule(successRule);
        return successes >= minSuccesses ? ActionOutcome.Pass : ActionOutcome.Fail;
    }

    private static int ParseSuccessCount(string rollLine)
    {
        var matches = SuccessCountRegex.Matches(rollLine);
        if (matches.Count == 0)
        {
            return 0;
        }

        return int.Parse(matches[^1].Groups[1].Value);
    }

    private static int MinSuccessesFromRule(string successRule)
    {
        if (string.IsNullOrWhiteSpace(successRule))
        {
            return 1;
        }

        if (Regex.IsMatch(successRule, @"one\s+or\s+more\s+success", RegexOptions.IgnoreCase))
        {
            return 1;
        }

        if (Regex.IsMatch(successRule, @"\bone\s+success\b", RegexOptions.IgnoreCase))
        {
            return 1;
        }

        var numbered = Regex.Match(successRule, @"\b(\d+)\s+success(?:es)?\b", RegexOptions.IgnoreCase);
        if (numbered.Success)
        {
            return int.Parse(numbered.Groups[1].Value);
        }

        return 1;
    }

    private static ActionOutcome? ResolveD20Check(
        string rollLine,
        RulesetRollDefinition? roll,
        RulesetRollMechanicsDefinition? rollMechanics)
    {
        if (!rollLine.Contains("1d", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var successRule = roll?.SuccessRule ?? string.Empty;
        if (IsAutomaticSuccess(successRule))
        {
            return ActionOutcome.Pass;
        }

        var (natural, total) = ParseD20Roll(rollLine);
        if (total is null)
        {
            return null;
        }

        if (natural == 1)
        {
            return ActionOutcome.Fail;
        }

        if (natural == 20)
        {
            return ActionOutcome.Pass;
        }

        var difficulty = ParseDifficulty(successRule, roll, rollMechanics);
        if (difficulty is null)
        {
            return null;
        }

        return total >= difficulty ? ActionOutcome.Pass : ActionOutcome.Fail;
    }

    private static bool IsAutomaticSuccess(string? successRule)
    {
        if (string.IsNullOrWhiteSpace(successRule))
        {
            return false;
        }

        return successRule.Contains("automatically hits", StringComparison.OrdinalIgnoreCase)
            || successRule.Contains("no attack roll required", StringComparison.OrdinalIgnoreCase)
            || successRule.Contains("automatically hit", StringComparison.OrdinalIgnoreCase);
    }

    private static (int? natural, int? total) ParseD20Roll(string rollLine)
    {
        int? natural = null;
        var naturalMatch = NaturalDieRegex.Match(rollLine);
        if (naturalMatch.Success)
        {
            natural = int.Parse(naturalMatch.Groups[1].Value);
        }

        var totalMatch = TotalAfterEqualsRegex.Match(rollLine);
        if (totalMatch.Success)
        {
            return (natural, int.Parse(totalMatch.Groups[1].Value));
        }

        var manualMatch = Regex.Match(rollLine, @"\(manual\s+(\d+)\)", RegexOptions.IgnoreCase);
        if (manualMatch.Success)
        {
            var baseRoll = int.Parse(manualMatch.Groups[1].Value);
            var mod = ModifierRegex.IsMatch(rollLine) ? int.Parse(ModifierRegex.Match(rollLine).Groups[1].Value) : 0;
            return (baseRoll, baseRoll + mod);
        }

        if (natural is null)
        {
            return (null, null);
        }

        var modifier = ModifierRegex.IsMatch(rollLine) ? int.Parse(ModifierRegex.Match(rollLine).Groups[1].Value) : 0;
        return (natural, natural.Value + modifier);
    }

    private static int? ParseDifficulty(
        string successRule,
        RulesetRollDefinition? roll,
        RulesetRollMechanicsDefinition? rollMechanics)
    {
        if (roll?.DifficultyClass is int actionDc)
        {
            return actionDc;
        }

        if (!string.IsNullOrWhiteSpace(successRule))
        {
            var dcMatch = DcRegex.Match(successRule);
            if (dcMatch.Success)
            {
                return int.Parse(dcMatch.Groups[1].Value);
            }

            var acMatch = AcRegex.Match(successRule);
            if (acMatch.Success)
            {
                return int.Parse(acMatch.Groups[1].Value);
            }

            if (successRule.Contains("vs target AC", StringComparison.OrdinalIgnoreCase)
                || successRule.Contains("meet or beat AC", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return rollMechanics?.SkillCheck?.DifficultyClass
            ?? rollMechanics?.AttributeCheck?.DifficultyClass
            ?? 15;
    }
}
