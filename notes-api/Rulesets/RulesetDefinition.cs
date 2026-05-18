using System.Text.Json.Serialization;

namespace NotesApi.Rulesets;

public class RulesetDefinition
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

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
}

public class RulesetDiceDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("notation")]
    public string Notation { get; set; } = string.Empty;
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

    [JsonPropertyName("attribute")]
    public string Attribute { get; set; } = string.Empty;

    [JsonPropertyName("skill")]
    public string Skill { get; set; } = string.Empty;

    [JsonPropertyName("modifiers")]
    public IEnumerable<RulesetModifierDefinition> Modifiers { get; set; } = Array.Empty<RulesetModifierDefinition>();

    [JsonPropertyName("successRule")]
    public string SuccessRule { get; set; } = string.Empty;
}

public class RulesetModifierDefinition
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("dicePerPoint")]
    public int? DicePerPoint { get; set; }
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
