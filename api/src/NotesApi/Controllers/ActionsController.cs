using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Rulesets;
using NotesApi.Services;

using Asp.Versioning;
namespace NotesApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api")]
public class ActionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IActionBroadcaster _broadcaster;

    public ActionsController(ApplicationDbContext db, IActionBroadcaster broadcaster)
    {
        _db = db;
        _broadcaster = broadcaster;
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
            .Include(s => s.InitiativeEntries)
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

            if (!isDm)
            {
                // Players may only submit during their prompted initiative turn.
                var encounter = session.ActiveCombatEncounterId.HasValue
                    ? await _db.Set<CombatEncounter>().FindAsync(session.ActiveCombatEncounterId.Value)
                    : null;

                var currentEntry = session.InitiativeEntries
                    .FirstOrDefault(i => i.IsCurrentTurn);

                var isPromptedPlayerTurn =
                    encounter?.PromptedTurnCharacterId.HasValue == true
                    && encounter.PromptedTurnCharacterId == actorCharacterId
                    && currentEntry?.CombatantType == CombatantType.Character
                    && currentEntry?.CombatantId == actorCharacterId;

                if (!isPromptedPlayerTurn)
                {
                    return BadRequest(new { errors = new[] { "Wait for the DM to prompt your turn before submitting a combat action." } });
                }
            }
            else if (!request.ActorNpcId.HasValue)
            {
                return BadRequest(new { errors = new[] { "During combat, resolve player actions from the initiative panel — only NPC actions can be queued here." } });
            }
        }

        var nextSequence = session.Actions.Count == 0 ? 1 : session.Actions.Max(a => a.Sequence) + 1;
        var now = DateTime.UtcNow;
        var combatRound = session.State == SessionMode.Combat
            ? (session.ActiveCombatEncounterId.HasValue
                ? (await _db.Set<CombatEncounter>().FindAsync(session.ActiveCombatEncounterId.Value))?.Round
                : null)
            : null;

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
            SessionModeAtSubmit = session.State.ToString(),
            CombatRound = combatRound,
            RollMode = session.DiceRollMode.ToString(),
            SubmittedAt = now,
        };

        _db.ActionRequests.Add(action);
        Touch(session);
        await _db.SaveChangesAsync();

        await _broadcaster.BroadcastToSessionAsync(session.Id, ActionEvents.ActionSubmitted, new
        {
            actionId = action.Id,
            participantId = participant?.Id,
            actorName = action.ActorName,
            actionTypeLabel = actionText,
            status = action.Status.ToString(),
        });

        return CreatedAtAction(nameof(GetActions), new { joinCode, sinceSequence = nextSequence - 1 }, ControllerHelpers.ToActionResponse(action));
    }

    // ── Phase 2: action state-machine endpoints ───────────────────────────

    /// <summary>DM opens an action in the resolution workspace (Pending|Submitted → DmReviewing).</summary>
    [HttpPatch("actions/{actionId:guid}/review")]
    [Authorize]
    public async Task<ActionResult<ActionQueueItemResponse>> BeginReview(Guid actionId)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.Resolution)
            .Include(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || !IsDm(action.Session?.Game?.DmUserId ?? ""))
            return NotFound();

        if (action.Status != ActionStatus.Pending)
            return BadRequest(new { errors = new[] { "Only pending actions can be opened for review." } });

        action.Status = ActionStatus.DmReviewing;
        Touch(action.Session!);
        await _db.SaveChangesAsync();

        await _broadcaster.BroadcastToSessionAsync(action.SessionId, ActionEvents.ActionDmReviewing, new { actionId });
        return Ok(ControllerHelpers.ToActionResponse(action));
    }

    /// <summary>DM sets the dice mode for a session.</summary>
    [HttpPatch("sessions/{joinCode}/dice-mode")]
    [Authorize]
    public async Task<ActionResult<SessionSummaryResponse>> SetDiceMode(string joinCode, SetSessionDiceModeRequest request)
    {
        var session = await _db.GameSessions
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode && s.IsActive);

        if (session is null) return NotFound();
        if (!IsDm(session.Game.DmUserId)) return Forbid();

        if (!Enum.TryParse<DiceRollMode>(request.Mode, ignoreCase: true, out var mode))
            return BadRequest(new { errors = new[] { "Mode must be App, Manual, or Hybrid." } });

        session.DiceRollMode = mode;
        Touch(session);
        await _db.SaveChangesAsync();

        return Ok(this.ToSessionSummaryResponse(session));
    }

    /// <summary>DM requests a roll from the acting player (DmReviewing|RollReceived → AwaitingRoll).</summary>
    [HttpPatch("actions/{actionId:guid}/request-roll")]
    [Authorize]
    public async Task<ActionResult<ActionQueueItemResponse>> RequestRoll(Guid actionId, RequestRollFromActionRequest request)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.Resolution)
            .Include(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || !IsDm(action.Session?.Game?.DmUserId ?? ""))
            return NotFound();

        var allowedStatuses = new[] { ActionStatus.DmReviewing, ActionStatus.RollReceived };
        if (!allowedStatuses.Contains(action.Status))
            return BadRequest(new { errors = new[] { "Action must be in DmReviewing or RollReceived state to request a roll." } });

        if (request.DifficultyModifier.HasValue)
            action.DmDifficultyModifier = request.DifficultyModifier.Value;

        if (request.Dc.HasValue)
            action.EffectiveDc = request.Dc.Value;

        action.Status = ActionStatus.AwaitingRoll;
        Touch(action.Session!);
        await _db.SaveChangesAsync();

        await _broadcaster.BroadcastToSessionAsync(action.SessionId, ActionEvents.ActionRollRequested, new
        {
            actionId,
            diceSpec = request.DiceSpec,
            label = request.Label,
            guidanceText = request.GuidanceText,
            dc = request.Dc,
            mode = action.RollMode,
        });

        return Ok(ControllerHelpers.ToActionResponse(action));
    }

    /// <summary>Player submits their roll result (AwaitingRoll → RollReceived).</summary>
    [HttpPatch("actions/{actionId:guid}/submit-roll")]
    public async Task<ActionResult<ActionQueueItemResponse>> SubmitRoll(Guid actionId, SubmitActionRollRequest request)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.Resolution)
            .Include(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null) return NotFound();

        var participant = await GetParticipantAsync(action.Session.GameId);
        var isDm = IsDm(action.Session.Game.DmUserId);
        if (participant is null && !isDm) return Unauthorized(new { errors = new[] { "Join the session before submitting a roll." } });

        if (action.Status != ActionStatus.AwaitingRoll && action.Status != ActionStatus.AwaitingFollowUpRoll)
            return BadRequest(new { errors = new[] { "Action is not awaiting a roll." } });

        // Build and store the roll data as JSON
        var rollData = new
        {
            individualRolls = request.IndividualRolls,
            baseModifier = request.BaseModifier,
            modifierKeys = request.ModifierKeys,
            total = request.Total,
            rollSummary = request.RollSummary,
        };

        action.RollDataJson = System.Text.Json.JsonSerializer.Serialize(rollData, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        action.Status = ActionStatus.RollReceived;
        Touch(action.Session!);
        await _db.SaveChangesAsync();

        await _broadcaster.BroadcastToDmAsync(
            action.SessionId,
            action.Session.Game.DmUserId,
            ActionEvents.ActionRollReceived,
            new { actionId, rollData });

        return Ok(ControllerHelpers.ToActionResponse(action));
    }

    /// <summary>DM triggers a reaction from another player, pausing the original action.</summary>
    [HttpPost("actions/{actionId:guid}/reactions")]
    [Authorize]
    public async Task<ActionResult<ActionQueueItemResponse>> TriggerReaction(Guid actionId, TriggerReactionRequest request)
    {
        var original = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.Session).ThenInclude(s => s.Actions)
            .Include(a => a.Resolution)
            .Include(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (original is null || !IsDm(original.Session?.Game?.DmUserId ?? ""))
            return NotFound();

        var validTriggerStatuses = new[] { ActionStatus.DmReviewing, ActionStatus.RollReceived, ActionStatus.Resolving };
        if (!validTriggerStatuses.Contains(original.Status))
            return BadRequest(new { errors = new[] { "Can only trigger reactions while reviewing or resolving an action." } });

        var reacting = await _db.GameParticipants
            .Include(p => p.Character)
            .FirstOrDefaultAsync(p => p.Id == request.ReactingParticipantId && p.GameId == original.Session!.GameId);

        if (reacting is null)
            return BadRequest(new { errors = new[] { "Reacting participant was not found in this game." } });

        // Create the child reaction action
        var now = DateTime.UtcNow;
        var nextSeq = original.Session!.Actions.Count == 0 ? 1 : original.Session.Actions.Max(a => a.Sequence) + 1;
        var reaction = new ActionRequest
        {
            Id = Guid.NewGuid(),
            SessionId = original.SessionId,
            ParentActionId = original.Id,
            FollowUpType = FollowUpTypes.Reaction,
            ActorCharacterId = reacting.CharacterId,
            ActorName = reacting.Character.Name,
            ActionText = request.ReactionType,
            Description = request.ContextNote,
            Status = ActionStatus.ReactionPending,
            Sequence = nextSeq,
            RollMode = original.RollMode,
            SessionModeAtSubmit = original.SessionModeAtSubmit,
            CombatRound = original.CombatRound,
            SubmittedAt = now,
        };
        _db.ActionRequests.Add(reaction);

        original.Status = ActionStatus.AwaitingReaction;
        Touch(original.Session!);
        await _db.SaveChangesAsync();

        await _broadcaster.BroadcastToParticipantAsync(
            original.SessionId,
            reacting.JoinToken,
            ActionEvents.ActionReactionRequested,
            new
            {
                reactionId = reaction.Id,
                parentActionId = original.Id,
                reactionType = request.ReactionType,
                diceSpec = request.DiceSpec,
                context = request.ContextNote,
            });

        return Ok(ControllerHelpers.ToActionResponse(reaction));
    }

    /// <summary>Reacting player submits their reaction roll (ReactionPending → RollReceived).</summary>
    [HttpPatch("reactions/{reactionId:guid}/submit")]
    public async Task<ActionResult<ActionQueueItemResponse>> SubmitReaction(Guid reactionId, SubmitActionRollRequest request)
    {
        var reaction = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.ParentAction).ThenInclude(p => p!.Resolution)
            .Include(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .FirstOrDefaultAsync(a => a.Id == reactionId && a.FollowUpType == FollowUpTypes.Reaction);

        if (reaction is null) return NotFound();

        var participant = await GetParticipantAsync(reaction.Session.GameId);
        var isDm = IsDm(reaction.Session.Game.DmUserId);
        if (participant is null && !isDm)
            return Unauthorized(new { errors = new[] { "Join the session before submitting a reaction roll." } });

        if (reaction.Status != ActionStatus.ReactionPending)
            return BadRequest(new { errors = new[] { "This reaction is not awaiting a roll." } });

        var rollData = new
        {
            individualRolls = request.IndividualRolls,
            baseModifier = request.BaseModifier,
            modifierKeys = request.ModifierKeys,
            total = request.Total,
            rollSummary = request.RollSummary,
        };

        reaction.RollDataJson = System.Text.Json.JsonSerializer.Serialize(rollData, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        reaction.Status = ActionStatus.RollReceived;

        // Resume the original action so the DM can see both results
        if (reaction.ParentAction is not null && reaction.ParentAction.Status == ActionStatus.AwaitingReaction)
        {
            reaction.ParentAction.Status = ActionStatus.Resolving;
        }

        Touch(reaction.Session!);
        await _db.SaveChangesAsync();

        await _broadcaster.BroadcastToDmAsync(
            reaction.SessionId,
            reaction.Session.Game.DmUserId,
            ActionEvents.ActionReactionReceived,
            new { reactionId, rollData });

        return Ok(ControllerHelpers.ToActionResponse(reaction));
    }

    /// <summary>DM moves action into the Resolving state (with optional DC/modifier).</summary>
    [HttpPatch("actions/{actionId:guid}/begin-resolve")]
    [Authorize]
    public async Task<ActionResult<ActionQueueItemResponse>> BeginResolve(Guid actionId, BeginResolveRequest request)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.Resolution)
            .Include(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || !IsDm(action.Session?.Game?.DmUserId ?? ""))
            return NotFound();

        var allowedStatuses = new[] { ActionStatus.DmReviewing, ActionStatus.RollReceived, ActionStatus.AwaitingReaction };
        if (!allowedStatuses.Contains(action.Status))
            return BadRequest(new { errors = new[] { "Action cannot be moved to Resolving from its current state." } });

        if (request.DifficultyModifier.HasValue)
            action.DmDifficultyModifier = request.DifficultyModifier.Value;

        if (request.EffectiveDc.HasValue)
            action.EffectiveDc = request.EffectiveDc.Value;

        action.Status = ActionStatus.Resolving;
        Touch(action.Session!);
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToActionResponse(action));
    }

    [HttpPut("actions/{actionId:guid}/resolve")]
    [Authorize]
    public async Task<ActionResult<ActionQueueItemResponse>> Resolve(Guid actionId, ResolveActionRequest request)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(a => a.Session).ThenInclude(s => s.InitiativeEntries)
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

        // Accept resolution from any active state — Pending (legacy path), or new state-machine states
        var resolvableStatuses = new[]
        {
            ActionStatus.Pending, ActionStatus.DmReviewing, ActionStatus.RollReceived,
            ActionStatus.Resolving, ActionStatus.AwaitingFollowUpRoll,
        };
        if (!resolvableStatuses.Contains(action.Status))
        {
            return BadRequest(new { errors = new[] { "Action cannot be resolved from its current state." } });
        }

        var statChanges = request.StatChanges ?? [];
        var outcome = ActionOutcomeResolver.Resolve(
            action.Session.Game.Ruleset.DefinitionJson,
            action.ActionKey,
            action.Description,
            action.RollPrompts);

        var now = DateTime.UtcNow;
        action.Status = ActionStatus.Published;
        action.PublishedAt = now;
        action.ResolvedAt = now;

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

        action.Resolution.ResolutionText = request.ResolutionText?.Trim() ?? string.Empty;
        action.Resolution.Outcome = outcome.HasValue ? outcome.Value : null;
        action.Resolution.RollSummary = request.RollSummary;
        action.Resolution.AdditionalActions = null;
        action.Resolution.StatChangesJson = JsonSerializer.Serialize(statChanges);
        action.Resolution.PublishedAt = now;

        await ApplyAllStatChangesAsync(action.Session.GameId, statChanges);

        foreach (var prompt in action.RollPrompts.Where(p => p.Status == RollPromptStatus.Pending))
        {
            prompt.Status = RollPromptStatus.Cancelled;
        }

        await CombatTurnAdvanceService.TryAdvanceAfterActionAsync(_db, action.Session, action);

        Touch(action.Session);
        await _db.SaveChangesAsync();

        var response = ControllerHelpers.ToActionResponse(action);
        await _broadcaster.BroadcastToSessionAsync(action.SessionId, ActionEvents.ActionResolved, new
        {
            actionId = action.Id,
            outcome = response.Outcome,
            statChangesJson = response.StatChangesJson,
            narrative = response.ResolutionText,
            resolvedAt = action.ResolvedAt,
        });

        return Ok(response);
    }

    [HttpPut("actions/{actionId:guid}/reject")]
    [Authorize]
    public async Task<ActionResult<ActionQueueItemResponse>> Reject(Guid actionId, RejectActionRequest request)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.Session).ThenInclude(s => s.InitiativeEntries)
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

        var rejectableStatuses = new[] { ActionStatus.Pending, ActionStatus.DmReviewing, ActionStatus.RollReceived, ActionStatus.Resolving };
        if (!rejectableStatuses.Contains(action.Status))
        {
            return BadRequest(new { errors = new[] { "Action cannot be rejected from its current state." } });
        }

        var now = DateTime.UtcNow;
        action.Status = ActionStatus.Rejected;
        action.PublishedAt = now;
        action.ResolvedAt = now;

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

        await CombatTurnAdvanceService.TryAdvanceAfterActionAsync(_db, action.Session, action);

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
                GuidanceText = string.IsNullOrWhiteSpace(item.GuidanceText) ? null : item.GuidanceText.Trim(),
                CheckMode = checkMode,
                ResultKind = resultKind,
                ActionKey = string.IsNullOrWhiteSpace(item.ActionKey) ? null : item.ActionKey.Trim(),
                SkillKey = string.IsNullOrWhiteSpace(item.SkillKey) ? null : item.SkillKey.Trim(),
                AttributeKey = string.IsNullOrWhiteSpace(item.AttributeKey) ? null : item.AttributeKey.Trim(),
                CustomCheckText = string.IsNullOrWhiteSpace(item.CustomCheckText) ? null : item.CustomCheckText.Trim(),
                Dc = item.Dc,
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

    [HttpPost("actions/{actionId:guid}/roll-prompts/start-chain")]
    [Authorize]
    public async Task<ActionResult<RollPromptResponse>> StartRollChain(Guid actionId)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Characters)
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
            return BadRequest(new { errors = new[] { "Roll chains can only be started for pending actions." } });
        }

        if (action.RollPrompts.Any(p => p.Status == RollPromptStatus.Pending))
        {
            return BadRequest(new { errors = new[] { "A roll prompt is already waiting for this action." } });
        }

        var chain = RollChainCatalog.GetChain(action.Session.Game.Ruleset.DefinitionJson, action.ActionKey);
        if (chain.Count == 0)
        {
            return BadRequest(new { errors = new[] { "This action has no roll chain defined in the ruleset." } });
        }

        var now = DateTime.UtcNow;
        var prompt = RollChainOrchestrator.TryCreateFirstPrompt(
            action,
            action.Session.Game,
            action.Session.Game.Ruleset.DefinitionJson,
            now);

        if (prompt is null)
        {
            return BadRequest(new { errors = new[] { "Could not start the roll chain — the acting player character was not found." } });
        }

        _db.ActionRollPrompts.Add(prompt);
        Touch(action.Session);
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToRollPromptResponse(prompt, action));
    }

    /// <summary>
    /// DM rolls directly on behalf of the player character, bypassing the player prompt flow.
    /// Creates a completed roll prompt with an optional DC check for auto pass/fail resolution.
    /// </summary>
    [HttpPost("actions/{actionId:guid}/roll-prompts/dm-roll")]
    [Authorize]
    public async Task<ActionResult<RollPromptResponse>> DmRollForAction(Guid actionId, DmRollRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RollSummary))
        {
            return BadRequest(new { errors = new[] { "Roll result is required." } });
        }

        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Characters)
            .Include(a => a.RollPrompts)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || action.Session?.Game is null)
        {
            return NotFound();
        }

        if (!IsDm(action.Session.Game.DmUserId))
        {
            return Forbid();
        }

        if (action.Status != ActionStatus.Pending)
        {
            return BadRequest(new { errors = new[] { "Can only roll for pending actions." } });
        }

        if (action.RollPrompts.Any(p => p.Status == RollPromptStatus.Pending))
        {
            return BadRequest(new { errors = new[] { "Cancel the outstanding roll prompt before rolling directly." } });
        }

        // Determine target character — prefer the actor, fall back to first character in session.
        var targetCharacter = action.ActorCharacterId.HasValue
            ? action.Session.Game.Characters.FirstOrDefault(c => c.Id == action.ActorCharacterId.Value)
            : action.Session.Game.Characters.FirstOrDefault();

        if (targetCharacter is null)
        {
            return BadRequest(new { errors = new[] { "No player character found to attribute this roll to." } });
        }

        var rollSummary = request.RollSummary.Trim();
        var now = DateTime.UtcNow;

        // Resolve auto outcome from DC if provided.
        string? autoResolveOutcome = null;
        if (request.Dc.HasValue)
        {
            var rollData = RollResultParser.TryParseJson(request.RollResultJson)
                ?? RollResultParser.ParseFromSummary(rollSummary, null, RollPromptResultKind.PassFail);
            var primary = RollResultParser.GetPrimaryValue(rollData, RollPromptResultKind.PassFail, rollSummary);
            if (primary.HasValue)
            {
                autoResolveOutcome = primary.Value >= request.Dc.Value
                    ? RollChainOutcomes.Success
                    : RollChainOutcomes.Failure;
            }
        }

        var prompt = new ActionRollPrompt
        {
            Id = Guid.NewGuid(),
            ActionRequestId = action.Id,
            TargetCharacterId = targetCharacter.Id,
            TargetCharacter = targetCharacter,
            CheckMode = "Action",
            ResultKind = RollPromptResultKind.PassFail,
            ActionKey = action.ActionKey,
            PromptLabel = "DM roll",
            Dc = request.Dc,
            DmRolled = true,
            RollSummary = rollSummary,
            RollResultJson = string.IsNullOrWhiteSpace(request.RollResultJson) ? null : request.RollResultJson.Trim(),
            AutoResolveOutcome = autoResolveOutcome,
            Status = RollPromptStatus.Completed,
            CreatedAt = now,
            CompletedAt = now,
        };

        _db.ActionRollPrompts.Add(prompt);
        Touch(action.Session);
        await _db.SaveChangesAsync();

        return Ok(ControllerHelpers.ToRollPromptResponse(prompt, action));
    }

    [HttpPut("roll-prompts/{promptId:guid}/submit")]
    public async Task<ActionResult<RollPromptResponse>> SubmitRollPrompt(Guid promptId, SubmitRollPromptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RollSummary))
        {
            return BadRequest(new { errors = new[] { "Roll result is required." } });
        }

        var actionPrompt = await _db.ActionRollPrompts
            .Include(p => p.ActionRequest).ThenInclude(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Ruleset)
            .Include(p => p.ActionRequest).ThenInclude(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.Characters)
            .Include(p => p.ActionRequest).ThenInclude(a => a.Session).ThenInclude(s => s.Game).ThenInclude(g => g.NpcsAndMonsters)
            .Include(p => p.ActionRequest).ThenInclude(a => a.RollPrompts)
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
            var rollSummary = request.RollSummary.Trim();
            actionPrompt.RollSummary = rollSummary;
            actionPrompt.RollResultJson = string.IsNullOrWhiteSpace(request.RollResultJson)
                ? null
                : request.RollResultJson.Trim();
            actionPrompt.Status = RollPromptStatus.Completed;
            actionPrompt.CompletedAt = now;

            if (request.Pushed)
            {
                await ApplyPushStressAsync(actionPrompt.TargetCharacter, actionPrompt.ActionRequest.Session.Game.Ruleset.DefinitionJson);
            }

            // If the DM set a DC and this prompt has no roll-chain step, auto-resolve pass/fail.
            if (actionPrompt.Dc.HasValue && string.IsNullOrWhiteSpace(actionPrompt.ChainStepKey)
                && actionPrompt.AutoResolveOutcome is null)
            {
                var rollData = RollResultParser.TryParseJson(actionPrompt.RollResultJson)
                    ?? RollResultParser.ParseFromSummary(rollSummary, null, actionPrompt.ResultKind);
                var primary = RollResultParser.GetPrimaryValue(rollData, actionPrompt.ResultKind, rollSummary);
                if (primary.HasValue)
                {
                    actionPrompt.AutoResolveOutcome = primary.Value >= actionPrompt.Dc.Value
                        ? RollChainOutcomes.Success
                        : RollChainOutcomes.Failure;
                }
            }

            var chainResult = await RollChainOrchestrator.ProcessCompletedPromptAsync(
                _db,
                actionPrompt,
                actionPrompt.ActionRequest,
                actionPrompt.ActionRequest.Session.Game,
                actionPrompt.ActionRequest.Session.Game.Ruleset.DefinitionJson,
                rollSummary,
                actionPrompt.RollResultJson,
                now);

            Touch(actionPrompt.ActionRequest.Session);
            await _db.SaveChangesAsync();

            var response = ControllerHelpers.ToRollPromptResponse(actionPrompt, actionPrompt.ActionRequest);
            response.AutoResolveMessage = chainResult.AutoResolveMessage;
            return Ok(response);
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
        var sessionRollSummary = request.RollSummary.Trim();
        var queuedAction = SessionRollPromptQueueService.CreatePendingAction(
            sessionPrompt.Session,
            sessionPrompt,
            sessionPrompt.Session.Game.Ruleset.DefinitionJson,
            sessionRollSummary,
            completedAt);

        sessionPrompt.RollSummary = sessionRollSummary;
        sessionPrompt.RollResultJson = string.IsNullOrWhiteSpace(request.RollResultJson)
            ? null
            : request.RollResultJson.Trim();
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

    // ── Phase 2: state-machine helpers ────────────────────────────────────

    private async Task<ActionResult<ActionQueueItemResponse>?> LoadActionForDmAsync(Guid actionId)
    {
        var action = await _db.ActionRequests
            .Include(a => a.Session).ThenInclude(s => s.Game)
            .Include(a => a.Resolution)
            .Include(a => a.RollPrompts).ThenInclude(p => p.TargetCharacter)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null || action.Session is null || action.Session.Game is null)
            return null;

        if (!IsDm(action.Session.Game.DmUserId))
            return null;

        return ControllerHelpers.ToActionResponse(action);
    }

    /// <summary>
    /// Pre-loads all characters and NPCs referenced by <paramref name="statChanges"/> in two
    /// bulk queries, applies every change in memory, then lets the caller's SaveChangesAsync
    /// flush everything in a single round-trip.
    /// </summary>
    private async Task ApplyAllStatChangesAsync(Guid gameId, IEnumerable<StatChangeRequest> statChanges)
    {
        var changes = statChanges.ToList();
        if (changes.Count == 0) return;

        var characterIds = changes
            .Where(c => c.TargetType.Equals("Character", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.TargetId)
            .Distinct()
            .ToList();

        var npcIds = changes
            .Where(c => c.TargetType.Equals("NpcOrMonster", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.TargetId)
            .Distinct()
            .ToList();

        var characters = characterIds.Count > 0
            ? (await _db.Characters.Where(c => c.GameId == gameId && characterIds.Contains(c.Id)).ToListAsync())
              .ToDictionary(c => c.Id)
            : new Dictionary<Guid, Character>();

        var npcs = npcIds.Count > 0
            ? (await _db.NpcsAndMonsters.Where(n => n.GameId == gameId && npcIds.Contains(n.Id)).ToListAsync())
              .ToDictionary(n => n.Id)
            : new Dictionary<Guid, NpcOrMonster>();

        foreach (var change in changes)
        {
            if (change.TargetType.Equals("Character", StringComparison.OrdinalIgnoreCase))
            {
                if (!characters.TryGetValue(change.TargetId, out var character)) continue;
                ApplyHealthAndArmor(character, change);
                if (change.SetGameValues is { Count: > 0 } || change.GameValueDeltas is { Count: > 0 })
                    RulesetCharacterData.ApplyGameValues(character, change.SetGameValues, change.GameValueDeltas);
                if (change.AttributeDeltas is { Count: > 0 })
                    RulesetCharacterData.ApplyNestedDeltas(character, "attributes", change.AttributeDeltas);
                if (change.InventoryDeltas is { Count: > 0 })
                    CharacterInventory.ApplyDeltas(character, change.InventoryDeltas);
                if (change.AddStatusKeys is { Count: > 0 } || change.RemoveStatusKeys is { Count: > 0 })
                    RulesetCharacterData.ApplyStatusChanges(character, change.AddStatusKeys, change.RemoveStatusKeys);

                // Auto-apply threshold status effects after HP changes.
                if (change.HealthDelta.HasValue || change.SetHealth.HasValue)
                {
                    if (character.Health <= 0)
                        RulesetCharacterData.ApplyStatusChanges(character, ["broken"], null);
                    else
                        RulesetCharacterData.ApplyStatusChanges(character, null, ["broken"]);
                }
            }
            else if (change.TargetType.Equals("NpcOrMonster", StringComparison.OrdinalIgnoreCase))
            {
                if (!npcs.TryGetValue(change.TargetId, out var npc)) continue;
                ApplyHealthAndArmor(npc, change);
                if (change.AddStatusKeys is { Count: > 0 } || change.RemoveStatusKeys is { Count: > 0 })
                    RulesetCharacterData.ApplyStatusChanges(npc, change.AddStatusKeys, change.RemoveStatusKeys);
            }
        }
    }

    /// <summary>Alien RPG push: add +1 stress when a player pushes a roll.</summary>
    private static Task ApplyPushStressAsync(Character character, string definitionJson)
    {
        var definition = JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (definition?.DiceRollerKey != "d6-pool")
        {
            return Task.CompletedTask;
        }

        RulesetCharacterData.ApplyGameValues(character, null, new Dictionary<string, int> { ["stress"] = 1 });
        character.UpdatedAt = DateTime.UtcNow;
        return Task.CompletedTask;
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
