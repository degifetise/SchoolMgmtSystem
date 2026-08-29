namespace HaladeHighSchool.Api.Models;

/// <summary>
/// The five assessment categories. Persisted as text so the values match the
/// AssessmentTypes lookup table that the Assessments foreign key points at.
/// Weights total 100%: Quiz 10, Assignment 10, Test 20, MidExam 30, FinalExam 30.
/// </summary>
public enum AssessmentType
{
    Quiz,
    Assignment,
    Test,
    MidExam,
    FinalExam
}

/// <summary>
/// Read model over the AssessmentTypes lookup table. The weights live in the
/// database so the report card view and the API always agree.
/// </summary>
public class AssessmentTypeWeight
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public decimal WeightPercentage { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
