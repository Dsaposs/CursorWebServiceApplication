using Microsoft.AspNetCore.Identity;

namespace NotesApi.Models;

public class ApplicationUser : IdentityUser
{
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
