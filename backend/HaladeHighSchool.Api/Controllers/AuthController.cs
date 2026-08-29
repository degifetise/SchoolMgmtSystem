using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HaladeHighSchool.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IRegistrationRequestService _registrations;
    private readonly ISystemSettingsService _settings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IRegistrationRequestService registrations,
        ISystemSettingsService settings,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _registrations = registrations;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Authenticates a user and returns a JWT access token with role claims.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        // The same response for an unknown email and a wrong password, so the endpoint
        // cannot be used to discover which accounts exist.
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials",
                Detail = "The email or password is incorrect.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (!user.IsActive)
        {
            /* An applicant awaiting approval has no login at all, so a deactivated account here
               is always one an administrator switched off. */
            return Unauthorized(new ProblemDetails
            {
                Title = "Account disabled",
                Detail = "This account has been deactivated. Contact the school administrator.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var signIn = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (signIn.IsLockedOut)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Account locked",
                Detail = "Too many failed attempts. Try again in 15 minutes.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (!signIn.Succeeded)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials",
                Detail = "The email or password is incorrect.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var response = await _tokenService.CreateAuthResponseAsync(user, GetIpAddress(), cancellationToken);
        _logger.LogInformation("User {Email} signed in", user.Email);

        return Ok(response);
    }

    /// <summary>
    /// Submits an application for a place. Anonymous, and allowed only while the
    /// AllowSelfRegistration system setting is on.
    ///
    /// Nothing is provisioned here: the request is recorded as Pending and an administrator
    /// issues the student number, the school sign-in address and a temporary password when they
    /// approve it. That is why the response carries no token and no credentials.
    /// </summary>
    [HttpPost("register-student")]
    [AllowAnonymous]
    [ProducesResponseType<RegistrationSubmittedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterStudent(
        RegisterStudentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _settings.IsSelfRegistrationAllowedAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Registration closed",
                Detail = "Self-registration is disabled. Ask the school administrator to create your account.",
                Status = StatusCodes.Status403Forbidden
            });
        }

        var result = await _registrations.SubmitAsync(request, cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return result.Failure switch
            {
                RegistrationFailure.Conflict => Conflict(new ProblemDetails
                {
                    Title = result.Title,
                    Detail = string.Join(" ", result.Errors),
                    Status = StatusCodes.Status409Conflict
                }),
                _ => BadRequest(new ValidationProblemDetails
                {
                    Title = result.Title,
                    Status = StatusCodes.Status400BadRequest,
                    Errors = { ["registration"] = [.. result.Errors] }
                }),
            };
        }

        var submitted = result.Value;

        return Accepted(new RegistrationSubmittedResponse
        {
            RequestId = submitted.Id,
            Status = submitted.Status,
            FullName = submitted.FullName,
            ContactEmail = submitted.ContactEmail,
            GradeLevelName = submitted.GradeLevel?.Name ?? string.Empty,
            SectionName = submitted.Section?.Name ?? string.Empty,
            SubmittedAt = submitted.SubmittedAt,
            Message =
                "Your registration has been received. The school will review it and email your "
              + "student number, sign-in address and a temporary password to "
              + $"{submitted.ContactEmail}."
        });
    }

    /// <summary>Exchanges a refresh token for a new access token, rotating the refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _tokenService.RefreshAsync(request.RefreshToken, GetIpAddress(), cancellationToken);

        if (response is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid refresh token",
                Detail = "The refresh token is expired, revoked or unknown. Sign in again.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        return Ok(response);
    }

    /// <summary>Revokes the supplied refresh token.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _tokenService.RevokeAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>Returns the signed-in user's profile, including student or teacher context.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        return Ok(await _tokenService.BuildProfileAsync(user, cancellationToken));
    }

    // Changing a password lives on AccountController, which also writes the audit row.

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
