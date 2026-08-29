using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>Sections A, B and C (and any further sections an administrator adds).</summary>
[ApiController]
[Route("api/sections")]
[Authorize]
[Produces("application/json")]
public class SectionsController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;

    public SectionsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType<IEnumerable<SectionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SectionResponse>>> GetSections(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Sections.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var items = await query
            .OrderBy(s => s.Name)
            .Select(s => new SectionResponse
            {
                Id = s.Id,
                Name = s.Name,
                Code = s.Code,
                Capacity = s.Capacity,
                IsActive = s.IsActive,
                StudentCount = s.Students.Count(st => st.IsActive)
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SectionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SectionResponse>> CreateSection(
        CreateSectionRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim();

        if (await _db.Sections.AnyAsync(s => s.Name == name || s.Code == code, cancellationToken))
        {
            return ConflictProblem("Duplicate section", $"A section named '{name}' or coded '{code}' already exists.");
        }

        var section = new Section
        {
            Name = name,
            Code = code,
            Capacity = request.Capacity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Sections.Add(section);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetSections), new { }, new SectionResponse
        {
            Id = section.Id,
            Name = section.Name,
            Code = section.Code,
            Capacity = section.Capacity,
            IsActive = section.IsActive,
            StudentCount = 0
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SectionResponse>> UpdateSection(
        int id,
        UpdateSectionRequest request,
        CancellationToken cancellationToken)
    {
        var section = await _db.Sections.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (section is null)
        {
            return NotFoundProblem($"Section {id} was not found.");
        }

        var name = request.Name.Trim();

        if (await _db.Sections.AnyAsync(s => s.Id != id && s.Name == name, cancellationToken))
        {
            return ConflictProblem("Duplicate name", $"Another section is already named '{name}'.");
        }

        var enrolled = await _db.Students.CountAsync(s => s.SectionId == id && s.IsActive, cancellationToken);

        if (request.Capacity < enrolled)
        {
            return BadRequestProblem(
                "Capacity too low",
                $"{enrolled} active students are already in this section, so the capacity cannot be set to {request.Capacity}.");
        }

        section.Name = name;
        section.Capacity = request.Capacity;
        section.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new SectionResponse
        {
            Id = section.Id,
            Name = section.Name,
            Code = section.Code,
            Capacity = section.Capacity,
            IsActive = section.IsActive,
            StudentCount = enrolled
        });
    }

    /// <summary>
    /// Deletes an empty section. Sections referenced by students or timetables are kept,
    /// because the foreign keys are NO ACTION by design.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteSection(int id, CancellationToken cancellationToken)
    {
        var section = await _db.Sections.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (section is null)
        {
            return NotFoundProblem($"Section {id} was not found.");
        }

        var students = await _db.Students.CountAsync(s => s.SectionId == id, cancellationToken);
        var assignments = await _db.TeacherSubjects.CountAsync(ts => ts.SectionId == id, cancellationToken);
        var lessons = await _db.Lessons.CountAsync(l => l.SectionId == id, cancellationToken);
        var assessments = await _db.Assessments.CountAsync(a => a.SectionId == id, cancellationToken);
        var announcements = await _db.Announcements.CountAsync(a => a.SectionId == id, cancellationToken);

        if (students > 0 || assignments > 0 || lessons > 0 || assessments > 0 || announcements > 0)
        {
            return ConflictProblem(
                "Section is in use",
                $"This section is referenced by {students} student(s), {assignments} teaching assignment(s), " +
                $"{lessons} lesson(s), {assessments} assessment(s) and {announcements} announcement(s). " +
                "Deactivate it instead.");
        }

        _db.Sections.Remove(section);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
