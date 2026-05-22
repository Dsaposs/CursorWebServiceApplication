using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AdminController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<AdminUserReportResponse>>> GetUsers()
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .GroupJoin(
                _db.Games.AsNoTracking(),
                u => u.Id,
                g => g.DmUserId,
                (u, games) => new AdminUserReportResponse
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email,
                    HasPasswordHash = u.PasswordHash != null,
                    GamesHostedCount = games.Count(),
                })
            .ToListAsync();

        return Ok(users);
    }
}
