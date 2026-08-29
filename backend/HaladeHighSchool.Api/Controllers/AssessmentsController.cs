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
/// Quizzes, assignments, tests, mid exams and final exams. The weighting that turns these
/// into a report card lives in the AssessmentTypes table, not here.
/// </summary>
[ApiController]
[Route("api/assessments")]
[Authorize]
[Produces("application/json")]
public class AssessmentsController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITeachingAssignmentService _assignments;
    private readonly ISystemSettingsService _settings;
    private readonly ILogger<AssessmentsController> _logger;

    public AssessmentsController(
        ApplicationDbContext db,
        ITeachingAssignmentService assignments,
        ISystemSettingsService settings,
        ILogger<AssessmentsController> logger)
    {
        _db = db;
        _assignments = assignments;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Assessments visible to the caller: everything for an admin, the teacher's own
    /// subjects for a teacher, and the active assessments of their own class for a student.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<AssessmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AssessmentResponse>>> GetAssessments(
        [FromQuery] int? subjectId,
        [FromQuery] int? sectionId,
        [FromQuery] AssessmentType? type,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Assessments.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        if (subjectId is int subject)
        {
            query = query.Where(a => a.SubjectId == subject);
        }

        if (sectionId is int section)
        {
            query = query.Where(a => a.SectionId == section || a.SectionId == null);
        }

        if (type is AssessmentType assessmentType)
        {
            query = query.Where(a => a.AssessmentType == assessmentType);
        }

        if (!User.IsAdmin())
        {
            if (User.GetTeacherId() is int teacherId)
            {
                var taught = await _assignments.GetTaughtSubjectIdsAsync(teacherId, cancellationToken);
                query = query.Where(a => taught.Contains(a.SubjectId));
            }
            else if (User.IsInRole(Roles.Student))
            {
                var student = await GetStudentScopeAsync(_db, User.GetUserId(), cancellationToken);
                if (student is null)
                {
                    return ForbiddenProblem("Your account is not linked to an active student profile.");
                }

                query = query.Where(a =>
                    a.IsActive &&
                    a.Subject!.GradeLevelId == student.GradeLevelId &&
                    (a.SectionId == null || a.SectionId == student.SectionId));
            }
            else
            {
                return ForbiddenProblem("Your account is not linked to a student or teacher profile.");
            }
        }

        var items = await Project(query)
            .OrderBy(a => a.SubjectCode)
            .ThenBy(a => a.AssessmentType)
            .ThenBy(a => a.Title)
            .ToListAsync(cancellationToken);

        return Ok(await WithWeightsAsync(items, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<AssessmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentResponse>> GetAssessment(int id, CancellationToken cancellationToken)
    {
        var assessment = await Project(_db.Assessments.AsNoTracking().Where(a => a.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

        return assessment is null
            ? NotFoundProblem($"Assessment {id} was not found.")
            : Ok(await WithWeightAsync(assessment, cancellationToken));
    }

    /// <summary>Creates an assessment for a subject the caller is allowed to teach.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<AssessmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AssessmentResponse>> CreateAssessment(
        CreateAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId && s.IsActive, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.SubjectId), "Unknown or inactive subject.");
            return ValidationProblem(ModelState);
        }

        if (request.SectionId is int sectionId &&
            !await _db.Sections.AnyAsync(s => s.Id == sectionId && s.IsActive, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.SectionId), "Unknown or inactive section.");
            return ValidationProblem(ModelState);
        }

        if (await AuthoriseSubjectAsync(request.SubjectId, request.SectionId, cancellationToken) is { } denial)
        {
            return denial;
        }

        var assessment = new Assessment
        {
            Title = request.Title.Trim(),
            AssessmentType = request.AssessmentType,
            MaxScore = request.MaxScore,
            SubjectId = request.SubjectId,
            SectionId = request.SectionId,
            TeacherId = User.GetTeacherId(),
            AcademicYear = await _settings.GetAcademicYearAsync(cancellationToken),
            DueDate = request.DueDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Assessments.Add(assessment);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await Project(_db.Assessments.AsNoTracking().Where(a => a.Id == assessment.Id))
            .FirstAsync(cancellationToken);

        _logger.LogInformation(
            "Created {Type} '{Title}' for {Subject}", created.AssessmentType, created.Title, created.SubjectCode);

        return CreatedAtAction(
            nameof(GetAssessment),
            new { id = assessment.Id },
            await WithWeightAsync(created, cancellationToken));
    }

    /// <summary>
    /// Edits an assessment. The maximum score cannot be lowered below a mark that has already
    /// been entered, which the database trigger would reject anyway.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<AssessmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentResponse>> UpdateAssessment(
        int id,
        UpdateAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var assessment = await _db.Assessments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (assessment is null)
        {
            return NotFoundProblem($"Assessment {id} was not found.");
        }

        if (await AuthoriseSubjectAsync(assessment.SubjectId, assessment.SectionId, cancellationToken) is { } denial)
        {
            return denial;
        }

        if (request.MaxScore < assessment.MaxScore)
        {
            var highestMark = await _db.Marks
                .Where(m => m.AssessmentId == id)
                .Select(m => (decimal?)m.Score)
                .MaxAsync(cancellationToken);

            if (highestMark > request.MaxScore)
            {
                ModelState.AddModelError(nameof(request.MaxScore),
                    $"A mark of {highestMark} has already been entered, so the maximum cannot drop to {request.MaxScore}.");
                return ValidationProblem(ModelState);
            }
        }

        assessment.Title = request.Title.Trim();
        assessment.MaxScore = request.MaxScore;
        assessment.DueDate = request.DueDate;
        assessment.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await Project(_db.Assessments.AsNoTracking().Where(a => a.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(await WithWeightAsync(updated, cancellationToken));
    }

    /// <summary>
    /// Deletes an assessment. Refused once marks exist, because the Marks foreign key
    /// cascades and would silently delete the students' scores.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAssessment(int id, CancellationToken cancellationToken)
    {
        var assessment = await _db.Assessments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (assessment is null)
        {
            return NotFoundProblem($"Assessment {id} was not found.");
        }

        if (await AuthoriseSubjectAsync(assessment.SubjectId, assessment.SectionId, cancellationToken) is { } denial)
        {
            return denial;
        }

        var markCount = await _db.Marks.CountAsync(m => m.AssessmentId == id, cancellationToken);
        if (markCount > 0)
        {
            return ConflictProblem(
                "Assessment has marks",
                $"{markCount} mark(s) have been entered against this assessment. Deactivate it instead of deleting.");
        }

        _db.Assessments.Remove(assessment);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Returns null when the caller may manage the subject, otherwise a 403 result.</summary>
    private async Task<ActionResult?> AuthoriseSubjectAsync(
        int subjectId,
        int? sectionId,
        CancellationToken cancellationToken)
    {
        if (User.IsAdmin())
        {
            return null;
        }

        var teacherId = User.GetTeacherId();
        if (teacherId is null)
        {
            return ForbiddenProblem("Your account is not linked to a teacher profile.");
        }

        return await _assignments.IsAssignedAsync(teacherId.Value, subjectId, sectionId, cancellationToken)
            ? null
            : ForbiddenProblem("You are not assigned to teach this subject to this section.");
    }

    /// <summary>
    /// Stamps each assessment with its weight. The five weights are read from the lookup
    /// table and applied after materialisation, so the report card and this list always
    /// quote the same percentages.
    /// </summary>
    private async Task<List<AssessmentResponse>> WithWeightsAsync(
        List<AssessmentResponse> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var weights = await _db.AssessmentTypeWeights
            .AsNoTracking()
            .ToDictionaryAsync(w => w.Name, w => w.WeightPercentage, cancellationToken);

        return items
            .Select(a => a with
            {
                WeightPercentage = weights.GetValueOrDefault(a.AssessmentType.ToString())
            })
            .ToList();
    }

    private async Task<AssessmentResponse> WithWeightAsync(
        AssessmentResponse assessment,
        CancellationToken cancellationToken) =>
        (await WithWeightsAsync([assessment], cancellationToken))[0];

    private static IQueryable<AssessmentResponse> Project(IQueryable<Assessment> query) =>
        query.Select(a => new AssessmentResponse
        {
            Id = a.Id,
            Title = a.Title,
            AssessmentType = a.AssessmentType,
            MaxScore = a.MaxScore,
            SubjectId = a.SubjectId,
            SubjectName = a.Subject!.SubjectName,
            SubjectCode = a.Subject.Code,
            GradeLevelId = a.Subject.GradeLevelId,
            GradeLevelName = a.Subject.GradeLevel!.Name,
            SectionId = a.SectionId,
            SectionName = a.Section != null ? a.Section.Name : null,
            TeacherId = a.TeacherId,
            TeacherName = a.Teacher != null && a.Teacher.User != null ? a.Teacher.User.FullName : null,
            AcademicYear = a.AcademicYear,
            DueDate = a.DueDate,
            IsActive = a.IsActive,
            MarkCount = a.Marks.Count,
            PublishedMarkCount = a.Marks.Count(m => m.IsPublished),
            CreatedAt = a.CreatedAt
        });
}
