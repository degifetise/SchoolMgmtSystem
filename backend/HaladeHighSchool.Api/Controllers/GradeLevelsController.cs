using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// Grades 9-12. The set of grades is fixed by a database CHECK constraint, so this
/// controller exposes reads for everyone and edits (name, description, activation) to admins.
/// </summary>
[ApiController]
[Route("api/grade-levels")]
[Authorize]
[Produces("application/json")]
public class GradeLevelsController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;

    public GradeLevelsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>All grade levels, with subject and student counts.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<GradeLevelResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GradeLevelResponse>>> GetGradeLevels(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.GradeLevels.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(g => g.IsActive);
        }

        var items = await query
            .OrderBy(g => g.Level)
            .Select(g => new GradeLevelResponse
            {
                Id = g.Id,
                Name = g.Name,
                Level = g.Level,
                Description = g.Description,
                IsActive = g.IsActive,
                SubjectCount = g.Subjects.Count(s => s.IsActive),
                StudentCount = g.Students.Count(s => s.IsActive)
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Renames a grade level or toggles its availability.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<GradeLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GradeLevelResponse>> UpdateGradeLevel(
        int id,
        UpdateGradeLevelRequest request,
        CancellationToken cancellationToken)
    {
        var gradeLevel = await _db.GradeLevels.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (gradeLevel is null)
        {
            return NotFoundProblem($"Grade level {id} was not found.");
        }

        var name = request.Name.Trim();

        if (await _db.GradeLevels.AnyAsync(g => g.Id != id && g.Name == name, cancellationToken))
        {
            return ConflictProblem("Duplicate name", $"Another grade level is already named '{name}'.");
        }

        gradeLevel.Name = name;
        gradeLevel.Description = request.Description;
        gradeLevel.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new GradeLevelResponse
        {
            Id = gradeLevel.Id,
            Name = gradeLevel.Name,
            Level = gradeLevel.Level,
            Description = gradeLevel.Description,
            IsActive = gradeLevel.IsActive,
            SubjectCount = await _db.Subjects.CountAsync(s => s.GradeLevelId == id && s.IsActive, cancellationToken),
            StudentCount = await _db.Students.CountAsync(s => s.GradeLevelId == id && s.IsActive, cancellationToken)
        });
    }
}
