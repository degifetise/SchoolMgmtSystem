using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

/// <summary>Where a password change came from, recorded alongside the outcome.</summary>
public record PasswordChangeContext
{
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public record PasswordChangeResult
{
    public bool Succeeded { get; init; }
    public DateTime ChangedAt { get; init; }

    /// <summary>Identity's own messages, safe to show: they describe policy, not the password.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>True when the current password did not match, as opposed to a policy failure.</summary>
    public bool CurrentPasswordWrong { get; init; }
}

public interface IAccountService
{
    /// <summary>
    /// Changes the signed-in user's password and records the attempt. Touches the password hash
    /// and nothing else: no email, user name, role or claim is read from the caller.
    /// </summary>
    Task<PasswordChangeResult> ChangePasswordAsync(
        ApplicationUser user,
        string currentPassword,
        string newPassword,
        PasswordChangeContext context,
        CancellationToken cancellationToken = default);
}

public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountService> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(
        ApplicationUser user,
        string currentPassword,
        string newPassword,
        PasswordChangeContext context,
        CancellationToken cancellationToken = default)
    {
        /* ChangePasswordAsync verifies the current password itself and re-stamps the security
           stamp on success, which is what invalidates other sessions. It is used rather than
           RemovePassword/AddPassword so a wrong current password cannot leave an account with
           no password at all. */
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (result.Succeeded)
        {
            var log = await WriteLogAsync(user.Id, PasswordChangeOutcome.Succeeded, null, context, cancellationToken);

            _logger.LogInformation("Password changed for {UserId}", user.Id);

            return new PasswordChangeResult { Succeeded = true, ChangedAt = log.ChangedAt };
        }

        var errors = result.Errors.Select(e => e.Description).ToArray();

        // Identity reports a mismatch with this code; everything else is a policy failure.
        var wrongCurrent = result.Errors.Any(e => e.Code == "PasswordMismatch");

        await WriteLogAsync(
            user.Id,
            PasswordChangeOutcome.Failed,
            // Recorded so a run of attempts against one account is visible. Never a password.
            string.Join("; ", errors),
            context,
            cancellationToken);

        _logger.LogWarning(
            "Password change refused for {UserId}: {Reason}",
            user.Id,
            wrongCurrent ? "current password did not match" : "new password rejected by policy");

        return new PasswordChangeResult
        {
            Succeeded = false,
            Errors = errors,
            CurrentPasswordWrong = wrongCurrent,
        };
    }

    private async Task<PasswordChangeLog> WriteLogAsync(
        string userId,
        string outcome,
        string? failureReason,
        PasswordChangeContext context,
        CancellationToken cancellationToken)
    {
        var log = new PasswordChangeLog
        {
            UserId = userId,
            ChangedAt = DateTime.UtcNow,
            Outcome = outcome,
            // Truncated to the column width rather than risking a write that throws.
            FailureReason = Truncate(failureReason, 300),
            IpAddress = Truncate(context.IpAddress, 45),
            UserAgent = Truncate(context.UserAgent, 400),
        };

        _db.PasswordChangeLogs.Add(log);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            /* The password has already changed by this point, so failing the request would be a
               lie. The audit gap is logged instead. */
            _logger.LogError(ex, "Could not record the password change for {UserId}", userId);
            _db.Entry(log).State = EntityState.Detached;
        }

        return log;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
