using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

public interface IReportCardService
{
    /// <summary>
    /// One student's weighted results per subject. Null when no such student exists.
    /// Used by the staff-facing report card endpoint.
    /// </summary>
    Task<ReportCardResponse?> BuildAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same figures plus the headline summary and the weighting they were derived from, so
    /// a student's results screen is one request rather than three.
    /// </summary>
    Task<MyResultsResponse?> BuildMyResultsAsync(int studentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Composes report cards from vw_StudentSubjectPerformance. Every weighted total and letter
/// grade is computed by SQL Server from published marks only, so this service never recalculates
/// a grade - it groups and summarises what the view returns.
/// </summary>
public class ReportCardService : IReportCardService
{
    private readonly ApplicationDbContext _db;
    private readonly ISystemSettingsService _settings;
    private readonly IGradingPolicyService _grading;

    public ReportCardService(
        ApplicationDbContext db,
        ISystemSettingsService settings,
        IGradingPolicyService grading)
    {
        _db = db;
        _settings = settings;
        _grading = grading;
    }

    public async Task<ReportCardResponse?> BuildAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.GradeLevel)
            .Include(s => s.Section)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student is null)
        {
            return null;
        }

        var rows = await (
            from performance in _db.StudentSubjectPerformances
            join subject in _db.Subjects on performance.SubjectId equals subject.Id
            where performance.StudentId == studentId
            orderby subject.Code
            select new
            {
                performance.SubjectId,
                subject.SubjectName,
                subject.Code,
                performance.QuizScore,
                performance.AssignmentScore,
                performance.TestScore,
                performance.MidExamScore,
                performance.FinalExamScore,
                performance.TotalScore,
                performance.LetterGrade,
            }).ToListAsync(cancellationToken);

        var passMark = await _settings.GetPassMarkPercentageAsync(cancellationToken);
        var academicYear = await _settings.GetAcademicYearAsync(cancellationToken);

        var subjects = rows.Select(r => new SubjectReportCard
        {
            SubjectId = r.SubjectId,
            SubjectName = r.SubjectName,
            SubjectCode = r.Code,
            QuizScore = r.QuizScore,
            AssignmentScore = r.AssignmentScore,
            TestScore = r.TestScore,
            MidExamScore = r.MidExamScore,
            FinalExamScore = r.FinalExamScore,
            TotalScore = r.TotalScore,
            LetterGrade = r.LetterGrade,
            IsPass = r.TotalScore >= passMark,
        }).ToList();

        return new ReportCardResponse
        {
            StudentId = student.Id,
            StudentIdNumber = student.StudentIdNumber,
            StudentName = student.User?.FullName ?? student.StudentIdNumber,
            GradeLevelName = student.GradeLevel?.Name ?? string.Empty,
            SectionName = student.Section?.Name ?? string.Empty,
            AcademicYear = academicYear,
            PassMarkPercentage = passMark,
            AverageTotal = subjects.Count == 0
                ? null
                : Math.Round(subjects.Average(s => s.TotalScore), 2),
            Subjects = subjects,
        };
    }

    public async Task<MyResultsResponse?> BuildMyResultsAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var card = await BuildAsync(studentId, cancellationToken);

        if (card is null)
        {
            return null;
        }

        var weights = await _grading.GetActiveAsync(cancellationToken);

        /* A subject only appears in the view once it has a published mark, so "marked" is the
           count of components that carry one rather than the number of subjects. */
        var strongest = card.Subjects.OrderByDescending(s => s.TotalScore).FirstOrDefault();
        var weakest = card.Subjects.OrderBy(s => s.TotalScore).FirstOrDefault();

        var summary = new ResultsSummary
        {
            SubjectCount = card.Subjects.Count,
            SubjectsPassed = card.Subjects.Count(s => s.IsPass),
            WeightedAverage = card.AverageTotal,
            ComponentsMarked = card.Subjects.Sum(s => new[]
            {
                s.QuizScore, s.AssignmentScore, s.TestScore, s.MidExamScore, s.FinalExamScore,
            }.Count(score => score is not null)),
            StrongestSubject = strongest?.SubjectName,
            StrongestSubjectTotal = strongest?.TotalScore,
            WeakestSubject = card.Subjects.Count > 1 ? weakest?.SubjectName : null,
            WeakestSubjectTotal = card.Subjects.Count > 1 ? weakest?.TotalScore : null,
        };

        return new MyResultsResponse
        {
            StudentId = card.StudentId,
            StudentIdNumber = card.StudentIdNumber,
            StudentName = card.StudentName,
            GradeLevelName = card.GradeLevelName,
            SectionName = card.SectionName,
            AcademicYear = card.AcademicYear,
            PassMarkPercentage = card.PassMarkPercentage,
            Subjects = card.Subjects,
            Summary = summary,
            GradingWeights = weights,
        };
    }
}
