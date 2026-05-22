using System.ComponentModel.DataAnnotations;

namespace NotesApi.Models;

public enum SessionMode
{
    Exploration = 0,
    Combat = 1,
}

public enum ActionStatus
{
    /// <summary>Player has submitted; waiting for DM attention.</summary>
    Pending = 0,
    /// <summary>DM has confirmed resolution; action is visible to all.</summary>
    Published = 1,
    Rejected = 2,
    Cancelled = 3,
    /// <summary>DM has opened the action in the resolution workspace.</summary>
    DmReviewing = 4,
    /// <summary>DM has sent a roll prompt; waiting for player roll.</summary>
    AwaitingRoll = 5,
    /// <summary>Player roll received; DM reviewing.</summary>
    RollReceived = 6,
    /// <summary>DM has triggered a reaction; original action is paused.</summary>
    AwaitingReaction = 7,
    /// <summary>Reaction child action created; waiting for reacting player.</summary>
    ReactionPending = 8,
    /// <summary>All rolls complete; DM is writing resolution text.</summary>
    Resolving = 9,
    /// <summary>DM has requested a follow-up roll.</summary>
    AwaitingFollowUpRoll = 10,
}

public enum DiceRollMode
{
    App = 0,
    Manual = 1,
    Hybrid = 2,
}

public static class FollowUpTypes
{
    public const string Reaction = "reaction";
    public const string Chain = "chain";
}

public enum ActionOutcome
{
    Pass = 0,
    Fail = 1,
}

public enum RollPromptStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
}

public enum CombatantType
{
    Character = 0,
    NpcOrMonster = 1,
}

public class Ruleset
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(80)]
    public string DiceNotation { get; set; } = string.Empty;

    public bool IsPlaceholder { get; set; }

    public string CharacterTemplateJson { get; set; } = "{}";

    public string DefinitionJson { get; set; } = "{}";

    public ICollection<Game> Games { get; set; } = new List<Game>();
}

public class Game
{
    public Guid Id { get; set; }

    [Required]
    public string DmUserId { get; set; } = string.Empty;

    public ApplicationUser DmUser { get; set; } = null!;

    [Required, MaxLength(50)]
    public string RulesetCode { get; set; } = string.Empty;

    public Ruleset Ruleset { get; set; } = null!;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(32)]
    public string InviteCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();

    public ICollection<Character> Characters { get; set; } = new List<Character>();

    public ICollection<NpcOrMonster> NpcsAndMonsters { get; set; } = new List<NpcOrMonster>();
}

public class GameParticipant
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }

    public Game Game { get; set; } = null!;

    public Guid CharacterId { get; set; }

    public Character Character { get; set; } = null!;

    [Required, MaxLength(160)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string JoinToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime LastSeenAt { get; set; }
}

public class Character
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }

    public Game Game { get; set; } = null!;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(160)]
    public string PlayerName { get; set; } = string.Empty;

    public int MaxHealth { get; set; } = 10;

    public int Health { get; set; } = 10;

    public int Armor { get; set; }

    public string InventoryJson { get; set; } = "[]";

    public string RulesetDataJson { get; set; } = "{}";

    /// <summary>JSON array of status effect keys currently applied (e.g. ["stunned","panicking"]).</summary>
    public string StatusEffectsJson { get; set; } = "[]";

    [MaxLength(80)]
    public string ClassKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<GameParticipant> Participants { get; set; } = new List<GameParticipant>();
}

public class NpcOrMonster
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }

    public Game Game { get; set; } = null!;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Kind { get; set; } = "NPC";

    public int MaxHealth { get; set; } = 10;

    public int Health { get; set; } = 10;

    public int Armor { get; set; }

    public string StatBlockJson { get; set; } = "{}";

    /// <summary>JSON array of status effect keys currently applied (e.g. ["stunned","burning"]).</summary>
    public string StatusEffectsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class GameSession
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }

    public Game Game { get; set; } = null!;

    [Required, MaxLength(32)]
    public string JoinCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public SessionMode State { get; set; } = SessionMode.Exploration;

    public int Version { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string NpcVisibilitiesJson { get; set; } = "{}";

    /// <summary>Session-level dice input mode set by the DM at session start.</summary>
    public DiceRollMode DiceRollMode { get; set; } = DiceRollMode.App;

    /// <summary>In Combat mode, the participant whose turn is currently active.</summary>
    public Guid? ActiveTurnParticipantId { get; set; }

    public Guid? ActiveCombatEncounterId { get; set; }

    public CombatEncounter? ActiveCombatEncounter { get; set; }

    public ICollection<ActionRequest> Actions { get; set; } = new List<ActionRequest>();

    public ICollection<CombatEncounter> CombatEncounters { get; set; } = new List<CombatEncounter>();

    public ICollection<InitiativeEntry> InitiativeEntries { get; set; } = new List<InitiativeEntry>();

    public ICollection<SessionRollPrompt> SessionRollPrompts { get; set; } = new List<SessionRollPrompt>();

    public ICollection<SessionNote> SessionNotes { get; set; } = new List<SessionNote>();
}

