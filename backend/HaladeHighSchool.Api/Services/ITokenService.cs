using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;

namespace HaladeHighSchool.Api.Services;

public interface ITokenService
{
    /// <summary>Issues an access token plus a persisted refresh token for the user.</summary>
    Task<AuthResponse> CreateAuthResponseAsync(ApplicationUser user, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a valid refresh token for a new token pair, revoking the old one.
    /// Returns null when the token is unknown, expired or already revoked.
    /// </summary>
    Task<AuthResponse?> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Revokes a single refresh token. Returns false when it was not active.</summary>
    Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Builds the profile payload the frontend stores after login.</summary>
    Task<UserProfileResponse> BuildProfileAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
