namespace HaladeHighSchool.Api.DTOs;

/// <summary>
/// Result of GET /api/health/db-check. Deliberately free of anything that would help an
/// attacker: no connection string, no server or login name, no schema detail.
/// </summary>
public record DbHealthResponse
{
    /// <summary>"Healthy", "Degraded" or "Unhealthy".</summary>
    public string Status { get; init; } = "Unhealthy";

    public bool CanConnect { get; init; }

    /// <summary>Database name only, so a misconfigured environment is obvious.</summary>
    public string Database { get; init; } = string.Empty;

    /// <summary>ADO.NET connection state, for example "Open" or "Closed".</summary>
    public string ConnectionState { get; init; } = "Unknown";

    public string AcademicYear { get; init; } = string.Empty;

    /// <summary>Round trip for the connectivity probe, in milliseconds.</summary>
    public long LatencyMs { get; init; }

    public DateTime CheckedAtUtc { get; init; }

    public DbIntegritySummary Integrity { get; init; } = new();

    /// <summary>Present only when the probe failed.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Cheap sanity checks on the objects the portal cannot work without: the grading policy has
/// to total 100% for a report card to mean anything, and the weighted view has to be queryable.
/// </summary>
public record DbIntegritySummary
{
    public bool SchemaReachable { get; init; }
    public int AssessmentTypeCount { get; init; }
    public decimal GradingWeightTotal { get; init; }

    /// <summary>True when the weights add up to exactly 100%.</summary>
    public bool GradingPolicyValid { get; init; }

    /// <summary>True when vw_StudentSubjectPerformance answered a probe query.</summary>
    public bool ReportCardViewAvailable { get; init; }

    public int SettingsCount { get; init; }
}
