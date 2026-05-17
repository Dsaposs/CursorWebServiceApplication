using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Extensions;
using NotesApi.Models;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NotesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoteResponse>>> GetAll()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var notes = await _db.Notes
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return Ok(notes.Select(n => n.ToResponse()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteResponse>> GetById(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var note = await _db.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (note is null)
        {
            return NotFound();
        }

        return Ok(note.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<NoteResponse>> Create([FromBody] CreateNoteRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var now = DateTime.UtcNow;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            Content = request.Content,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = note.Id }, note.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NoteResponse>> Update(Guid id, [FromBody] UpdateNoteRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (note is null)
        {
            return NotFound();
        }

        note.Title = request.Title;
        note.Content = request.Content;
        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(note.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (note is null)
        {
            return NotFound();
        }

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue("sub");
}
