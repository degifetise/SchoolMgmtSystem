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
/// Mark entry for teachers and read-only published results for students.
/// A teacher may only touch marks for a subject/section pair they are assigned to in
/// TeacherSubjects; administrators are not restricted.
/// </summary>
[ApiController]
[Route("api/marks")]
[Authorize]
[Produces("application/json")]
public class MarksController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITeachingAssignmentService _assignments;
    private readonly ISystemSettingsService _settings;
    private readonly IGradingPolicyService _grading;
    private readonly IReportCardService _reportCards;
    private readonly ILogger<MarksController> _logger;

    public MarksController(
        ApplicationDbContext db,
        ITeachingAssignmentService assignments,
        ISystemSettingsService settings,
        IGradingPolicyService grading,
        IReportCardService reportCards,
        ILogger<MarksController> logger)
    {
        _db = db;
        _assignments = assignments;
        _settings = settings;
        _grading = grading;
        _reportCards = reportCards;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Teacher: mark entry
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the mark-entry sheet for an assessment: every eligible student with their
    /// current score, or null where no mark has been entered yet.
    /// </summary>
    [HttpGet("assessment/{assessmentId:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<GradebookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GradebookResponse>> GetGradebook(
        int assessmentId,
        CancellationToken cancellationToken)
    {
        var assessment = await _db.Assessments
            .AsNoTracking()
            .Include(a => a.Subject)
            .Include(a => a.Section)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken);

        if (assessment?.Subject is null)
        {
            return NotFoundProblem($"Assessment {assessmentId} was not found.");
        }

        if (await AuthoriseAssessmentAsync(assessment, cancellationToken) is { } denial)
        {
            return denial;
        }

        var weight = await _grading.GetWeightAsync(assessment.AssessmentType, cancellationToken);

        var students = await EligibleStudentsQuery(assessment)
            .OrderBy(s => s.Section!.Code)
            .ThenBy(s => s.StudentIdNumber)
            .Select(s => new
            {
                s.Id,
                s.StudentIdNumber,
                StudentName = s.User != null ? s.User.FullName : s.StudentIdNumber,
                SectionName = s.Section!.Name
            })
            .ToListAsync(cancellationToken);

        var marks = await _db.Marks
            .AsNoTracking()
            .Where(m => m.AssessmentId == assessmentId)
            .ToDictionaryAsync(m => m.StudentId, cancellationToken);

        var rows = students.Select(s =>
        {
            marks.TryGetValue(s.Id, out var mark);
            return new GradebookRow
            {
                StudentId = s.Id,
                StudentIdNumber = s.StudentIdNumber,
                StudentName = s.StudentName,
                SectionName = s.SectionName,
                MarkId = mark?.Id,
                Score = mark?.Score,
                Remark = mark?.Remark,
                IsPublished = mark?.IsPublished ?? false
            };
        }).ToList();

        return Ok(new GradebookResponse
        {
            AssessmentId = assessment.Id,
            AssessmentTitle = assessment.Title,
            AssessmentType = assessment.AssessmentType,
            MaxScore = assessment.MaxScore,
            WeightPercentage = weight,
            SubjectId = assessment.SubjectId,
            SubjectName = assessment.Subject.SubjectName,
            SubjectCode = assessment.Subject.Code,
            SectionName = assessment.Section?.Name,
            MarkedCount = rows.Count(r => r.Score is not null),
            TotalStudents = rows.Count,
            Rows = rows
        });
    }

    /// <summary>Creates or overwrites a single student's score for an assessment.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<MarkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MarkResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MarkResponse>> UpsertMark(
        MarkUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var assessment = await _db.Assessments
            .Include(a => a.Subject)
            .FirstOrDefaultAsync(a => a.Id == request.AssessmentId, cancellationToken);

        if (assessment?.Subject is null)
        {
            return NotFoundProblem($"Assessment {request.AssessmentId} was not found.");
        }

        if (await AuthoriseAssessmentAsync(assessment, cancellationToken) is { } denial)
        {
            return denial;
        }

        if (request.Score > assessment.MaxScore)
        {
            ModelState.AddModelError(nameof(request.Score),
                $"Score {request.Score} exceeds the maximum of {assessment.MaxScore} for '{assessment.Title}'.");
            return ValidationProblem(ModelState);
        }

        var student = await EligibleStudentsQuery(assessment)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);

        if (student is null)
        {
            ModelState.AddModelError(nameof(request.StudentId),
                "The student is not enrolled in the grade or section this assessment belongs to.");
            return ValidationProblem(ModelState);
        }

        var teacherId = await ResolveEnteringTeacherIdAsync(assessment, cancellationToken);
        if (teacherId is null)
        {
            return BadRequestProblem(
                "No teacher of record",
                "Marks must be attributed to a teacher. Assign a teacher to this subject first.");
        }

        var existing = await _db.Marks
            .FirstOrDefaultAsync(
                m => m.StudentId == request.StudentId && m.AssessmentId == request.AssessmentId,
                cancellationToken);

        var isNew = existing is null;

        if (existing is null)
        {
            existing = new Mark
            {
                StudentId = request.StudentId,
                AssessmentId = request.AssessmentId,
                // Taken from the assessment: the database trigger rejects any mismatch.
                SubjectId = assessment.SubjectId,
                EnteredByTeacherId = teacherId.Value,
                CreatedAt = DateTime.UtcNow
            };
            _db.Marks.Add(existing);
        }
        else
        {
            existing.UpdatedAt = DateTime.UtcNow;
            existing.EnteredByTeacherId = teacherId.Value;
        }

        existing.Score = request.Score;
        existing.Remark = request.Remark;
        SetPublished(existing, request.IsPublished);

        await _db.SaveChangesAsync(cancellationToken);

        var response = await BuildMarkResponseAsync(existing.Id, cancellationToken);

        return isNew
            ? CreatedAtAction(nameof(GetMark), new { id = existing.Id }, response)
            : Ok(response);
    }

    /// <summary>Enters or corrects scores for a whole class in one request.</summary>
    [HttpPost("bulk")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<BulkMarkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BulkMarkResponse>> BulkUpsert(
        BulkMarkRequest request,
        CancellationToken cancellationToken)
    {
        var assessment = await _db.Assessments
            .Include(a => a.Subject)
            .FirstOrDefaultAsync(a => a.Id == request.AssessmentId, cancellationToken);

        if (assessment?.Subject is null)
        {
            return NotFoundProblem($"Assessment {request.AssessmentId} was not found.");
        }

        if (await AuthoriseAssessmentAsync(assessment, cancellationToken) is { } denial)
        {
            return denial;
        }

        var duplicateIds = request.Entries
            .GroupBy(e => e.StudentId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            ModelState.AddModelError(nameof(request.Entries),
                $"Each student may appear once. Repeated: {string.Join(", ", duplicateIds)}.");
            return ValidationProblem(ModelState);
        }

        var overMax = request.Entries.Where(e => e.Score > assessment.MaxScore).ToList();
        if (overMax.Count > 0)
        {
            ModelState.AddModelError(nameof(request.Entries),
                $"{overMax.Count} score(s) exceed the maximum of {assessment.MaxScore}.");
            return ValidationProblem(ModelState);
        }

        var eligibleIds = await EligibleStudentsQuery(assessment)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var requestedIds = request.Entries.Select(e => e.StudentId).ToList();
        var ineligible = requestedIds.Except(eligibleIds).ToList();

        if (ineligible.Count > 0)
        {
            ModelState.AddModelError(nameof(request.Entries),
                $"Not enrolled in this class: {string.Join(", ", ineligible)}.");
            return ValidationProblem(ModelState);
        }

        var teacherId = await ResolveEnteringTeacherIdAsync(assessment, cancellationToken);
        if (teacherId is null)
        {
            return BadRequestProblem(
                "No teacher of record",
                "Marks must be attributed to a teacher. Assign a teacher to this subject first.");
        }

        var existingMarks = await _db.Marks
            .Where(m => m.AssessmentId == request.AssessmentId && requestedIds.Contains(m.StudentId))
            .ToDictionaryAsync(m => m.StudentId, cancellationToken);

        var created = 0;
        var updated = 0;

        foreach (var entry in request.Entries)
        {
            if (existingMarks.TryGetValue(entry.StudentId, out var mark))
            {
                mark.Score = entry.Score;
                mark.Remark = entry.Remark;
                mark.EnteredByTeacherId = teacherId.Value;
                mark.UpdatedAt = DateTime.UtcNow;
                SetPublished(mark, request.IsPublished);
                updated++;
            }
            else
            {
                var newMark = new Mark
                {
                    StudentId = entry.StudentId,
                    AssessmentId = request.AssessmentId,
                    SubjectId = assessment.SubjectId,
                    Score = entry.Score,
                    Remark = entry.Remark,
                    EnteredByTeacherId = teacherId.Value,
                    CreatedAt = DateTime.UtcNow
                };
                SetPublished(newMark, request.IsPublished);
                _db.Marks.Add(newMark);
                created++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bulk marks for assessment {AssessmentId}: {Created} created, {Updated} updated",
            request.AssessmentId, created, updated);

        return Ok(new BulkMarkResponse
        {
            AssessmentId = request.AssessmentId,
            Created = created,
            Updated = updated,
            IsPublished = request.IsPublished
        });
    }

    /// <summary>Corrects an existing mark.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<MarkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarkResponse>> UpdateMark(
        int id,
        MarkUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var mark = await _db.Marks
            .Include(m => m.Assessment)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (mark?.Assessment is null)
        {
            return NotFoundProblem($"Mark {id} was not found.");
        }

        if (await AuthoriseAssessmentAsync(mark.Assessment, cancellationToken) is { } denial)
        {
            return denial;
        }

        if (request.Score > mark.Assessment.MaxScore)
        {
            ModelState.AddModelError(nameof(request.Score),
                $"Score {request.Score} exceeds the maximum of {mark.Assessment.MaxScore}.");
            return ValidationProblem(ModelState);
        }

        mark.Score = request.Score;
        mark.Remark = request.Remark;
        mark.UpdatedAt = DateTime.UtcNow;

        if (request.IsPublished is bool publish)
        {
            SetPublished(mark, publish);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await BuildMarkResponseAsync(mark.Id, cancellationToken));
    }

    /// <summary>Publishes or unpublishes every mark for an assessment in one operation.</summary>
    [HttpPut("assessment/{assessmentId:int}/publish")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<PublishMarksResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishMarksResponse>> PublishAssessmentMarks(
        int assessmentId,
        PublishMarksRequest request,
        CancellationToken cancellationToken)
    {
        var assessment = await _db.Assessments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken);

        if (assessment is null)
        {
            return NotFoundProblem($"Assessment {assessmentId} was not found.");
        }

        if (await AuthoriseAssessmentAsync(assessment, cancellationToken) is { } denial)
        {
            return denial;
        }

        var publishedAt = request.IsPublished ? DateTime.UtcNow : (DateTime?)null;

        var affected = await _db.Marks
            .Where(m => m.AssessmentId == assessmentId && m.IsPublished != request.IsPublished)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.IsPublished, request.IsPublished)
                .SetProperty(m => m.PublishedAt, publishedAt)
                .SetProperty(m => m.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        _logger.LogInformation(
            "Assessment {AssessmentId} marks set to IsPublished={IsPublished} for {Affected} rows",
            assessmentId, request.IsPublished, affected);

        return Ok(new PublishMarksResponse
        {
            AssessmentId = assessmentId,
            IsPublished = request.IsPublished,
            AffectedMarks = affected
        });
    }

    /// <summary>Deletes a mark, for example when it was entered against the wrong student.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMark(int id, CancellationToken cancellationToken)
    {
        var mark = await _db.Marks
            .Include(m => m.Assessment)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (mark?.Assessment is null)
        {
            return NotFoundProblem($"Mark {id} was not found.");
        }

        if (await AuthoriseAssessmentAsync(mark.Assessment, cancellationToken) is { } denial)
        {
            return denial;
        }

        _db.Marks.Remove(mark);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Returns a single mark.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<MarkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarkResponse>> GetMark(int id, CancellationToken cancellationToken)
    {
        var response = await BuildMarkResponseAsync(id, cancellationToken);
        return response is null ? NotFoundProblem($"Mark {id} was not found.") : Ok(response);
    }

    // -----------------------------------------------------------------------
    // Student: published results
    // -----------------------------------------------------------------------

    /// <summary>Published marks for the signed-in student.</summary>
    [HttpGet("me")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType<IEnumerable<MarkResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<MarkResponse>>> GetMyMarks(
        [FromQuery] int? subjectId,
        CancellationToken cancellationToken)
    {
        var studentId = User.GetStudentId();
        if (studentId is null)
        {
            return Forbid();
        }

        var marks = await PublishedMarksQuery(studentId.Value, subjectId).ToListAsync(cancellationToken);
        return Ok(marks);
    }

    // A student reads their own results from GET api/students/my-results, which returns the same
    // figures plus the summary and weighting their screen needs.

    /// <summary>All marks for one student, including unpublished ones.</summary>
    [HttpGet("student/{studentId:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<IEnumerable<MarkResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MarkResponse>>> GetStudentMarks(
        int studentId,
        [FromQuery] int? subjectId,
        CancellationToken cancellationToken)
    {
        var query = _db.Marks.AsNoTracking().Where(m => m.StudentId == studentId);

        if (subjectId is int subject)
        {
            query = query.Where(m => m.SubjectId == subject);
        }

        var marks = await ProjectMarks(query)
            .OrderBy(m => m.SubjectCode)
            .ThenBy(m => m.AssessmentType)
            .ToListAsync(cancellationToken);

        return Ok(marks);
    }

    /// <summary>Weighted report card for any student.</summary>
    [HttpGet("student/{studentId:int}/report-card")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<ReportCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportCardResponse>> GetStudentReportCard(
        int studentId,
        CancellationToken cancellationToken)
    {
        var reportCard = await _reportCards.BuildAsync(studentId, cancellationToken);
        return reportCard is null
            ? NotFoundProblem($"Student {studentId} was not found.")
            : Ok(reportCard);
    }

    /// <summary>The assessment weighting used to build report cards.</summary>
    [HttpGet("weights")]
    [ProducesResponseType<IEnumerable<AssessmentTypeWeightResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AssessmentTypeWeightResponse>>> GetWeights(
        CancellationToken cancellationToken)
    {
        return Ok(await _grading.GetActiveAsync(cancellationToken));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns null when the caller may manage the assessment, otherwise the 403 result to
    /// return. Admins always pass; teachers must own the subject/section in TeacherSubjects.
    /// </summary>
    private async Task<ActionResult?> AuthoriseAssessmentAsync(
        Assessment assessment,
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

        var isAssigned = await _assignments.IsAssignedAsync(
            teacherId.Value, assessment.SubjectId, assessment.SectionId, cancellationToken);

        return isAssigned
            ? null
            : ForbiddenProblem("You are not assigned to teach this subject to this section.");
    }

    /// <summary>
    /// Students who may receive a mark for the assessment: same grade level as the subject,
    /// and same section when the assessment targets a single section.
    /// </summary>
    private IQueryable<Student> EligibleStudentsQuery(Assessment assessment)
    {
        var query = _db.Students
            .AsNoTracking()
            .Include(s => s.Section)
            .Include(s => s.User)
            .Where(s => s.IsActive && s.GradeLevelId == assessment.Subject!.GradeLevelId);

        if (assessment.SectionId is int sectionId)
        {
            query = query.Where(s => s.SectionId == sectionId);
        }

        return query;
    }

    /// <summary>
    /// Marks are attributed to a teacher, not to the signed-in account: when an admin
    /// enters marks the assessment's own teacher, or the assigned subject teacher, is used.
    /// </summary>
    private async Task<int?> ResolveEnteringTeacherIdAsync(
        Assessment assessment,
        CancellationToken cancellationToken)
    {
        if (User.GetTeacherId() is int teacherId)
        {
            return teacherId;
        }

        if (assessment.TeacherId is int assessmentTeacherId)
        {
            return assessmentTeacherId;
        }

        return await _db.TeacherSubjects
            .AsNoTracking()
            .Where(ts => ts.SubjectId == assessment.SubjectId && ts.IsActive &&
                         (assessment.SectionId == null || ts.SectionId == assessment.SectionId))
            .Select(ts => (int?)ts.TeacherId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static void SetPublished(Mark mark, bool isPublished)
    {
        if (mark.IsPublished == isPublished)
        {
            return;
        }

        mark.IsPublished = isPublished;
        mark.PublishedAt = isPublished ? DateTime.UtcNow : null;
    }

    private IQueryable<MarkResponse> PublishedMarksQuery(int studentId, int? subjectId)
    {
        var query = _db.Marks
            .AsNoTracking()
            .Where(m => m.StudentId == studentId && m.IsPublished);

        if (subjectId is int subject)
        {
            query = query.Where(m => m.SubjectId == subject);
        }

        return ProjectMarks(query);
    }

    private static IQueryable<MarkResponse> ProjectMarks(IQueryable<Mark> query) =>
        query.Select(m => new MarkResponse
        {
            Id = m.Id,
            StudentId = m.StudentId,
            StudentIdNumber = m.Student!.StudentIdNumber,
            StudentName = m.Student.User != null ? m.Student.User.FullName : m.Student.StudentIdNumber,
            SubjectId = m.SubjectId,
            SubjectName = m.Subject!.SubjectName,
            SubjectCode = m.Subject.Code,
            AssessmentId = m.AssessmentId,
            AssessmentTitle = m.Assessment!.Title,
            AssessmentType = m.Assessment.AssessmentType,
            MaxScore = m.Assessment.MaxScore,
            Score = m.Score,
            Percentage = m.Assessment.MaxScore == 0 ? 0 : Math.Round(m.Score / m.Assessment.MaxScore * 100, 2),
            Remark = m.Remark,
            IsPublished = m.IsPublished,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt
        });

    private async Task<MarkResponse?> BuildMarkResponseAsync(int markId, CancellationToken cancellationToken) =>
        await ProjectMarks(_db.Marks.AsNoTracking().Where(m => m.Id == markId))
            .FirstOrDefaultAsync(cancellationToken);

}
