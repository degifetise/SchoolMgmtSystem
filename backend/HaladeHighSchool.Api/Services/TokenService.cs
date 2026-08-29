using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HaladeHighSchool.Api.Services;

public class TokenService : ITokenService
{
    private readonly ApplicationDbContext _db;
    private readonly JwtSettings _jwt;

    public TokenService(ApplicationDbContext db, IOptions<JwtSettings> jwtOptions)
    {
        _db = db;
        _jwt = jwtOptions.Value;
    }

    public async Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var profile = await BuildProfileAsync(user, cancellationToken);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var accessToken = CreateAccessToken(user, profile, expiresAt);
        var refreshToken = await IssueRefreshTokenAsync(user.Id, ipAddress, cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = expiresAt,
            User = profile
        };
    }

    public async Task<AuthResponse?> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (stored?.User is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        if (!stored.User.IsActive)
        {
            return null;
        }

        var replacement = await IssueRefreshTokenAsync(stored.UserId, ipAddress, cancellationToken, saveChanges: false);

        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByToken = replacement.Token;
        await _db.SaveChangesAsync(cancellationToken);

        var profile = await BuildProfileAsync(stored.User, cancellationToken);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

        return new AuthResponse
        {
            AccessToken = CreateAccessToken(stored.User, profile, expiresAt),
            RefreshToken = replacement.Token,
            ExpiresAt = expiresAt,
            User = profile
        };
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return false;
        }

        stored.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The roles, the student record and the teacher record in one round trip.
    ///
    /// A user is a student or a teacher or neither, and both sides are one-to-one with the
    /// account, so this is a pair of outer joins rather than three separate lookups. Sign-in and
    /// token refresh are the only callers, and both were paying for all three.
    /// </summary>
    public async Task<UserProfileResponse> BuildProfileAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == user.Id)
            .Select(u => new
            {
                Roles = (from userRole in _db.UserRoles
                         join role in _db.Roles on userRole.RoleId equals role.Id
                         where userRole.UserId == u.Id
                         select role.Name!).ToList(),
                StudentId = (int?)u.Student!.Id,
                u.Student!.StudentIdNumber,
                GradeLevelId = (int?)u.Student!.GradeLevelId,
                GradeLevelName = u.Student!.GradeLevel!.Name,
                SectionId = (int?)u.Student!.SectionId,
                SectionName = u.Student!.Section!.Name,
                TeacherId = (int?)u.Teacher!.Id,
                u.Teacher!.EmployeeId,
                u.Teacher!.Specialization
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new UserProfileResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            ProfileImageUrl = user.ProfileImageUrl,
            Roles = profile?.Roles ?? [],
            StudentId = profile?.StudentId,
            StudentIdNumber = profile?.StudentIdNumber,
            GradeLevelId = profile?.GradeLevelId,
            GradeLevelName = profile?.GradeLevelName,
            SectionId = profile?.SectionId,
            SectionName = profile?.SectionName,
            TeacherId = profile?.TeacherId,
            EmployeeId = profile?.EmployeeId,
            Specialization = profile?.Specialization
        };
    }

    private string CreateAccessToken(ApplicationUser user, UserProfileResponse profile, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(PortalClaims.FullName, user.FullName)
        };

        claims.AddRange(profile.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Carrying the domain ids avoids a database round-trip on every student or
        // teacher scoped request.
        if (profile.StudentId is int studentId)
        {
            claims.Add(new Claim(PortalClaims.StudentId, studentId.ToString()));
            claims.Add(new Claim(PortalClaims.GradeLevelId, profile.GradeLevelId!.Value.ToString()));
            claims.Add(new Claim(PortalClaims.SectionId, profile.SectionId!.Value.ToString()));
        }

        if (profile.TeacherId is int teacherId)
        {
            claims.Add(new Claim(PortalClaims.TeacherId, teacherId.ToString()));
        }

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwt.Key));
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<RefreshToken> IssueRefreshTokenAsync(
        string userId,
        string? ipAddress,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            Token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64)),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreatedByIp = ipAddress
        };

        _db.RefreshTokens.Add(token);

        if (saveChanges)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return token;
    }
}
