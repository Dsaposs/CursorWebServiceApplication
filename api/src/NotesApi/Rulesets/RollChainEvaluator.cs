using System.Text.Json;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Rulesets;

public static class RollChainEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? EvaluateAutoResolve(
        RulesetRollChainAutoResolveDefinition? autoResolve,
        RollResultData? rollData,
        string? resultKind,
        string? rollSummary,
        ActionRequest action,
        Game game)
    {
        if (autoResolve is null || string.IsNullOrWhiteSpace(autoResolve.Condition))
        {
            return null;
        }

        var primary = RollResultParser.GetPrimaryValue(rollData, resultKind, rollSummary);
        if (primary is null)
        {
            return string.Equals(autoResolve.Fallback, "dm_input", StringComparison.OrdinalIgnoreCase)
                ? RollChainOutcomes.NeedsDm
                : null;
        }

        var condition = autoResolve.Condition.Trim();
        if (TryEvaluateSuccessesCondition(condition, primary.Value, out var successOutcome))
        {
            return successOutcome;
        }

        if (TryEvaluateTotalVsTarget(condition, primary.Value, action, game, out var targetOutcome))
        {
            return targetOutcome;
        }

        return string.Equals(autoResolve.Fallback, "dm_input", StringComparison.OrdinalIgnoreCase)
            ? RollChainOutcomes.NeedsDm
            : null;
    }

    public static IEnumerable<StatChangeRequest> BuildEffectChanges(
        IEnumerable<RulesetRollChainEffectDefinition> effects,
        ActionRequest action,
        int rollValue)
    {
        foreach (var effect in effects)
        {
            if (!string.Equals(effect.Value, "roll.total", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(effect.Value, "roll.successes", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = ResolveEffectTarget(effect.Target, action);
            if (target is null)
            {
                continue;
            }

            var amount = rollValue;
            if (effect.MinResult.HasValue)
            {
                amount = Math.Max(effect.MinResult.Value, amount);
            }

            if (string.Equals(effect.Stat, "health", StringComparison.OrdinalIgnoreCase)
                && string.Equals(effect.Operation, "subtract", StringComparison.OrdinalIgnoreCase))
            {
                yield return new StatChangeRequest
                {
                    TargetType = target.Value.Type,
                    TargetId = target.Value.Id,
                    HealthDelta = -amount,
                };
            }
        }
    }

    public static IEnumerable<string> BuildStatusKeys(
        IEnumerable<RulesetRollChainStatusDefinition> statuses,
        string outcome,
        RollResultData? rollData,
        string? resultKind,
        string? rollSummary)
    {
        var primary = RollResultParser.GetPrimaryValue(rollData, resultKind, rollSummary) ?? 0;

        foreach (var status in statuses)
        {
            if (!EvaluateStatusCondition(status.Condition, outcome, primary))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(status.StatusKey))
            {
                yield return status.StatusKey;
            }
        }
    }

    private static bool TryEvaluateSuccessesCondition(string condition, int successes, out string outcome)
    {
        outcome = RollChainOutcomes.Failure;
        var match = System.Text.RegularExpressions.Regex.Match(
            condition,
            @"successes\s*>=\s*(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        var required = int.Parse(match.Groups[1].Value);
        outcome = successes >= required ? RollChainOutcomes.Success : RollChainOutcomes.Failure;
        return true;
    }

    private static bool TryEvaluateTotalVsTarget(
        string condition,
        int total,
        ActionRequest action,
        Game game,
        out string outcome)
    {
        outcome = RollChainOutcomes.Failure;

        if (condition.Contains("target.armor", StringComparison.OrdinalIgnoreCase))
        {
            var defense = ResolveTargetArmor(action, game);
            if (defense is null)
            {
                return false;
            }

            outcome = total >= defense.Value ? RollChainOutcomes.Success : RollChainOutcomes.Failure;
            return true;
        }

        var statMatch = System.Text.RegularExpressions.Regex.Match(
            condition,
            @"target\.stats\[([^\]]+)\]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (statMatch.Success)
        {
            var statKey = statMatch.Groups[1].Value;
            var statValue = ResolveTargetStat(action, game, statKey);
            if (statValue is null)
            {
                return false;
            }

            outcome = total >= statValue.Value ? RollChainOutcomes.Success : RollChainOutcomes.Failure;
            return true;
        }

        return false;
    }

    private static int? ResolveTargetArmor(ActionRequest action, Game game)
    {
        if (action.TargetCharacterId.HasValue)
        {
            var character = game.Characters.FirstOrDefault(c => c.Id == action.TargetCharacterId.Value);
            return character?.Armor;
        }

        if (action.TargetNpcId.HasValue)
        {
            var npc = game.NpcsAndMonsters.FirstOrDefault(n => n.Id == action.TargetNpcId.Value);
            return npc?.Armor;
        }

        return null;
    }

    private static int? ResolveTargetStat(ActionRequest action, Game game, string statKey)
    {
        if (action.TargetNpcId.HasValue)
        {
            var npc = game.NpcsAndMonsters.FirstOrDefault(n => n.Id == action.TargetNpcId.Value);
            if (npc is null)
            {
                return null;
            }

            return ReadStatFromJson(npc.StatBlockJson, statKey) ?? npc.Armor;
        }

        if (action.TargetCharacterId.HasValue)
        {
            var character = game.Characters.FirstOrDefault(c => c.Id == action.TargetCharacterId.Value);
            if (character is null)
            {
                return null;
            }

            if (statKey.Equals("armor", StringComparison.OrdinalIgnoreCase))
            {
                return character.Armor;
            }

            return ReadStatFromJson(character.RulesetDataJson, statKey);
        }

        return null;
    }

    private static int? ReadStatFromJson(string json, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.TryGetProperty("attributes", out var attrs)
                && attrs.TryGetProperty(key, out var attrVal)
                && attrVal.TryGetInt32(out var attrInt))
            {
                return attrInt;
            }

            if (doc.RootElement.TryGetProperty("gameValues", out var gvs)
                && gvs.TryGetProperty(key, out var gvVal)
                && gvVal.TryGetInt32(out var gvInt))
            {
                return gvInt;
            }

            if (doc.RootElement.TryGetProperty(key, out var direct)
                && direct.TryGetInt32(out var directInt))
            {
                return directInt;
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return null;
    }

    private static (string Type, Guid Id)? ResolveEffectTarget(string targetExpr, ActionRequest action)
    {
        if (targetExpr.Contains("action.target", StringComparison.OrdinalIgnoreCase))
        {
            if (action.TargetCharacterId.HasValue)
            {
                return ("Character", action.TargetCharacterId.Value);
            }

            if (action.TargetNpcId.HasValue)
            {
                return ("NpcOrMonster", action.TargetNpcId.Value);
            }
        }

        if (targetExpr.Contains("self", StringComparison.OrdinalIgnoreCase)
            && action.ActorCharacterId.HasValue)
        {
            return ("Character", action.ActorCharacterId.Value);
        }

        return null;
    }

    private static bool EvaluateStatusCondition(string? condition, string outcome, int primary)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return outcome == RollChainOutcomes.Success;
        }

        if (condition.Contains("result == 'success'", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("result==\"success\"", StringComparison.OrdinalIgnoreCase))
        {
            return outcome == RollChainOutcomes.Success;
        }

        var totalMatch = System.Text.RegularExpressions.Regex.Match(
            condition,
            @"roll\.total\s*>=\s*(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (totalMatch.Success)
        {
            return primary >= int.Parse(totalMatch.Groups[1].Value);
        }

        return outcome == RollChainOutcomes.Success;
    }

    public static List<StatChangeRequest> MergePendingEffects(string? pendingJson, IEnumerable<StatChangeRequest> newChanges)
    {
        var existing = new List<StatChangeRequest>();
        if (!string.IsNullOrWhiteSpace(pendingJson))
        {
            try
            {
                existing = JsonSerializer.Deserialize<List<StatChangeRequest>>(pendingJson, JsonOptions) ?? new List<StatChangeRequest>();
            }
            catch (JsonException)
            {
                existing = new List<StatChangeRequest>();
            }
        }

        existing.AddRange(newChanges);
        return existing;
    }

    public static string SerializePendingEffects(IEnumerable<StatChangeRequest> changes) =>
        JsonSerializer.Serialize(changes, JsonOptions);
}
