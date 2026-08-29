using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// A student's own results. Separate from StudentsController, which is administrative: the two
/// share the api/students prefix but not their audience, and combining them would mean either
/// admin-only actions on a student route or a student-reachable admin controller.
/// </summary>
[ApiController]
[Route("api/students")]
[Authorize(Roles = Roles.Student)]
[Produces("application/json")]
public class StudentResultsController : ControllerBase
{
    private readonly IReportCardService _reportCards;

    public StudentResultsController(IReportCardService reportCards)
    {
        _reportCards = reportCards;
    }

    /// <summary>
    /// Subjects, component scores, weighted totals and grade summary for the signed-in student.
    ///
    /// The student is taken from the token's student claim, so one student cannot read another's
    /// results by changing a parameter - there is no parameter to change. Only published marks
    /// reach the view this is built from.
    /// </summary>
    [HttpGet("my-results")]
    [ProducesResponseType<MyResultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyResultsResponse>> GetMyResults(CancellationToken cancellationToken)
    {
        var studentId = User.GetStudentId();

        /* A Student-role account with no student record is a provisioning fault rather than a
           bad request, so it is reported as such instead of an empty report card. */
        if (studentId is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "No student profile",
                Detail = "This account is not linked to a student record. Contact the school office.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        var results = await _reportCards.BuildMyResultsAsync(studentId.Value, cancellationToken);

        if (results is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "No student profile",
                Detail = "The linked student record no longer exists. Contact the school office.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(results);
    }
}
