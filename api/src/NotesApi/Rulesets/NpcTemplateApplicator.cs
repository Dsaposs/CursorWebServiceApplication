using System.Text.Json;
using NotesApi.DTOs;

namespace NotesApi.Rulesets;

/// <summary>Builds NPC create/update payloads from ruleset <c>npcTemplates</c> entries.</summary>
public static class NpcTemplateApplicator
{
    private static readonly JsonSerializerOptions SerializeOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildCreateRequest(
        RulesetDefinition definition,
        string templateKey,
        string? nameOverride,
        out CreateNpcRequest request,
        out string? error)
    {
        request = new CreateNpcRequest();
        error = null;

        var template = definition.NpcTemplates.FirstOrDefault(t =>
            string.Equals(t.Key, templateKey, StringComparison.OrdinalIgnoreCase));
        if (template is null)
        {
            error = $"Unknown NPC template '{templateKey}'.";
            return false;
        }

        var defaultMax = ReadRulesetDefaultMaxHealth(definition);
        var maxHealth = template.MaxHealth ?? defaultMax;
        var health = template.Health ?? maxHealth;
        var armor = template.Armor ?? ReadRulesetDefaultArmor(definition);

        request = new CreateNpcRequest
        {
            Name = string.IsNullOrWhiteSpace(nameOverride) ? template.Label : nameOverride.Trim(),
            Kind = string.IsNullOrWhiteSpace(template.Kind) ? "NPC" : template.Kind.Trim(),
            MaxHealth = maxHealth <= 0 ? 1 : maxHealth,
            Health = health <= 0 ? maxHealth : health,
            Armor = Math.Max(0, armor),
            StatBlockJson = SerializeStatBlock(template.DefaultStats),
        };

        return true;
    }

    public static string SerializeStatBlock(IReadOnlyDictionary<string, object> defaultStats)
    {
        if (defaultStats.Count == 0)
        {
            return "{}";
        }

        var normalized = defaultStats.ToDictionary(
            pair => pair.Key,
            pair => NormalizeJsonValue(pair.Value));

        return JsonSerializer.Serialize(normalized, SerializeOptions);
    }

    private static int ReadRulesetDefaultMaxHealth(RulesetDefinition definition)
    {
        if (definition.Character.Vitals.TryGetValue("health", out var healthNode)
            && healthNode is JsonElement healthElement
            && healthElement.ValueKind == JsonValueKind.Object
            && healthElement.TryGetProperty("defaultMax", out var defaultMaxElement)
            && defaultMaxElement.TryGetInt32(out var defaultMax))
        {
            return defaultMax;
        }

        return 10;
    }

    private static int ReadRulesetDefaultArmor(RulesetDefinition definition)
    {
        foreach (var vitalKey in new[] { "armor", "armorClass" })
        {
            if (definition.Character.Vitals.TryGetValue(vitalKey, out var armorNode)
                && armorNode is JsonElement armorElement
                && armorElement.ValueKind == JsonValueKind.Object
                && armorElement.TryGetProperty("default", out var defaultElement)
                && defaultElement.TryGetInt32(out var defaultArmor))
            {
                return defaultArmor;
            }
        }

        return 0;
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(property => property.Name, property => NormalizeJsonValue(property.Value)),
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(item => NormalizeJsonValue(item))
                    .ToList(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var integer)
                    ? integer
                    : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.GetRawText(),
            };
        }

        return value;
    }
}
