using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HaladeHighSchool.Api.Services;

/// <summary>
/// The assessment weighting from the AssessmentTypes lookup table: five rows that decide how
/// quizzes, assignments, tests and exams add up to a subject total.
///
/// The rows are a lookup, not data the portal writes, and vw_StudentSubjectPerformance already
/// applies them inside SQL Server. The API only ever needs to quote the same percentages back,
/// so they are read once and cached for the whole process instead of on every list, report card
/// and gradebook request.
/// </summary>
public interface IGradingPolicyService
{
    /// <summary>Every row, in display order, including any that are switched off.</summary>
    Task<IReadOnlyList<AssessmentTypeWeightResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The active rows in display order - the weighting a report card is built from.</summary>
    Task<IReadOnlyList<AssessmentTypeWeightResponse>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>One type's weight, or 0 when the lookup row is missing.</summary>
    Task<decimal> GetWeightAsync(AssessmentType type, CancellationToken cancellationToken = default);

    /// <summary>Weight by assessment type name, for stamping a list of assessments.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetWeightMapAsync(CancellationToken cancellationToken = default);
}

public class GradingPolicyService : IGradingPolicyService
{
    private const string CacheKey = "GradingPolicy:Weights";

    /// <summary>
    /// Nothing in the API writes to AssessmentTypes, so this only bounds how long a weight
    /// changed directly in the database takes to reach the API. Keep it short enough that the
    /// percentages the portal quotes cannot disagree with the report card view for long.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public GradingPolicyService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<AssessmentTypeWeightResponse>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        (await GetWeightsAsync(cancellationToken)).All;

    public async Task<IReadOnlyList<AssessmentTypeWeightResponse>> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        (await GetWeightsAsync(cancellationToken)).Active;

    public async Task<decimal> GetWeightAsync(
        AssessmentType type,
        CancellationToken cancellationToken = default) =>
        (await GetWeightsAsync(cancellationToken)).ByName.GetValueOrDefault(type.ToString());

    public async Task<IReadOnlyDictionary<string, decimal>> GetWeightMapAsync(
        CancellationToken cancellationToken = default) =>
        (await GetWeightsAsync(cancellationToken)).ByName;

    private async Task<WeightSnapshot> GetWeightsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out WeightSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        var rows = await _db.AssessmentTypeWeights
            .AsNoTracking()
            .OrderBy(w => w.DisplayOrder)
            .Select(w => new
            {
                w.Name,
                w.DisplayName,
                w.WeightPercentage,
                w.DisplayOrder,
                w.IsActive
            })
            .ToListAsync(cancellationToken);

        var weights = rows
            .Select(w => (
                Dto: new AssessmentTypeWeightResponse
                {
                    Name = w.Name,
                    DisplayName = w.DisplayName,
                    WeightPercentage = w.WeightPercentage,
                    DisplayOrder = w.DisplayOrder
                },
                w.IsActive))
            .ToList();

        var snapshot = new WeightSnapshot(
            weights.Select(w => w.Dto).ToList(),
            weights.Where(w => w.IsActive).Select(w => w.Dto).ToList(),
            rows.ToDictionary(w => w.Name, w => w.WeightPercentage, StringComparer.OrdinalIgnoreCase));

        _cache.Set(CacheKey, snapshot, CacheDuration);

        return snapshot;
    }

    /// <summary>
    /// Shared by every request for the lifetime of the cache entry, so all three views are
    /// materialised up front and handed out as read-only.
    /// </summary>
    private sealed record WeightSnapshot(
        IReadOnlyList<AssessmentTypeWeightResponse> All,
        IReadOnlyList<AssessmentTypeWeightResponse> Active,
        IReadOnlyDictionary<string, decimal> ByName);
}
