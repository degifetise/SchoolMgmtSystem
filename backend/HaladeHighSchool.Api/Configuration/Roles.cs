namespace HaladeHighSchool.Api.Configuration;

/// <summary>Role names seeded by the Phase 1 script.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public const string AdminOrTeacher = Admin + "," + Teacher;

    public static readonly string[] All = [Admin, Teacher, Student];
}

/// <summary>Custom JWT claim types used by the portal.</summary>
public static class PortalClaims
{
    /// <summary>Students.Id of the signed-in student, when the user is a student.</summary>
    public const string StudentId = "student_id";

    /// <summary>Teachers.Id of the signed-in teacher, when the user is a teacher.</summary>
    public const string TeacherId = "teacher_id";

    public const string GradeLevelId = "grade_level_id";

    public const string SectionId = "section_id";

    public const string FullName = "full_name";
}
