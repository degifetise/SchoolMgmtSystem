using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

/// <summary>
/// Outcome of a registration operation, with the reason a caller should translate into a status
/// code. Keeping the failure kind here means the controller decides HTTP shape and this service
/// decides the rules.
/// </summary>
public enum RegistrationFailure
{
    None = 0,

    /// <summary>The grade, section or request does not exist.</summary>
    NotFound,

    /// <summary>Submitted details are unusable: inactive class, duplicate application.</summary>
    Invalid,

    /// <summary>The request has already been decided, or the class filled up meanwhile.</summary>
    Conflict,
}

public record RegistrationResult<T> where T : class
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public RegistrationFailure Failure { get; init; }
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static RegistrationResult<T> Ok(T value) => new() { Succeeded = true, Value = value };

    public static RegistrationResult<T> Fail(RegistrationFailure failure, string title, params string[] errors) =>
        new() { Succeeded = false, Failure = failure, Title = title, Errors = errors };
}

public interface IRegistrationRequestService
{
    /// <summary>Records an application. Creates no login and no student record.</summary>
    Task<RegistrationResult<StudentRegistrationRequest>> SubmitAsync(
        RegisterStudentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Applications in one review state, oldest first.</summary>
    Task<IReadOnlyList<RegistrationRequestResponse>> ListAsync(
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues credentials and provisions the account. The returned temporary password exists
    /// only in this response.
    /// </summary>
    Task<RegistrationResult<ApprovedRegistrationResponse>> ApproveAsync(
        int requestId,
        string? note,
        string reviewerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Turns an application down. Nothing is provisioned.</summary>
    Task<RegistrationResult<RegistrationRequestResponse>> RejectAsync(
        int requestId,
        string? note,
        string reviewerUserId,
        CancellationToken cancellationToken = default);
}

public class RegistrationRequestService : IRegistrationRequestService
{
    private readonly ApplicationDbContext _db;
    private readonly IAccountProvisioningService _provisioning;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegistrationRequestService> _logger;

    public RegistrationRequestService(
        ApplicationDbContext db,
        IAccountProvisioningService provisioning,
        UserManager<ApplicationUser> userManager,
        ILogger<RegistrationRequestService> logger)
    {
        _db = db;
        _provisioning = provisioning;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<RegistrationResult<StudentRegistrationRequest>> SubmitAsync(
        RegisterStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var contactEmail = request.Email.Trim();

        var gradeLevel = await _db.GradeLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == request.GradeLevelId, cancellationToken);

        if (gradeLevel is null || !gradeLevel.IsActive)
        {
            return RegistrationResult<StudentRegistrationRequest>.Fail(
                RegistrationFailure.Invalid,
                "Unknown grade",
                "That grade level does not exist or is not taking students.");
        }

        var section = await _db.Sections
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, cancellationToken);

        if (section is null || !section.IsActive)
        {
            return RegistrationResult<StudentRegistrationRequest>.Fail(
                RegistrationFailure.Invalid,
                "Unknown section",
                "That section does not exist or is not taking students.");
        }

        // A second application while one is outstanding is a duplicate, not a new applicant.
        var alreadyPending = await _db.StudentRegistrationRequests.AnyAsync(
            r => r.ContactEmail == contactEmail && r.Status == RegistrationRequestStatus.Pending,
            cancellationToken);

        if (alreadyPending)
        {
            return RegistrationResult<StudentRegistrationRequest>.Fail(
                RegistrationFailure.Conflict,
                "Already applied",
                "A registration for this email address is already waiting to be reviewed.");
        }

        /* An address that already signs in is not an applicant. Reported the same way as a
           duplicate application so this endpoint cannot be used to test which addresses exist. */
        if (await _userManager.FindByEmailAsync(contactEmail) is not null)
        {
            return RegistrationResult<StudentRegistrationRequest>.Fail(
                RegistrationFailure.Conflict,
                "Already applied",
                "A registration for this email address is already waiting to be reviewed.");
        }

        var entity = new StudentRegistrationRequest
        {
            FullName = request.FullName.Trim(),
            ContactEmail = contactEmail,
            GradeLevelId = request.GradeLevelId,
            SectionId = request.SectionId,
            Status = RegistrationRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow,
        };

        _db.StudentRegistrationRequests.Add(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The filtered unique index is the real guard; two simultaneous submissions land here.
            _logger.LogWarning(ex, "Duplicate registration request for {ContactEmail}", contactEmail);
            return RegistrationResult<StudentRegistrationRequest>.Fail(
                RegistrationFailure.Conflict,
                "Already applied",
                "A registration for this email address is already waiting to be reviewed.");
        }

        entity.GradeLevel = gradeLevel;
        entity.Section = section;

        _logger.LogInformation(
            "Registration request {RequestId} submitted for {GradeLevel} {Section}",
            entity.Id,
            gradeLevel.Name,
            section.Name);

        return RegistrationResult<StudentRegistrationRequest>.Ok(entity);
    }

    public async Task<IReadOnlyList<RegistrationRequestResponse>> ListAsync(
        string status,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.StudentRegistrationRequests
            .AsNoTracking()
            .Where(r => r.Status == status)
            .OrderBy(r => r.SubmittedAt)
            .Select(r => new
            {
                r.Id,
                r.FullName,
                r.ContactEmail,
                r.GradeLevelId,
                GradeLevelName = r.GradeLevel!.Name,
                r.SectionId,
                SectionName = r.Section!.Name,
                r.Section!.Capacity,
                r.Status,
                r.SubmittedAt,
                r.ReviewedAt,
                r.ReviewedByUserId,
                r.ReviewNote,
                r.CreatedStudentId,
                r.IssuedEmail,
                StudentIdNumber = r.CreatedStudent != null ? r.CreatedStudent.StudentIdNumber : null,
                // Enrolled students only: what the reviewer needs is the seats actually taken.
                Occupancy = _db.Students.Count(s =>
                    s.SectionId == r.SectionId && s.GradeLevelId == r.GradeLevelId && s.IsActive),
            })
            .ToListAsync(cancellationToken);

        // Resolved separately because ReviewedByUserId deliberately carries no foreign key.
        var reviewerIds = rows.Select(r => r.ReviewedByUserId).Where(id => id != null).Distinct().ToList();
        var reviewers = reviewerIds.Count == 0
            ? []
            : await _db.Users
                .AsNoTracking()
                .Where(u => reviewerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return rows
            .Select(r => new RegistrationRequestResponse
            {
                Id = r.Id,
                FullName = r.FullName,
                ContactEmail = r.ContactEmail,
                GradeLevelId = r.GradeLevelId,
                GradeLevelName = r.GradeLevelName,
                SectionId = r.SectionId,
                SectionName = r.SectionName,
                Status = r.Status,
                SubmittedAt = r.SubmittedAt,
                SectionCapacity = r.Capacity,
                SectionOccupancy = r.Occupancy,
                ReviewedAt = r.ReviewedAt,
                ReviewedByName = r.ReviewedByUserId is not null && reviewers.TryGetValue(r.ReviewedByUserId, out var name)
                    ? name
                    : null,
                ReviewNote = r.ReviewNote,
                CreatedStudentId = r.CreatedStudentId,
                IssuedEmail = r.IssuedEmail,
                StudentIdNumber = r.StudentIdNumber,
            })
            .ToList();
    }

    public async Task<RegistrationResult<ApprovedRegistrationResponse>> ApproveAsync(
        int requestId,
        string? note,
        string reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.StudentRegistrationRequests
            .Include(r => r.GradeLevel)
            .Include(r => r.Section)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            return RegistrationResult<ApprovedRegistrationResponse>.Fail(
                RegistrationFailure.NotFound,
                "Request not found",
                $"No registration request with id {requestId} exists.");
        }

        if (request.Status != RegistrationRequestStatus.Pending)
        {
            return RegistrationResult<ApprovedRegistrationResponse>.Fail(
                RegistrationFailure.Conflict,
                "Already decided",
                $"This request was already {request.Status.ToLowerInvariant()}.");
        }

        /* Checked before provisioning as well as inside it: the class may have filled up while
           the application sat in the queue, and a clear message beats a provisioning failure. */
        var occupancy = await _db.Students.CountAsync(
            s => s.SectionId == request.SectionId && s.GradeLevelId == request.GradeLevelId && s.IsActive,
            cancellationToken);

        if (occupancy >= request.Section!.Capacity)
        {
            return RegistrationResult<ApprovedRegistrationResponse>.Fail(
                RegistrationFailure.Conflict,
                "Class is full",
                $"{request.GradeLevel!.Name} {request.Section.Name} is full "
              + $"({occupancy}/{request.Section.Capacity}). Free a seat or move the applicant first.");
        }

        /* Email and password are both left to the provisioning service: it generates the student
           number, derives the school address from it and produces a password that satisfies the
           configured Identity policy, all in one transaction. */
        var provisioned = await _provisioning.CreateStudentAsync(
            new ProvisionStudentRequest
            {
                FullName = request.FullName,
                GradeLevelId = request.GradeLevelId,
                SectionId = request.SectionId,
            },
            cancellationToken);

        if (!provisioned.Succeeded || provisioned.Entity is null)
        {
            return RegistrationResult<ApprovedRegistrationResponse>.Fail(
                RegistrationFailure.Invalid,
                "Could not create the account",
                [.. provisioned.Errors]);
        }

        var student = provisioned.Entity;
        var issuedEmail = await _db.Users
            .Where(u => u.Id == student.UserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        request.Status = RegistrationRequestStatus.Approved;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewNote = Trim(note);
        request.CreatedStudentId = student.Id;
        request.IssuedEmail = issuedEmail;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Registration request {RequestId} approved as student {StudentIdNumber} by {Reviewer}",
            request.Id,
            student.StudentIdNumber,
            reviewerUserId);

        return RegistrationResult<ApprovedRegistrationResponse>.Ok(new ApprovedRegistrationResponse
        {
            RequestId = request.Id,
            StudentId = student.Id,
            FullName = request.FullName,
            StudentIdNumber = student.StudentIdNumber,
            IssuedEmail = issuedEmail,
            ContactEmail = request.ContactEmail,
            // Always set here: the provisioning call above never supplies a password of its own.
            TemporaryPassword = provisioned.TemporaryPassword ?? string.Empty,
            GradeLevelName = request.GradeLevel!.Name,
            SectionName = request.Section!.Name,
            ApprovedAt = request.ReviewedAt!.Value,
            Message =
                $"Send these to {request.ContactEmail}. The temporary password is shown once and "
              + "cannot be retrieved again; the student should change it after signing in.",
        });
    }

    public async Task<RegistrationResult<RegistrationRequestResponse>> RejectAsync(
        int requestId,
        string? note,
        string reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.StudentRegistrationRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            return RegistrationResult<RegistrationRequestResponse>.Fail(
                RegistrationFailure.NotFound,
                "Request not found",
                $"No registration request with id {requestId} exists.");
        }

        if (request.Status != RegistrationRequestStatus.Pending)
        {
            return RegistrationResult<RegistrationRequestResponse>.Fail(
                RegistrationFailure.Conflict,
                "Already decided",
                $"This request was already {request.Status.ToLowerInvariant()}.");
        }

        request.Status = RegistrationRequestStatus.Rejected;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewNote = Trim(note);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Registration request {RequestId} rejected by {Reviewer}",
            request.Id,
            reviewerUserId);

        var rejected = await ListAsync(RegistrationRequestStatus.Rejected, cancellationToken);

        return RegistrationResult<RegistrationRequestResponse>.Ok(
            rejected.First(r => r.Id == request.Id));
    }

    private static string? Trim(string? note) =>
        note?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}
