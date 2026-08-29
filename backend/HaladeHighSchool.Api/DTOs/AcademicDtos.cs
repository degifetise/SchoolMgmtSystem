using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

// ---------------------------------------------------------------------------
// Grade levels
// ---------------------------------------------------------------------------
public record GradeLevelResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public int SubjectCount { get; init; }
    public int StudentCount { get; init; }
}

public record UpdateGradeLevelRequest
{
    [Required, MaxLength(50)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;
}

// ---------------------------------------------------------------------------
// Sections
// ---------------------------------------------------------------------------
public record SectionResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public bool IsActive { get; init; }
    public int StudentCount { get; init; }
}

public record CreateSectionRequest
{
    [Required, MaxLength(50)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(10)]
    public string Code { get; init; } = string.Empty;

    [Range(1, 200)]
    public int Capacity { get; init; } = 40;
}

public record UpdateSectionRequest
{
    [Required, MaxLength(50)]
    public string Name { get; init; } = string.Empty;

    [Range(1, 200)]
    public int Capacity { get; init; } = 40;

    public bool IsActive { get; init; } = true;
}

// ---------------------------------------------------------------------------
// Subjects
// ---------------------------------------------------------------------------
public record SubjectResponse
{
    public int Id { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int GradeLevelId { get; init; }
    public string GradeLevelName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CreditHours { get; init; }
    public bool IsActive { get; init; }
    public int LessonCount { get; init; }
    public int AssessmentCount { get; init; }
    public IReadOnlyList<string> Teachers { get; init; } = [];
}

public record CreateSubjectRequest
{
    [Required, MaxLength(150)]
    public string SubjectName { get; init; } = string.Empty;

    [Required, MaxLength(20)]
    public string Code { get; init; } = string.Empty;

    [Required]
    public int GradeLevelId { get; init; }

    [MaxLength(500)]
    public string? Description { get; init; }

    [Range(1, 10)]
    public int CreditHours { get; init; } = 3;
}

public record UpdateSubjectRequest
{
    [Required, MaxLength(150)]
    public string SubjectName { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; init; }

    [Range(1, 10)]
    public int CreditHours { get; init; } = 3;

    public bool IsActive { get; init; } = true;
}
