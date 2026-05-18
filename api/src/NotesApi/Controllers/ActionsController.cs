using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Rulesets;

namespace NotesApi.Controllers;

[ApiController]
[Route("api")]
public class ActionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ActionsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("sessions/{joinCode}/actions")]
    public async Task<ActionResult<IEnumerable<ActionQueueItemResponse>>> GetActions(string joinCode, int sinceSequence = 0)
    {
        var session = await _db.GameSessions
            .Include(s => s.Actions).ThenInclude(a => a.Resolution)
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode);

        if (session is null || !await CanReadSessionAsync(session))
        {
            return NotFound();
        }

        return Ok(session.Actions
            .Where(a => a.Sequence > sinceSequence)
            .OrderBy(a => a.Sequence)
            .Select(ControllerHelpers.ToActionResponse));
    }

    [HttpPost("sessions/{joinCode}/actions")]
    public async Task<ActionResult<ActionQueueItemResponse>> SubmitAction(string joinCode, SubmitActionRequest request)
    {
        var session = await _db.GameSessions
            .Include(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(s => s.Game).ThenInclude(g => g.NpcsAndMonsters)
            .Include(s => s.Actions)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode && s.IsActive);

        if (session is null)
        {
            return NotFound();
        }

        var participant = await GetParticipantAsync(session.GameId);
        var isDm = IsDm(session.Game.DmUserId);
        if (participant is null && !isDm)
        {
            return Unauthorized(new { errors = new[] { "Join the session before submitting an action." } });
        }

        var actorName = participant?.Character.Name;
        Guid? actorCharacterId = participant?.CharacterId;
        Guid? actorNpcId = null;
        string actorClassKey = participant?.Character.ClassKey ?? string.Empty;

        if (isDm && request.ActorNpcId.HasValue)
        {
            var npc = session.Game.NpcsAndMonsters.FirstOrDefault(n => n.Id == request.ActorNpcId.Value);
            if (npc is null)
            {
                return BadRequest(new { errors = new[] { "NPC or monster was not found in this game." } });
            }

            actorName = npc.Name;
            actorCharacterId = null;
            actorNpcId = npc.Id;
            actorClassKey = ReadClassKey(npc.StatBlockJson);
        }

        if (string.IsNullOrWhiteSpace(actorName))
        {
            return BadRequest(new { errors = new[] { "An actor is required." } });
        }

        var actionText = request.ActionText;
        if (!string.IsNullOrWhiteSpace(request.ActionKey))
        {
            var rulesetAction = RulesetActionCatalog.FindAction(session.Game.Ruleset.DefinitionJson, request.ActionKey);
            if (rulesetAction is null)
            {
                return BadRequest(new { errors = new[] { "Selected action is not available for this ruleset." } });
            }

            if (!RulesetActionCatalog.IsAllowedForClass(rulesetAction, actorClassKey))
            {
                return BadRequest(new { errors = new[] { "Selected action is not available for this actor." } });
            }

            actionText = string.IsNullOrWhiteSpace(actionText) ? rulesetAction.Label : actionText;
        }

        var nextSequence = session.Actions.Count == 0 ? 1 : session.Actions.Max(a => a.Sequence) + 1;
        var now = DateTime.UtcNow;
        var action = new ActionRequest
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ActorCharacterId = actorCharacterId,
            ActorNpcId = actorNpcId,
            ActorName = actorName,
            ActionKey = request.ActionKey,
            ActionText = actionText,
            TargetCharacterId = request.TargetCharacterId,
            TargetNpcId = request.TargetNpcId,
            TargetName = request.TargetName,
            Description = request.Description,
            Status = ActionStatus.Pending,
            Sequence = nextSequence,
            SubmittedAt = now,
        };

        _db.ActionRequests.Add(action);
        Touch(session);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetActions), new { joinCode, sinceSequence = nextSequence - 1 }, ControllerHelpers.ToActionResponse(action));
    }

    [HttpPut("actions/{actionId:guid}/resolve")]
    [Authorize]
    public async Task<ActionResult<ActionQueueItemResponse>> Resolve(Guid actionId, ResolveActionRequest request)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.Resolution)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || action.Session is null || action.Session.Game is null)
        {
            return NotFound();
        }

        if (!IsDm(action.Session.Game.DmUserId))
        {
            return NotFound();
        }

        var statChanges = request.StatChanges ?? [];

        var now = DateTime.UtcNow;
        action.Status = ActionStatus.Published;
        action.PublishedAt = now;

        // Explicitly add the resolution to the change tracker so EF Core
        // correctly issues an INSERT rather than relying on relationship fixup.
        if (action.Resolution is null)
        {
            action.Resolution = new ActionResolution
            {
                Id = Guid.NewGuid(),
                ActionRequestId = action.Id,
            };
            _db.ActionResolutions.Add(action.Resolution);
        }

        action.Resolution.ResolutionText = request.ResolutionText;
        action.Resolution.RollSummary = request.RollSummary;
        action.Resolution.AdditionalActions = request.AdditionalActions;
        action.Resolution.StatChangesJson = JsonSerializer.Serialize(statChanges);
        action.Resolution.PublishedAt = now;

        foreach (var statChange in statChanges)
        {
            await ApplyStatChangeAsync(action.Session.GameId, statChange);
        }

        Touch(action.Session);
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToActionResponse(action));
    }

    private async Task<bool> CanReadSessionAsync(GameSession session)
    {
        if (IsDm(session.Game.DmUserId))
        {
            return true;
        }

        return await GetParticipantAsync(session.GameId) is not null;
    }

    private async Task<GameParticipant?> GetParticipantAsync(Guid gameId)
    {
        var token = Request.Headers["X-Player-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var participant = await _db.GameParticipants
            .Include(p => p.Character)
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.JoinToken == token);

        if (participant is not null)
        {
            // Avoid writing on every polling/action-read request; a recent
            // presence heartbeat is enough for session ownership checks.
            if ((DateTime.UtcNow - participant.LastSeenAt).TotalSeconds > 30)
            {
                participant.LastSeenAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        return participant;
    }

    private bool IsDm(string dmUserId) => User.Identity?.IsAuthenticated == true && this.UserId() == dmUserId;

    private async Task ApplyStatChangeAsync(Guid gameId, StatChangeRequest statChange)
    {
        if (statChange.TargetType.Equals("Character", StringComparison.OrdinalIgnoreCase))
        {
            var character = await _db.Characters.FirstOrDefaultAsync(c => c.GameId == gameId && c.Id == statChange.TargetId);
            if (character is null) return;
            ApplyHealthAndArmor(character, statChange);
        }
        else if (statChange.TargetType.Equals("NpcOrMonster", StringComparison.OrdinalIgnoreCase))
        {
            var npc = await _db.NpcsAndMonsters.FirstOrDefaultAsync(n => n.GameId == gameId && n.Id == statChange.TargetId);
            if (npc is null) return;
            ApplyHealthAndArmor(npc, statChange);
        }
    }

    private static void ApplyHealthAndArmor(Character character, StatChangeRequest statChange)
    {
        if (statChange.SetHealth.HasValue) character.Health = statChange.SetHealth.Value;
        if (statChange.HealthDelta.HasValue) character.Health += statChange.HealthDelta.Value;
        if (statChange.SetArmor.HasValue) character.Armor = statChange.SetArmor.Value;
        character.Health = Math.Clamp(character.Health, 0, character.MaxHealth);
        character.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyHealthAndArmor(NpcOrMonster npc, StatChangeRequest statChange)
    {
        if (statChange.SetHealth.HasValue) npc.Health = statChange.SetHealth.Value;
        if (statChange.HealthDelta.HasValue) npc.Health += statChange.HealthDelta.Value;
        if (statChange.SetArmor.HasValue) npc.Armor = statChange.SetArmor.Value;
        npc.Health = Math.Clamp(npc.Health, 0, npc.MaxHealth);
        npc.UpdatedAt = DateTime.UtcNow;
    }

    private static void Touch(GameSession session)
    {
        session.Version++;
        session.UpdatedAt = DateTime.UtcNow;
    }

    private static string ReadClassKey(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("classKey", out var classKey) && classKey.ValueKind == JsonValueKind.String
                ? classKey.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
