namespace HaladeHighSchool.Api.Models;

/// <summary>
/// Mirrors CK_StudentRegistrationRequests_Status. Strings rather than an enum so the stored
/// value reads plainly in a query window.
/// </summary>
public static class RegistrationRequestStatus
{
    /// <summary>Submitted and waiting for an administrator.</summary>
    public const string Pending = "Pending";

    /// <summary>Approved: a login and a student record now exist.</summary>
    public const string Approved = "Approved";

    /// <summary>Turned down. No login was ever created.</summary>
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Pending, Approved, Rejected];
}

/// <summary>
/// An application to join the school. Deliberately not a user and not a student: nothing is
/// provisioned until an administrator approves, so a rejected application leaves no account
/// behind and an applicant never chooses their own credentials.
/// </summary>
public class StudentRegistrationRequest
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// The applicant's own address, used to reach them with the issued credentials. Not the
    /// sign-in address, which is generated from the student number at approval.
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;

    public int GradeLevelId { get; set; }

    public int SectionId { get; set; }

    /// <summary>One of <see cref="RegistrationRequestStatus"/>.</summary>
    public string Status { get; set; } = RegistrationRequestStatus.Pending;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    /// <summary>The administrator who decided. Not a foreign key - see the DDL.</summary>
    public string? ReviewedByUserId { get; set; }

    /// <summary>Why it was turned down, or any note kept against an approval.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>The student created by an approval. Null once that student is deleted.</summary>
    public int? CreatedStudentId { get; set; }

    /// <summary>The sign-in address issued on approval, kept so it can be quoted later.</summary>
    public string? IssuedEmail { get; set; }

    public GradeLevel? GradeLevel { get; set; }

    public Section? Section { get; set; }

    public Student? CreatedStudent { get; set; }
}
