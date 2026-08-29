using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.Configuration;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HMAC-SHA256 signing key. Must be at least 32 bytes; keep the real value in
    /// user-secrets or an environment variable rather than appsettings.json.
    /// </summary>
    [Required, MinLength(32)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int AccessTokenMinutes { get; set; } = 120;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}
