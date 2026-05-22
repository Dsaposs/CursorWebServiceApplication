using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotesApi.DTOs;

using Asp.Versioning;
namespace NotesApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/dice")]
[Authorize]
public class DiceController : ControllerBase
{
    private static readonly Regex SpecRegex = new(@"^(\d+)d(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Rolls a standard dice expression server-side using a cryptographically secure RNG.
    /// Supports notation like "1d20", "2d6", "4d6". Max 20 dice, max d100 faces.
    /// </summary>
    [HttpPost("roll")]
    public ActionResult<DiceRollResponse> Roll(DiceRollRequest request)
    {
        var match = SpecRegex.Match(request.Spec.Trim());
        if (!match.Success)
        {
            return BadRequest(new { errors = new[] { "Invalid dice spec. Use notation like '1d20', '2d6', or '4d6'." } });
        }

        var count = int.Parse(match.Groups[1].Value);
        var faces = int.Parse(match.Groups[2].Value);

        if (count is < 1 or > 20)
        {
            return BadRequest(new { errors = new[] { "Dice count must be between 1 and 20." } });
        }

        if (faces is < 2 or > 100)
        {
            return BadRequest(new { errors = new[] { "Die faces must be between 2 and 100." } });
        }

        var rolls = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            rolls.Add(RandomNumberGenerator.GetInt32(1, faces + 1));
        }

        return Ok(new DiceRollResponse
        {
            Spec = request.Spec.Trim().ToLowerInvariant(),
            Rolls = rolls,
            Total = rolls.Sum(),
        });
    }
}
