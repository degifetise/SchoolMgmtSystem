namespace HaladeHighSchool.Api.Models;

public class Assessment
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public AssessmentType AssessmentType { get; set; }

    public decimal MaxScore { get; set; }

    public int SubjectId { get; set; }

    /// <summary>Null means the assessment applies to every section taking the subject.</summary>
    public int? SectionId { get; set; }

    public int? TeacherId { get; set; }

    public string AcademicYear { get; set; } = string.Empty;

    public DateOnly? DueDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Subject? Subject { get; set; }

    public Section? Section { get; set; }

    public Teacher? Teacher { get; set; }

    public ICollection<Mark> Marks { get; set; } = new List<Mark>();
}
