namespace HaladeHighSchool.Api.Models;

public class Teacher
{
    public int Id { get; set; }

    public string EmployeeId { get; set; } = string.Empty;

    public string? Specialization { get; set; }

    /// <summary>Null when the login has been removed; the teaching record is kept.</summary>
    public string? UserId { get; set; }

    public string? Qualification { get; set; }

    public string? PhoneNumber { get; set; }

    public DateOnly? HireDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();

    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
}
