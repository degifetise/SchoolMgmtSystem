using HaladeHighSchool.Api.DTOs;

namespace HaladeHighSchool.Api.Services;

/// <summary>
/// Typed access to the admin editable SystemSettings table. The six rows are held in the
/// process memory cache for a few minutes and refreshed in place by
/// <see cref="UpdateSettingsAsync"/>, so an administrator's change still takes effect on the very
/// next request while ordinary traffic no longer queries the table at all.
///
/// A row edited outside the API - directly in SSMS, or by a second instance behind a load
/// balancer - is picked up when the entry expires rather than immediately.
/// </summary>
public interface ISystemSettingsService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Every setting in the table, for the admin settings screen.</summary>
    Task<SystemSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The publicly safe subset - school name, contact address, academic year and whether
    /// self-registration is open - for anonymous callers such as the login screen.
    /// </summary>
    Task<SchoolInfoResponse> GetSchoolInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the writable settings and returns the table as it now stands.
    /// Missing rows are inserted, so the settings screen still works against a database
    /// where a key was never seeded or was deleted by hand.
    /// </summary>
    Task<SystemSettingsResponse> UpdateSettingsAsync(
        UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Active academic year, e.g. "2026-2027". Falls back to the current year pair.</summary>
    Task<string> GetAcademicYearAsync(CancellationToken cancellationToken = default);

    Task<decimal> GetPassMarkPercentageAsync(CancellationToken cancellationToken = default);

    Task<long> GetMaxUploadBytesAsync(CancellationToken cancellationToken = default);

    Task<bool> IsSelfRegistrationAllowedAsync(CancellationToken cancellationToken = default);
}
