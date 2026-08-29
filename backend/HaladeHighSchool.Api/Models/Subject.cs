namespace HaladeHighSchool.Api.Models;

/// <summary>
/// A subject is defined per grade level, so "Mathematics" exists once for each
/// of Grade 9-12 with its own code (MATH-9, MATH-10, ...).
/// </summary>
public class Subject
{
    public int Id { get; set; }

    public string SubjectName { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public int GradeLevelId { get; set; }

    public string? Description { get; set; }

    public int CreditHours { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GradeLevel? GradeLevel { get; set; }

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();

    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();

    public ICollection<Mark> Marks { get; set; } = new List<Mark>();
}
