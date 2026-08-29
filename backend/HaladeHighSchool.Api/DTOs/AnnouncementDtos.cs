using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

public record AnnouncementResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string TargetRole { get; init; } = "All";
    public int? GradeLevelId { get; init; }
    public string? GradeLevelName { get; init; }
    public int? SectionId { get; init; }
    public string? SectionName { get; init; }
    public string? CreatedByUserId { get; init; }
    public string? CreatedByName { get; init; }
    public bool IsPublished { get; init; }
    public bool IsPinned { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CreateAnnouncementRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Content { get; init; } = string.Empty;

    /// <summary>All, Admin, Teacher or Student. Teachers may only post to Student or All.</summary>
    [Required, RegularExpression("^(All|Admin|Teacher|Student)$",
        ErrorMessage = "TargetRole must be All, Admin, Teacher or Student.")]
    public string TargetRole { get; init; } = "All";

    /// <summary>Null targets every grade. Teachers must scope to a class they teach.</summary>
    public int? GradeLevelId { get; init; }

    /// <summary>Null targets every section.</summary>
    public int? SectionId { get; init; }

    public bool IsPublished { get; init; } = true;

    public bool IsPinned { get; init; }

    public DateTime? ExpiresAt { get; init; }
}

public record UpdateAnnouncementRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Content { get; init; } = string.Empty;

    public bool IsPublished { get; init; } = true;

    public bool IsPinned { get; init; }

    public DateTime? ExpiresAt { get; init; }
}
