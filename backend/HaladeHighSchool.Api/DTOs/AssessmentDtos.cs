using System.ComponentModel.DataAnnotations;
using HaladeHighSchool.Api.Models;

namespace HaladeHighSchool.Api.DTOs;

public record AssessmentResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public AssessmentType AssessmentType { get; init; }
    public decimal WeightPercentage { get; init; }
    public decimal MaxScore { get; init; }
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public int GradeLevelId { get; init; }
    public string GradeLevelName { get; init; } = string.Empty;
    public int? SectionId { get; init; }
    public string? SectionName { get; init; }
    public int? TeacherId { get; init; }
    public string? TeacherName { get; init; }
    public string AcademicYear { get; init; } = string.Empty;
    public DateOnly? DueDate { get; init; }
    public bool IsActive { get; init; }
    public int MarkCount { get; init; }
    public int PublishedMarkCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CreateAssessmentRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public AssessmentType AssessmentType { get; init; }

    [Required, Range(0.01, 1000)]
    public decimal MaxScore { get; init; }

    [Required]
    public int SubjectId { get; init; }

    /// <summary>Null applies the assessment to every section taking the subject.</summary>
    public int? SectionId { get; init; }

    public DateOnly? DueDate { get; init; }
}

public record UpdateAssessmentRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, Range(0.01, 1000)]
    public decimal MaxScore { get; init; }

    public DateOnly? DueDate { get; init; }

    public bool IsActive { get; init; } = true;
}
