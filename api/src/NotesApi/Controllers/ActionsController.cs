using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Rulesets;
using NotesApi.Services;

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
            .Include(s => s.Game)
            .Include(s => s.Actions).ThenInclude(a => a.Resolution)
            .Include(s => s.Actions).ThenInclude(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .Include(s => s.Actions).ThenInclude(a => a.CombatEncounter)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode);

        if (session is null || !await CanReadSessionAsync(session))
        {
            return NotFound();
        }

        var npcVisibilities = ControllerHelpers.ParseNpcVisibilities(session.NpcVisibilitiesJson);
        var playerView = !IsDm(session.Game.DmUserId);

        return Ok(session.Actions
            .Where(a => a.Sequence > sinceSequence)
            .OrderBy(a => a.Sequence)
            .Select(a => ControllerHelpers.ToActionResponse(a, playerView: playerView, npcVisibilities: npcVisibilities)));
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
        NpcOrMonster? actorNpc = null;
        string actorClassKey = participant?.Character.ClassKey ?? string.Empty;

        if (isDm && request.ActorNpcId.HasValue)
        {
            actorNpc = session.Game.NpcsAndMonsters.FirstOrDefault(n => n.Id == request.ActorNpcId.Value);
            if (actorNpc is null)
            {
                return BadRequest(new { errors = new[] { "NPC or monster was not found in this game." } });
            }

            actorName = actorNpc.Name;
            actorCharacterId = null;
            actorNpcId = actorNpc.Id;
            actorClassKey = ReadClassKey(actorNpc.StatBlockJson);
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

            if (!string.IsNullOrWhiteSpace(rulesetAction.RequiredItemKey))
            {
                var inventory = actorNpc is not null
                    ? CharacterInventory.ParseFromStatBlock(actorNpc.StatBlockJson)
                    : CharacterInventory.Parse(participant?.Character.InventoryJson);

                if (!CharacterInventory.HasItem(inventory, rulesetAction.RequiredItemKey))
                {
                    var item = RulesetActionCatalog.FindItem(session.Game.Ruleset.DefinitionJson, rulesetAction.RequiredItemKey);
                    var itemLabel = item?.Label ?? rulesetAction.RequiredItemKey;
                    return BadRequest(new { errors = new[] { $"This action requires {itemLabel} in inventory." } });
                }
            }

            actionText = string.IsNullOrWhiteSpace(actionText) ? rulesetAction.Label : actionText;
        }

        if (session.State == SessionMode.Combat)
        {
            await CombatEncounterLifecycle.EnsureEncounterForCombatAsync(_db, session);
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
            CombatEncounterId = CombatEncounterLifecycle.ResolveActionEncounterId(session),
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
            .Include(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(a => a.Resolution)
            .Include(a => a.RollPrompts)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || action.Session is null || action.Session.Game is null)
        {
            return NotFound();
        }

        if (!IsDm(action.Session.Game.DmUserId))
        {
            return NotFound();
        }

        if (action.Status != ActionStatus.Pending)
        {
            return BadRequest(new { errors = new[] { "Only pending actions can be resolved." } });
        }

        var statChanges = request.StatChanges ?? [];
        var outcome = ActionOutcomeResolver.Resolve(
            action.Session.Game.Ruleset.DefinitionJson,
            action.ActionKey,
            action.Description);

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
        action.Resolution.Outcome = outcome.HasValue ? outcome.Value : null;
        action.Resolution.RollSummary = request.RollSummary;
        action.Resolution.AdditionalActions = request.AdditionalActions;
        action.Resolution.StatChangesJson = JsonSerializer.Serialize(statChanges);
        action.Resolution.PublishedAt = now;

        foreach (var statChange in statChanges)
        {
            await ApplyStatChangeAsync(action.Session.GameId, statChange);
        }

        foreach (var prompt in action.RollPrompts.Where(p => p.Status == RollPromptStatus.Pending))
        {
            prompt.Status = RollPromptStatus.Cancelled;
        }

        Touch(action.Session);
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToActionResponse(action));
    }

    [HttpPut("actions/{actionId:guid}/reject")]
    [Authorize]
    public async Task<ActionResult<ActionQueueItemResponse>> Reject(Guid actionId, RejectActionRequest request)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.Resolution)
            .Include(a => a.RollPrompts)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || action.Session?.Game is null)
        {
            return NotFound();
        }

        if (!IsDm(action.Session.Game.DmUserId))
        {
            return NotFound();
        }

        if (action.Status != ActionStatus.Pending)
        {
            return BadRequest(new { errors = new[] { "Only pending actions can be rejected." } });
        }

        var now = DateTime.UtcNow;
        action.Status = ActionStatus.Rejected;
        action.PublishedAt = now;

        if (action.Resolution is null)
        {
            action.Resolution = new ActionResolution
            {
                Id = Guid.NewGuid(),
                ActionRequestId = action.Id,
            };
            _db.ActionResolutions.Add(action.Resolution);
        }

        action.Resolution.ResolutionText = string.IsNullOrWhiteSpace(request.RejectionReason)
            ? "The DM rejected this action."
            : request.RejectionReason.Trim();
        action.Resolution.Outcome = null;
        action.Resolution.RollSummary = null;
        action.Resolution.AdditionalActions = null;
        action.Resolution.StatChangesJson = "[]";
        action.Resolution.PublishedAt = now;

        foreach (var prompt in action.RollPrompts.Where(p => p.Status == RollPromptStatus.Pending))
        {
            prompt.Status = RollPromptStatus.Cancelled;
        }

        Touch(action.Session);
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToActionResponse(action));
    }

    [HttpDelete("actions/{actionId:guid}")]
    public async Task<ActionResult> Cancel(Guid actionId)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.RollPrompts)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || action.Session is null)
        {
            return NotFound();
        }

        if (action.Status != ActionStatus.Pending)
        {
            return BadRequest(new { errors = new[] { "Only pending actions can be withdrawn." } });
        }

        var participant = await GetParticipantAsync(action.Session.GameId);
        var isDm = IsDm(action.Session.Game.DmUserId);
        var isOwner = participant is not null
            && action.ActorCharacterId.HasValue
            && participant.CharacterId == action.ActorCharacterId.Value;

        if (!isDm && !isOwner)
        {
            return Unauthorized(new { errors = new[] { "You can only withdraw your own pending actions." } });
        }

        foreach (var prompt in action.RollPrompts.Where(p => p.Status == RollPromptStatus.Pending))
        {
            prompt.Status = RollPromptStatus.Cancelled;
        }

        action.Status = ActionStatus.Cancelled;
        Touch(action.Session);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("actions/{actionId:guid}/roll-prompts")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<RollPromptResponse>>> CreateRollPrompts(
        Guid actionId,
        CreateRollPromptsRequest request)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Characters)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || action.Session?.Game is null)
        {
            return NotFound();
        }

        if (!IsDm(action.Session.Game.DmUserId))
        {
            return NotFound();
        }

        if (action.Status != ActionStatus.Pending)
        {
            return BadRequest(new { errors = new[] { "Roll prompts can only be sent for pending actions still awaiting resolution." } });
        }

        var prompts = request.Prompts?.ToList() ?? [];
        if (prompts.Count == 0)
        {
            return BadRequest(new { errors = new[] { "At least one roll prompt is required." } });
        }

        var created = new List<ActionRollPrompt>();
        var now = DateTime.UtcNow;

        foreach (var item in prompts)
        {
            var character = action.Session.Game.Characters.FirstOrDefault(c => c.Id == item.TargetCharacterId);
            if (character is null)
            {
                return BadRequest(new { errors = new[] { "Target character was not found in this game." } });
            }

            if (!RollPromptValidator.TryNormalizeCheckMode(item.CheckMode, out var checkMode))
            {
                return BadRequest(new { errors = new[] { "CheckMode must be Action, Skill, Attribute, or Custom." } });
            }

            if (!RollPromptValidator.TryNormalizeResultKind(item.ResultKind, out var resultKind))
            {
                return BadRequest(new { errors = new[] { "ResultKind must be PassFail or Total." } });
            }

            var validationError = RollPromptValidator.ValidateCheck(
                checkMode,
                item,
                action.Session.Game.Ruleset.DefinitionJson,
                character.ClassKey);
            if (validationError is not null)
            {
                return BadRequest(new { errors = new[] { validationError } });
            }

            var prompt = new ActionRollPrompt
            {
                Id = Guid.NewGuid(),
                ActionRequestId = action.Id,
                TargetCharacterId = character.Id,
                TargetCharacter = character,
                PromptLabel = string.IsNullOrWhiteSpace(item.PromptLabel) ? null : item.PromptLabel.Trim(),
                CheckMode = checkMode,
                ResultKind = resultKind,
                ActionKey = string.IsNullOrWhiteSpace(item.ActionKey) ? null : item.ActionKey.Trim(),
                SkillKey = string.IsNullOrWhiteSpace(item.SkillKey) ? null : item.SkillKey.Trim(),
                AttributeKey = string.IsNullOrWhiteSpace(item.AttributeKey) ? null : item.AttributeKey.Trim(),
                CustomCheckText = string.IsNullOrWhiteSpace(item.CustomCheckText) ? null : item.CustomCheckText.Trim(),
                Status = RollPromptStatus.Pending,
                CreatedAt = now,
            };
            _db.ActionRollPrompts.Add(prompt);
            created.Add(prompt);
        }

        Touch(action.Session);
        await _db.SaveChangesAsync();

        return Ok(created.Select(p => ControllerHelpers.ToRollPromptResponse(p, action)));
    }

    [HttpPut("roll-prompts/{promptId:guid}/submit")]
    public async Task<ActionResult<RollPromptResponse>> SubmitRollPrompt(Guid promptId, SubmitRollPromptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RollSummary))
        {
            return BadRequest(new { errors = new[] { "Roll result is required." } });
        }

        var actionPrompt = await _db.ActionRollPrompts
            .Include(p => p.ActionRequest).ThenInclude(a => a.Session).ThenInclude(s => s.Game)
            .Include(p => p.TargetCharacter)
            .FirstOrDefaultAsync(p => p.Id == promptId);

        if (actionPrompt is not null)
        {
            if (actionPrompt.ActionRequest?.Session is null)
            {
                return NotFound();
            }

            if (!actionPrompt.ActionRequest.Session.IsActive || actionPrompt.ActionRequest.Status != ActionStatus.Pending)
            {
                return BadRequest(new { errors = new[] { "This roll prompt is no longer active." } });
            }

            if (actionPrompt.Status != RollPromptStatus.Pending)
            {
                return BadRequest(new { errors = new[] { "This roll has already been submitted." } });
            }

            var participant = await GetParticipantAsync(actionPrompt.ActionRequest.Session.GameId);
            if (participant is null || participant.CharacterId != actionPrompt.TargetCharacterId)
            {
                return Unauthorized(new { errors = new[] { "Only the prompted player can submit this roll." } });
            }

            var now = DateTime.UtcNow;
            actionPrompt.RollSummary = request.RollSummary.Trim();
            actionPrompt.Status = RollPromptStatus.Completed;
            actionPrompt.CompletedAt = now;
            Touch(actionPrompt.ActionRequest.Session);
            await _db.SaveChangesAsync();

            return Ok(ControllerHelpers.ToRollPromptResponse(actionPrompt, actionPrompt.ActionRequest));
        }

        var sessionPrompt = await _db.SessionRollPrompts
            .Include(p => p.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(p => p.Session).ThenInclude(s => s.Actions)
            .Include(p => p.TargetCharacter)
            .FirstOrDefaultAsync(p => p.Id == promptId);

        if (sessionPrompt is null || sessionPrompt.Session is null)
        {
            return NotFound();
        }

        if (!sessionPrompt.Session.IsActive)
        {
            return BadRequest(new { errors = new[] { "This roll prompt is no longer active." } });
        }

        if (sessionPrompt.Status != RollPromptStatus.Pending)
        {
            return BadRequest(new { errors = new[] { "This roll has already been submitted." } });
        }

        var sessionParticipant = await GetParticipantAsync(sessionPrompt.Session.GameId);
        if (sessionParticipant is null || sessionParticipant.CharacterId != sessionPrompt.TargetCharacterId)
        {
            return Unauthorized(new { errors = new[] { "Only the prompted player can submit this roll." } });
        }

        var completedAt = DateTime.UtcNow;
        var rollSummary = request.RollSummary.Trim();
        var queuedAction = SessionRollPromptQueueService.CreatePendingAction(
            sessionPrompt.Session,
            sessionPrompt,
            sessionPrompt.Session.Game.Ruleset.DefinitionJson,
            rollSummary,
            completedAt);

        sessionPrompt.RollSummary = rollSummary;
        sessionPrompt.Status = RollPromptStatus.Completed;
        sessionPrompt.CompletedAt = completedAt;
        sessionPrompt.ActionRequestId = queuedAction.Id;
        sessionPrompt.ActionRequest = queuedAction;

        _db.ActionRequests.Add(queuedAction);
        Touch(sessionPrompt.Session);
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToSessionRollPromptResponse(sessionPrompt));
    }

    [HttpDelete("roll-prompts/{promptId:guid}")]
    [Authorize]
    public async Task<ActionResult> CancelRollPrompt(Guid promptId)
    {
        var actionPrompt = await _db.ActionRollPrompts
            .Include(p => p.ActionRequest).ThenInclude(a => a.Session).ThenInclude(s => s.Game)
            .FirstOrDefaultAsync(p => p.Id == promptId);

        if (actionPrompt is not null)
        {
            if (actionPrompt.ActionRequest?.Session?.Game is null)
            {
                return NotFound();
            }

            if (!IsDm(actionPrompt.ActionRequest.Session.Game.DmUserId))
            {
                return NotFound();
            }

            if (actionPrompt.Status != RollPromptStatus.Pending)
            {
                return BadRequest(new { errors = new[] { "Only pending roll prompts can be cancelled." } });
            }

            actionPrompt.Status = RollPromptStatus.Cancelled;
            Touch(actionPrompt.ActionRequest.Session);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        var sessionPrompt = await _db.SessionRollPrompts
            .Include(p => p.Session).ThenInclude(s => s.Game)
            .FirstOrDefaultAsync(p => p.Id == promptId);

        if (sessionPrompt is null || sessionPrompt.Session?.Game is null)
        {
            return NotFound();
        }

        if (!IsDm(sessionPrompt.Session.Game.DmUserId))
        {
            return NotFound();
        }

        if (sessionPrompt.Status != RollPromptStatus.Pending)
        {
            return BadRequest(new { errors = new[] { "Only pending roll prompts can be cancelled." } });
        }

        sessionPrompt.Status = RollPromptStatus.Cancelled;
        Touch(sessionPrompt.Session);
        await _db.SaveChangesAsync();

        return NoContent();
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
            if (statChange.SetGameValues is { Count: > 0 } || statChange.GameValueDeltas is { Count: > 0 })
                RulesetCharacterData.ApplyGameValues(character, statChange.SetGameValues, statChange.GameValueDeltas);
            if (statChange.AttributeDeltas is { Count: > 0 })
                RulesetCharacterData.ApplyNestedDeltas(character, "attributes", statChange.AttributeDeltas);
            if (statChange.InventoryDeltas is { Count: > 0 })
                CharacterInventory.ApplyDeltas(character, statChange.InventoryDeltas);
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
