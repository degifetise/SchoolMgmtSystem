using Microsoft.AspNetCore.Identity;

namespace HaladeHighSchool.Api.Models;

/// <summary>
/// Identity user for the portal. Maps to the AspNetUsers table created in Phase 1,
/// including the school specific columns appended to the default Identity schema.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public string? ProfileImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public Student? Student { get; set; }

    public Teacher? Teacher { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
