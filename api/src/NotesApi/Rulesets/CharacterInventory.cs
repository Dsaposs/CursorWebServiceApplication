using System.Text.Json;
using System.Text.Json.Nodes;
using NotesApi.Models;

namespace NotesApi.Rulesets;

public sealed record InventoryEntry(string ItemKey, int Quantity);

public static class CharacterInventory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<InventoryEntry> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<InventoryEntry>();
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<InventoryEntryDto>>(json, JsonOptions) ?? new List<InventoryEntryDto>();
            return entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.ItemKey) && entry.Quantity > 0)
                .Select(entry => new InventoryEntry(entry.ItemKey.Trim(), entry.Quantity))
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<InventoryEntry>();
        }
    }

    public static IReadOnlyList<InventoryEntry> ParseFromStatBlock(string? statBlockJson)
    {
        if (string.IsNullOrWhiteSpace(statBlockJson))
        {
            return Array.Empty<InventoryEntry>();
        }

        try
        {
            var root = JsonNode.Parse(statBlockJson) as JsonObject;
            if (root?["inventory"] is not JsonArray inventory)
            {
                return Array.Empty<InventoryEntry>();
            }

            return inventory
                .OfType<JsonObject>()
                .Select(entry => new InventoryEntryDto
                {
                    ItemKey = entry["itemKey"]?.GetValue<string>() ?? string.Empty,
                    Quantity = entry["quantity"]?.GetValue<int>() ?? 0,
                })
                .Where(dto => !string.IsNullOrWhiteSpace(dto.ItemKey) && dto.Quantity > 0)
                .Select(dto => new InventoryEntry(dto.ItemKey.Trim(), dto.Quantity))
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<InventoryEntry>();
        }
    }

    public static bool HasItem(IReadOnlyList<InventoryEntry> inventory, string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
        {
            return false;
        }

        return inventory.Any(entry =>
            entry.ItemKey.Equals(itemKey, StringComparison.OrdinalIgnoreCase) && entry.Quantity > 0);
    }

    public static string Serialize(IEnumerable<InventoryEntry> entries, string definitionJson)
    {
        var definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, JsonOptions);
        var validKeys = definition?.Items
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var normalized = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ItemKey) && entry.Quantity > 0)
            .GroupBy(entry => entry.ItemKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new InventoryEntryDto
            {
                ItemKey = group.Key,
                Quantity = group.Sum(entry => entry.Quantity),
            })
            .ToList();

        foreach (var entry in normalized)
        {
            if (!validKeys.Contains(entry.ItemKey))
            {
                throw new ArgumentException($"Item '{entry.ItemKey}' is not defined in this ruleset.");
            }
        }

        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public static void ApplyDeltas(Character character, Dictionary<string, int>? inventoryDeltas)
    {
        if (inventoryDeltas is not { Count: > 0 })
        {
            return;
        }

        var entries = Parse(character.InventoryJson).ToDictionary(
            entry => entry.ItemKey,
            entry => entry.Quantity,
            StringComparer.OrdinalIgnoreCase);

        foreach (var kv in inventoryDeltas)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            var key = kv.Key.Trim();
            var next = (entries.TryGetValue(key, out var current) ? current : 0) + kv.Value;
            if (next <= 0)
            {
                entries.Remove(key);
            }
            else
            {
                entries[key] = next;
            }
        }

        var serialized = entries
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new InventoryEntryDto { ItemKey = kv.Key, Quantity = kv.Value })
            .ToList();

        character.InventoryJson = JsonSerializer.Serialize(serialized, JsonOptions);
        character.UpdatedAt = DateTime.UtcNow;
    }

    private sealed class InventoryEntryDto
    {
        public string ItemKey { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
