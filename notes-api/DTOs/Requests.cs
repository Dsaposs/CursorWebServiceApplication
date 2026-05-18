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
    [Required, MaxLength(160)]
    public string CharacterName { get; set; } = string.Empty;

    [MaxLength(160)]
    public string PlayerName { get; set; } = string.Empty;
}

public class CreateNpcRequest
{
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

public class StartSessionRequest
{
    [MaxLength(160)]
    public string? Title { get; set; }
}

public class ChangeSessionStateRequest
{
    [Required]
    public string State { get; set; } = "Exploration";
}

public class SubmitActionRequest
{
    public Guid? ActorNpcId { get; set; }

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
    [Required]
    public string ResolutionText { get; set; } = string.Empty;

    public string? RollSummary { get; set; }

    public string? AdditionalActions { get; set; }

    public IEnumerable<StatChangeRequest> StatChanges { get; set; } = Array.Empty<StatChangeRequest>();
}

public class StatChangeRequest
{
    [Required]
    public string TargetType { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    public int? HealthDelta { get; set; }

    public int? SetHealth { get; set; }

    public int? SetArmor { get; set; }
}

public class SetupCombatRequest
{
    public IEnumerable<CombatantRequest> Combatants { get; set; } = Array.Empty<CombatantRequest>();
}

public class CombatantRequest
{
    [Required]
    public string Type { get; set; } = string.Empty;

    public Guid Id { get; set; }

    public int Initiative { get; set; }
}
