namespace HaladeHighSchool.Api.Models;

/// <summary>
/// Keyless read model over the vw_StudentSubjectPerformance view. The weighted totals
/// and letter grade are computed by SQL Server from published marks only, so the report
/// card figures cannot drift from the weights held in the AssessmentTypes table.
/// </summary>
public class StudentSubjectPerformance
{
    public int StudentId { get; set; }

    public int SubjectId { get; set; }

    public decimal? QuizScore { get; set; }

    public decimal? AssignmentScore { get; set; }

    public decimal? TestScore { get; set; }

    public decimal? MidExamScore { get; set; }

    public decimal? FinalExamScore { get; set; }

    public decimal TotalScore { get; set; }

    public string LetterGrade { get; set; } = string.Empty;
}
