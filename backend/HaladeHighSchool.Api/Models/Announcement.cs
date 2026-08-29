namespace HaladeHighSchool.Api.Models;

public class Announcement
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>All, Admin, Teacher or Student.</summary>
    public string TargetRole { get; set; } = "All";

    /// <summary>Null means every grade.</summary>
    public int? GradeLevelId { get; set; }

    /// <summary>Null means every section.</summary>
    public int? SectionId { get; set; }

    public string? CreatedByUserId { get; set; }

    public bool IsPublished { get; set; } = true;

    public bool IsPinned { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public GradeLevel? GradeLevel { get; set; }

    public Section? Section { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
