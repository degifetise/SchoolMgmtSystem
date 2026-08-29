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
/// Lesson notes and downloadable materials. Teachers publish per subject and optionally per
/// section; students only ever see published lessons for their own class.
/// </summary>
[ApiController]
[Route("api/lessons")]
[Authorize]
[Produces("application/json")]
public class LessonsController : PortalControllerBase
{
    private const long MaxRequestBytes = 50L * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly ITeachingAssignmentService _assignments;
    private readonly ISystemSettingsService _settings;
    private readonly ILessonFileStorage _storage;
    private readonly ILogger<LessonsController> _logger;

    public LessonsController(
        ApplicationDbContext db,
        ITeachingAssignmentService assignments,
        ISystemSettingsService settings,
        ILessonFileStorage storage,
        ILogger<LessonsController> logger)
    {
        _db = db;
        _assignments = assignments;
        _settings = settings;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Lessons the caller is allowed to see.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<LessonResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<LessonResponse>>> GetLessons(
        [FromQuery] int? subjectId,
        [FromQuery] int? sectionId,
        [FromQuery] bool includeUnpublished = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Lessons.AsNoTracking();

        if (subjectId is int subject)
        {
            query = query.Where(l => l.SubjectId == subject);
        }

        if (sectionId is int section)
        {
            query = query.Where(l => l.SectionId == section || l.SectionId == null);
        }

        if (User.IsAdmin())
        {
            if (!includeUnpublished)
            {
                query = query.Where(l => l.IsPublished);
            }
        }
        else if (User.GetTeacherId() is int teacherId)
        {
            // A teacher sees their own drafts plus published lessons from anyone else
            // teaching the same subjects.
            var taught = await _assignments.GetTaughtSubjectIdsAsync(teacherId, cancellationToken);
            query = query.Where(l =>
                taught.Contains(l.SubjectId) && (l.IsPublished || l.TeacherId == teacherId));
        }
        else if (User.IsInRole(Roles.Student))
        {
            var student = await GetStudentScopeAsync(_db, User.GetUserId(), cancellationToken);
            if (student is null)
            {
                return ForbiddenProblem("Your account is not linked to an active student profile.");
            }

            query = query.Where(l =>
                l.IsPublished &&
                l.Subject!.GradeLevelId == student.GradeLevelId &&
                (l.SectionId == null || l.SectionId == student.SectionId));
        }
        else
        {
            return ForbiddenProblem("Your account is not linked to a student or teacher profile.");
        }

        var items = await Project(query)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<LessonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LessonResponse>> GetLesson(int id, CancellationToken cancellationToken)
    {
        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Subject)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (lesson?.Subject is null)
        {
            return NotFoundProblem($"Lesson {id} was not found.");
        }

        if (await AuthoriseReadAsync(lesson, cancellationToken) is { } denial)
        {
            return denial;
        }

        var response = await Project(_db.Lessons.AsNoTracking().Where(l => l.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(response);
    }

    /// <summary>Creates a lesson. Attach a file afterwards with POST /api/lessons/{id}/file.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<LessonResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LessonResponse>> CreateLesson(
        CreateLessonRequest request,
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

        if (await AuthoriseWriteAsync(request.SubjectId, request.SectionId, cancellationToken) is { } denial)
        {
            return denial;
        }

        var teacherId = await ResolveOwningTeacherIdAsync(request.SubjectId, request.SectionId, cancellationToken);
        if (teacherId is null)
        {
            return BadRequestProblem(
                "No teacher of record",
                "Lessons must belong to a teacher. Assign a teacher to this subject first.");
        }

        var lesson = new Lesson
        {
            Title = request.Title.Trim(),
            Content = request.Content,
            SubjectId = request.SubjectId,
            SectionId = request.SectionId,
            TeacherId = teacherId.Value,
            IsPublished = request.IsPublished,
            CreatedAt = DateTime.UtcNow
        };

        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await Project(_db.Lessons.AsNoTracking().Where(l => l.Id == lesson.Id))
            .FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(GetLesson), new { id = lesson.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<LessonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LessonResponse>> UpdateLesson(
        int id,
        UpdateLessonRequest request,
        CancellationToken cancellationToken)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (lesson is null)
        {
            return NotFoundProblem($"Lesson {id} was not found.");
        }

        if (await AuthoriseWriteAsync(lesson.SubjectId, lesson.SectionId, cancellationToken) is { } denial)
        {
            return denial;
        }

        if (request.SectionId is int sectionId &&
            !await _db.Sections.AnyAsync(s => s.Id == sectionId && s.IsActive, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.SectionId), "Unknown or inactive section.");
            return ValidationProblem(ModelState);
        }

        lesson.Title = request.Title.Trim();
        lesson.Content = request.Content;
        lesson.SectionId = request.SectionId;
        lesson.IsPublished = request.IsPublished;
        lesson.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await Project(_db.Lessons.AsNoTracking().Where(l => l.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLesson(int id, CancellationToken cancellationToken)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (lesson is null)
        {
            return NotFoundProblem($"Lesson {id} was not found.");
        }

        if (await AuthoriseWriteAsync(lesson.SubjectId, lesson.SectionId, cancellationToken) is { } denial)
        {
            return denial;
        }

        var storedPath = lesson.FileUrl;

        _db.Lessons.Remove(lesson);
        await _db.SaveChangesAsync(cancellationToken);

        if (storedPath is not null)
        {
            _storage.Delete(storedPath);
        }

        return NoContent();
    }

    // -----------------------------------------------------------------------
    // Attachments
    // -----------------------------------------------------------------------

    /// <summary>Attaches or replaces the lesson's downloadable material.</summary>
    [HttpPost("{id:int}/file")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType<LessonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LessonResponse>> UploadFile(
        int id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (lesson is null)
        {
            return NotFoundProblem($"Lesson {id} was not found.");
        }

        if (await AuthoriseWriteAsync(lesson.SubjectId, lesson.SectionId, cancellationToken) is { } denial)
        {
            return denial;
        }

        if (file is null || file.Length == 0)
        {
            return BadRequestProblem("Empty upload", "Attach a file in the 'file' form field.");
        }

        if (!_storage.IsAllowed(file.FileName))
        {
            return BadRequestProblem(
                "File type not allowed",
                $"Allowed extensions: {string.Join(", ", _storage.AllowedExtensions.Order())}.");
        }

        var maxBytes = await _settings.GetMaxUploadBytesAsync(cancellationToken);
        if (file.Length > maxBytes)
        {
            return BadRequestProblem(
                "File too large",
                $"The file is {file.Length / 1024 / 1024} MB; the limit is {maxBytes / 1024 / 1024} MB.");
        }

        var previous = lesson.FileUrl;
        var stored = await _storage.SaveAsync(file, cancellationToken);

        lesson.FileUrl = stored.RelativePath;
        lesson.FileName = stored.OriginalFileName;
        lesson.FileSizeBytes = stored.SizeBytes;
        lesson.ContentType = stored.ContentType;
        lesson.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Only discard the old file once the new one is safely recorded.
        if (previous is not null)
        {
            _storage.Delete(previous);
        }

        _logger.LogInformation("Attached {FileName} to lesson {LessonId}", stored.OriginalFileName, id);

        var response = await Project(_db.Lessons.AsNoTracking().Where(l => l.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Streams the lesson material. Files are stored outside wwwroot, so this endpoint is the
    /// only way to reach them and every download passes the same read authorisation.
    /// </summary>
    [HttpGet("{id:int}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(int id, CancellationToken cancellationToken)
    {
        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Subject)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (lesson?.Subject is null)
        {
            return NotFoundProblem($"Lesson {id} was not found.");
        }

        if (await AuthoriseReadAsync(lesson, cancellationToken) is { } denial)
        {
            return denial;
        }

        if (lesson.FileUrl is null)
        {
            return NotFoundProblem("This lesson has no attachment.");
        }

        var path = _storage.ResolveExistingPath(lesson.FileUrl);
        if (path is null)
        {
            _logger.LogError("Lesson {LessonId} references a missing file {Path}", id, lesson.FileUrl);
            return NotFoundProblem("The attachment is no longer available on the server.");
        }

        return PhysicalFile(
            path,
            lesson.ContentType ?? "application/octet-stream",
            lesson.FileName ?? Path.GetFileName(path));
    }

    /// <summary>Removes the attachment but keeps the lesson notes.</summary>
    [HttpDelete("{id:int}/file")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(int id, CancellationToken cancellationToken)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (lesson is null)
        {
            return NotFoundProblem($"Lesson {id} was not found.");
        }

        if (await AuthoriseWriteAsync(lesson.SubjectId, lesson.SectionId, cancellationToken) is { } denial)
        {
            return denial;
        }

        var storedPath = lesson.FileUrl;

        lesson.FileUrl = null;
        lesson.FileName = null;
        lesson.FileSizeBytes = null;
        lesson.ContentType = null;
        lesson.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        if (storedPath is not null)
        {
            _storage.Delete(storedPath);
        }

        return NoContent();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<ActionResult?> AuthoriseWriteAsync(
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

    private async Task<ActionResult?> AuthoriseReadAsync(Lesson lesson, CancellationToken cancellationToken)
    {
        if (User.IsAdmin())
        {
            return null;
        }

        if (User.GetTeacherId() is int teacherId)
        {
            if (lesson.TeacherId == teacherId)
            {
                return null;
            }

            return await _assignments.IsAssignedAsync(teacherId, lesson.SubjectId, null, cancellationToken)
                ? null
                : ForbiddenProblem("This lesson belongs to a subject you do not teach.");
        }

        var student = await GetStudentScopeAsync(_db, User.GetUserId(), cancellationToken);
        if (student is null)
        {
            return ForbiddenProblem("Your account is not linked to an active student profile.");
        }

        var isForStudent =
            lesson.IsPublished &&
            lesson.Subject!.GradeLevelId == student.GradeLevelId &&
            (lesson.SectionId is null || lesson.SectionId == student.SectionId);

        return isForStudent ? null : ForbiddenProblem("This lesson is not shared with your class.");
    }

    /// <summary>
    /// Lessons must belong to a teacher: an admin acting alone inherits the teacher assigned
    /// to the subject and section.
    /// </summary>
    private async Task<int?> ResolveOwningTeacherIdAsync(
        int subjectId,
        int? sectionId,
        CancellationToken cancellationToken)
    {
        if (User.GetTeacherId() is int teacherId)
        {
            return teacherId;
        }

        return await _db.TeacherSubjects
            .AsNoTracking()
            .Where(ts => ts.SubjectId == subjectId && ts.IsActive &&
                         (sectionId == null || ts.SectionId == sectionId))
            .Select(ts => (int?)ts.TeacherId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<LessonResponse> Project(IQueryable<Lesson> query) =>
        query.Select(l => new LessonResponse
        {
            Id = l.Id,
            Title = l.Title,
            Content = l.Content,
            SubjectId = l.SubjectId,
            SubjectName = l.Subject!.SubjectName,
            SubjectCode = l.Subject.Code,
            GradeLevelId = l.Subject.GradeLevelId,
            GradeLevelName = l.Subject.GradeLevel!.Name,
            TeacherId = l.TeacherId,
            TeacherName = l.Teacher!.User != null ? l.Teacher.User.FullName : string.Empty,
            SectionId = l.SectionId,
            SectionName = l.Section != null ? l.Section.Name : null,
            IsPublished = l.IsPublished,
            HasAttachment = l.FileUrl != null,
            FileName = l.FileName,
            FileSizeBytes = l.FileSizeBytes,
            ContentType = l.ContentType,
            DownloadUrl = l.FileUrl != null ? $"/api/lessons/{l.Id}/file" : null,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        });
}
