using System.ComponentModel.DataAnnotations;

namespace NotesApi.Models;

public enum SessionMode
{
    Exploration = 0,
    Combat = 1,
}

public enum ActionStatus
{
    Pending = 0,
    Published = 1,
    Rejected = 2,
    Cancelled = 3,
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

    public DateTime SubmittedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public ActionResolution? Resolution { get; set; }

    public ICollection<ActionRollPrompt> RollPrompts { get; set; } = new List<ActionRollPrompt>();
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

    public bool IsCurrentTurn { get; set; }

    public DateTime CreatedAt { get; set; }
}
