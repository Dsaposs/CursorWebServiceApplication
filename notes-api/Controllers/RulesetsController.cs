using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RulesetsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public RulesetsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RulesetResponse>>> GetAll()
    {
        var rulesets = await _db.Rulesets
            .AsNoTracking()
            .OrderBy(r => r.IsPlaceholder)
            .ThenBy(r => r.DisplayName)
            .Select(r => ControllerHelpers.ToRulesetResponse(r))
            .ToListAsync();

        return Ok(rulesets);
    }
}
