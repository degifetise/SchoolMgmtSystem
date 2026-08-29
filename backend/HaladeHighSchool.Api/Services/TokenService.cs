using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HaladeHighSchool.Api.Services;

public class TokenService : ITokenService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtSettings _jwt;

    public TokenService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IOptions<JwtSettings> jwtOptions)
    {
        _db = db;
        _userManager = userManager;
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

    public async Task<UserProfileResponse> BuildProfileAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.GradeLevel)
            .Include(s => s.Section)
            .FirstOrDefaultAsync(s => s.UserId == user.Id, cancellationToken);

        var teacher = await _db.Teachers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == user.Id, cancellationToken);

        return new UserProfileResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            ProfileImageUrl = user.ProfileImageUrl,
            Roles = roles.ToList(),
            StudentId = student?.Id,
            StudentIdNumber = student?.StudentIdNumber,
            GradeLevelId = student?.GradeLevelId,
            GradeLevelName = student?.GradeLevel?.Name,
            SectionId = student?.SectionId,
            SectionName = student?.Section?.Name,
            TeacherId = teacher?.Id,
            EmployeeId = teacher?.EmployeeId,
            Specialization = teacher?.Specialization
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
