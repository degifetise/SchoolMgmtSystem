namespace HaladeHighSchool.Api.Models;

public class Student
{
    public int Id { get; set; }

    public string StudentIdNumber { get; set; } = string.Empty;

    public int GradeLevelId { get; set; }

    public int SectionId { get; set; }

    /// <summary>
    /// Null when the login has been removed. The database sets this to NULL instead of
    /// deleting the student so marks and report cards are preserved.
    /// </summary>
    public string? UserId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? GuardianName { get; set; }

    public string? GuardianPhone { get; set; }

    public string? Address { get; set; }

    public DateOnly EnrollmentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GradeLevel? GradeLevel { get; set; }

    public Section? Section { get; set; }

    public ApplicationUser? User { get; set; }

    public ICollection<Mark> Marks { get; set; } = new List<Mark>();
}
