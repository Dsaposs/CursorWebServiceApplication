using System.Text.Json;
using NotesApi.Models;

namespace NotesApi.Rulesets;

public static class ActionTargetStats
{
    public static int? ResolveTargetArmor(ActionRequest action, Game game)
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

    public static int? ResolveTargetStat(ActionRequest action, Game game, string statKey)
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
            // ignore malformed stat blocks
        }

        return null;
    }
}
