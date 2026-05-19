using System.Text.Json;

namespace NotesApi.Rulesets;

public class RulesetDefinitionValidator
{
    private static readonly HashSet<string> KnownDiceRollerKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "d6-pool",
        "d20-check",
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public RulesetValidationResult Validate(string definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return RulesetValidationResult.Invalid("Ruleset definition JSON is required.");
        }

        RulesetDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return RulesetValidationResult.Invalid($"Ruleset JSON is invalid: {ex.Message}");
        }

        if (definition is null)
        {
            return RulesetValidationResult.Invalid("Ruleset definition JSON is empty.");
        }

        var errors = new List<string>();
        Require(definition.SchemaVersion is 1 or 2, "schemaVersion must be 1 or 2.", errors);

        var rollerKey = string.IsNullOrWhiteSpace(definition.DiceRollerKey)
            ? (definition.SchemaVersion >= 2 ? "d6-pool" : "d20-check")
            : definition.DiceRollerKey;
        Require(KnownDiceRollerKeys.Contains(rollerKey), $"diceRollerKey '{rollerKey}' is not a known dice roller.", errors);
        if (definition.SchemaVersion >= 2)
        {
            Require(rollerKey == "d6-pool", "schema version 2 rulesets must use diceRollerKey 'd6-pool'.", errors);
        }

        RequireKey(definition.Code, "code", errors);
        Require(definition.Code.Length <= 50, "code must be 50 characters or fewer.", errors);
        Require(!string.IsNullOrWhiteSpace(definition.DisplayName), "displayName is required.", errors);
        Require(definition.DisplayName.Length <= 120, "displayName must be 120 characters or fewer.", errors);
        Require(!string.IsNullOrWhiteSpace(definition.DiceNotation), "diceNotation is required.", errors);

        var attributeKeys = ValidateUniqueKeys(definition.Character.Attributes.Select(a => a.Key), "character.attributes", errors);
        var skillKeys = ValidateUniqueKeys(definition.Character.Skills.Select(s => s.Key), "character.skills", errors);
        var classKeys = ValidateUniqueKeys(definition.Character.Classes.Select(c => c.Key), "character.classes", errors);
        var gameValueKeys = ValidateUniqueKeys(definition.Character.GameValues.Select(v => v.Key), "character.gameValues", errors);
        var diceKeys = ValidateUniqueKeys(definition.Dice.Select(d => d.Key), "dice", errors);
        ValidateUniqueKeys(definition.Actions.Select(a => a.Key), "actions", errors);
        ValidateUniqueKeys(definition.NpcTemplates.Select(n => n.Key), "npcTemplates", errors);

        foreach (var skill in definition.Character.Skills)
        {
            RequireKey(skill.Key, "skill.key", errors);
            Require(attributeKeys.Contains(skill.Attribute), $"Skill '{skill.Key}' references missing attribute '{skill.Attribute}'.", errors);
        }

        foreach (var characterClass in definition.Character.Classes)
        {
            RequireKey(characterClass.Key, "class.key", errors);
            foreach (var skillKey in characterClass.AvailableSkills)
            {
                Require(skillKeys.Contains(skillKey), $"Class '{characterClass.Key}' references missing skill '{skillKey}'.", errors);
            }
        }

        foreach (var action in definition.Actions)
        {
            RequireKey(action.Key, "action.key", errors);
            Require(!string.IsNullOrWhiteSpace(action.Label), $"Action '{action.Key}' label is required.", errors);
            Require(diceKeys.Contains(action.Roll.Dice), $"Action '{action.Key}' references missing dice definition '{action.Roll.Dice}'.", errors);
            Require(attributeKeys.Contains(action.Roll.Attribute), $"Action '{action.Key}' references missing attribute '{action.Roll.Attribute}'.", errors);
            Require(skillKeys.Contains(action.Roll.Skill), $"Action '{action.Key}' references missing skill '{action.Roll.Skill}'.", errors);

            foreach (var classKey in action.AllowedClasses)
            {
                Require(classKeys.Contains(classKey), $"Action '{action.Key}' references missing class '{classKey}'.", errors);
            }

            foreach (var modifier in action.Roll.Modifiers.Where(m => m.Source.Equals("gameValue", StringComparison.OrdinalIgnoreCase)))
            {
                Require(gameValueKeys.Contains(modifier.Key), $"Action '{action.Key}' modifier references missing game value '{modifier.Key}'.", errors);
            }

            if (definition.SchemaVersion >= 2 && action.Roll.DicePoolMode is not null and not "attribute+skill")
            {
                errors.Add($"Action '{action.Key}' dicePoolMode must be 'attribute+skill' for schema version 2.");
            }
        }

        if (definition.SchemaVersion >= 2)
        {
            ValidateRollMechanics(definition, diceKeys, gameValueKeys, errors);
        }

        return errors.Count > 0
            ? RulesetValidationResult.Invalid(errors)
            : RulesetValidationResult.Valid(definition, Normalize(definition), BuildCharacterTemplate(definition));
    }

    private static string Normalize(RulesetDefinition definition) => JsonSerializer.Serialize(definition, JsonOptions);

    private static string BuildCharacterTemplate(RulesetDefinition definition)
    {
        var attributes = definition.Character.Attributes.ToDictionary(a => a.Key, a => (object)a.Default);
        var skills = definition.Character.Skills.ToDictionary(s => s.Key, s => (object)s.Default);
        var gameValues = definition.Character.GameValues.ToDictionary(v => v.Key, v => v.Default ?? 0);

        return JsonSerializer.Serialize(new
        {
            attributes,
            skills,
            gameValues,
            experience = 0,
        }, JsonOptions);
    }

    private static HashSet<string> ValidateUniqueKeys(IEnumerable<string> keys, string path, List<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"{path} contains an item with a missing key.");
                continue;
            }

            if (!seen.Add(key))
            {
                duplicates.Add(key);
            }
        }

        foreach (var duplicate in duplicates)
        {
            errors.Add($"{path} contains duplicate key '{duplicate}'.");
        }

        return seen;
    }

    private static void RequireKey(string value, string name, List<string> errors) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{name} is required.", errors);

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition)
        {
            errors.Add(message);
        }
    }

    private static void ValidateRollMechanics(
        RulesetDefinition definition,
        HashSet<string> diceKeys,
        HashSet<string> gameValueKeys,
        List<string> errors)
    {
        if (definition.RollMechanics is null)
        {
            errors.Add("rollMechanics is required for schema version 2.");
            return;
        }

        ValidateCheckDefinition(definition.RollMechanics.SkillCheck, "rollMechanics.skillCheck", diceKeys, gameValueKeys, errors);
        ValidateCheckDefinition(definition.RollMechanics.AttributeCheck, "rollMechanics.attributeCheck", diceKeys, gameValueKeys, errors);
    }

    private static void ValidateCheckDefinition(
        RulesetCheckDefinition? check,
        string path,
        HashSet<string> diceKeys,
        HashSet<string> gameValueKeys,
        List<string> errors)
    {
        if (check is null)
        {
            errors.Add($"{path} is required for schema version 2.");
            return;
        }

        Require(diceKeys.Contains(check.DiceKey), $"{path} references missing dice definition '{check.DiceKey}'.", errors);
        Require(
            check.PoolMode is "attribute+skill" or "attribute" or "fixed",
            $"{path}.poolMode must be 'attribute+skill', 'attribute', or 'fixed'.",
            errors);

        foreach (var modifier in check.Modifiers.Where(m => m.Source.Equals("gameValue", StringComparison.OrdinalIgnoreCase)))
        {
            Require(gameValueKeys.Contains(modifier.Key), $"{path} modifier references missing game value '{modifier.Key}'.", errors);
        }
    }
}

public class RulesetValidationResult
{
    private RulesetValidationResult(
        bool isValid,
        RulesetDefinition? definition,
        string normalizedJson,
        string characterTemplateJson,
        IEnumerable<string> errors)
    {
        IsValid = isValid;
        Definition = definition;
        NormalizedJson = normalizedJson;
        CharacterTemplateJson = characterTemplateJson;
        Errors = errors;
    }

    public bool IsValid { get; }
    public RulesetDefinition? Definition { get; }
    public string NormalizedJson { get; }
    public string CharacterTemplateJson { get; }
    public IEnumerable<string> Errors { get; }

    public static RulesetValidationResult Valid(RulesetDefinition definition, string normalizedJson, string characterTemplateJson) =>
        new(true, definition, normalizedJson, characterTemplateJson, Array.Empty<string>());

    public static RulesetValidationResult Invalid(params string[] errors) =>
        Invalid((IEnumerable<string>)errors);

    public static RulesetValidationResult Invalid(IEnumerable<string> errors) =>
        new(false, null, string.Empty, "{}", errors);
}
