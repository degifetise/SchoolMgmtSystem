using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// The anonymous read surface behind the public site: the home, about and events pages.
/// Every other controller requires a token, so keeping these few endpoints in one place makes
/// the whole unauthenticated surface auditable from a single file.
///
/// Two rules hold everywhere below:
///   * aggregates only - never a student, teacher, mark or lesson;
///   * published, school-wide content only - anything scoped to a role, grade or section
///     stays behind /api/announcements, which requires a token.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[Produces("application/json")]
public class PublicController : PortalControllerBase
{
    private const int DefaultEventCount = 20;
    private const int MaxEventCount = 50;

    private readonly ApplicationDbContext _db;
    private readonly ISystemSettingsService _settings;
    private readonly IGradingPolicyService _grading;

    public PublicController(
        ApplicationDbContext db,
        ISystemSettingsService settings,
        IGradingPolicyService grading)
    {
        _db = db;
        _settings = settings;
        _grading = grading;
    }

    /// <summary>School identity, headline figures, grades taught and the grading policy.</summary>
    [HttpGet("overview")]
    [ProducesResponseType<PublicOverviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicOverviewResponse>> GetOverview(CancellationToken cancellationToken)
    {
        var schoolInfo = await _settings.GetSchoolInfoAsync(cancellationToken);

        var gradeLevels = await _db.GradeLevels
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.Level)
            .Select(g => new PublicGradeLevel
            {
                Id = g.Id,
                Name = g.Name,
                Level = g.Level,
                Description = g.Description,
                SubjectCount = g.Subjects.Count(s => s.IsActive)
            })
            .ToListAsync(cancellationToken);

        var sections = await _db.Sections
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .Select(s => new PublicSection { Id = s.Id, Name = s.Name, Code = s.Code })
            .ToListAsync(cancellationToken);

        var totals = new PublicTotals
        {
            Students = await _db.Students.CountAsync(s => s.IsActive, cancellationToken),
            Teachers = await _db.Teachers.CountAsync(t => t.IsActive, cancellationToken),
            Subjects = await _db.Subjects.CountAsync(s => s.IsActive, cancellationToken),
            GradeLevels = gradeLevels.Count,
            Sections = sections.Count
        };

        var weights = await _grading.GetAllAsync(cancellationToken);

        return Ok(new PublicOverviewResponse
        {
            SchoolName = schoolInfo.SchoolName,
            ContactEmail = schoolInfo.ContactEmail,
            AcademicYear = schoolInfo.AcademicYear,
            AllowSelfRegistration = schoolInfo.AllowSelfRegistration,
            Totals = totals,
            GradeLevels = gradeLevels,
            Sections = sections,
            GradingWeights = weights
        });
    }

    /// <summary>
    /// Published, unexpired, school-wide notices, pinned ones first.
    ///
    /// The filter is deliberately narrow: TargetRole must be "All" and both GradeLevelId and
    /// SectionId must be null. A notice aimed at teachers, or at one class, can name people or
    /// carry internal detail, so it never appears here however it was published.
    /// </summary>
    [HttpGet("events")]
    [ProducesResponseType<IEnumerable<PublicEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublicEventResponse>>> GetEvents(
        [FromQuery] int take = DefaultEventCount,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, MaxEventCount);
        var now = DateTime.UtcNow;

        var events = await _db.Announcements
            .AsNoTracking()
            .Where(a => a.IsPublished
                     && a.TargetRole == "All"
                     && a.GradeLevelId == null
                     && a.SectionId == null
                     && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new PublicEventResponse
            {
                Id = a.Id,
                Title = a.Title,
                Content = a.Content,
                IsPinned = a.IsPinned,
                PostedAt = a.CreatedAt,
                ExpiresAt = a.ExpiresAt
            })
            .ToListAsync(cancellationToken);

        return Ok(events);
    }
}
