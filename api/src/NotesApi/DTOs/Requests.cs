using System.ComponentModel.DataAnnotations;

namespace NotesApi.DTOs;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(7)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Password must contain an uppercase letter and a number.")]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class CreateGameRequest
{
    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string RulesetCode { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class UpdateGameRequest
{
    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class JoinGameRequest
{
    public Guid? CharacterId { get; set; }

    [MaxLength(160)]
    public string CharacterName { get; set; } = string.Empty;

    [MaxLength(160)]
    public string PlayerName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ClassKey { get; set; } = string.Empty;

    /// <summary>Skill ranks to assign at creation (keys must be class availableSkills).</summary>
    public Dictionary<string, int>? SkillAllocations { get; set; }

    /// <summary>Starting item chosen from the class startingItemOptions list.</summary>
    [MaxLength(80)]
    public string? StartingItemKey { get; set; }
}

public class UpdateCharacterInventoryRequest
{
    public IEnumerable<InventoryEntryRequest> Inventory { get; set; } = Array.Empty<InventoryEntryRequest>();
}

public class InventoryEntryRequest
{
    [Required, MaxLength(80)]
    public string ItemKey { get; set; } = string.Empty;

    [Range(0, 999)]
    public int Quantity { get; set; }
}

public class ImportRulesetRequest
{
    [Required]
    public string DefinitionJson { get; set; } = string.Empty;
}

public class CreateNpcRequest
{
    /// <summary>When set, applies a ruleset <c>npcTemplates</c> preset; explicit fields override template defaults.</summary>
    [MaxLength(80)]
    public string? TemplateKey { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Kind { get; set; } = "NPC";

    public int MaxHealth { get; set; } = 10;

    public int Health { get; set; } = 10;

    public int Armor { get; set; }

    public string StatBlockJson { get; set; } = "{}";
}

public class UpdateNpcRequest : CreateNpcRequest
{
}

public class ChangeSessionStateRequest
{
    [Required]
    public string State { get; set; } = "Exploration";
}

public class SubmitActionRequest
{
    public Guid? ActorNpcId { get; set; }

    [MaxLength(80)]
    public string? ActionKey { get; set; }

    [Required, MaxLength(240)]
    public string ActionText { get; set; } = string.Empty;

    public Guid? TargetCharacterId { get; set; }

    public Guid? TargetNpcId { get; set; }

    [MaxLength(160)]
    public string? TargetName { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class ResolveActionRequest
{
    public string? ResolutionText { get; set; }

    public string? RollSummary { get; set; }

    public IEnumerable<StatChangeRequest> StatChanges { get; set; } = Array.Empty<StatChangeRequest>();
}

public class RejectActionRequest
{
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
}

public class CreateRollPromptRequest
{
    [Required]
    public Guid TargetCharacterId { get; set; }

    /// <summary>Action, Skill, Attribute, or Custom.</summary>
    [Required, MaxLength(20)]
    public string CheckMode { get; set; } = "Custom";

    /// <summary>PassFail or Total.</summary>
    [MaxLength(20)]
    public string? ResultKind { get; set; }

    [MaxLength(80)]
    public string? ActionKey { get; set; }

    [MaxLength(80)]
    public string? SkillKey { get; set; }

    [MaxLength(80)]
    public string? AttributeKey { get; set; }

    [MaxLength(240)]
    public string? CustomCheckText { get; set; }

    [MaxLength(200)]
    public string? PromptLabel { get; set; }

    /// <summary>Contextual note shown to the player explaining the purpose of the roll.</summary>
    [MaxLength(500)]
    public string? GuidanceText { get; set; }

    /// <summary>
    /// Optional difficulty class. When set, pass/fail is auto-resolved by comparing the
    /// roll's primary value (successes or total) against this number.
    /// </summary>
    [Range(1, 100)]
    public int? Dc { get; set; }
}

public class CreateRollPromptsRequest
{
    public IEnumerable<CreateRollPromptRequest> Prompts { get; set; } = Array.Empty<CreateRollPromptRequest>();
}

public class CreateSessionRollPromptsRequest
{
    public IEnumerable<CreateRollPromptRequest> Prompts { get; set; } = Array.Empty<CreateRollPromptRequest>();
}

public class SubmitRollPromptRequest
{
    [Required, MaxLength(500)]
    public string RollSummary { get; set; } = string.Empty;

    /// <summary>Structured dice breakdown (groups, total, successes) as JSON.</summary>
    public string? RollResultJson { get; set; }

    /// <summary>Alien RPG: player pushed the roll (+1 stress die). Server may apply stress gain.</summary>
    public bool Pushed { get; set; }
}

public class StatChangeRequest
{
    [Required]
    public string TargetType { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    public int? HealthDelta { get; set; }

    public int? SetHealth { get; set; }

    public int? SetArmor { get; set; }

    /// <summary>
    /// Ruleset-specific game values to set (absolute) on the target character (e.g. stress, experience).
    /// Only applicable when TargetType is "Character".
    /// </summary>
    public Dictionary<string, int>? SetGameValues { get; set; }

    /// <summary>
    /// Delta changes to ruleset-specific game values (e.g. stress +1, experience -2).
    /// Applied after SetGameValues. Only applicable when TargetType is "Character".
    /// </summary>
    public Dictionary<string, int>? GameValueDeltas { get; set; }

    /// <summary>
    /// Delta changes to character attributes (e.g. strength +1).
    /// Only applicable when TargetType is "Character".
    /// </summary>
    public Dictionary<string, int>? AttributeDeltas { get; set; }

    /// <summary>
    /// Inventory quantity deltas by item key (negative removes items).
    /// Only applicable when TargetType is "Character".
    /// </summary>
    public Dictionary<string, int>? InventoryDeltas { get; set; }

    /// <summary>
    /// Status effect keys to add to the target (e.g. ["stunned","burning"]).
    /// Applicable to both Character and NpcOrMonster targets.
    /// </summary>
    public List<string>? AddStatusKeys { get; set; }

    /// <summary>
    /// Status effect keys to remove from the target.
    /// Applicable to both Character and NpcOrMonster targets.
    /// </summary>
    public List<string>? RemoveStatusKeys { get; set; }
}

public class DmRollRequest
{
    /// <summary>Roll result as entered or generated by the DM (e.g. "3 successes" or "14").</summary>
    [Required, MaxLength(500)]
    public string RollSummary { get; set; } = string.Empty;

    /// <summary>Structured dice breakdown JSON (optional).</summary>
    public string? RollResultJson { get; set; }

    /// <summary>
    /// Difficulty class to check the roll against. Pass if roll primary value >= DC; fail otherwise.
    /// When omitted, the outcome is left for the DM to interpret on resolve.
    /// </summary>
    [Range(1, 100)]
    public int? Dc { get; set; }
}

public class SetSessionDiceModeRequest
{
    /// <summary>"App", "Manual", or "Hybrid".</summary>
    [Required, MaxLength(20)]
    public string Mode { get; set; } = "App";
}

public class RequestRollFromActionRequest
{
    /// <summary>Dice notation, e.g. "1d20", "2d6". Used for App and Hybrid modes.</summary>
    [Required, MaxLength(40)]
    public string DiceSpec { get; set; } = string.Empty;

    /// <summary>Human-readable label shown to the player, e.g. "Attack Roll — STR".</summary>
    [MaxLength(200)]
    public string? Label { get; set; }

    /// <summary>Contextual guidance explaining why the roll is needed.</summary>
    [MaxLength(500)]
    public string? GuidanceText { get; set; }

    /// <summary>Difficulty class. When set, pass/fail is auto-resolved on submission.</summary>
    [Range(1, 100)]
    public int? Dc { get; set; }

    /// <summary>DM-applied difficulty modifier applied on top of the base DC.</summary>
    public int? DifficultyModifier { get; set; }
}

public class SubmitActionRollRequest
{
    /// <summary>Individual die values (e.g. [14] for 1d20, [3, 5] for 2d6).</summary>
    [Required]
    public List<int> IndividualRolls { get; set; } = new();

    /// <summary>Base stat modifier applied before any situational bonuses.</summary>
    public int BaseModifier { get; set; }

    /// <summary>Keys of situational modifier options selected by the player.</summary>
    public List<string> ModifierKeys { get; set; } = new();

    /// <summary>Calculated total: sum(individualRolls) + baseModifier + sum(selectedModifiers).</summary>
    public int Total { get; set; }

    /// <summary>Optional human-readable summary override (e.g. "3 successes — Push the Roll").</summary>
    [MaxLength(500)]
    public string? RollSummary { get; set; }
}

public class TriggerReactionRequest
{
    /// <summary>Participant ID of the player being asked to react.</summary>
    public Guid ReactingParticipantId { get; set; }

    /// <summary>Type label for the reaction, e.g. "Opportunity Attack", "Counter".</summary>
    [Required, MaxLength(80)]
    public string ReactionType { get; set; } = string.Empty;

    /// <summary>Dice notation for the reaction roll, e.g. "1d20".</summary>
    [MaxLength(40)]
    public string? DiceSpec { get; set; }

    /// <summary>Optional DM note explaining the trigger context.</summary>
    [MaxLength(500)]
    public string? ContextNote { get; set; }
}

public class BeginResolveRequest
{
    /// <summary>Optional DM difficulty modifier to apply to the roll effective total.</summary>
    public int? DifficultyModifier { get; set; }

    /// <summary>Optional final DC to record for the resolution.</summary>
    [Range(1, 100)]
    public int? EffectiveDc { get; set; }
}

public class DiceRollRequest
{
    /// <summary>Standard dice notation, e.g. "1d20", "2d6", "4d6".</summary>
    [Required, MaxLength(40)]
    public string Spec { get; set; } = string.Empty;
}

public class SetupCombatRequest
{
    public IEnumerable<CombatantRequest> Combatants { get; set; } = Array.Empty<CombatantRequest>();
}

public class PromptTurnRequest
{
    public Guid CharacterId { get; set; }
}

public class CombatantRequest
{
    [Required]
    public string Type { get; set; } = string.Empty;

    public Guid Id { get; set; }

    public int Initiative { get; set; }
}

public class SetNpcVisibilityRequest
{
    [Required]
    public Guid NpcId { get; set; }

    [Required]
    public string Visibility { get; set; } = "Visible";
}

public class UpsertSessionNoteRequest
{
    [MaxLength(20000)]
    public string Content { get; set; } = string.Empty;
}

// ── Campaigns ──────────────────────────────────────────────────────────────

public class CreateCampaignRequest
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid GameId { get; set; }
}

public class UpdateCampaignRequest
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class AddCampaignMemberRequest
{
    [Required]
    public string UserEmail { get; set; } = string.Empty;
}

public class CreateScheduledSessionRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public DateTime ScheduledAt { get; set; }

    public int DurationMinutes { get; set; } = 120;

    public string Recurrence { get; set; } = "None";

    [MaxLength(100)]
    public string? RecurrenceCron { get; set; }
}

public class UpdateScheduledSessionRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public int? DurationMinutes { get; set; }

    public bool? IsCancelled { get; set; }
}

public class LinkSessionRequest
{
    [Required]
    public Guid SessionId { get; set; }
}
