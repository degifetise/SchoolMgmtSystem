using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// The student registration queue. Administrators only: approving one issues real credentials.
/// </summary>
[ApiController]
[Route("api/admin/registration-requests")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class RegistrationRequestsController : ControllerBase
{
    private readonly IRegistrationRequestService _registrations;

    public RegistrationRequestsController(IRegistrationRequestService registrations)
    {
        _registrations = registrations;
    }

    /// <summary>
    /// Applications awaiting review, oldest first. Pass status=Approved or status=Rejected to
    /// read what has already been decided.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<RegistrationRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RegistrationRequestResponse>>> GetRequests(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var requested = string.IsNullOrWhiteSpace(status)
            ? RegistrationRequestStatus.Pending
            : RegistrationRequestStatus.All.FirstOrDefault(
                s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));

        if (requested is null)
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Unknown status",
                Status = StatusCodes.Status400BadRequest,
                Errors =
                {
                    ["status"] = [$"Status must be one of: {string.Join(", ", RegistrationRequestStatus.All)}."],
                },
            });
        }

        return Ok(await _registrations.ListAsync(requested, cancellationToken));
    }

    /// <summary>
    /// Approves an application: generates the student number and the school sign-in address,
    /// creates the login and the student record, and marks the request Approved.
    ///
    /// The temporary password is in this response and nowhere else. It is stored only as a hash,
    /// so it cannot be shown again.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType<ApprovedRegistrationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovedRegistrationResponse>> Approve(
        int id,
        ReviewRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        var reviewerId = User.GetUserId();
        if (reviewerId is null)
        {
            return Unauthorized();
        }

        var result = await _registrations.ApproveAsync(id, request?.Note, reviewerId, cancellationToken);

        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : Problem(result);
    }

    /// <summary>
    /// Turns an application down. Nothing is provisioned, so the applicant is free to apply
    /// again later.
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType<RegistrationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegistrationRequestResponse>> Reject(
        int id,
        ReviewRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        var reviewerId = User.GetUserId();
        if (reviewerId is null)
        {
            return Unauthorized();
        }

        var result = await _registrations.RejectAsync(id, request?.Note, reviewerId, cancellationToken);

        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : Problem(result);
    }

    /// <summary>Turns a service failure into the matching status code.</summary>
    private ActionResult Problem<T>(RegistrationResult<T> result) where T : class
    {
        var detail = string.Join(" ", result.Errors);

        return result.Failure switch
        {
            RegistrationFailure.NotFound => NotFound(new ProblemDetails
            {
                Title = result.Title,
                Detail = detail,
                Status = StatusCodes.Status404NotFound,
            }),
            RegistrationFailure.Conflict => Conflict(new ProblemDetails
            {
                Title = result.Title,
                Detail = detail,
                Status = StatusCodes.Status409Conflict,
            }),
            _ => BadRequest(new ValidationProblemDetails
            {
                Title = result.Title,
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["registration"] = [.. result.Errors] },
            }),
        };
    }
}
