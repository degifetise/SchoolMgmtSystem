using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// Administrative management of student profiles: enrolment, grade and section
/// assignment, activation and password resets.
/// </summary>
[ApiController]
[Route("api/students")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class StudentsController : PortalControllerBase
{
    private const int MaxPageSize = 100;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountProvisioningService _provisioning;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAccountProvisioningService provisioning,
        ILogger<StudentsController> logger)
    {
        _db = db;
        _userManager = userManager;
        _provisioning = provisioning;
        _logger = logger;
    }

    /// <summary>Paged, filterable student roster.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<StudentListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StudentListItem>>> GetStudents(
        [FromQuery] int? gradeLevelId,
        [FromQuery] int? sectionId,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Students.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        if (gradeLevelId is int grade)
        {
            query = query.Where(s => s.GradeLevelId == grade);
        }

        if (sectionId is int section)
        {
            query = query.Where(s => s.SectionId == section);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s =>
                s.StudentIdNumber.Contains(term) ||
                (s.User != null && s.User.FullName.Contains(term)) ||
                (s.User != null && s.User.Email != null && s.User.Email.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.GradeLevel!.Level)
            .ThenBy(s => s.Section!.Code)
            .ThenBy(s => s.StudentIdNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentListItem
            {
                Id = s.Id,
                StudentIdNumber = s.StudentIdNumber,
                FullName = s.User != null ? s.User.FullName : string.Empty,
                Email = s.User != null ? s.User.Email : null,
                GradeLevelId = s.GradeLevelId,
                GradeLevelName = s.GradeLevel!.Name,
                SectionId = s.SectionId,
                SectionName = s.Section!.Name,
                Gender = s.Gender,
                IsActive = s.IsActive,
                HasLogin = s.UserId != null,
                EnrollmentDate = s.EnrollmentDate
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<StudentListItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    // Registration approval lives on api/admin/registration-requests: an applicant has no
    // Students row until it is approved, so there is nothing to review here.

    /// <summary>Full profile for one student.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<StudentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDetailResponse>> GetStudent(int id, CancellationToken cancellationToken)
    {
        var student = await ProjectDetail(_db.Students.AsNoTracking().Where(s => s.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

        return student is null ? NotFoundProblem($"Student {id} was not found.") : Ok(student);
    }

    /// <summary>
    /// Enrols a new student, creating the login and the profile together. When no password
    /// is supplied a temporary one is generated and returned exactly once.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CreateStudentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateStudentResponse>> CreateStudent(
        CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _provisioning.CreateStudentAsync(new ProvisionStudentRequest
        {
            Email = request.Email,
            Password = request.Password,
            FullName = request.FullName,
            GradeLevelId = request.GradeLevelId,
            SectionId = request.SectionId,
            StudentIdNumber = request.StudentIdNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            GuardianName = request.GuardianName,
            GuardianPhone = request.GuardianPhone,
            Address = request.Address
        }, cancellationToken);

        if (!result.Succeeded || result.Entity is null)
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Student not created",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["student"] = result.Errors.ToArray() }
            });
        }

        var detail = await ProjectDetail(_db.Students.AsNoTracking().Where(s => s.Id == result.Entity.Id))
            .FirstAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {Admin} enrolled student {StudentIdNumber}",
            User.GetUserId(), detail.StudentIdNumber);

        return CreatedAtAction(nameof(GetStudent), new { id = detail.Id }, new CreateStudentResponse
        {
            Student = detail,
            TemporaryPassword = result.TemporaryPassword
        });
    }

    /// <summary>Updates the student's personal and guardian details.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<StudentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDetailResponse>> UpdateStudent(
        int id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            return NotFoundProblem($"Student {id} was not found.");
        }

        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender;
        student.GuardianName = request.GuardianName;
        student.GuardianPhone = request.GuardianPhone;
        student.Address = request.Address;

        // The display name lives on the Identity user, not on the student row.
        if (student.User is not null)
        {
            student.User.FullName = request.FullName.Trim();
            student.User.ProfileImageUrl = request.ProfileImageUrl;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var detail = await ProjectDetail(_db.Students.AsNoTracking().Where(s => s.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(detail);
    }

    /// <summary>Moves a student to a different grade level and/or section.</summary>
    [HttpPut("{id:int}/assignment")]
    [ProducesResponseType<StudentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDetailResponse>> AssignClass(
        int id,
        AssignClassRequest request,
        CancellationToken cancellationToken)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            return NotFoundProblem($"Student {id} was not found.");
        }

        var gradeLevel = await _db.GradeLevels
            .FirstOrDefaultAsync(g => g.Id == request.GradeLevelId && g.IsActive, cancellationToken);

        if (gradeLevel is null)
        {
            ModelState.AddModelError(nameof(request.GradeLevelId), "Unknown or inactive grade level.");
            return ValidationProblem(ModelState);
        }

        var section = await _db.Sections
            .FirstOrDefaultAsync(s => s.Id == request.SectionId && s.IsActive, cancellationToken);

        if (section is null)
        {
            ModelState.AddModelError(nameof(request.SectionId), "Unknown or inactive section.");
            return ValidationProblem(ModelState);
        }

        var isMoving = student.GradeLevelId != request.GradeLevelId || student.SectionId != request.SectionId;

        if (isMoving)
        {
            var occupancy = await _db.Students.CountAsync(
                s => s.GradeLevelId == request.GradeLevelId
                     && s.SectionId == request.SectionId
                     && s.IsActive
                     && s.Id != id,
                cancellationToken);

            if (occupancy >= section.Capacity)
            {
                ModelState.AddModelError(nameof(request.SectionId),
                    $"{gradeLevel.Name} {section.Name} is full ({occupancy}/{section.Capacity}).");
                return ValidationProblem(ModelState);
            }
        }

        student.GradeLevelId = request.GradeLevelId;
        student.SectionId = request.SectionId;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Student {StudentIdNumber} assigned to {Grade} {Section}",
            student.StudentIdNumber, gradeLevel.Name, section.Name);

        var detail = await ProjectDetail(_db.Students.AsNoTracking().Where(s => s.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(detail);
    }

    /// <summary>
    /// Activates or deactivates a student. The login is disabled at the same time so a
    /// deactivated student cannot sign in, while all marks and report cards are retained.
    /// </summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType<StudentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDetailResponse>> SetStatus(
        int id,
        SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            return NotFoundProblem($"Student {id} was not found.");
        }

        student.IsActive = request.IsActive;

        if (student.User is not null)
        {
            student.User.IsActive = request.IsActive;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var detail = await ProjectDetail(_db.Students.AsNoTracking().Where(s => s.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(detail);
    }

    /// <summary>Issues a new temporary password for a student who cannot sign in.</summary>
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType<ResetPasswordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(
        int id,
        CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            return NotFoundProblem($"Student {id} was not found.");
        }

        if (student.UserId is null)
        {
            return BadRequestProblem(
                "No login",
                "This student has no linked account, so there is no password to reset.");
        }

        var user = await _userManager.FindByIdAsync(student.UserId);
        if (user is null)
        {
            return NotFoundProblem("The linked account no longer exists.");
        }

        var temporaryPassword = _provisioning.GenerateTemporaryPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Password not reset",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["password"] = result.Errors.Select(e => e.Description).ToArray() }
            });
        }

        _logger.LogInformation(
            "Admin {Admin} reset the password for student {StudentIdNumber}",
            User.GetUserId(), student.StudentIdNumber);

        return Ok(new ResetPasswordResponse { TemporaryPassword = temporaryPassword });
    }

    /// <summary>
    /// Removes a student record. Refused once marks exist, because deleting the row would
    /// cascade the academic history away; deactivate the student instead.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStudent(int id, CancellationToken cancellationToken)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            return NotFoundProblem($"Student {id} was not found.");
        }

        var marksCount = await _db.Marks.CountAsync(m => m.StudentId == id, cancellationToken);
        if (marksCount > 0)
        {
            return ConflictProblem(
                "Student has academic records",
                $"This student has {marksCount} recorded mark(s). Deactivate the student instead of deleting.");
        }

        var userId = student.UserId;

        _db.Students.Remove(student);
        await _db.SaveChangesAsync(cancellationToken);

        if (userId is not null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                await _userManager.DeleteAsync(user);
            }
        }

        _logger.LogWarning(
            "Admin {Admin} deleted student {StudentIdNumber}",
            User.GetUserId(), student.StudentIdNumber);

        return NoContent();
    }

    /// <summary>Roster counts per grade and section, for the admin dashboard.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<IEnumerable<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _db.Students
            .AsNoTracking()
            .Where(s => s.IsActive)
            .GroupBy(s => new { s.GradeLevelId, GradeLevelName = s.GradeLevel!.Name, s.SectionId, SectionName = s.Section!.Name })
            .Select(g => new
            {
                g.Key.GradeLevelId,
                g.Key.GradeLevelName,
                g.Key.SectionId,
                g.Key.SectionName,
                StudentCount = g.Count()
            })
            .OrderBy(g => g.GradeLevelId)
            .ThenBy(g => g.SectionId)
            .ToListAsync(cancellationToken);

        return Ok(summary);
    }

    /// <summary>Re-reads one student through the shared detail projection.</summary>
    private Task<StudentDetailResponse?> BuildDetailAsync(int id, CancellationToken cancellationToken) =>
        ProjectDetail(_db.Students.AsNoTracking().Where(s => s.Id == id))
            .FirstOrDefaultAsync(cancellationToken)!;

    private static IQueryable<StudentDetailResponse> ProjectDetail(IQueryable<Student> query) =>
        query.Select(s => new StudentDetailResponse
        {
            Id = s.Id,
            StudentIdNumber = s.StudentIdNumber,
            FullName = s.User != null ? s.User.FullName : string.Empty,
            Email = s.User != null ? s.User.Email : null,
            GradeLevelId = s.GradeLevelId,
            GradeLevelName = s.GradeLevel!.Name,
            SectionId = s.SectionId,
            SectionName = s.Section!.Name,
            Gender = s.Gender,
            IsActive = s.IsActive,
            HasLogin = s.UserId != null,
            EnrollmentDate = s.EnrollmentDate,
            UserId = s.UserId,
            DateOfBirth = s.DateOfBirth,
            GuardianName = s.GuardianName,
            GuardianPhone = s.GuardianPhone,
            Address = s.Address,
            ProfileImageUrl = s.User != null ? s.User.ProfileImageUrl : null,
            CreatedAt = s.CreatedAt,
            MarksCount = s.Marks.Count
        });
}
