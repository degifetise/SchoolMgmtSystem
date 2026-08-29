namespace HaladeHighSchool.Api.Models;

/// <summary>
/// A single score for one student on one assessment. The database enforces one row
/// per student/assessment pair and rejects scores above the assessment's MaxScore.
/// Students only ever see rows where <see cref="IsPublished"/> is true.
/// </summary>
public class Mark
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int SubjectId { get; set; }

    public int AssessmentId { get; set; }

    public decimal Score { get; set; }

    public string? Remark { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int EnteredByTeacherId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Student? Student { get; set; }

    public Subject? Subject { get; set; }

    public Assessment? Assessment { get; set; }

    public Teacher? EnteredByTeacher { get; set; }
}
