using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

public static class SystemSettingsLimits
{
    /// <summary>
    /// Kestrel's default MaxRequestBodySize is 30,000,000 bytes and Program.cs does not raise
    /// it, so an upload limit above this could never actually be honoured - the request would
    /// be rejected by the server before LessonsController got the chance to check the setting.
    /// </summary>
    public const int MaxUploadCeilingMb = 28;
}

/// <summary>
/// The subset of settings safe to serve without authentication, so the login screen and the
/// dashboard header can render the school's identity before anybody has signed in.
/// AllowSelfRegistration is included so the login page knows whether to offer a sign-up link.
/// </summary>
public record SchoolInfoResponse
{
    public string SchoolName { get; init; } = string.Empty;
    public string? ContactEmail { get; init; }
    public string AcademicYear { get; init; } = string.Empty;
    public bool AllowSelfRegistration { get; init; }
}

/// <summary>The full contents of the SystemSettings table, all of it admin editable.</summary>
public record SystemSettingsResponse
{
    public string SchoolName { get; init; } = string.Empty;
    public string? ContactEmail { get; init; }
    public string AcademicYear { get; init; } = string.Empty;
    public decimal PassMarkPercentage { get; init; }
    public int MaxUploadSizeMb { get; init; }
    public bool AllowSelfRegistration { get; init; }

    /// <summary>When any setting was last actually changed, or null on an unseeded table.</summary>
    public DateTime? LastUpdatedAt { get; init; }
}

public record UpdateSystemSettingsRequest : IValidatableObject
{
    [Required, MaxLength(200)]
    public string SchoolName { get; init; } = string.Empty;

    /// <summary>
    /// Optional. Send null or an empty string to clear it. The address is checked in
    /// <see cref="Validate"/> rather than with [EmailAddress], because that attribute treats
    /// an empty string as invalid and would make the field impossible to clear.
    /// </summary>
    [MaxLength(256)]
    public string? ContactEmail { get; init; }

    /// <summary>When false, only an administrator can create accounts.</summary>
    public bool AllowSelfRegistration { get; init; }

    [Range(0, 100)]
    public decimal PassMarkPercentage { get; init; } = 50m;

    [Range(1, SystemSettingsLimits.MaxUploadCeilingMb)]
    public int MaxUploadSizeMb { get; init; } = 25;

    /// <summary>
    /// Character class is [0-9] rather than \d because the value is written into columns
    /// guarded by CK_Assessments_AcademicYear, whose LIKE pattern accepts ASCII digits only.
    /// </summary>
    [Required]
    [RegularExpression(
        "^[0-9]{4}-[0-9]{4}$",
        ErrorMessage = "AcademicYear must look like '2026-2027'.")]
    public string AcademicYear { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(ContactEmail) &&
            !new EmailAddressAttribute().IsValid(ContactEmail))
        {
            yield return new ValidationResult(
                "ContactEmail must be a valid email address, or empty to remove it.",
                [nameof(ContactEmail)]);
        }

        if (AcademicYear.Length != 9 ||
            !int.TryParse(AcademicYear[..4], out var start) ||
            !int.TryParse(AcademicYear[5..], out var end))
        {
            yield break;
        }

        if (end != start + 1)
        {
            yield return new ValidationResult(
                $"AcademicYear must span two consecutive years, so '{start}-{start + 1}' rather than '{AcademicYear}'.",
                [nameof(AcademicYear)]);
        }
    }
}
