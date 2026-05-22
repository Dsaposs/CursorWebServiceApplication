using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Rulesets;

using Asp.Versioning;
namespace NotesApi.Controllers;

[ApiVersion("1.0")]
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

        var (resolved, resolveError) = await TryResolveCreateRequestAsync(game, request);
        if (resolveError is not null)
        {
            return BadRequest(new { errors = new[] { resolveError } });
        }

        var now = DateTime.UtcNow;
        var npc = new NpcOrMonster
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = resolved.Name,
            Kind = resolved.Kind,
            MaxHealth = resolved.MaxHealth <= 0 ? 1 : resolved.MaxHealth,
            Health = resolved.Health <= 0 ? resolved.MaxHealth : resolved.Health,
            Armor = resolved.Armor,
            StatBlockJson = resolved.StatBlockJson,
            CreatedAt = now,
            UpdatedAt = now,
        };

        npc.Health = Math.Clamp(npc.Health, 0, npc.MaxHealth);
        _db.NpcsAndMonsters.Add(npc);

        var activeSession = await _db.GameSessions
            .FirstOrDefaultAsync(s => s.GameId == game.Id && s.IsActive);
        if (activeSession is not null)
        {
            activeSession.Version++;
            activeSession.UpdatedAt = now;
        }

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

        var activeSession = await _db.GameSessions
            .FirstOrDefaultAsync(s => s.GameId == game.Id && s.IsActive);
        if (activeSession is not null)
        {
            activeSession.Version++;
            activeSession.UpdatedAt = npc.UpdatedAt;
        }

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

    private async Task<(CreateNpcRequest Resolved, string? Error)> TryResolveCreateRequestAsync(
        Game game,
        CreateNpcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            return (request, null);
        }

        var ruleset = await _db.Rulesets.AsNoTracking().FirstOrDefaultAsync(r => r.Code == game.RulesetCode);
        if (ruleset is null)
        {
            return (request, $"Ruleset '{game.RulesetCode}' is not available.");
        }

        RulesetDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<RulesetDefinition>(
                ruleset.DefinitionJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch
        {
            return (request, "Ruleset definition could not be parsed.");
        }

        if (definition is null)
        {
            return (request, "Ruleset definition could not be parsed.");
        }

        if (!NpcTemplateApplicator.TryBuildCreateRequest(
                definition,
                request.TemplateKey,
                request.Name,
                out var fromTemplate,
                out var error))
        {
            return (request, error);
        }

        var resolved = fromTemplate;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            resolved.Name = request.Name.Trim();
        }

        return (resolved, null);
    }
}
