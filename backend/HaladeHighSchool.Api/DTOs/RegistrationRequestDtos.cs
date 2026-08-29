using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

/// <summary>One application in the queue, as an administrator sees it.</summary>
public record RegistrationRequestResponse
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public int GradeLevelId { get; init; }
    public string GradeLevelName { get; init; } = string.Empty;
    public int SectionId { get; init; }
    public string SectionName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }

    /// <summary>Seats in the class asked for, so capacity is visible at the decision.</summary>
    public int SectionCapacity { get; init; }
    public int SectionOccupancy { get; init; }

    public DateTime? ReviewedAt { get; init; }
    public string? ReviewedByName { get; init; }
    public string? ReviewNote { get; init; }

    /// <summary>Set once approved. The temporary password is never returned again.</summary>
    public int? CreatedStudentId { get; init; }
    public string? IssuedEmail { get; init; }
    public string? StudentIdNumber { get; init; }
}

/// <summary>Optional note recorded against an approval or a rejection.</summary>
public record ReviewRegistrationRequest
{
    [MaxLength(300)]
    public string? Note { get; init; }
}

/// <summary>
/// The credentials issued by an approval. The temporary password is in this response and
/// nowhere else - it is not stored in readable form and cannot be fetched again, so it has to
/// be passed on to the student from here.
/// </summary>
public record ApprovedRegistrationResponse
{
    public int RequestId { get; init; }
    public int StudentId { get; init; }
    public string FullName { get; init; } = string.Empty;

    /// <summary>Auto-generated, of the form HHS-{year}-{sequence}.</summary>
    public string StudentIdNumber { get; init; } = string.Empty;

    /// <summary>The generated sign-in address, derived from the student number.</summary>
    public string IssuedEmail { get; init; } = string.Empty;

    /// <summary>Where to send the credentials: the address the applicant gave.</summary>
    public string ContactEmail { get; init; } = string.Empty;

    /// <summary>Shown once. Not recoverable.</summary>
    public string TemporaryPassword { get; init; } = string.Empty;

    public string GradeLevelName { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public DateTime ApprovedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}
