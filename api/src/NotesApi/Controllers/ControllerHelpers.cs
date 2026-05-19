using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.Models;

namespace NotesApi.Controllers;

public static partial class ControllerHelpers
{
    public static string UserId(this ControllerBase controller) =>
        controller.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User id missing.");

    public static string NewCode() => Convert.ToHexString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty)[..12].ToLowerInvariant();

    public static string NewToken() => Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();

    public static string JoinUrl(this ControllerBase controller, string path) => path;

    public const string NpcVisibilityVisible = "Visible";
    public const string NpcVisibilityHidden = "Hidden";

    /// <summary>
    /// Parses the session's NPC visibility JSON. Missing keys default to Hidden.
    /// Legacy "Obscured" values are treated as Hidden.
    /// </summary>
    public static Dictionary<string, string> ParseNpcVisibilities(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "{}")
            return new Dictionary<string, string>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    public static string NormalizeNpcVisibility(string? visibility) =>
        string.Equals(visibility, NpcVisibilityVisible, StringComparison.OrdinalIgnoreCase)
            ? NpcVisibilityVisible
            : NpcVisibilityHidden;

    public static string GetNpcVisibility(Guid npcId, Dictionary<string, string>? visibilities) =>
        NormalizeNpcVisibility(visibilities?.GetValueOrDefault(npcId.ToString()));

    public static bool IsNpcVisible(Guid npcId, Dictionary<string, string>? visibilities) =>
        GetNpcVisibility(npcId, visibilities) == NpcVisibilityVisible;

    public static async Task<Game?> GetOwnedGameAsync(this ApplicationDbContext db, Guid gameId, string userId) =>
        await db.Games
            .Include(g => g.Ruleset)
            .Include(g => g.Characters)
            .Include(g => g.NpcsAndMonsters)
            .Include(g => g.Sessions)
            .FirstOrDefaultAsync(g => g.Id == gameId && g.DmUserId == userId);
}
