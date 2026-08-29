using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// The signed-in user's own account. Every action here works on the account in the bearer token
/// and no other, which is why nothing takes a user id.
/// </summary>
[ApiController]
[Route("api/account")]
[Authorize]
[Produces("application/json")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accounts;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(IAccountService accounts, UserManager<ApplicationUser> userManager)
    {
        _accounts = accounts;
        _userManager = userManager;
    }

    /// <summary>
    /// Changes the caller's password and records the attempt in PasswordChangeLogs.
    ///
    /// Only the password hash is touched. Email, user name, roles and every other identity
    /// attribute are read from the token and never from the request body, so this endpoint
    /// cannot be used to edit an identity - posting extra fields has no effect.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType<ChangePasswordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);

        // A token for an account that has since been deleted or switched off is not usable.
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var result = await _accounts.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword,
            new PasswordChangeContext
            {
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
            },
            cancellationToken);

        if (!result.Succeeded)
        {
            // Keyed by field so the form can show the message against the right input.
            var key = result.CurrentPasswordWrong
                ? nameof(ChangePasswordRequest.CurrentPassword)
                : nameof(ChangePasswordRequest.NewPassword);

            return BadRequest(new ValidationProblemDetails
            {
                Title = "Password not changed",
                Status = StatusCodes.Status400BadRequest,
                Errors = { [key] = [.. result.Errors] },
            });
        }

        return Ok(new ChangePasswordResponse
        {
            ChangedAt = result.ChangedAt,
            Message = "Your password has been changed. Use it the next time you sign in.",
        });
    }
}
