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

    [JsonPropertyName("items")]
    public IEnumerable<RulesetItemDefinition> Items { get; set; } = Array.Empty<RulesetItemDefinition>();

    [JsonPropertyName("npcTemplates")]
    public IEnumerable<RulesetNpcTemplateDefinition> NpcTemplates { get; set; } = Array.Empty<RulesetNpcTemplateDefinition>();

    /// <summary>
    /// Describes how skill checks and attribute checks are resolved in this ruleset.
    /// When absent, skill/attribute checks fall back to a single generic die roll.
    /// </summary>
    [JsonPropertyName("rollMechanics")]
    public RulesetRollMechanicsDefinition? RollMechanics { get; set; }

    /// <summary>
    /// Status conditions available in this ruleset (e.g. stunned, panicking, broken).
    /// The DM can apply and remove these during action resolution.
    /// </summary>
    [JsonPropertyName("statusEffects")]
    public IEnumerable<RulesetStatusEffectDefinition> StatusEffects { get; set; } = Array.Empty<RulesetStatusEffectDefinition>();

    /// <summary>How initiative order is determined when combat starts.</summary>
    [JsonPropertyName("initiative")]
    public RulesetInitiativeDefinition? Initiative { get; set; }
}

/// <summary>Rules for rolling initiative at the start of combat.</summary>
public class RulesetInitiativeDefinition
{
    [JsonPropertyName("attribute")]
    public string Attribute { get; set; } = string.Empty;

    [JsonPropertyName("skill")]
    public string Skill { get; set; } = string.Empty;

    /// <summary>Optional tie-breaker dice notation (e.g. "1d6", "1d8").</summary>
    [JsonPropertyName("tieBreakerDice")]
    public string TieBreakerDice { get; set; } = "1d6";

    [JsonPropertyName("guidanceText")]
    public string? GuidanceText { get; set; }
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

    /// <summary>Maximum rank per skill during character creation (e.g. 1 for D&amp;D proficiency, 3 for Alien RPG).</summary>
    [JsonPropertyName("maxSkillRank")]
    public int? MaxSkillRank { get; set; }

    /// <summary>Item keys the player may choose one of when creating this class.</summary>
    [JsonPropertyName("startingItemOptions")]
    public IEnumerable<string> StartingItemOptions { get; set; } = Array.Empty<string>();

    /// <summary>Primary spellcasting attribute for this class (e.g. intelligence for wizard).</summary>
    [JsonPropertyName("spellcastingAttribute")]
    public string? SpellcastingAttribute { get; set; }
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

    /// <summary>Actor must have this item in inventory to use the action.</summary>
    [JsonPropertyName("requiredItemKey")]
    public string? RequiredItemKey { get; set; }

    [JsonPropertyName("roll")]
    public RulesetRollDefinition Roll { get; set; } = new();

    /// <summary>Multi-step roll sequence (attack → damage, etc.) driven by JSON.</summary>
    [JsonPropertyName("rollChain")]
    public IEnumerable<RulesetRollChainStepDefinition> RollChain { get; set; } = Array.Empty<RulesetRollChainStepDefinition>();

    /// <summary>combat | exploration — combat actions appear during initiative turns.</summary>
    [JsonPropertyName("context")]
    public string? Context { get; set; }

    /// <summary>weaponAttack | spellAttack | spellSave | autoHit — drives target and resolution rules.</summary>
    [JsonPropertyName("attackType")]
    public string? AttackType { get; set; }

    /// <summary>When true, the action must specify a target (for AC or spell save DC context).</summary>
    [JsonPropertyName("requiresTarget")]
    public bool? RequiresTarget { get; set; }

    /// <summary>Spellcasting attribute for spell attacks and saves (defaults from class when omitted).</summary>
    [JsonPropertyName("spellcastingAttribute")]
    public string? SpellcastingAttribute { get; set; }

    /// <summary>Damage dice for spell or unarmed actions without a required weapon item.</summary>
    [JsonPropertyName("damageRoll")]
    public RulesetDamageRollDefinition? DamageRoll { get; set; }
}

public class RulesetItemDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "gear";

    [JsonPropertyName("modifiers")]
    public IEnumerable<RulesetModifierDefinition> Modifiers { get; set; } = Array.Empty<RulesetModifierDefinition>();

    /// <summary>When set, overrides the action roll for attacks made with this item.</summary>
    [JsonPropertyName("attackRoll")]
    public RulesetRollDefinition? AttackRoll { get; set; }

    [JsonPropertyName("damageRoll")]
    public RulesetDamageRollDefinition? DamageRoll { get; set; }
}

public class RulesetDamageRollDefinition
{
    [JsonPropertyName("notation")]
    public string Notation { get; set; } = string.Empty;

    [JsonPropertyName("bonusAttribute")]
    public string? BonusAttribute { get; set; }

    [JsonPropertyName("flatBonus")]
    public int? FlatBonus { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
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

    /// <summary>Adds this many dice to a pool roll (not scaled by stat value).</summary>
    [JsonPropertyName("flatDice")]
    public int? FlatDice { get; set; }

    /// <summary>Flat bonus added to a d20 attack total.</summary>
    [JsonPropertyName("attackBonus")]
    public int? AttackBonus { get; set; }
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

    /// <summary>Optional scenario tag for grouping adventure-specific presets (e.g. "hopes-last-day").</summary>
    [JsonPropertyName("scenario")]
    public string? Scenario { get; set; }

    /// <summary>Flavor text shown when selecting this template.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>NPC or Monster; defaults to NPC when omitted.</summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("maxHealth")]
    public int? MaxHealth { get; set; }

    [JsonPropertyName("health")]
    public int? Health { get; set; }

    [JsonPropertyName("armor")]
    public int? Armor { get; set; }

    [JsonPropertyName("defaultStats")]
    public Dictionary<string, object> DefaultStats { get; set; } = new();
}

/// <summary>A named condition that can be applied to characters and NPCs during a session.</summary>
public class RulesetStatusEffectDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>True if this status is harmful (renders as red/orange badge); false for buffs.</summary>
    [JsonPropertyName("isNegative")]
    public bool IsNegative { get; set; } = true;

    /// <summary>
    /// When set, the status is automatically applied when a character's HP reaches this threshold.
    /// Null means no automatic threshold. Use 0 for "broken at zero HP".
    /// </summary>
    [JsonPropertyName("autoApplyAtHpThreshold")]
    public int? AutoApplyAtHpThreshold { get; set; }
}