public class SessionNote
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public GameSession Session { get; set; } = null!;

    /// <summary>Dm or Player — identifies which owner id field applies.</summary>
    [Required, MaxLength(16)]
    public string OwnerKind { get; set; } = SessionNoteOwnerKinds.Dm;

    /// <summary>DM identity user id, or GameParticipant id for a player.</summary>
    [Required, MaxLength(128)]
    public string OwnerId { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public static class SessionNoteOwnerKinds
{
    public const string Dm = "Dm";
    public const string Player = "Player";
}

public class SessionRollPrompt
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public GameSession Session { get; set; } = null!;

    public Guid TargetCharacterId { get; set; }

    public Character TargetCharacter { get; set; } = null!;

    [MaxLength(200)]
    public string? PromptLabel { get; set; }

    /// <summary>Contextual guidance shown to the player explaining why they are rolling.</summary>
    [MaxLength(500)]
    public string? GuidanceText { get; set; }

    /// <summary>Action, Skill, Attribute, or Custom.</summary>
    [Required, MaxLength(20)]
    public string CheckMode { get; set; } = "Skill";

    /// <summary>PassFail (successes / vs DC) or Total (sum dice values).</summary>
    [Required, MaxLength(20)]
    public string ResultKind { get; set; } = RollPromptResultKind.PassFail;

    [MaxLength(80)]
    public string? ActionKey { get; set; }

    [MaxLength(80)]
    public string? SkillKey { get; set; }

    [MaxLength(80)]
    public string? AttributeKey { get; set; }

    [MaxLength(240)]
    public string? CustomCheckText { get; set; }

    public RollPromptStatus Status { get; set; } = RollPromptStatus.Pending;

    [MaxLength(500)]
    public string? RollSummary { get; set; }

    /// <summary>Structured dice breakdown (groups, total, successes).</summary>
    public string? RollResultJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Groups prompts sent together in one DM skill-check request.</summary>
    public Guid? SkillCheckBatchId { get; set; }

    /// <summary>Pending action queued when the player submits their roll.</summary>
    public Guid? ActionRequestId { get; set; }

    public ActionRequest? ActionRequest { get; set; }
}

public class CombatEncounter
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public GameSession Session { get; set; } = null!;

    public int Sequence { get; set; }

    /// <summary>Current round number. Starts at 1 and increments each time the initiative order wraps.</summary>
    public int Round { get; set; } = 1;

    /// <summary>
    /// Set by the DM to explicitly prompt a specific character to take their action.
    /// Cleared automatically when the turn advances. Null means no player has been prompted yet.
    /// </summary>
    public Guid? PromptedTurnCharacterId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public ICollection<ActionRequest> Actions { get; set; } = new List<ActionRequest>();
}

