using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

public record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public UserProfileResponse User { get; init; } = new();
}

public record UserProfileResponse
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Populated when the user is a student.</summary>
    public int? StudentId { get; init; }
    public string? StudentIdNumber { get; init; }
    public int? GradeLevelId { get; init; }
    public string? GradeLevelName { get; init; }
    public int? SectionId { get; init; }
    public string? SectionName { get; init; }

    /// <summary>Populated when the user is a teacher.</summary>
    public int? TeacherId { get; init; }
    public string? EmployeeId { get; init; }
    public string? Specialization { get; init; }
}

/// <summary>
/// What an applicant sends to ask for a place. No password and no student number: the portal
/// issues both at approval, so an applicant cannot choose their own credentials or claim an
/// identifier that is not theirs.
/// </summary>
public record RegisterStudentRequest
{
    [Required(ErrorMessage = "Tell us your full name.")]
    [MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    /// <summary>The applicant's own address. The issued credentials are sent here.</summary>
    [Required(ErrorMessage = "An email address is required so the school can reach you.")]
    [EmailAddress(ErrorMessage = "That does not look like an email address.")]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a grade.")]
    public int GradeLevelId { get; init; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a section.")]
    public int SectionId { get; init; }
}

/// <summary>
/// Receipt for a submitted request. Carries no token and no credentials: nothing exists to sign
/// in with until an administrator approves it.
/// </summary>
public record RegistrationSubmittedResponse
{
    public int RequestId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
