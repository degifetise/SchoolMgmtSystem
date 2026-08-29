using HaladeHighSchool.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>The class a student belongs to, used to scope what they are allowed to read.</summary>
public sealed record StudentScope(int StudentId, int GradeLevelId, int SectionId);

/// <summary>
/// Shared ProblemDetails helpers so every endpoint in the portal reports failures in the
/// same shape, which lets the Axios interceptor render errors generically.
/// </summary>
public abstract class PortalControllerBase : ControllerBase
{
    /// <summary>
    /// Resolves the caller's class from the database rather than trusting the token, so a
    /// transfer between sections takes effect without waiting for the token to expire.
    /// </summary>
    protected static Task<StudentScope?> GetStudentScopeAsync(
        ApplicationDbContext db,
        string? userId,
        CancellationToken cancellationToken) =>
        db.Students
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .Select(s => new StudentScope(s.Id, s.GradeLevelId, s.SectionId))
            .FirstOrDefaultAsync(cancellationToken);

    protected NotFoundObjectResult NotFoundProblem(string detail) =>
        NotFound(new ProblemDetails
        {
            Title = "Not found",
            Detail = detail,
            Status = StatusCodes.Status404NotFound
        });

    protected ObjectResult ForbiddenProblem(string detail) =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Forbidden",
            Detail = detail,
            Status = StatusCodes.Status403Forbidden
        });

    protected ConflictObjectResult ConflictProblem(string title, string detail) =>
        Conflict(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status409Conflict
        });

    protected BadRequestObjectResult BadRequestProblem(string title, string detail) =>
        BadRequest(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status400BadRequest
        });
}
