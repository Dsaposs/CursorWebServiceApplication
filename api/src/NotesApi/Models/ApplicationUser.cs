using Microsoft.AspNetCore.Identity;

namespace NotesApi.Models;

public class ApplicationUser : IdentityUser
{
    public ICollection<Game> GamesHosted { get; set; } = new List<Game>();
}
