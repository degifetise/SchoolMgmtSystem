using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// Subject catalogue. Subjects are defined per grade level, so Mathematics exists once for
/// each of Grades 9-12 with its own code.
/// </summary>
[ApiController]
[Route("api/subjects")]
[Authorize]
[Produces("application/json")]
public class SubjectsController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITeachingAssignmentService _assignments;

    public SubjectsController(ApplicationDbContext db, ITeachingAssignmentService assignments)
    {
        _db = db;
        _assignments = assignments;
    }

    /// <summary>
    /// Subject list, optionally filtered by grade. Students are always scoped to their own
    /// grade level regardless of the query string.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<SubjectResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SubjectResponse>>> GetSubjects(
        [FromQuery] int? gradeLevelId,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var isStudent = User.IsInRole(Roles.Student) && !User.IsAdmin() && !User.IsInRole(Roles.Teacher);
        var effectiveGrade = isStudent
            ? await GetStudentGradeLevelIdAsync(cancellationToken)
            : gradeLevelId;

        if (isStudent && effectiveGrade is null)
        {
            return ForbiddenProblem("Your account is not linked to a student profile.");
        }

        var query = _db.Subjects.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        if (effectiveGrade is int grade)
        {
            query = query.Where(s => s.GradeLevelId == grade);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.SubjectName.Contains(term) || s.Code.Contains(term));
        }

        var items = await Project(query)
            .OrderBy(s => s.GradeLevelId)
            .ThenBy(s => s.SubjectName)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>
    /// The caller's own subjects: a student's grade catalogue, or the subjects a teacher is
    /// assigned to teach.
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType<IEnumerable<SubjectResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<SubjectResponse>>> GetMySubjects(CancellationToken cancellationToken)
    {
        if (User.GetTeacherId() is int teacherId)
        {
            var taught = await _assignments.GetTaughtSubjectIdsAsync(teacherId, cancellationToken);

            var subjects = await Project(_db.Subjects.AsNoTracking().Where(s => taught.Contains(s.Id)))
                .OrderBy(s => s.GradeLevelId)
                .ThenBy(s => s.SubjectName)
                .ToListAsync(cancellationToken);

            return Ok(subjects);
        }

        var gradeLevelId = await GetStudentGradeLevelIdAsync(cancellationToken);
        if (gradeLevelId is null)
        {
            return ForbiddenProblem("Your account is not linked to a student or teacher profile.");
        }

        var catalogue = await Project(
                _db.Subjects.AsNoTracking().Where(s => s.GradeLevelId == gradeLevelId && s.IsActive))
            .OrderBy(s => s.SubjectName)
            .ToListAsync(cancellationToken);

        return Ok(catalogue);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<SubjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubjectResponse>> GetSubject(int id, CancellationToken cancellationToken)
    {
        var subject = await Project(_db.Subjects.AsNoTracking().Where(s => s.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

        return subject is null ? NotFoundProblem($"Subject {id} was not found.") : Ok(subject);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SubjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubjectResponse>> CreateSubject(
        CreateSubjectRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _db.GradeLevels.AnyAsync(g => g.Id == request.GradeLevelId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.GradeLevelId), "Unknown grade level.");
            return ValidationProblem(ModelState);
        }

        var name = request.SubjectName.Trim();
        var code = request.Code.Trim();

        if (await _db.Subjects.AnyAsync(s => s.Code == code, cancellationToken))
        {
            return ConflictProblem("Duplicate code", $"Subject code '{code}' is already in use.");
        }

        if (await _db.Subjects.AnyAsync(
                s => s.GradeLevelId == request.GradeLevelId && s.SubjectName == name, cancellationToken))
        {
            return ConflictProblem("Duplicate subject", $"'{name}' already exists for this grade level.");
        }

        var subject = new Subject
        {
            SubjectName = name,
            Code = code,
            GradeLevelId = request.GradeLevelId,
            Description = request.Description,
            CreditHours = request.CreditHours,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await Project(_db.Subjects.AsNoTracking().Where(s => s.Id == subject.Id))
            .FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(GetSubject), new { id = subject.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SubjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubjectResponse>> UpdateSubject(
        int id,
        UpdateSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (subject is null)
        {
            return NotFoundProblem($"Subject {id} was not found.");
        }

        var name = request.SubjectName.Trim();

        if (await _db.Subjects.AnyAsync(
                s => s.Id != id && s.GradeLevelId == subject.GradeLevelId && s.SubjectName == name,
                cancellationToken))
        {
            return ConflictProblem("Duplicate subject", $"'{name}' already exists for this grade level.");
        }

        subject.SubjectName = name;
        subject.Description = request.Description;
        subject.CreditHours = request.CreditHours;
        subject.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await Project(_db.Subjects.AsNoTracking().Where(s => s.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(updated);
    }

    /// <summary>
    /// Deletes a subject that has no teaching history. Deleting a subject with assessments
    /// would cascade its marks away, so that is refused in favour of deactivation.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteSubject(int id, CancellationToken cancellationToken)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (subject is null)
        {
            return NotFoundProblem($"Subject {id} was not found.");
        }

        var assessments = await _db.Assessments.CountAsync(a => a.SubjectId == id, cancellationToken);
        var lessons = await _db.Lessons.CountAsync(l => l.SubjectId == id, cancellationToken);
        var assignments = await _db.TeacherSubjects.CountAsync(ts => ts.SubjectId == id, cancellationToken);

        if (assessments > 0 || lessons > 0 || assignments > 0)
        {
            return ConflictProblem(
                "Subject is in use",
                $"This subject has {assessments} assessment(s), {lessons} lesson(s) and {assignments} teaching assignment(s). Deactivate it instead of deleting.");
        }

        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private Task<int?> GetStudentGradeLevelIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        return _db.Students
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => (int?)s.GradeLevelId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<SubjectResponse> Project(IQueryable<Subject> query) =>
        query.Select(s => new SubjectResponse
        {
            Id = s.Id,
            SubjectName = s.SubjectName,
            Code = s.Code,
            GradeLevelId = s.GradeLevelId,
            GradeLevelName = s.GradeLevel!.Name,
            Description = s.Description,
            CreditHours = s.CreditHours,
            IsActive = s.IsActive,
            LessonCount = s.Lessons.Count,
            AssessmentCount = s.Assessments.Count,
            Teachers = s.TeacherSubjects
                .Where(ts => ts.IsActive && ts.Teacher!.User != null)
                .Select(ts => ts.Teacher!.User!.FullName)
                .Distinct()
                .ToList()
        });
}
