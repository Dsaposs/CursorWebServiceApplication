using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Rulesets;

using Asp.Versioning;
namespace NotesApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/games/{gameId:guid}/characters")]
[Authorize]
public class CharactersController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CharactersController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPut("{characterId:guid}/inventory")]
    public async Task<ActionResult<CharacterResponse>> UpdateInventory(
        Guid gameId,
        Guid characterId,
        UpdateCharacterInventoryRequest request)
    {
        var game = await _db.GetOwnedGameAsync(gameId, this.UserId());
        if (game is null)
        {
            return NotFound();
        }

        var character = await _db.Characters.FirstOrDefaultAsync(c => c.GameId == game.Id && c.Id == characterId);
        if (character is null)
        {
            return NotFound();
        }

        var definition = await _db.Rulesets.AsNoTracking()
            .Where(r => r.Code == game.RulesetCode)
            .Select(r => r.DefinitionJson)
            .FirstOrDefaultAsync();

        if (definition is null)
        {
            return BadRequest(new { errors = new[] { "Ruleset definition not found." } });
        }

        try
        {
            character.InventoryJson = CharacterInventory.Serialize(
                request.Inventory.Select(entry => new InventoryEntry(entry.ItemKey, entry.Quantity)),
                definition);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }

        character.UpdatedAt = DateTime.UtcNow;
        game.UpdatedAt = character.UpdatedAt;
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToCharacterResponse(character));
    }
}
