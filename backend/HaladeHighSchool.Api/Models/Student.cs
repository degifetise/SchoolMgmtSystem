using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HaladeHighSchool.Api.Models;

public class Student
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string StudentIdNumber { get; set; } = string.Empty;

    public int GradeLevelId { get; set; }

    public int SectionId { get; set; }

    /// <summary>
    /// Foreign key pointing to ApplicationUser (Id is int).
    /// Null when the login account has been removed, preserving historical student performance records.
    /// </summary>
    public int? UserId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    [MaxLength(100)]
    public string? GuardianName { get; set; }

    [MaxLength(20)]
    public string? GuardianPhone { get; set; }

    [MaxLength(255)]
    public string? Address { get; set; }

    public DateOnly EnrollmentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey(nameof(GradeLevelId))]
    public virtual GradeLevel? GradeLevel { get; set; }

    [ForeignKey(nameof(SectionId))]
    public virtual Section? Section { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser? User { get; set; }

    public virtual ICollection<Mark> Marks { get; set; } = new List<Mark>();
}