public class ActionRequest
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public GameSession Session { get; set; } = null!;

    public Guid? ActorCharacterId { get; set; }

    public Guid? ActorNpcId { get; set; }

    [Required, MaxLength(160)]
    public string ActorName { get; set; } = string.Empty;

    [Required, MaxLength(240)]
    public string ActionText { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? ActionKey { get; set; }

    public Guid? TargetCharacterId { get; set; }

    public Guid? TargetNpcId { get; set; }

    [MaxLength(160)]
    public string? TargetName { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public ActionStatus Status { get; set; }

    public int Sequence { get; set; }

    public Guid? CombatEncounterId { get; set; }

    public CombatEncounter? CombatEncounter { get; set; }

    public Guid? SkillCheckBatchId { get; set; }

    [MaxLength(200)]
    public string? SkillCheckGroupLabel { get; set; }

    /// <summary>Tracks progress through a ruleset rollChain ({ "stepIndex": 0, "lastOutcome": "success" }).</summary>
    public string? RollChainStateJson { get; set; }

    /// <summary>Stat changes suggested by completed roll-chain steps, pending DM confirmation on resolve.</summary>
    public string PendingChainEffectsJson { get; set; } = "[]";

    // ── Phase 2: state-machine fields ─────────────────────────────────────

    /// <summary>Non-null for reactions and follow-up chain actions; points to the originating action.</summary>
    public Guid? ParentActionId { get; set; }

    public ActionRequest? ParentAction { get; set; }

    /// <summary>"reaction" | "chain" — see FollowUpTypes constants.</summary>
    [MaxLength(20)]
    public string? FollowUpType { get; set; }

    /// <summary>Step index within a multi-roll chain (null for the root action).</summary>
    public int? ChainStep { get; set; }

    /// <summary>Session mode that was active when the player submitted this action.</summary>
    [MaxLength(20)]
    public string? SessionModeAtSubmit { get; set; }

    /// <summary>Combat round number when submitted; null outside of combat.</summary>
    public int? CombatRound { get; set; }

    /// <summary>DM-applied difficulty modifier (positive = harder, negative = easier).</summary>
    public int? DmDifficultyModifier { get; set; }

    /// <summary>Final effective DC used for the primary roll.</summary>
    public int? EffectiveDc { get; set; }

    /// <summary>Dice input mode for this action (App/Manual/Hybrid).</summary>
    [MaxLength(20)]
    public string RollMode { get; set; } = "App";

    /// <summary>Structured roll data from the primary roll (JSON: spec, individualRolls, baseModifier, modifierKeys, total).</summary>
    public string? RollDataJson { get; set; }

    /// <summary>Optional player-written flavour description of their action.</summary>
    [MaxLength(500)]
    public string? FlavourText { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public ActionResolution? Resolution { get; set; }

    public ICollection<ActionRollPrompt> RollPrompts { get; set; } = new List<ActionRollPrompt>();

    public ICollection<ActionRequest> ChildActions { get; set; } = new List<ActionRequest>();
}

public class ActionRollPrompt
{
    public Guid Id { get; set; }

    public Guid ActionRequestId { get; set; }

    public ActionRequest ActionRequest { get; set; } = null!;

    public Guid TargetCharacterId { get; set; }

    public Character TargetCharacter { get; set; } = null!;

    [MaxLength(200)]
    public string? PromptLabel { get; set; }

    /// <summary>Contextual guidance shown to the player explaining why they are rolling.</summary>
    [MaxLength(500)]
    public string? GuidanceText { get; set; }

    /// <summary>Action, Skill, Attribute, or Custom.</summary>
    [Required, MaxLength(20)]
    public string CheckMode { get; set; } = "Custom";

    /// <summary>PassFail (successes / vs DC) or Total (sum dice values).</summary>
    [Required, MaxLength(20)]
    public string ResultKind { get; set; } = RollPromptResultKind.PassFail;

    [MaxLength(80)]
    public string? ActionKey { get; set; }

    [MaxLength(80)]
    public string? SkillKey { get; set; }

    [MaxLength(80)]
    public string? AttributeKey { get; set; }

    [MaxLength(240)]
    public string? CustomCheckText { get; set; }

    public RollPromptStatus Status { get; set; } = RollPromptStatus.Pending;

    [MaxLength(500)]
    public string? RollSummary { get; set; }

    /// <summary>Structured dice breakdown (groups, total, successes).</summary>
    public string? RollResultJson { get; set; }

    /// <summary>Key of the rollChain step this prompt belongs to.</summary>
    [MaxLength(80)]
    public string? ChainStepKey { get; set; }

    /// <summary>Outcome from auto-resolve: success, failure, or needs_dm.</summary>
    [MaxLength(20)]
    public string? AutoResolveOutcome { get; set; }

    /// <summary>
    /// Optional difficulty class set by the DM. When the player submits their roll,
    /// pass/fail is automatically resolved by comparing the roll's primary value against this threshold.
    /// Also used when the DM rolls directly on behalf of the player.
    /// </summary>
    public int? Dc { get; set; }

    /// <summary>True when this prompt was rolled by the DM on behalf of the player.</summary>
    public bool DmRolled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public class ActionResolution
{
    public Guid Id { get; set; }

    public Guid ActionRequestId { get; set; }

    public ActionRequest ActionRequest { get; set; } = null!;

    [Required]
    public string ResolutionText { get; set; } = string.Empty;

    public string? RollSummary { get; set; }

    public string? AdditionalActions { get; set; }

    public string StatChangesJson { get; set; } = "[]";

    public ActionOutcome? Outcome { get; set; }

    public DateTime PublishedAt { get; set; }
}

public class InitiativeEntry
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public GameSession Session { get; set; } = null!;

    public CombatantType CombatantType { get; set; }

    public Guid CombatantId { get; set; }

    [Required, MaxLength(160)]
    public string CombatantName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>Rolled initiative value used to sort turn order (higher acts first).</summary>
    public int InitiativeScore { get; set; }

    public bool IsCurrentTurn { get; set; }

    public DateTime CreatedAt { get; set; }
}
