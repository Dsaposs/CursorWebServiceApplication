using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;

using Asp.Versioning;
namespace NotesApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/campaigns")]
[Authorize]
public class CampaignController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    // ── LIST ──────────────────────────────────────────────────────────────

    /// <summary>Returns all campaigns owned by or joined by the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> ListCampaigns()
    {
        var userId = userManager.GetUserId(User)!;

        var campaigns = await db.Campaigns
            .AsNoTracking()
            .Include(c => c.Game)
            .Where(c => c.OwnerId == userId || c.Members.Any(m => m.UserId == userId))
            .Select(c => new CampaignResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                OwnerId = c.OwnerId,
                GameId = c.GameId,
                GameName = c.Game.Name,
                CreatedAt = c.CreatedAt,
                MemberCount = c.Members.Count,
                ScheduledSessionCount = c.ScheduledSessions.Count(s => !s.IsCancelled),
            })
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(campaigns);
    }

    /// <summary>Returns a single campaign with its members and upcoming scheduled sessions.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCampaign(Guid id)
    {
        var userId = userManager.GetUserId(User)!;

        var campaign = await db.Campaigns
            .AsNoTracking()
            .Include(c => c.Game)
            .Include(c => c.Members).ThenInclude(m => m.User)
            .Include(c => c.ScheduledSessions)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign is null)
            return NotFound();

        var isMember = campaign.OwnerId == userId || campaign.Members.Any(m => m.UserId == userId);
        if (!isMember)
            return Forbid();

        return Ok(MapCampaignDetail(campaign));
    }

    // ── CREATE / UPDATE / DELETE ──────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest req)
    {
        var userId = userManager.GetUserId(User)!;

        var gameExists = await db.Games.AnyAsync(g => g.Id == req.GameId && g.DmUserId == userId);
        if (!gameExists)
            return BadRequest("Game not found or you are not the DM.");

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            OwnerId = userId,
            GameId = req.GameId,
            CreatedAt = DateTime.UtcNow,
        };

        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCampaign), new { id = campaign.Id }, new { campaign.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] UpdateCampaignRequest req)
    {
        var userId = userManager.GetUserId(User)!;
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId);

        if (campaign is null)
            return NotFound();

        campaign.Name = req.Name;
        campaign.Description = req.Description;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCampaign(Guid id)
    {
        var userId = userManager.GetUserId(User)!;
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId);

        if (campaign is null)
            return NotFound();

        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync();

        return NoContent();
    }

    // ── MEMBERS ───────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddCampaignMemberRequest req)
    {
        var userId = userManager.GetUserId(User)!;
        var campaign = await db.Campaigns
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId);

        if (campaign is null)
            return NotFound();

        var invitee = await userManager.FindByEmailAsync(req.UserEmail);
        if (invitee is null)
            return BadRequest("User not found.");

        if (campaign.Members.Any(m => m.UserId == invitee.Id))
            return Conflict("User is already a member.");

        campaign.Members.Add(new CampaignMember
        {
            Id = Guid.NewGuid(),
            CampaignId = id,
            UserId = invitee.Id,
            JoinedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId)
    {
        var userId = userManager.GetUserId(User)!;
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId);
        if (campaign is null)
            return NotFound();

        var member = await db.CampaignMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.CampaignId == id);
        if (member is null)
            return NotFound();

        db.CampaignMembers.Remove(member);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── SCHEDULED SESSIONS ────────────────────────────────────────────────

    [HttpGet("{id:guid}/schedule")]
    public async Task<IActionResult> ListSchedule(Guid id)
    {
        var userId = userManager.GetUserId(User)!;
        var isMember = await db.Campaigns.AnyAsync(c =>
            c.Id == id && (c.OwnerId == userId || c.Members.Any(m => m.UserId == userId)));

        if (!isMember)
            return Forbid();

        var sessions = await db.ScheduledSessions
            .AsNoTracking()
            .Include(s => s.Campaign)
            .Where(s => s.CampaignId == id)
            .OrderBy(s => s.ScheduledAt)
            .Select(s => MapSchedule(s))
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpPost("{id:guid}/schedule")]
    public async Task<IActionResult> AddScheduledSession(Guid id, [FromBody] CreateScheduledSessionRequest req)
    {
        var userId = userManager.GetUserId(User)!;
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId);
        if (campaign is null)
            return NotFound();

        if (!Enum.TryParse<RecurrenceType>(req.Recurrence, ignoreCase: true, out var recurrence))
            recurrence = RecurrenceType.None;

        var scheduled = new ScheduledSession
        {
            Id = Guid.NewGuid(),
            CampaignId = id,
            Title = req.Title,
            Notes = req.Notes,
            ScheduledAt = req.ScheduledAt.ToUniversalTime(),
            DurationMinutes = req.DurationMinutes,
            Recurrence = recurrence,
            RecurrenceCron = req.RecurrenceCron,
            CreatedAt = DateTime.UtcNow,
        };

        db.ScheduledSessions.Add(scheduled);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(ListSchedule), new { id }, new { scheduled.Id });
    }

    [HttpPatch("{id:guid}/schedule/{scheduleId:guid}")]
    public async Task<IActionResult> UpdateScheduledSession(
        Guid id, Guid scheduleId, [FromBody] UpdateScheduledSessionRequest req)
    {
        var userId = userManager.GetUserId(User)!;
        var isOwner = await db.Campaigns.AnyAsync(c => c.Id == id && c.OwnerId == userId);
        if (!isOwner)
            return Forbid();

        var session = await db.ScheduledSessions.FirstOrDefaultAsync(s => s.Id == scheduleId && s.CampaignId == id);
        if (session is null)
            return NotFound();

        if (req.Title is not null) session.Title = req.Title;
        if (req.Notes is not null) session.Notes = req.Notes;
        if (req.ScheduledAt.HasValue) session.ScheduledAt = req.ScheduledAt.Value.ToUniversalTime();
        if (req.DurationMinutes.HasValue) session.DurationMinutes = req.DurationMinutes.Value;
        if (req.IsCancelled.HasValue) session.IsCancelled = req.IsCancelled.Value;

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>DM starts a live session from a scheduled slot. Requires a game session to already exist.</summary>
    [HttpPost("{id:guid}/schedule/{scheduleId:guid}/link")]
    public async Task<IActionResult> LinkGameSession(Guid id, Guid scheduleId, [FromBody] LinkSessionRequest req)
    {
        var userId = userManager.GetUserId(User)!;
        var isOwner = await db.Campaigns.AnyAsync(c => c.Id == id && c.OwnerId == userId);
        if (!isOwner)
            return Forbid();

        var scheduled = await db.ScheduledSessions.FirstOrDefaultAsync(s => s.Id == scheduleId && s.CampaignId == id);
        if (scheduled is null)
            return NotFound();

        var sessionExists = await db.GameSessions.AnyAsync(s => s.Id == req.SessionId);
        if (!sessionExists)
            return BadRequest("Game session not found.");

        scheduled.LinkedSessionId = req.SessionId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Mapping helpers ───────────────────────────────────────────────────

    private static object MapCampaignDetail(Campaign c) => new
    {
        c.Id,
        c.Name,
        c.Description,
        c.OwnerId,
        c.GameId,
        GameName = c.Game.Name,
        c.CreatedAt,
        Members = c.Members.Select(m => new CampaignMemberResponse
        {
            Id = m.Id,
            UserId = m.UserId,
            UserName = m.User.UserName ?? string.Empty,
            UserEmail = m.User.Email,
            JoinedAt = m.JoinedAt,
        }),
        UpcomingSchedule = c.ScheduledSessions
            .Where(s => !s.IsCancelled && s.ScheduledAt >= DateTime.UtcNow)
            .OrderBy(s => s.ScheduledAt)
            .Select(s => MapSchedule(s)),
    };

    private static ScheduledSessionResponse MapSchedule(ScheduledSession s) => new()
    {
        Id = s.Id,
        CampaignId = s.CampaignId,
        CampaignName = s.Campaign?.Name ?? string.Empty,
        Title = s.Title,
        Notes = s.Notes,
        ScheduledAt = s.ScheduledAt,
        DurationMinutes = s.DurationMinutes,
        Recurrence = s.Recurrence.ToString(),
        RecurrenceCron = s.RecurrenceCron,
        LinkedSessionId = s.LinkedSessionId,
        IsCancelled = s.IsCancelled,
        CreatedAt = s.CreatedAt,
    };
}
