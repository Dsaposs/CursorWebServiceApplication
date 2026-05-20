using System.Text.Json;
using System.Text.RegularExpressions;

namespace NotesApi.Rulesets;

public static class RollResultParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Regex SuccessCountRegex = new(
        @"(\d+)\s+success(?:es)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TotalAfterEqualsRegex = new(
        @"=\s*(\d+)\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex TotalAfterArrowRegex = new(
        @"→\s*total\s+(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static RollResultData? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RollResultData>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(RollResultData data) =>
        JsonSerializer.Serialize(data, JsonOptions);

    public static RollResultData ParseFromSummary(string rollSummary, string? rollerKey, string? resultKind)
    {
        var structured = TryParseJson(rollSummary);
        if (structured is not null)
        {
            return structured;
        }

        var data = new RollResultData
        {
            RollerKey = rollerKey,
            ResultKind = resultKind,
        };

        if (string.Equals(resultKind, "Total", StringComparison.OrdinalIgnoreCase))
        {
            data.Total = ParseTotal(rollSummary);
        }
        else
        {
            data.Successes = ParseSuccesses(rollSummary);
        }

        return data;
    }

    public static int? GetPrimaryValue(RollResultData? data, string? resultKind, string? rollSummary)
    {
        if (data is not null)
        {
            if (string.Equals(resultKind, "Total", StringComparison.OrdinalIgnoreCase) && data.Total.HasValue)
            {
                return data.Total;
            }

            if (data.Successes.HasValue)
            {
                return data.Successes;
            }

            if (data.Total.HasValue)
            {
                return data.Total;
            }
        }

        if (string.IsNullOrWhiteSpace(rollSummary))
        {
            return null;
        }

        return string.Equals(resultKind, "Total", StringComparison.OrdinalIgnoreCase)
            ? ParseTotal(rollSummary)
            : ParseSuccesses(rollSummary);
    }

    private static int ParseSuccesses(string rollSummary)
    {
        var matches = SuccessCountRegex.Matches(rollSummary);
        if (matches.Count == 0)
        {
            return 0;
        }

        return int.Parse(matches[^1].Groups[1].Value);
    }

    private static int? ParseTotal(string rollSummary)
    {
        var equalsMatch = TotalAfterEqualsRegex.Match(rollSummary);
        if (equalsMatch.Success)
        {
            return int.Parse(equalsMatch.Groups[1].Value);
        }

        var arrowMatch = TotalAfterArrowRegex.Match(rollSummary);
        if (arrowMatch.Success)
        {
            return int.Parse(arrowMatch.Groups[1].Value);
        }

        return null;
    }
}
