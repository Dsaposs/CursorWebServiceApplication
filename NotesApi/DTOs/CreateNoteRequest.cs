using System.ComponentModel.DataAnnotations;

namespace NotesApi.DTOs;

public class CreateNoteRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;
}
