using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Rulesets;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RulesetsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly RulesetDefinitionValidator _validator;

    public RulesetsController(ApplicationDbContext db, RulesetDefinitionValidator validator)
    {
        _db = db;
        _validator = validator;
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

    [HttpGet("{code}")]
    public async Task<ActionResult<RulesetDetailResponse>> Get(string code)
    {
        var ruleset = await _db.Rulesets
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == code);

        if (ruleset is null)
        {
            return NotFound();
        }

        return Ok(ControllerHelpers.ToRulesetDetailResponse(ruleset));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("import")]
    public async Task<ActionResult<RulesetImportResponse>> Import(ImportRulesetRequest request)
    {
        var validation = _validator.Validate(request.DefinitionJson);
        if (!validation.IsValid || validation.Definition is null)
        {
            return BadRequest(new RulesetValidationErrorResponse { Errors = validation.Errors });
        }

        var definition = validation.Definition;
        var ruleset = await _db.Rulesets.FindAsync(definition.Code);
        var created = ruleset is null;

        if (ruleset is null)
        {
            ruleset = new Ruleset { Code = definition.Code };
            _db.Rulesets.Add(ruleset);
        }

        ruleset.DisplayName = definition.DisplayName;
        ruleset.Description = definition.Description;
        ruleset.DiceNotation = definition.DiceNotation;
        ruleset.IsPlaceholder = false;
        ruleset.CharacterTemplateJson = validation.CharacterTemplateJson;
        ruleset.DefinitionJson = validation.NormalizedJson;

        await _db.SaveChangesAsync();

        return Ok(new RulesetImportResponse
        {
            Ruleset = ControllerHelpers.ToRulesetDetailResponse(ruleset),
            Created = created,
        });
    }
}
