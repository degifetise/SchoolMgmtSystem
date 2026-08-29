namespace HaladeHighSchool.Api.Models;

public static class PasswordChangeOutcome
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

/// <summary>
/// One password change attempt. Failures are recorded too: a series of them against a single
/// account is the thing worth noticing, and successes alone cannot show it.
/// </summary>
public class PasswordChangeLog
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Succeeded or Failed - see <see cref="PasswordChangeOutcome"/>.</summary>
    public string Outcome { get; set; } = PasswordChangeOutcome.Succeeded;

    /// <summary>
    /// Why the attempt failed, in the words Identity used. Never holds a password or any part
    /// of one.
    /// </summary>
    public string? FailureReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public ApplicationUser? User { get; set; }
}
