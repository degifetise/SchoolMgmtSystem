using System.ComponentModel.DataAnnotations;
using HaladeHighSchool.Api.Models;

namespace HaladeHighSchool.Api.DTOs;

/// <summary>Create or overwrite the single score a student has for one assessment.</summary>
public record MarkUpsertRequest
{
    [Required]
    public int StudentId { get; init; }

    [Required]
    public int AssessmentId { get; init; }

    [Required, Range(0, 1000)]
    public decimal Score { get; init; }

    [MaxLength(300)]
    public string? Remark { get; init; }

    /// <summary>Publish immediately. Defaults to false so teachers can review first.</summary>
    public bool IsPublished { get; init; }
}

/// <summary>Enter a whole class worth of scores for one assessment in a single call.</summary>
public record BulkMarkRequest
{
    [Required]
    public int AssessmentId { get; init; }

    [Required, MinLength(1)]
    public List<BulkMarkEntry> Entries { get; init; } = [];

    public bool IsPublished { get; init; }
}

public record BulkMarkEntry
{
    [Required]
    public int StudentId { get; init; }

    [Required, Range(0, 1000)]
    public decimal Score { get; init; }

    [MaxLength(300)]
    public string? Remark { get; init; }
}

public record MarkUpdateRequest
{
    [Required, Range(0, 1000)]
    public decimal Score { get; init; }

    [MaxLength(300)]
    public string? Remark { get; init; }

    public bool? IsPublished { get; init; }
}

public record PublishMarksRequest
{
    public bool IsPublished { get; init; } = true;
}

public record MarkResponse
{
    public int Id { get; init; }
    public int StudentId { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public int AssessmentId { get; init; }
    public string AssessmentTitle { get; init; } = string.Empty;

    /// <summary>Serialised as text ("Quiz", "MidExam", ...) by the string enum converter.</summary>
    public AssessmentType AssessmentType { get; init; }

    public decimal MaxScore { get; init; }
    public decimal Score { get; init; }
    public decimal Percentage { get; init; }
    public string? Remark { get; init; }
    public bool IsPublished { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>One row per student for a teacher's mark-entry screen; Score is null when unmarked.</summary>
public record GradebookRow
{
    public int StudentId { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public int? MarkId { get; init; }
    public decimal? Score { get; init; }
    public string? Remark { get; init; }
    public bool IsPublished { get; init; }
}

public record GradebookResponse
{
    public int AssessmentId { get; init; }
    public string AssessmentTitle { get; init; } = string.Empty;
    public AssessmentType AssessmentType { get; init; }
    public decimal MaxScore { get; init; }
    public decimal WeightPercentage { get; init; }
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public string? SectionName { get; init; }
    public int MarkedCount { get; init; }
    public int TotalStudents { get; init; }
    public List<GradebookRow> Rows { get; init; } = [];
}

/// <summary>Weighted result for one subject, sourced from vw_StudentSubjectPerformance.</summary>
public record SubjectReportCard
{
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public decimal? QuizScore { get; init; }
    public decimal? AssignmentScore { get; init; }
    public decimal? TestScore { get; init; }
    public decimal? MidExamScore { get; init; }
    public decimal? FinalExamScore { get; init; }
    public decimal TotalScore { get; init; }
    public string LetterGrade { get; init; } = string.Empty;
    public bool IsPass { get; init; }
}

public record ReportCardResponse
{
    public int StudentId { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public string AcademicYear { get; init; } = string.Empty;
    public decimal PassMarkPercentage { get; init; }
    public decimal? AverageTotal { get; init; }
    public List<SubjectReportCard> Subjects { get; init; } = [];
}

/// <summary>Headline figures a student sees above their subject breakdown.</summary>
public record ResultsSummary
{
    /// <summary>Subjects carrying at least one published mark.</summary>
    public int SubjectCount { get; init; }

    public int SubjectsPassed { get; init; }

    /// <summary>Mean of the weighted subject totals. Null before anything is published.</summary>
    public decimal? WeightedAverage { get; init; }

    /// <summary>Published components across all subjects, out of five per subject.</summary>
    public int ComponentsMarked { get; init; }

    public string? StrongestSubject { get; init; }
    public decimal? StrongestSubjectTotal { get; init; }

    /// <summary>Omitted when there is only one subject, where it would repeat the strongest.</summary>
    public string? WeakestSubject { get; init; }
    public decimal? WeakestSubjectTotal { get; init; }
}

/// <summary>
/// Everything a student's results screen needs in one response: who they are, the weighted
/// result per subject with its component scores, the summary, and the weighting behind it.
/// </summary>
public record MyResultsResponse
{
    public int StudentId { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public string AcademicYear { get; init; } = string.Empty;
    public decimal PassMarkPercentage { get; init; }

    public List<SubjectReportCard> Subjects { get; init; } = [];
    public ResultsSummary Summary { get; init; } = new();
    public IReadOnlyList<AssessmentTypeWeightResponse> GradingWeights { get; init; } = [];
}

public record BulkMarkResponse
{
    public int AssessmentId { get; init; }
    public int Created { get; init; }
    public int Updated { get; init; }
    public bool IsPublished { get; init; }
}

public record PublishMarksResponse
{
    public int AssessmentId { get; init; }
    public bool IsPublished { get; init; }
    public int AffectedMarks { get; init; }
}

public record AssessmentTypeWeightResponse
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public decimal WeightPercentage { get; init; }
    public int DisplayOrder { get; init; }
}
