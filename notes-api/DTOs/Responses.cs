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
    public string AttributesJson { get; set; } = "{}";
    public string SkillsJson { get; set; } = "{}";
    public string InventoryJson { get; set; } = "[]";
    public string RulesetDataJson { get; set; } = "{}";
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
    public string Visibility { get; set; } = "Visible";
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
}

public class ActionQueueItemResponse
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? ActionKey { get; set; }
    public string ActionText { get; set; } = string.Empty;
    public string? TargetName { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResolutionText { get; set; }
    public string? RollSummary { get; set; }
    public string? AdditionalActions { get; set; }
    public string StatChangesJson { get; set; } = "[]";
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
    public bool IsCurrentTurn { get; set; }
}

public class AdminUserReportResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool HasPasswordHash { get; set; }
    public int GamesHostedCount { get; set; }
}
