using System.Text.Json.Serialization;

namespace NotesApi.Rulesets;

public class RulesetDefinition
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>Key of the shared dice roller (e.g. d6-pool, d20-check).</summary>
    [JsonPropertyName("diceRollerKey")]
    public string? DiceRollerKey { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("diceNotation")]
    public string DiceNotation { get; set; } = string.Empty;

    [JsonPropertyName("dice")]
    public IEnumerable<RulesetDiceDefinition> Dice { get; set; } = Array.Empty<RulesetDiceDefinition>();

    [JsonPropertyName("character")]
    public RulesetCharacterDefinition Character { get; set; } = new();

    [JsonPropertyName("actions")]
    public IEnumerable<RulesetActionDefinition> Actions { get; set; } = Array.Empty<RulesetActionDefinition>();

    [JsonPropertyName("npcTemplates")]
    public IEnumerable<RulesetNpcTemplateDefinition> NpcTemplates { get; set; } = Array.Empty<RulesetNpcTemplateDefinition>();

    /// <summary>
    /// Describes how skill checks and attribute checks are resolved in this ruleset.
    /// When absent, skill/attribute checks fall back to a single generic die roll.
    /// </summary>
    [JsonPropertyName("rollMechanics")]
    public RulesetRollMechanicsDefinition? RollMechanics { get; set; }
}

public class RulesetDiceDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("notation")]
    public string Notation { get; set; } = string.Empty;

    /// <summary>
    /// When set, each die meeting or exceeding this value counts as one success
    /// instead of summing all die values. Used for dice-pool systems like Alien RPG (target = 6).
    /// </summary>
    [JsonPropertyName("successTarget")]
    public int? SuccessTarget { get; set; }
}

public class RulesetCharacterDefinition
{
    [JsonPropertyName("vitals")]
    public Dictionary<string, object> Vitals { get; set; } = new();

    [JsonPropertyName("attributes")]
    public IEnumerable<RulesetAttributeDefinition> Attributes { get; set; } = Array.Empty<RulesetAttributeDefinition>();

    [JsonPropertyName("gameValues")]
    public IEnumerable<RulesetGameValueDefinition> GameValues { get; set; } = Array.Empty<RulesetGameValueDefinition>();

    [JsonPropertyName("classes")]
    public IEnumerable<RulesetClassDefinition> Classes { get; set; } = Array.Empty<RulesetClassDefinition>();

    [JsonPropertyName("skills")]
    public IEnumerable<RulesetSkillDefinition> Skills { get; set; } = Array.Empty<RulesetSkillDefinition>();
}

public class RulesetAttributeDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    public int Default { get; set; }

    [JsonPropertyName("min")]
    public int? Min { get; set; }

    [JsonPropertyName("max")]
    public int? Max { get; set; }
}

public class RulesetGameValueDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "number";

    [JsonPropertyName("default")]
    public object? Default { get; set; }

    [JsonPropertyName("min")]
    public int? Min { get; set; }
}

public class RulesetClassDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("availableSkills")]
    public IEnumerable<string> AvailableSkills { get; set; } = Array.Empty<string>();

    [JsonPropertyName("startingSkillPoints")]
    public int StartingSkillPoints { get; set; }
}

public class RulesetSkillDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("attribute")]
    public string Attribute { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    public int Default { get; set; }
}

public class RulesetActionDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("allowedClasses")]
    public IEnumerable<string> AllowedClasses { get; set; } = Array.Empty<string>();

    [JsonPropertyName("roll")]
    public RulesetRollDefinition Roll { get; set; } = new();
}

public class RulesetRollDefinition
{
    [JsonPropertyName("dice")]
    public string Dice { get; set; } = string.Empty;

    /// <summary>
    /// "attribute+skill" — dice count equals attribute value + skill value.
    /// Omit (or null) for fixed-dice systems like d20.
    /// </summary>
    [JsonPropertyName("dicePoolMode")]
    public string? DicePoolMode { get; set; }

    [JsonPropertyName("attribute")]
    public string Attribute { get; set; } = string.Empty;

    [JsonPropertyName("skill")]
    public string Skill { get; set; } = string.Empty;

    [JsonPropertyName("modifiers")]
    public IEnumerable<RulesetModifierDefinition> Modifiers { get; set; } = Array.Empty<RulesetModifierDefinition>();

    [JsonPropertyName("successRule")]
    public string SuccessRule { get; set; } = string.Empty;

    /// <summary>Default DC for this action when the success rule does not specify one.</summary>
    [JsonPropertyName("difficultyClass")]
    public int? DifficultyClass { get; set; }
}


public class RulesetModifierDefinition
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("dicePerPoint")]
    public int? DicePerPoint { get; set; }

    /// <summary>
    /// When true, dice from this modifier are treated as stress dice (e.g. Alien RPG).
    /// A roll of 1 on a stress die triggers a panic check.
    /// </summary>
    [JsonPropertyName("isStressDice")]
    public bool IsStressDice { get; set; }
}

/// <summary>Defines how free-form skill and attribute checks are resolved for this ruleset.</summary>
public class RulesetRollMechanicsDefinition
{
    /// <summary>How to roll when a player/NPC makes a skill check (not from a predefined action).</summary>
    [JsonPropertyName("skillCheck")]
    public RulesetCheckDefinition? SkillCheck { get; set; }

    /// <summary>How to roll when a player/NPC makes a raw attribute check.</summary>
    [JsonPropertyName("attributeCheck")]
    public RulesetCheckDefinition? AttributeCheck { get; set; }
}

public class RulesetCheckDefinition
{
    /// <summary>Key into the dice array that defines which die type and successTarget to use.</summary>
    [JsonPropertyName("diceKey")]
    public string DiceKey { get; set; } = string.Empty;

    /// <summary>
    /// "attribute+skill" — pool = attribute value + skill value.
    /// "attribute"       — pool = attribute value only.
    /// "fixed"           — pool uses the diceKey notation as-is.
    /// </summary>
    [JsonPropertyName("poolMode")]
    public string PoolMode { get; set; } = "fixed";

    /// <summary>Extra modifiers applied on top of the base pool (e.g. stress dice).</summary>
    [JsonPropertyName("modifiers")]
    public IEnumerable<RulesetModifierDefinition> Modifiers { get; set; } = Array.Empty<RulesetModifierDefinition>();

    /// <summary>Human-readable description of what constitutes success and failure.</summary>
    [JsonPropertyName("successRule")]
    public string? SuccessRule { get; set; }

    /// <summary>Default DC for d20 checks when the success rule does not specify one.</summary>
    [JsonPropertyName("difficultyClass")]
    public int? DifficultyClass { get; set; }
}

public class RulesetNpcTemplateDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("defaultStats")]
    public Dictionary<string, object> DefaultStats { get; set; } = new();
}
