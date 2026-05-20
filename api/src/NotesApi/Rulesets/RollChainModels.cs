using System.Text.Json.Serialization;

namespace NotesApi.Rulesets;

public class RulesetRollChainStepDefinition
{
    [JsonPropertyName("step")]
    public string Step { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>Action, Skill, Attribute, or Custom — maps to roll prompt check mode.</summary>
    [JsonPropertyName("checkMode")]
    public string CheckMode { get; set; } = "Action";

    [JsonPropertyName("resultKind")]
    public string ResultKind { get; set; } = "PassFail";

    [JsonPropertyName("guidanceText")]
    public string? GuidanceText { get; set; }

    [JsonPropertyName("customCheckText")]
    public string? CustomCheckText { get; set; }

    /// <summary>When set to equippedWeapon, damage notation comes from the actor's relevant item.</summary>
    [JsonPropertyName("diceSource")]
    public string? DiceSource { get; set; }

    [JsonPropertyName("autoResolve")]
    public RulesetRollChainAutoResolveDefinition? AutoResolve { get; set; }

    [JsonPropertyName("onSuccess")]
    public string? OnSuccess { get; set; }

    [JsonPropertyName("onFailure")]
    public string? OnFailure { get; set; }

    [JsonPropertyName("onComplete")]
    public string? OnComplete { get; set; }

    [JsonPropertyName("applyEffects")]
    public IEnumerable<RulesetRollChainEffectDefinition> ApplyEffects { get; set; } = Array.Empty<RulesetRollChainEffectDefinition>();

    [JsonPropertyName("applyStatuses")]
    public IEnumerable<RulesetRollChainStatusDefinition> ApplyStatuses { get; set; } = Array.Empty<RulesetRollChainStatusDefinition>();
}

public class RulesetRollChainAutoResolveDefinition
{
    /// <summary>
    /// Simple conditions: successes >= N, total >= target.armor, total >= target.stats[key].
    /// </summary>
    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("fallback")]
    public string Fallback { get; set; } = "dm_input";
}

public class RulesetRollChainEffectDefinition
{
    [JsonPropertyName("target")]
    public string Target { get; set; } = "action.target";

    [JsonPropertyName("stat")]
    public string Stat { get; set; } = "health";

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "subtract";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "roll.total";

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("minResult")]
    public int? MinResult { get; set; }
}

public class RulesetRollChainStatusDefinition
{
    [JsonPropertyName("target")]
    public string Target { get; set; } = "self";

    [JsonPropertyName("statusKey")]
    public string StatusKey { get; set; } = string.Empty;

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

public class RollResultData
{
    [JsonPropertyName("rollerKey")]
    public string? RollerKey { get; set; }

    [JsonPropertyName("resultKind")]
    public string? ResultKind { get; set; }

    [JsonPropertyName("groups")]
    public List<RollResultDieGroup> Groups { get; set; } = new();

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("successes")]
    public int? Successes { get; set; }

    [JsonPropertyName("naturalDie")]
    public int? NaturalDie { get; set; }

    [JsonPropertyName("pushed")]
    public bool Pushed { get; set; }

    [JsonPropertyName("stressGained")]
    public int StressGained { get; set; }
}

public class RollResultDieGroup
{
    [JsonPropertyName("notation")]
    public string Notation { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("values")]
    public List<int> Values { get; set; } = new();

    [JsonPropertyName("isStress")]
    public bool IsStress { get; set; }
}

public class RollChainState
{
    [JsonPropertyName("stepIndex")]
    public int StepIndex { get; set; }

    [JsonPropertyName("lastOutcome")]
    public string? LastOutcome { get; set; }
}

public static class RollChainOutcomes
{
    public const string Success = "success";
    public const string Failure = "failure";
    public const string NeedsDm = "needs_dm";
}
