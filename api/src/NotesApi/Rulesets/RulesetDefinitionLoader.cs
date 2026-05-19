using System.Text.RegularExpressions;

namespace NotesApi.Rulesets;

/// <summary>Loads ruleset JSON definitions and schemas from the Rulesets content directory.</summary>
public static class RulesetDefinitionLoader
{
    private static readonly Regex VersionedDefinitionPattern = new(
        @"^(.+)\.v(\d+)\.json$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string DefinitionsDirectory => Path.Combine(RulesetsRoot, "Definitions");

    private static string RulesetsRoot => Path.Combine(AppContext.BaseDirectory, "Rulesets");

    public static int GetLatestSchemaVersion(string rulesetCode)
    {
        if (!Directory.Exists(DefinitionsDirectory))
        {
            throw new DirectoryNotFoundException($"Ruleset definitions directory not found: {DefinitionsDirectory}");
        }

        var latestVersion = Directory
            .EnumerateFiles(DefinitionsDirectory, $"{rulesetCode}.v*.json")
            .Select(path => VersionedDefinitionPattern.Match(Path.GetFileName(path)))
            .Where(match => match.Success && match.Groups[1].Value.Equals(rulesetCode, StringComparison.OrdinalIgnoreCase))
            .Select(match => int.Parse(match.Groups[2].Value))
            .DefaultIfEmpty()
            .Max();

        if (latestVersion == 0)
        {
            throw new FileNotFoundException($"No versioned definition files found for ruleset '{rulesetCode}'.", DefinitionsDirectory);
        }

        return latestVersion;
    }

    public static string LoadLatestDefinition(string rulesetCode) =>
        LoadDefinition(rulesetCode, GetLatestSchemaVersion(rulesetCode));

    public static string LoadDefinition(string rulesetCode, int schemaVersion)
    {
        var path = Path.Combine(DefinitionsDirectory, $"{rulesetCode}.v{schemaVersion}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Ruleset definition file not found for '{rulesetCode}' (schema v{schemaVersion}).", path);
        }

        return File.ReadAllText(path);
    }

    public static string LoadCharacterTemplate(string rulesetCode)
    {
        var path = Path.Combine(DefinitionsDirectory, $"{rulesetCode}.character-template.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Character template file not found for '{rulesetCode}'.", path);
        }

        return File.ReadAllText(path);
    }

    public static string LoadSchema(int schemaVersion)
    {
        var path = Path.Combine(RulesetsRoot, "Schemas", $"ruleset-definition.v{schemaVersion}.schema.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Ruleset schema file not found for version {schemaVersion}.", path);
        }

        return File.ReadAllText(path);
    }
}
