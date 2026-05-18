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
            .OrderBy(user => user.UserName)
            .Select(user => new AdminUserReportResponse
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                HasPasswordHash = user.PasswordHash != null,
                NotesCreatedCount = user.NotesCreatedCount,
                NotesDeletedCount = user.NotesDeletedCount,
            })
            .ToListAsync();

        return Ok(users);
    }
}
