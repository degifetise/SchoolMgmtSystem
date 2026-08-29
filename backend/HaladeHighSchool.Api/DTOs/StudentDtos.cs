using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record StudentListItem
{
    public int Id { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public int GradeLevelId { get; init; }
    public string GradeLevelName { get; init; } = string.Empty;
    public int SectionId { get; init; }
    public string SectionName { get; init; } = string.Empty;
    public string? Gender { get; init; }
    public bool IsActive { get; init; }
    public bool HasLogin { get; init; }
    public DateOnly EnrollmentDate { get; init; }
}

public record StudentDetailResponse : StudentListItem
{
    public string? UserId { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public string? Address { get; init; }
    public string? ProfileImageUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public int MarksCount { get; init; }
}

public record CreateStudentRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Optional. A temporary password is generated when omitted.</summary>
    [MinLength(8), MaxLength(100)]
    public string? Password { get; init; }

    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    public int GradeLevelId { get; init; }

    [Required]
    public int SectionId { get; init; }

    [MaxLength(30)]
    public string? StudentIdNumber { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female or Other.")]
    public string? Gender { get; init; }

    [MaxLength(150)]
    public string? GuardianName { get; init; }

    [MaxLength(30)]
    public string? GuardianPhone { get; init; }

    [MaxLength(250)]
    public string? Address { get; init; }
}

public record CreateStudentResponse
{
    public StudentDetailResponse Student { get; init; } = new();

    /// <summary>Returned once, only when the API generated the password.</summary>
    public string? TemporaryPassword { get; init; }
}

public record UpdateStudentRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    public DateOnly? DateOfBirth { get; init; }

    [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female or Other.")]
    public string? Gender { get; init; }

    [MaxLength(150)]
    public string? GuardianName { get; init; }

    [MaxLength(30)]
    public string? GuardianPhone { get; init; }

    [MaxLength(250)]
    public string? Address { get; init; }

    [MaxLength(500)]
    public string? ProfileImageUrl { get; init; }
}

/// <summary>Move a student to a different grade and/or section.</summary>
public record AssignClassRequest
{
    [Required]
    public int GradeLevelId { get; init; }

    [Required]
    public int SectionId { get; init; }
}

public record SetActiveRequest
{
    public bool IsActive { get; init; }
}

public record ResetPasswordResponse
{
    public string TemporaryPassword { get; init; } = string.Empty;
}
