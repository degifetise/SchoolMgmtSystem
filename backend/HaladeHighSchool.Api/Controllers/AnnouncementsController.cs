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
/// School noticeboard. Administrators post school-wide notices; teachers post to the classes
/// they teach. Every reader only sees notices addressed to them.
/// </summary>
[ApiController]
[Route("api/announcements")]
[Authorize]
[Produces("application/json")]
public class AnnouncementsController : PortalControllerBase
{
    private const string TargetAll = "All";
    private const string TargetStudent = "Student";

    private readonly ApplicationDbContext _db;
    private readonly ILogger<AnnouncementsController> _logger;

    public AnnouncementsController(ApplicationDbContext db, ILogger<AnnouncementsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// The caller's noticeboard, pinned notices first. Administrators can additionally ask for
    /// drafts and expired notices.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<AnnouncementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AnnouncementResponse>>> GetAnnouncements(
        [FromQuery] bool includeUnpublished = false,
        [FromQuery] bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _db.Announcements.AsNoTracking();

        if (User.IsAdmin())
        {
            if (!includeUnpublished)
            {
                query = query.Where(a => a.IsPublished);
            }

            if (!includeExpired)
            {
                query = query.Where(a => a.ExpiresAt == null || a.ExpiresAt > now);
            }
        }
        else
        {
            query = query.Where(a => a.IsPublished && (a.ExpiresAt == null || a.ExpiresAt > now));

            if (User.GetTeacherId() is not null)
            {
                var userId = User.GetUserId();
                query = query.Where(a =>
                    a.TargetRole == TargetAll ||
                    a.TargetRole == Roles.Teacher ||
                    a.CreatedByUserId == userId);
            }
            else if (User.IsInRole(Roles.Student))
            {
                var student = await GetStudentScopeAsync(_db, User.GetUserId(), cancellationToken);
                if (student is null)
                {
                    return ForbiddenProblem("Your account is not linked to an active student profile.");
                }

                query = query.Where(a =>
                    (a.TargetRole == TargetAll || a.TargetRole == TargetStudent) &&
                    (a.GradeLevelId == null || a.GradeLevelId == student.GradeLevelId) &&
                    (a.SectionId == null || a.SectionId == student.SectionId));
            }
            else
            {
                return ForbiddenProblem("Your account is not linked to a student or teacher profile.");
            }
        }

        var items = await Project(query)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<AnnouncementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementResponse>> GetAnnouncement(
        int id,
        CancellationToken cancellationToken)
    {
        var announcement = await _db.Announcements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (announcement is null)
        {
            return NotFoundProblem($"Announcement {id} was not found.");
        }

        if (await AuthoriseReadAsync(announcement, cancellationToken) is { } denial)
        {
            return denial;
        }

        var response = await Project(_db.Announcements.AsNoTracking().Where(a => a.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Posts a notice. Teachers may only address students in a class they teach; school-wide
    /// and staff notices are reserved for administrators.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<AnnouncementResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AnnouncementResponse>> CreateAnnouncement(
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        if (request.GradeLevelId is int gradeLevelId &&
            !await _db.GradeLevels.AnyAsync(g => g.Id == gradeLevelId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.GradeLevelId), "Unknown grade level.");
            return ValidationProblem(ModelState);
        }

        if (request.SectionId is int sectionId &&
            !await _db.Sections.AnyAsync(s => s.Id == sectionId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.SectionId), "Unknown section.");
            return ValidationProblem(ModelState);
        }

        if (request.ExpiresAt is DateTime expiry && expiry <= DateTime.UtcNow)
        {
            ModelState.AddModelError(nameof(request.ExpiresAt), "The expiry date must be in the future.");
            return ValidationProblem(ModelState);
        }

        if (!User.IsAdmin())
        {
            if (await AuthoriseTeacherPostAsync(request, cancellationToken) is { } denial)
            {
                return denial;
            }
        }

        var announcement = new Announcement
        {
            Title = request.Title.Trim(),
            Content = request.Content,
            TargetRole = request.TargetRole,
            GradeLevelId = request.GradeLevelId,
            SectionId = request.SectionId,
            CreatedByUserId = User.GetUserId(),
            IsPublished = request.IsPublished,
            IsPinned = request.IsPinned,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await Project(_db.Announcements.AsNoTracking().Where(a => a.Id == announcement.Id))
            .FirstAsync(cancellationToken);

        _logger.LogInformation(
            "Announcement '{Title}' posted to {TargetRole} by {UserId}",
            created.Title, created.TargetRole, User.GetUserId());

        return CreatedAtAction(nameof(GetAnnouncement), new { id = announcement.Id }, created);
    }

    /// <summary>Edits a notice. Teachers may only edit their own.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<AnnouncementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementResponse>> UpdateAnnouncement(
        int id,
        UpdateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var announcement = await _db.Announcements.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (announcement is null)
        {
            return NotFoundProblem($"Announcement {id} was not found.");
        }

        if (!User.IsAdmin() && announcement.CreatedByUserId != User.GetUserId())
        {
            return ForbiddenProblem("You can only edit announcements you posted.");
        }

        announcement.Title = request.Title.Trim();
        announcement.Content = request.Content;
        announcement.IsPublished = request.IsPublished;
        announcement.IsPinned = request.IsPinned;
        announcement.ExpiresAt = request.ExpiresAt;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await Project(_db.Announcements.AsNoTracking().Where(a => a.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(updated);
    }

    /// <summary>Deletes a notice. Teachers may only delete their own.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAnnouncement(int id, CancellationToken cancellationToken)
    {
        var announcement = await _db.Announcements.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (announcement is null)
        {
            return NotFoundProblem($"Announcement {id} was not found.");
        }

        if (!User.IsAdmin() && announcement.CreatedByUserId != User.GetUserId())
        {
            return ForbiddenProblem("You can only delete announcements you posted.");
        }

        _db.Announcements.Remove(announcement);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ActionResult?> AuthoriseTeacherPostAsync(
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var teacherId = User.GetTeacherId();
        if (teacherId is null)
        {
            return ForbiddenProblem("Your account is not linked to a teacher profile.");
        }

        if (request.TargetRole is not (TargetAll or TargetStudent))
        {
            return ForbiddenProblem("Teachers can only address students. Ask an administrator for staff notices.");
        }

        if (request.GradeLevelId is null || request.SectionId is null)
        {
            return ForbiddenProblem(
                "Teachers must address a specific grade and section. School-wide notices are posted by administrators.");
        }

        var teachesClass = await _db.TeacherSubjects
            .AsNoTracking()
            .AnyAsync(ts =>
                    ts.TeacherId == teacherId.Value &&
                    ts.IsActive &&
                    ts.SectionId == request.SectionId &&
                    ts.Subject!.GradeLevelId == request.GradeLevelId,
                cancellationToken);

        return teachesClass
            ? null
            : ForbiddenProblem("You do not teach that grade and section.");
    }

    private async Task<ActionResult?> AuthoriseReadAsync(
        Announcement announcement,
        CancellationToken cancellationToken)
    {
        if (User.IsAdmin())
        {
            return null;
        }

        var isVisible = announcement.IsPublished &&
                        (announcement.ExpiresAt is null || announcement.ExpiresAt > DateTime.UtcNow);

        if (announcement.CreatedByUserId == User.GetUserId())
        {
            return null;
        }

        if (User.GetTeacherId() is not null)
        {
            return isVisible && announcement.TargetRole is TargetAll or Roles.Teacher
                ? null
                : ForbiddenProblem("This announcement is not addressed to you.");
        }

        var student = await GetStudentScopeAsync(_db, User.GetUserId(), cancellationToken);
        if (student is null)
        {
            return ForbiddenProblem("Your account is not linked to an active student profile.");
        }

        var isForStudent = isVisible &&
                           announcement.TargetRole is TargetAll or TargetStudent &&
                           (announcement.GradeLevelId is null ||
                            announcement.GradeLevelId == student.GradeLevelId) &&
                           (announcement.SectionId is null ||
                            announcement.SectionId == student.SectionId);

        return isForStudent ? null : ForbiddenProblem("This announcement is not addressed to you.");
    }

    private static IQueryable<AnnouncementResponse> Project(IQueryable<Announcement> query) =>
        query.Select(a => new AnnouncementResponse
        {
            Id = a.Id,
            Title = a.Title,
            Content = a.Content,
            TargetRole = a.TargetRole,
            GradeLevelId = a.GradeLevelId,
            GradeLevelName = a.GradeLevel != null ? a.GradeLevel.Name : null,
            SectionId = a.SectionId,
            SectionName = a.Section != null ? a.Section.Name : null,
            CreatedByUserId = a.CreatedByUserId,
            CreatedByName = a.CreatedByUser != null ? a.CreatedByUser.FullName : null,
            IsPublished = a.IsPublished,
            IsPinned = a.IsPinned,
            ExpiresAt = a.ExpiresAt,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        });
}
