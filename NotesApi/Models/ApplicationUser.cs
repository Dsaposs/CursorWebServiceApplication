using Microsoft.AspNetCore.Identity;

namespace NotesApi.Models;

public class ApplicationUser : IdentityUser
{
    public ICollection<Note> Notes { get; set; } = new List<Note>();

    public int NotesCreatedCount { get; set; }

    public int NotesDeletedCount { get; set; }
}
