namespace HaladeHighSchool.Api.Models;

/// <summary>
/// One line of a teacher's timetable: this teacher teaches this subject to this section.
/// Authorisation for mark entry and lesson upload is derived from these rows.
/// </summary>
public class TeacherSubject
{
    public int Id { get; set; }

    public int TeacherId { get; set; }

    public int SubjectId { get; set; }

    public int SectionId { get; set; }

    public string AcademicYear { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Teacher? Teacher { get; set; }

    public Subject? Subject { get; set; }

    public Section? Section { get; set; }
}
