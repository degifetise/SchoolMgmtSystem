using HaladeHighSchool.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

/// <summary>
/// Single source of truth for "may this teacher act on this class?". Marks, lessons,
/// assessments and class announcements all authorise against the same TeacherSubjects rows.
/// </summary>
public interface ITeachingAssignmentService
{
    /// <summary>
    /// True when the teacher is actively assigned to the subject. A null
    /// <paramref name="sectionId"/> means the target covers every section, so any active
    /// assignment for the subject is enough.
    /// </summary>
    Task<bool> IsAssignedAsync(
        int teacherId,
        int subjectId,
        int? sectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Subject ids the teacher currently teaches.</summary>
    Task<List<int>> GetTaughtSubjectIdsAsync(int teacherId, CancellationToken cancellationToken = default);
}

public class TeachingAssignmentService : ITeachingAssignmentService
{
    private readonly ApplicationDbContext _db;

    public TeachingAssignmentService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> IsAssignedAsync(
        int teacherId,
        int subjectId,
        int? sectionId,
        CancellationToken cancellationToken = default) =>
        _db.TeacherSubjects
            .AsNoTracking()
            .AnyAsync(ts =>
                    ts.TeacherId == teacherId &&
                    ts.SubjectId == subjectId &&
                    ts.IsActive &&
                    (sectionId == null || ts.SectionId == sectionId),
                cancellationToken);

    public Task<List<int>> GetTaughtSubjectIdsAsync(
        int teacherId,
        CancellationToken cancellationToken = default) =>
        _db.TeacherSubjects
            .AsNoTracking()
            .Where(ts => ts.TeacherId == teacherId && ts.IsActive)
            .Select(ts => ts.SubjectId)
            .Distinct()
            .ToListAsync(cancellationToken);
}
