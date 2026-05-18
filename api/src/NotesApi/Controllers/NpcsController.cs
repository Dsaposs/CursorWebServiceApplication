using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/games/{gameId:guid}/npcs")]
[Authorize]
public class NpcsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NpcsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<ActionResult<NpcResponse>> Create(Guid gameId, CreateNpcRequest request)
    {
        var game = await _db.GetOwnedGameAsync(gameId, this.UserId());
        if (game is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var npc = new NpcOrMonster
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = request.Name,
            Kind = request.Kind,
            MaxHealth = request.MaxHealth <= 0 ? 1 : request.MaxHealth,
            Health = request.Health <= 0 ? request.MaxHealth : request.Health,
            Armor = request.Armor,
            StatBlockJson = request.StatBlockJson,
            CreatedAt = now,
            UpdatedAt = now,
        };

        npc.Health = Math.Clamp(npc.Health, 0, npc.MaxHealth);
        _db.NpcsAndMonsters.Add(npc);
        game.UpdatedAt = now;
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToNpcResponse(npc));
    }

    [HttpPut("{npcId:guid}")]
    public async Task<ActionResult<NpcResponse>> Update(Guid gameId, Guid npcId, UpdateNpcRequest request)
    {
        var game = await _db.GetOwnedGameAsync(gameId, this.UserId());
        if (game is null)
        {
            return NotFound();
        }

        var npc = await _db.NpcsAndMonsters.FirstOrDefaultAsync(n => n.GameId == game.Id && n.Id == npcId);
        if (npc is null)
        {
            return NotFound();
        }

        npc.Name = request.Name;
        npc.Kind = request.Kind;
        npc.MaxHealth = request.MaxHealth <= 0 ? 1 : request.MaxHealth;
        npc.Health = Math.Clamp(request.Health, 0, npc.MaxHealth);
        npc.Armor = request.Armor;
        npc.StatBlockJson = request.StatBlockJson;
        npc.UpdatedAt = DateTime.UtcNow;
        game.UpdatedAt = npc.UpdatedAt;
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToNpcResponse(npc));
    }

    [HttpDelete("{npcId:guid}")]
    public async Task<IActionResult> Delete(Guid gameId, Guid npcId)
    {
        var game = await _db.GetOwnedGameAsync(gameId, this.UserId());
        if (game is null)
        {
            return NotFound();
        }

        var npc = await _db.NpcsAndMonsters.FirstOrDefaultAsync(n => n.GameId == game.Id && n.Id == npcId);
        if (npc is null)
        {
            return NotFound();
        }

        _db.NpcsAndMonsters.Remove(npc);
        game.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
