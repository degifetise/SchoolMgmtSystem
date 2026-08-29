using HaladeHighSchool.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Data;

/// <summary>
/// The schema is owned by the Phase 1 T-SQL script, so this context is configured to
/// match that database exactly rather than to generate it. Delete behaviours mirror the
/// foreign keys defined in SQL, which keeps EF's in-memory fix-up consistent with what
/// the server will actually do.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentTypeWeight> AssessmentTypeWeights => Set<AssessmentTypeWeight>();
    public DbSet<Mark> Marks => Set<Mark>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<StudentRegistrationRequest> StudentRegistrationRequests => Set<StudentRegistrationRequest>();
    public DbSet<PasswordChangeLog> PasswordChangeLogs => Set<PasswordChangeLog>();

    /// <summary>Weighted report card rows produced by vw_StudentSubjectPerformance.</summary>
    public DbSet<StudentSubjectPerformance> StudentSubjectPerformances => Set<StudentSubjectPerformance>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        ConfigureAcademicStructure(builder);
        ConfigurePeople(builder);
        ConfigureContent(builder);
        ConfigureAssessment(builder);
        ConfigureCommunication(builder);
        ConfigureViews(builder);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            entity.Property(u => u.ProfileImageUrl).HasMaxLength(500);
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(u => u.IsActive).HasDatabaseName("IX_AspNetUsers_IsActive");
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.Property(t => t.Token).HasMaxLength(256).IsRequired();
            entity.Property(t => t.ReplacedByToken).HasMaxLength(256);
            entity.Property(t => t.CreatedByIp).HasMaxLength(45);
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Ignore(t => t.IsActive);
            entity.HasIndex(t => t.Token).IsUnique().HasDatabaseName("UQ_RefreshTokens_Token");

            entity.HasOne(t => t.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAcademicStructure(ModelBuilder builder)
    {
        builder.Entity<GradeLevel>(entity =>
        {
            entity.ToTable("GradeLevels");
            entity.Property(g => g.Name).HasMaxLength(50).IsRequired();
            entity.Property(g => g.Description).HasMaxLength(250);
            entity.Property(g => g.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(g => g.Name).IsUnique().HasDatabaseName("UQ_GradeLevels_Name");
            entity.HasIndex(g => g.Level).IsUnique().HasDatabaseName("UQ_GradeLevels_Level");
        });

        builder.Entity<Section>(entity =>
        {
            entity.ToTable("Sections");
            entity.Property(s => s.Name).HasMaxLength(50).IsRequired();
            entity.Property(s => s.Code).HasMaxLength(10).IsRequired();
            entity.Property(s => s.Capacity).HasDefaultValue(40);
            entity.Property(s => s.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(s => s.Name).IsUnique().HasDatabaseName("UQ_Sections_Name");
            entity.HasIndex(s => s.Code).IsUnique().HasDatabaseName("UQ_Sections_Code");
        });

        builder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subjects");
            entity.Property(s => s.SubjectName).HasMaxLength(150).IsRequired();
            entity.Property(s => s.Code).HasMaxLength(20).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(500);
            entity.Property(s => s.CreditHours).HasDefaultValue(3);
            entity.Property(s => s.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(s => s.Code).IsUnique().HasDatabaseName("UQ_Subjects_Code");
            entity.HasIndex(s => new { s.SubjectName, s.GradeLevelId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Subjects_Name_Grade");

            entity.HasOne(s => s.GradeLevel)
                  .WithMany(g => g.Subjects)
                  .HasForeignKey(s => s.GradeLevelId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePeople(ModelBuilder builder)
    {
        builder.Entity<Teacher>(entity =>
        {
            entity.ToTable("Teachers");
            entity.Property(t => t.EmployeeId).HasMaxLength(30).IsRequired();
            entity.Property(t => t.Specialization).HasMaxLength(150);
            entity.Property(t => t.Qualification).HasMaxLength(150);
            entity.Property(t => t.PhoneNumber).HasMaxLength(30);
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(t => t.EmployeeId).IsUnique().HasDatabaseName("UQ_Teachers_EmployeeId");

            // Filtered unique index: at most one teacher per login, many rows without a login.
            entity.HasIndex(t => t.UserId)
                  .IsUnique()
                  .HasFilter("[UserId] IS NOT NULL")
                  .HasDatabaseName("UQ_Teachers_UserId");

            entity.HasOne(t => t.User)
                  .WithOne(u => u.Teacher)
                  .HasForeignKey<Teacher>(t => t.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.Property(s => s.StudentIdNumber).HasMaxLength(30).IsRequired();
            entity.Property(s => s.Gender).HasMaxLength(10);
            entity.Property(s => s.GuardianName).HasMaxLength(150);
            entity.Property(s => s.GuardianPhone).HasMaxLength(30);
            entity.Property(s => s.Address).HasMaxLength(250);
            entity.Property(s => s.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(s => s.StudentIdNumber).IsUnique().HasDatabaseName("UQ_Students_StudentIdNumber");

            entity.HasIndex(s => s.UserId)
                  .IsUnique()
                  .HasFilter("[UserId] IS NOT NULL")
                  .HasDatabaseName("UQ_Students_UserId");

            entity.HasOne(s => s.GradeLevel)
                  .WithMany(g => g.Students)
                  .HasForeignKey(s => s.GradeLevelId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Section)
                  .WithMany(sec => sec.Students)
                  .HasForeignKey(s => s.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.User)
                  .WithOne(u => u.Student)
                  .HasForeignKey<Student>(s => s.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StudentRegistrationRequest>(entity =>
        {
            entity.ToTable("StudentRegistrationRequests");
            entity.Property(r => r.FullName).HasMaxLength(150).IsRequired();
            entity.Property(r => r.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(r => r.Status).HasMaxLength(20).IsRequired();
            entity.Property(r => r.SubmittedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(r => r.ReviewedByUserId).HasMaxLength(450);
            entity.Property(r => r.ReviewNote).HasMaxLength(300);
            entity.Property(r => r.IssuedEmail).HasMaxLength(256);

            // Matches the filtered unique index: one open request per contact address.
            entity.HasIndex(r => r.ContactEmail)
                  .IsUnique()
                  .HasFilter("[Status] = N'Pending'")
                  .HasDatabaseName("UQ_StudentRegistrationRequests_PendingContactEmail");

            entity.HasIndex(r => new { r.Status, r.SubmittedAt })
                  .HasDatabaseName("IX_StudentRegistrationRequests_Status_SubmittedAt");

            entity.HasOne(r => r.GradeLevel)
                  .WithMany()
                  .HasForeignKey(r => r.GradeLevelId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Section)
                  .WithMany()
                  .HasForeignKey(r => r.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.CreatedStudent)
                  .WithMany()
                  .HasForeignKey(r => r.CreatedStudentId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PasswordChangeLog>(entity =>
        {
            entity.ToTable("PasswordChangeLogs");
            entity.Property(l => l.Outcome).HasMaxLength(20).IsRequired();
            entity.Property(l => l.FailureReason).HasMaxLength(300);
            entity.Property(l => l.IpAddress).HasMaxLength(45);
            entity.Property(l => l.UserAgent).HasMaxLength(400);
            entity.Property(l => l.ChangedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(l => new { l.UserId, l.ChangedAt })
                  .HasDatabaseName("IX_PasswordChangeLogs_UserId_ChangedAt");

            entity.HasOne(l => l.User)
                  .WithMany()
                  .HasForeignKey(l => l.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeacherSubject>(entity =>
        {
            entity.ToTable("TeacherSubjects");
            entity.Property(ts => ts.AcademicYear).HasMaxLength(9).IsRequired();
            entity.Property(ts => ts.AssignedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(ts => new { ts.TeacherId, ts.SubjectId, ts.SectionId, ts.AcademicYear })
                  .IsUnique()
                  .HasDatabaseName("UQ_TeacherSubjects_Assignment");

            entity.HasOne(ts => ts.Teacher)
                  .WithMany(t => t.TeacherSubjects)
                  .HasForeignKey(ts => ts.TeacherId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ts => ts.Subject)
                  .WithMany(s => s.TeacherSubjects)
                  .HasForeignKey(ts => ts.SubjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ts => ts.Section)
                  .WithMany(s => s.TeacherSubjects)
                  .HasForeignKey(ts => ts.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureContent(ModelBuilder builder)
    {
        builder.Entity<Lesson>(entity =>
        {
            entity.ToTable("Lessons");
            entity.Property(l => l.Title).HasMaxLength(200).IsRequired();
            entity.Property(l => l.FileUrl).HasMaxLength(500);
            entity.Property(l => l.FileName).HasMaxLength(255);
            entity.Property(l => l.ContentType).HasMaxLength(100);
            entity.Property(l => l.IsPublished).HasDefaultValue(true);
            entity.Property(l => l.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(l => l.Subject)
                  .WithMany(s => s.Lessons)
                  .HasForeignKey(l => l.SubjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Teacher)
                  .WithMany(t => t.Lessons)
                  .HasForeignKey(l => l.TeacherId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Section)
                  .WithMany()
                  .HasForeignKey(l => l.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAssessment(ModelBuilder builder)
    {
        builder.Entity<AssessmentTypeWeight>(entity =>
        {
            entity.ToTable("AssessmentTypes");
            entity.HasKey(a => a.Name);
            entity.Property(a => a.Name).HasMaxLength(20);
            entity.Property(a => a.DisplayName).HasMaxLength(50).IsRequired();
            entity.Property(a => a.WeightPercentage).HasPrecision(5, 2);
        });

        builder.Entity<Assessment>(entity =>
        {
            entity.ToTable("Assessments");
            entity.Property(a => a.Title).HasMaxLength(200).IsRequired();
            entity.Property(a => a.MaxScore).HasPrecision(6, 2);
            entity.Property(a => a.AcademicYear).HasMaxLength(9).IsRequired();
            entity.Property(a => a.IsActive).HasDefaultValue(true);
            entity.Property(a => a.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // Stored as text to satisfy FK_Assessments_AssessmentTypes_AssessmentType.
            entity.Property(a => a.AssessmentType)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            entity.HasOne(a => a.Subject)
                  .WithMany(s => s.Assessments)
                  .HasForeignKey(a => a.SubjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Section)
                  .WithMany()
                  .HasForeignKey(a => a.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Teacher)
                  .WithMany(t => t.Assessments)
                  .HasForeignKey(a => a.TeacherId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Mark>(entity =>
        {
            // TR_Marks_Validate is an AFTER trigger. SQL Server cannot use an OUTPUT
            // clause on a table with triggers, so EF must be told it exists or every
            // SaveChanges against Marks fails at runtime.
            entity.ToTable("Marks", t => t.HasTrigger("TR_Marks_Validate"));

            entity.Property(m => m.Score).HasPrecision(6, 2);
            entity.Property(m => m.Remark).HasMaxLength(300);
            entity.Property(m => m.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(m => new { m.StudentId, m.AssessmentId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Marks_Student_Assessment");

            entity.HasOne(m => m.Student)
                  .WithMany(s => s.Marks)
                  .HasForeignKey(m => m.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Assessment)
                  .WithMany(a => a.Marks)
                  .HasForeignKey(m => m.AssessmentId)
                  .OnDelete(DeleteBehavior.Cascade);

            // NO ACTION in SQL to avoid a multiple cascade path through Assessments.
            entity.HasOne(m => m.Subject)
                  .WithMany(s => s.Marks)
                  .HasForeignKey(m => m.SubjectId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.EnteredByTeacher)
                  .WithMany()
                  .HasForeignKey(m => m.EnteredByTeacherId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCommunication(ModelBuilder builder)
    {
        builder.Entity<Announcement>(entity =>
        {
            entity.ToTable("Announcements");
            entity.Property(a => a.Title).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Content).IsRequired();
            entity.Property(a => a.TargetRole).HasMaxLength(20).HasDefaultValue("All").IsRequired();
            entity.Property(a => a.IsPublished).HasDefaultValue(true);
            entity.Property(a => a.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(a => a.GradeLevel)
                  .WithMany()
                  .HasForeignKey(a => a.GradeLevelId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Section)
                  .WithMany()
                  .HasForeignKey(a => a.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(a => a.CreatedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(s => s.Key);
            entity.Property(s => s.Key).HasMaxLength(100);
            entity.Property(s => s.Value).HasMaxLength(500);
            entity.Property(s => s.Description).HasMaxLength(300);
            entity.Property(s => s.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }

    private static void ConfigureViews(ModelBuilder builder)
    {
        builder.Entity<StudentSubjectPerformance>(entity =>
        {
            entity.HasNoKey().ToView("vw_StudentSubjectPerformance");
            entity.Property(p => p.QuizScore).HasPrecision(6, 2);
            entity.Property(p => p.AssignmentScore).HasPrecision(6, 2);
            entity.Property(p => p.TestScore).HasPrecision(6, 2);
            entity.Property(p => p.MidExamScore).HasPrecision(6, 2);
            entity.Property(p => p.FinalExamScore).HasPrecision(6, 2);
            entity.Property(p => p.TotalScore).HasPrecision(6, 2);
        });
    }
}
