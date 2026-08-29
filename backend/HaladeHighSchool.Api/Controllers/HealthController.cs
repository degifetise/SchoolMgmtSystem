using System.Diagnostics;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// Liveness probes. Anonymous, because a probe that needs a token is useless to a load
/// balancer or an uptime monitor, so the payload is limited to facts that are safe to publish.
/// </summary>
[ApiController]
[Route("api/health")]
[AllowAnonymous]
[Produces("application/json")]
public class HealthController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISystemSettingsService _settings;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ApplicationDbContext db,
        ISystemSettingsService settings,
        ILogger<HealthController> logger)
    {
        _db = db;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Process liveness, with no database involved.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new { status = "Healthy", checkedAtUtc = DateTime.UtcNow });

    /// <summary>
    /// Database connectivity, the active academic year and a few integrity signals.
    /// Returns 200 when the database answers and 503 when it does not, so an uptime monitor
    /// can alert on the status code alone.
    /// </summary>
    [HttpGet("db-check")]
    [ProducesResponseType<DbHealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<DbHealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DbHealthResponse>> DbCheck(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // CanConnectAsync goes through the configured EnableRetryOnFailure strategy, so a
            // transient failure is retried here exactly as it would be for a real request.
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            if (!canConnect)
            {
                return ServiceUnavailable(new DbHealthResponse
                {
                    Status = "Unhealthy",
                    CanConnect = false,
                    Database = connection.Database,
                    ConnectionState = connection.State.ToString(),
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                    CheckedAtUtc = DateTime.UtcNow,
                    Error = "The database refused the connection."
                });
            }

            var integrity = await CheckIntegrityAsync(cancellationToken);
            var academicYear = await _settings.GetAcademicYearAsync(cancellationToken);

            var response = new DbHealthResponse
            {
                Status = integrity.SchemaReachable && integrity.GradingPolicyValid ? "Healthy" : "Degraded",
                CanConnect = true,
                Database = connection.Database,
                ConnectionState = connection.State.ToString(),
                AcademicYear = academicYear,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                CheckedAtUtc = DateTime.UtcNow,
                Integrity = integrity
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "The database health check failed.");

            return ServiceUnavailable(new DbHealthResponse
            {
                Status = "Unhealthy",
                CanConnect = false,
                Database = connection.Database,
                ConnectionState = connection.State.ToString(),
                LatencyMs = stopwatch.ElapsedMilliseconds,
                CheckedAtUtc = DateTime.UtcNow,
                // Type name only: the message can carry server and login detail.
                Error = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// Reads the smallest tables the portal depends on. A failure here means the connection
    /// works but the Phase 1 script has not been run against this database.
    /// </summary>
    private async Task<DbIntegritySummary> CheckIntegrityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var weights = await _db.AssessmentTypeWeights
                .AsNoTracking()
                .Select(w => w.WeightPercentage)
                .ToListAsync(cancellationToken);

            var settingsCount = await _db.SystemSettings.AsNoTracking().CountAsync(cancellationToken);
            var total = weights.Sum();

            bool viewAvailable;
            try
            {
                await _db.StudentSubjectPerformances.AsNoTracking().Take(1).ToListAsync(cancellationToken);
                viewAvailable = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "vw_StudentSubjectPerformance did not answer the health probe.");
                viewAvailable = false;
            }

            return new DbIntegritySummary
            {
                SchemaReachable = true,
                AssessmentTypeCount = weights.Count,
                GradingWeightTotal = total,
                GradingPolicyValid = total == 100m,
                ReportCardViewAvailable = viewAvailable,
                SettingsCount = settingsCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The schema integrity probe failed.");
            return new DbIntegritySummary { SchemaReachable = false };
        }
    }

    private ObjectResult ServiceUnavailable(DbHealthResponse response) =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, response);
}
