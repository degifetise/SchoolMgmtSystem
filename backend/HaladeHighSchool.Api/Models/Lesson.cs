namespace HaladeHighSchool.Api.Models;

public class Lesson
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Content { get; set; }

    public string? FileUrl { get; set; }

    public string? FileName { get; set; }

    public long? FileSizeBytes { get; set; }

    public string? ContentType { get; set; }

    public int SubjectId { get; set; }

    public int TeacherId { get; set; }

    /// <summary>Null means the lesson is shared with every section taking the subject.</summary>
    public int? SectionId { get; set; }

    public bool IsPublished { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Subject? Subject { get; set; }

    public Teacher? Teacher { get; set; }

    public Section? Section { get; set; }
}
