using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

public record LessonResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Content { get; init; }
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public int GradeLevelId { get; init; }
    public string GradeLevelName { get; init; } = string.Empty;
    public int TeacherId { get; init; }
    public string TeacherName { get; init; } = string.Empty;
    public int? SectionId { get; init; }
    public string? SectionName { get; init; }
    public bool IsPublished { get; init; }
    public bool HasAttachment { get; init; }
    public string? FileName { get; init; }
    public long? FileSizeBytes { get; init; }
    public string? ContentType { get; init; }

    /// <summary>
    /// Authorised download route. Attachments are stored outside wwwroot, so this is the
    /// only way to fetch them.
    /// </summary>
    public string? DownloadUrl { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CreateLessonRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    public string? Content { get; init; }

    [Required]
    public int SubjectId { get; init; }

    /// <summary>Null shares the lesson with every section taking the subject.</summary>
    public int? SectionId { get; init; }

    public bool IsPublished { get; init; } = true;
}

public record UpdateLessonRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    public string? Content { get; init; }

    public int? SectionId { get; init; }

    public bool IsPublished { get; init; } = true;
}
