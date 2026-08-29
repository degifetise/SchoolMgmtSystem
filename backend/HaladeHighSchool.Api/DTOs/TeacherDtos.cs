using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

public record TeacherListItem
{
    public int Id { get; init; }
    public string EmployeeId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Specialization { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsActive { get; init; }
    public bool HasLogin { get; init; }
    public int AssignmentCount { get; init; }
}

public record TeacherDetailResponse : TeacherListItem
{
    public string? UserId { get; init; }
    public string? Qualification { get; init; }
    public DateOnly? HireDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<TeachingAssignmentResponse> Assignments { get; init; } = [];
}

public record CreateTeacherRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Optional. A temporary password is generated when omitted.</summary>
    [MinLength(8), MaxLength(100)]
    public string? Password { get; init; }

    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    /// <summary>Optional. Generated as EMP-{year}-{sequence} when omitted.</summary>
    [MaxLength(30)]
    public string? EmployeeId { get; init; }

    [MaxLength(150)]
    public string? Specialization { get; init; }

    [MaxLength(150)]
    public string? Qualification { get; init; }

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }

    public DateOnly? HireDate { get; init; }
}

public record CreateTeacherResponse
{
    public TeacherDetailResponse Teacher { get; init; } = new();

    /// <summary>Returned once, only when the API generated the password.</summary>
    public string? TemporaryPassword { get; init; }
}

public record UpdateTeacherRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(150)]
    public string? Specialization { get; init; }

    [MaxLength(150)]
    public string? Qualification { get; init; }

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }

    public DateOnly? HireDate { get; init; }

    [MaxLength(500)]
    public string? ProfileImageUrl { get; init; }
}

/// <summary>Gives a teacher one subject in one section.</summary>
public record CreateTeachingAssignmentRequest
{
    [Required]
    public int SubjectId { get; init; }

    [Required]
    public int SectionId { get; init; }
}

/// <summary>
/// One student on a teacher's class list, with their standing in that teacher's subject.
/// Scores come from vw_StudentSubjectPerformance, so they are the same weighted figures the
/// student sees on their own report card - published marks only.
/// </summary>
public record ClassRosterEntry
{
    public int StudentId { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Gender { get; init; }
    public bool IsActive { get; init; }

    /// <summary>Null until at least one mark in this subject has been published.</summary>
    public decimal? TotalScore { get; init; }
    public string? LetterGrade { get; init; }
    public bool? IsPass { get; init; }

    /// <summary>How many of the five weighted components carry a published mark.</summary>
    public int ComponentsMarked { get; init; }
}

/// <summary>A teacher's class list plus the headline figures for that class.</summary>
public record ClassRosterResponse
{
    public int AssignmentId { get; init; }
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public int SectionId { get; init; }
    public string SectionName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public string AcademicYear { get; init; } = string.Empty;
    public decimal PassMarkPercentage { get; init; }

    /// <summary>Mean weighted total across the students who have a published mark.</summary>
    public decimal? ClassAverage { get; init; }
    public int MarkedCount { get; init; }
    public int PassCount { get; init; }

    public IReadOnlyList<ClassRosterEntry> Students { get; init; } = [];
}

public record TeachingAssignmentResponse
{
    public int Id { get; init; }
    public int TeacherId { get; init; }
    public string TeacherName { get; init; } = string.Empty;
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public int GradeLevelId { get; init; }
    public string GradeLevelName { get; init; } = string.Empty;
    public int SectionId { get; init; }
    public string SectionName { get; init; } = string.Empty;
    public string AcademicYear { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int StudentCount { get; init; }
}
