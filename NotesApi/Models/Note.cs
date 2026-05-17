using System.ComponentModel.DataAnnotations;

namespace NotesApi.Models;

public class Note
{
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    [MaxLength(200)]
    public string? Title { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
