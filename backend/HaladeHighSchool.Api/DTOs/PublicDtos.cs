namespace HaladeHighSchool.Api.DTOs;

/// <summary>
/// Everything the unauthenticated marketing pages need, in one call: identity, aggregate
/// figures, what is taught, and the grading policy. Aggregates only - no personal data.
/// </summary>
public record PublicOverviewResponse
{
    public string SchoolName { get; init; } = string.Empty;
    public string? ContactEmail { get; init; }
    public string AcademicYear { get; init; } = string.Empty;
    public bool AllowSelfRegistration { get; init; }

    public PublicTotals Totals { get; init; } = new();
    public IReadOnlyList<PublicGradeLevel> GradeLevels { get; init; } = [];

    /// <summary>
    /// Class sections, needed by the public registration form because a student row cannot
    /// exist without one. Names only - no roster, no occupancy.
    /// </summary>
    public IReadOnlyList<PublicSection> Sections { get; init; } = [];

    /// <summary>The AssessmentTypes lookup table, so the client never hard-codes the weights.</summary>
    public IReadOnlyList<AssessmentTypeWeightResponse> GradingWeights { get; init; } = [];
}

public record PublicTotals
{
    public int Students { get; init; }
    public int Teachers { get; init; }
    public int Subjects { get; init; }
    public int GradeLevels { get; init; }
    public int Sections { get; init; }
}

public record PublicSection
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

public record PublicGradeLevel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public string? Description { get; init; }
    public int SubjectCount { get; init; }
}

/// <summary>
/// A school-wide announcement presented as a noticeboard entry. There is no separate Events
/// table; the "Explore events" page is the public slice of the Announcements feed.
/// </summary>
public record PublicEventResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool IsPinned { get; init; }
    public DateTime PostedAt { get; init; }

    /// <summary>Null when the notice does not expire.</summary>
    public DateTime? ExpiresAt { get; init; }
}
