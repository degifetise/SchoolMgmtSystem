using System.Security.Claims;
using HaladeHighSchool.Api.Configuration;

namespace HaladeHighSchool.Api.Services;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static int? GetStudentId(this ClaimsPrincipal principal)
        => ParseInt(principal.FindFirstValue(PortalClaims.StudentId));

    public static int? GetTeacherId(this ClaimsPrincipal principal)
        => ParseInt(principal.FindFirstValue(PortalClaims.TeacherId));

    public static bool IsAdmin(this ClaimsPrincipal principal)
        => principal.IsInRole(Roles.Admin);

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;
}
