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

    /// <summary>
    /// Parses the session's NPC visibility JSON into a dictionary keyed by NPC ID string.
    /// Missing keys default to "Visible".
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

    public static async Task<Game?> GetOwnedGameAsync(this ApplicationDbContext db, Guid gameId, string userId) =>
        await db.Games
            .Include(g => g.Ruleset)
            .Include(g => g.Characters)
            .Include(g => g.NpcsAndMonsters)
            .Include(g => g.Sessions)
            .FirstOrDefaultAsync(g => g.Id == gameId && g.DmUserId == userId);
}
