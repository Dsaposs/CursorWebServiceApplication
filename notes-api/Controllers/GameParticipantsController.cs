using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/game-participants")]
public class GameParticipantsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public GameParticipantsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPost("join/{inviteCode}")]
    public async Task<ActionResult<JoinGameResponse>> JoinGame(string inviteCode, JoinGameRequest request)
    {
        var game = await _db.Games
            .Include(g => g.Ruleset)
            .Include(g => g.Characters)
            .Include(g => g.NpcsAndMonsters)
            .Include(g => g.Sessions)
            .FirstOrDefaultAsync(g => g.InviteCode == inviteCode);

        if (game is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var normalizedName = request.CharacterName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { errors = new[] { "Character name is required." } });
        }

        var character = await _db.Characters.FirstOrDefaultAsync(c => c.GameId == game.Id && c.Name == normalizedName);

        if (character is null)
        {
            character = new Character
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                Name = normalizedName,
                PlayerName = string.IsNullOrWhiteSpace(request.PlayerName) ? normalizedName : request.PlayerName.Trim(),
                Health = 10,
                MaxHealth = 10,
                Armor = 0,
                AttributesJson = "{}",
                SkillsJson = "{}",
                InventoryJson = "[]",
                RulesetDataJson = game.Ruleset.CharacterTemplateJson,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Characters.Add(character);
            await _db.SaveChangesAsync();
        }

        // Reuse the most-recent participant token for this character so the
        // same player can rejoin without losing their token.
        var participant = await _db.GameParticipants
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(p => p.GameId == game.Id && p.CharacterId == character.Id);

        if (participant is null)
        {
            participant = new GameParticipant
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                CharacterId = character.Id,
                DisplayName = character.Name,
                JoinToken = ControllerHelpers.NewToken(),
                CreatedAt = now,
                LastSeenAt = now,
            };
            _db.GameParticipants.Add(participant);
        }
        else
        {
            participant.LastSeenAt = now;
        }

        await _db.SaveChangesAsync();
        await _db.Entry(game).Collection(g => g.Characters).LoadAsync();

        return Ok(new JoinGameResponse
        {
            ParticipantToken = participant.JoinToken,
            Character = ControllerHelpers.ToCharacterResponse(character),
            Game = this.ToGameResponse(game),
        });
    }
}
