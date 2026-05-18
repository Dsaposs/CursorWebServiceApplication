using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public GamesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<GameResponse>>> GetAll()
    {
        var userId = this.UserId();
        var games = await _db.Games
            .AsNoTracking()
            .Include(g => g.Ruleset)
            .Include(g => g.Characters)
            .Include(g => g.NpcsAndMonsters)
            .Include(g => g.Sessions)
            .Where(g => g.DmUserId == userId)
            .OrderByDescending(g => g.UpdatedAt)
            .ToListAsync();

        return Ok(games.Select(this.ToGameResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GameResponse>> GetById(Guid id)
    {
        var game = await _db.GetOwnedGameAsync(id, this.UserId());
        return game is null ? NotFound() : Ok(this.ToGameResponse(game));
    }

    [HttpPost]
    public async Task<ActionResult<GameResponse>> Create(CreateGameRequest request)
    {
        var userId = this.UserId();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { errors = new[] { "Game name is required." } });
        }

        if (await GameNameExistsAsync(name))
        {
            return BadRequest(new { errors = new[] { "A game with this name already exists." } });
        }

        var rulesetExists = await _db.Rulesets.AnyAsync(r => r.Code == request.RulesetCode);
        if (!rulesetExists)
        {
            return BadRequest(new { errors = new[] { "Select a valid ruleset." } });
        }

        var now = DateTime.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            DmUserId = userId,
            RulesetCode = request.RulesetCode,
            Name = name,
            Description = request.Description,
            InviteCode = await NewUniqueInviteCodeAsync(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        game = (await _db.GetOwnedGameAsync(game.Id, userId))!;
        return CreatedAtAction(nameof(GetById), new { id = game.Id }, this.ToGameResponse(game));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GameResponse>> Update(Guid id, UpdateGameRequest request)
    {
        var game = await _db.GetOwnedGameAsync(id, this.UserId());
        if (game is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { errors = new[] { "Game name is required." } });
        }

        if (await GameNameExistsAsync(name, id))
        {
            return BadRequest(new { errors = new[] { "A game with this name already exists." } });
        }

        game.Name = name;
        game.Description = request.Description;
        game.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(this.ToGameResponse(game));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var game = await _db.GetOwnedGameAsync(id, this.UserId());
        if (game is null)
        {
            return NotFound();
        }

        _db.Games.Remove(game);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> NewUniqueInviteCodeAsync()
    {
        string code;
        do
        {
            code = ControllerHelpers.NewCode();
        }
        while (await _db.Games.AnyAsync(g => g.InviteCode == code));

        return code;
    }

    private async Task<bool> GameNameExistsAsync(string name, Guid? excludingGameId = null)
    {
        var normalizedName = name.ToLower();
        return await _db.Games.AnyAsync(g =>
            g.Name.ToLower() == normalizedName
            && (!excludingGameId.HasValue || g.Id != excludingGameId.Value));
    }
}
