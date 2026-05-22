namespace NotesApi.DTOs;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class RegisterResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class RulesetResponse
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DiceNotation { get; set; } = string.Empty;
    public bool IsPlaceholder { get; set; }
    public string CharacterTemplateJson { get; set; } = "{}";
    public string DefinitionJson { get; set; } = "{}";
}

public class RulesetDetailResponse : RulesetResponse
{
}

public class RulesetImportResponse
{
    public RulesetDetailResponse Ruleset { get; set; } = new();
    public bool Created { get; set; }
}

public class RulesetValidationErrorResponse
{
    public IEnumerable<string> Errors { get; set; } = Array.Empty<string>();
}

public class GameResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RulesetCode { get; set; } = string.Empty;
    public string RulesetName { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public string InviteUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IEnumerable<CharacterResponse> Characters { get; set; } = Array.Empty<CharacterResponse>();
    public IEnumerable<NpcResponse> NpcsAndMonsters { get; set; } = Array.Empty<NpcResponse>();
    public IEnumerable<SessionSummaryResponse> Sessions { get; set; } = Array.Empty<SessionSummaryResponse>();
}

public class CharacterResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int MaxHealth { get; set; }
    public int Health { get; set; }
    public int Armor { get; set; }
    public string InventoryJson { get; set; } = "[]";
    public string RulesetDataJson { get; set; } = "{}";
    public string StatusEffectsJson { get; set; } = "[]";
    public string ClassKey { get; set; } = string.Empty;
}

public class NpcResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int MaxHealth { get; set; }
    public int Health { get; set; }
    public int Armor { get; set; }
    public string StatBlockJson { get; set; } = "{}";
    public string StatusEffectsJson { get; set; } = "[]";
    public string Visibility { get; set; } = "Hidden";
}

public class JoinGameResponse
{
    public string ParticipantToken { get; set; } = string.Empty;
    public CharacterResponse Character { get; set; } = new();
    public GameResponse Game { get; set; } = new();
}

public class GameJoinOptionsResponse
{
    public string InviteCode { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string RulesetCode { get; set; } = string.Empty;
    public RulesetDetailResponse Ruleset { get; set; } = new();
}

public class SessionJoinOptionsResponse
{
    public SessionSummaryResponse Session { get; set; } = new();
    public RulesetDetailResponse Ruleset { get; set; } = new();
    public IEnumerable<CharacterResponse> AvailableCharacters { get; set; } = Array.Empty<CharacterResponse>();
}

public class SessionSummaryResponse
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public string JoinUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string State { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SessionStateResponse : SessionSummaryResponse
{
    public GameResponse Game { get; set; } = new();
    public CharacterResponse? Character { get; set; }
    public IEnumerable<ActionQueueItemResponse> Actions { get; set; } = Array.Empty<ActionQueueItemResponse>();
    public IEnumerable<InitiativeEntryResponse> Initiative { get; set; } = Array.Empty<InitiativeEntryResponse>();
    public IEnumerable<RollPromptResponse> RollPrompts { get; set; } = Array.Empty<RollPromptResponse>();
    public IEnumerable<CombatEncounterResponse> CombatEncounters { get; set; } = Array.Empty<CombatEncounterResponse>();
}

public class CombatEncounterResponse
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public int Round { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
    public Guid? PromptedTurnCharacterId { get; set; }
}

public class RollPromptResponse
{
    public Guid Id { get; set; }
    public bool IsSessionPrompt { get; set; }
    public Guid? ActionRequestId { get; set; }
    public int? ActionSequence { get; set; }
    public Guid TargetCharacterId { get; set; }
    public string TargetCharacterName { get; set; } = string.Empty;
    public string? PromptLabel { get; set; }
    public string? GuidanceText { get; set; }
    public string CheckMode { get; set; } = string.Empty;
    public string ResultKind { get; set; } = string.Empty;
    public string? ActionKey { get; set; }
    public string? SkillKey { get; set; }
    public string? AttributeKey { get; set; }
    public string? CustomCheckText { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RollSummary { get; set; }
    public string? RollResultJson { get; set; }
    public string? ChainStepKey { get; set; }
    public string? AutoResolveOutcome { get; set; }
    public string? AutoResolveMessage { get; set; }
    public Guid? ResultActionRequestId { get; set; }
    public int? Dc { get; set; }
    public bool DmRolled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ActionQueueItemResponse
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public Guid? ActorCharacterId { get; set; }
    public Guid? ActorNpcId { get; set; }
    public string? ActionKey { get; set; }
    public string ActionText { get; set; } = string.Empty;
    public Guid? TargetNpcId { get; set; }
    public string? TargetName { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResolutionText { get; set; }
    public string? RollSummary { get; set; }
    public string? AdditionalActions { get; set; }
    public string? Outcome { get; set; }
    public string StatChangesJson { get; set; } = "[]";
    public string PendingChainEffectsJson { get; set; } = "[]";
    public IEnumerable<RollPromptResponse> FollowUpRolls { get; set; } = Array.Empty<RollPromptResponse>();
    public Guid? CombatEncounterId { get; set; }
    public int? CombatEncounterSequence { get; set; }
    public bool IsSkillCheckResponse { get; set; }
    public Guid? SkillCheckBatchId { get; set; }
    public string? SkillCheckGroupLabel { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class InitiativeEntryResponse
{
    public Guid Id { get; set; }
    public string CombatantType { get; set; } = string.Empty;
    public Guid CombatantId { get; set; }
    public string CombatantName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int InitiativeScore { get; set; }
    public bool IsCurrentTurn { get; set; }
}

public class CombatStartResponse
{
    public IEnumerable<InitiativeEntryResponse> Initiative { get; set; } = Array.Empty<InitiativeEntryResponse>();
    public IEnumerable<InitiativeRollSummaryResponse> Rolls { get; set; } = Array.Empty<InitiativeRollSummaryResponse>();
    public string? GuidanceText { get; set; }
}

public class InitiativeRollSummaryResponse
{
    public string CombatantType { get; set; } = string.Empty;
    public Guid CombatantId { get; set; }
    public string CombatantName { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class SessionNoteResponse
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime SessionStartedAt { get; set; }
    public DateTime? SessionEndedAt { get; set; }
    public bool SessionIsActive { get; set; }
    public bool CanEdit { get; set; }
}

public class SessionNotesContextResponse
{
    public Guid SessionId { get; set; }
    public bool IsSessionActive { get; set; }
    public SessionNoteResponse? CurrentNote { get; set; }
    public IEnumerable<SessionNoteResponse> PreviousNotes { get; set; } = Array.Empty<SessionNoteResponse>();
}

public class GameSessionNotesResponse
{
    public Guid GameId { get; set; }
    public IEnumerable<SessionNoteResponse> Notes { get; set; } = Array.Empty<SessionNoteResponse>();
}

public class AdminUserReportResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool HasPasswordHash { get; set; }
    public int GamesHostedCount { get; set; }
}
