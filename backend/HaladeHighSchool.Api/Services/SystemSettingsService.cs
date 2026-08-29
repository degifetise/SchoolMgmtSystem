using System.Globalization;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

public class SystemSettingsService : ISystemSettingsService
{
    public const string SchoolNameKey = "SchoolName";
    public const string ContactEmailKey = "ContactEmail";
    public const string AcademicYearKey = "AcademicYear";
    public const string PassMarkKey = "PassMarkPercentage";
    public const string MaxUploadSizeKey = "MaxUploadSizeMb";
    public const string SelfRegistrationKey = "AllowSelfRegistration";

    private const string DefaultSchoolName = "Halade High School";
    private const decimal DefaultPassMark = 50m;
    private const int DefaultMaxUploadMb = 25;

    private readonly ApplicationDbContext _db;

    public SystemSettingsService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        _db.SystemSettings
            .AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<SystemSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.SystemSettings.AsNoTracking().ToListAsync(cancellationToken);

        string? Value(string key) => rows.FirstOrDefault(r => r.Key == key)?.Value;

        return new SystemSettingsResponse
        {
            SchoolName = Value(SchoolNameKey) is { Length: > 0 } name ? name : DefaultSchoolName,
            ContactEmail = Value(ContactEmailKey),
            AcademicYear = NormalizeAcademicYear(Value(AcademicYearKey)),
            PassMarkPercentage = ParsePassMark(Value(PassMarkKey)),
            MaxUploadSizeMb = ParseMaxUploadMb(Value(MaxUploadSizeKey)),
            AllowSelfRegistration = ParseSelfRegistration(Value(SelfRegistrationKey)),
            LastUpdatedAt = rows.Count == 0 ? null : rows.Max(r => r.UpdatedAt)
        };
    }

    public async Task<SchoolInfoResponse> GetSchoolInfoAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.SystemSettings
            .AsNoTracking()
            .Where(s => s.Key == SchoolNameKey
                     || s.Key == ContactEmailKey
                     || s.Key == AcademicYearKey
                     || s.Key == SelfRegistrationKey)
            .Select(s => new { s.Key, s.Value })
            .ToListAsync(cancellationToken);

        string? Value(string key) => rows.FirstOrDefault(r => r.Key == key)?.Value;

        return new SchoolInfoResponse
        {
            SchoolName = Value(SchoolNameKey) is { Length: > 0 } name ? name : DefaultSchoolName,
            ContactEmail = Value(ContactEmailKey),
            AcademicYear = NormalizeAcademicYear(Value(AcademicYearKey)),
            AllowSelfRegistration = ParseSelfRegistration(Value(SelfRegistrationKey))
        };
    }

    public async Task<SystemSettingsResponse> UpdateSettingsAsync(
        UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        // An empty contact email is stored as NULL rather than "", so a cleared field reads
        // back as absent instead of as a blank address.
        var contactEmail = string.IsNullOrWhiteSpace(request.ContactEmail)
            ? null
            : request.ContactEmail.Trim();

        (string Key, string? Value, string Description)[] incoming =
        [
            (SchoolNameKey,
                request.SchoolName.Trim(),
                "Displayed in the portal header and reports"),
            (ContactEmailKey,
                contactEmail,
                "Public contact address"),
            (SelfRegistrationKey,
                request.AllowSelfRegistration ? "true" : "false",
                "When false only Admins can create accounts"),
            (PassMarkKey,
                request.PassMarkPercentage.ToString(CultureInfo.InvariantCulture),
                "Minimum weighted total to pass a subject"),
            (MaxUploadSizeKey,
                request.MaxUploadSizeMb.ToString(CultureInfo.InvariantCulture),
                "Maximum lesson/resource upload size in MB"),
            (AcademicYearKey,
                request.AcademicYear,
                "Active academic year")
        ];

        var keys = incoming.Select(i => i.Key).ToArray();
        var existing = await _db.SystemSettings
            .Where(s => keys.Contains(s.Key))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var (key, value, description) in incoming)
        {
            var row = existing.FirstOrDefault(s => s.Key == key);

            if (row is null)
            {
                _db.SystemSettings.Add(new SystemSetting
                {
                    Key = key,
                    Value = value,
                    Description = description,
                    UpdatedAt = now
                });

                continue;
            }

            // Leave UpdatedAt alone when nothing changed, so it reports the last real edit.
            if (row.Value == value)
            {
                continue;
            }

            row.Value = value;
            row.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(cancellationToken);
    }

    public async Task<string> GetAcademicYearAsync(CancellationToken cancellationToken = default) =>
        NormalizeAcademicYear(await GetAsync(AcademicYearKey, cancellationToken));

    public async Task<decimal> GetPassMarkPercentageAsync(CancellationToken cancellationToken = default) =>
        ParsePassMark(await GetAsync(PassMarkKey, cancellationToken));

    public async Task<long> GetMaxUploadBytesAsync(CancellationToken cancellationToken = default) =>
        ParseMaxUploadMb(await GetAsync(MaxUploadSizeKey, cancellationToken)) * 1024L * 1024L;

    public async Task<bool> IsSelfRegistrationAllowedAsync(CancellationToken cancellationToken = default) =>
        ParseSelfRegistration(await GetAsync(SelfRegistrationKey, cancellationToken));

    /// <summary>
    /// The database CHECK constraints only accept yyyy-yyyy, so never pass anything else on.
    /// </summary>
    private static string NormalizeAcademicYear(string? value) =>
        IsAcademicYear(value)
            ? value!
            : $"{DateTime.UtcNow.Year}-{DateTime.UtcNow.Year + 1}";

    /// <summary>
    /// Clamped to 0-100 because a value edited directly in SSMS is not covered by the
    /// Range validation on the request, and a pass mark above 100 could never be met.
    /// </summary>
    private static decimal ParsePassMark(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 0m, 100m)
            : DefaultPassMark;

    /// <summary>
    /// Also clamped, so a hand-edited row cannot advertise an upload limit that Kestrel
    /// would reject before the request reached the controller.
    /// </summary>
    private static int ParseMaxUploadMb(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? Math.Min(parsed, SystemSettingsLimits.MaxUploadCeilingMb)
            : DefaultMaxUploadMb;

    private static bool ParseSelfRegistration(string? value) =>
        bool.TryParse(value, out var allowed) && allowed;

    private static bool IsAcademicYear(string? value) =>
        value is { Length: 9 } &&
        value[4] == '-' &&
        value[..4].All(char.IsAsciiDigit) &&
        value[5..].All(char.IsAsciiDigit);
}
