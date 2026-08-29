using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// Site-wide configuration held in the SystemSettings table. These values gate real
/// behaviour elsewhere in the API - self registration in AuthController, the upload ceiling
/// in LessonsController, and the academic year and pass mark used by marks and report cards -
/// so the whole controller is restricted to administrators.
/// </summary>
[ApiController]
[Route("api/system-settings")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class SystemSettingsController : PortalControllerBase
{
    private readonly ISystemSettingsService _settings;
    private readonly ILogger<SystemSettingsController> _logger;

    public SystemSettingsController(
        ISystemSettingsService settings,
        ILogger<SystemSettingsController> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// The school's public identity. Deliberately anonymous so the login screen and the
    /// dashboard header can show the school name before a token exists. Only non-sensitive
    /// values are returned; the pass mark and upload limit stay behind the admin endpoint.
    /// </summary>
    [HttpGet("school-info")]
    [AllowAnonymous]
    [ProducesResponseType<SchoolInfoResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolInfoResponse>> GetSchoolInfo(CancellationToken cancellationToken)
    {
        return Ok(await _settings.GetSchoolInfoAsync(cancellationToken));
    }

    /// <summary>All current system settings.</summary>
    [HttpGet]
    [ProducesResponseType<SystemSettingsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemSettingsResponse>> GetSettings(
        CancellationToken cancellationToken)
    {
        return Ok(await _settings.GetSettingsAsync(cancellationToken));
    }

    /// <summary>
    /// Updates the writable settings and returns the stored result. Changes apply to the next
    /// request without a restart, because settings are read from the database each time.
    /// </summary>
    [HttpPut]
    [ProducesResponseType<SystemSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SystemSettingsResponse>> UpdateSettings(
        UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _settings.UpdateSettingsAsync(request, cancellationToken);

        _logger.LogInformation(
            "System settings updated by {User}: school '{SchoolName}', academic year {AcademicYear}, " +
            "pass mark {PassMark}, upload limit {UploadMb} MB, self registration {SelfRegistration}",
            User.Identity?.Name,
            updated.SchoolName,
            updated.AcademicYear,
            updated.PassMarkPercentage,
            updated.MaxUploadSizeMb,
            updated.AllowSelfRegistration);

        return Ok(updated);
    }
}
