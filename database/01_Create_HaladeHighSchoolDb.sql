/* ============================================================================
   HALADE HIGH SCHOOL PORTAL
   Phase 1 - Database Schema (Microsoft SQL Server 2022 / SSMS compatible)

   Target database : HaladeHighSchoolDb
   Auth model      : ASP.NET Core Identity (EF Core) + JWT (refresh tokens)
   Scope           : Grades 9-12, Sections A-C

   Execution       : Open in SSMS, ensure SQLCMD mode is OFF, press F5.
                     The script is idempotent - it can be re-run safely.

   Delete semantics:
     * Academic data (Students, Teachers, Marks) is never hard-deleted by a
       cascade. Login deletion sets Students.UserId / Teachers.UserId to NULL
       so historical marks and report cards survive. Use the IsActive flags
       for de-activation instead of DELETE.
   ============================================================================ */

/* ----------------------------------------------------------------------------
   0. DATABASE
   ---------------------------------------------------------------------------- */
IF DB_ID(N'HaladeHighSchoolDb') IS NULL
BEGIN
    CREATE DATABASE [HaladeHighSchoolDb];
END
GO

ALTER DATABASE [HaladeHighSchoolDb] SET RECOVERY SIMPLE WITH NO_WAIT;
GO

USE [HaladeHighSchoolDb];
GO

/* ANSI_NULLS / QUOTED_IDENTIFIER must be ON for the filtered indexes below.
   SSMS enables them by default; sqlcmd does not, so set them explicitly. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ----------------------------------------------------------------------------
   1. EF CORE MIGRATION HISTORY
      Present so the schema can later be baselined with `dotnet ef migrations`.
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.__EFMigrationsHistory
    (
        MigrationId    nvarchar(150) NOT NULL,
        ProductVersion nvarchar(32)  NOT NULL,
        CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY CLUSTERED (MigrationId)
    );
END
GO

/* ----------------------------------------------------------------------------
   2. ASP.NET CORE IDENTITY
      Column names/types match the default EF Core Identity model so the
      ApplicationUser/IdentityRole entities map without a custom convention.
      Custom ApplicationUser columns: FullName, ProfileImageUrl, IsActive,
      CreatedAt, LastLoginAt.
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoles
    (
        Id               nvarchar(450)  NOT NULL,
        [Name]           nvarchar(256)  NULL,
        NormalizedName   nvarchar(256)  NULL,
        ConcurrencyStamp nvarchar(max)  NULL,
        CONSTRAINT PK_AspNetRoles PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX RoleNameIndex
        ON dbo.AspNetRoles (NormalizedName)
        WHERE NormalizedName IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUsers
    (
        Id                   nvarchar(450)  NOT NULL,
        UserName             nvarchar(256)  NULL,
        NormalizedUserName   nvarchar(256)  NULL,
        Email                nvarchar(256)  NULL,
        NormalizedEmail      nvarchar(256)  NULL,
        EmailConfirmed       bit            NOT NULL CONSTRAINT DF_AspNetUsers_EmailConfirmed       DEFAULT (0),
        PasswordHash         nvarchar(max)  NULL,
        SecurityStamp        nvarchar(max)  NULL,
        ConcurrencyStamp     nvarchar(max)  NULL,
        PhoneNumber          nvarchar(max)  NULL,
        PhoneNumberConfirmed bit            NOT NULL CONSTRAINT DF_AspNetUsers_PhoneNumberConfirmed DEFAULT (0),
        TwoFactorEnabled     bit            NOT NULL CONSTRAINT DF_AspNetUsers_TwoFactorEnabled     DEFAULT (0),
        LockoutEnd           datetimeoffset(7) NULL,
        LockoutEnabled       bit            NOT NULL CONSTRAINT DF_AspNetUsers_LockoutEnabled       DEFAULT (1),
        AccessFailedCount    int            NOT NULL CONSTRAINT DF_AspNetUsers_AccessFailedCount    DEFAULT (0),
        -- Application specific columns
        FullName             nvarchar(150)  NOT NULL CONSTRAINT DF_AspNetUsers_FullName             DEFAULT (N''),
        ProfileImageUrl      nvarchar(500)  NULL,
        IsActive             bit            NOT NULL CONSTRAINT DF_AspNetUsers_IsActive             DEFAULT (1),
        CreatedAt            datetime2(7)   NOT NULL CONSTRAINT DF_AspNetUsers_CreatedAt            DEFAULT (SYSUTCDATETIME()),
        LastLoginAt          datetime2(7)   NULL,
        CONSTRAINT PK_AspNetUsers PRIMARY KEY CLUSTERED (Id)
    );

    CREATE NONCLUSTERED INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail);

    CREATE UNIQUE NONCLUSTERED INDEX UserNameIndex
        ON dbo.AspNetUsers (NormalizedUserName)
        WHERE NormalizedUserName IS NOT NULL;

    CREATE NONCLUSTERED INDEX IX_AspNetUsers_IsActive ON dbo.AspNetUsers (IsActive);
END
GO

IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserRoles
    (
        UserId nvarchar(450) NOT NULL,
        RoleId nvarchar(450) NOT NULL,
        CONSTRAINT PK_AspNetUserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
        CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
        CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId FOREIGN KEY (RoleId)
            REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles (RoleId);
END
GO

IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoleClaims
    (
        Id         int           IDENTITY(1,1) NOT NULL,
        RoleId     nvarchar(450) NOT NULL,
        ClaimType  nvarchar(max) NULL,
        ClaimValue nvarchar(max) NULL,
        CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId FOREIGN KEY (RoleId)
            REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims (RoleId);
END
GO

IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserClaims
    (
        Id         int           IDENTITY(1,1) NOT NULL,
        UserId     nvarchar(450) NOT NULL,
        ClaimType  nvarchar(max) NULL,
        ClaimValue nvarchar(max) NULL,
        CONSTRAINT PK_AspNetUserClaims PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims (UserId);
END
GO

IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserLogins
    (
        LoginProvider       nvarchar(450) NOT NULL,
        ProviderKey         nvarchar(450) NOT NULL,
        ProviderDisplayName nvarchar(max) NULL,
        UserId              nvarchar(450) NOT NULL,
        CONSTRAINT PK_AspNetUserLogins PRIMARY KEY CLUSTERED (LoginProvider, ProviderKey),
        CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins (UserId);
END
GO

IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserTokens
    (
        UserId        nvarchar(450) NOT NULL,
        LoginProvider nvarchar(450) NOT NULL,
        [Name]        nvarchar(450) NOT NULL,
        [Value]       nvarchar(max) NULL,
        CONSTRAINT PK_AspNetUserTokens PRIMARY KEY CLUSTERED (UserId, LoginProvider, [Name]),
        CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
END
GO

/* JWT refresh tokens (rotation + revocation support). */
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens
    (
        Id             int            IDENTITY(1,1) NOT NULL,
        UserId         nvarchar(450)  NOT NULL,
        Token          nvarchar(256)  NOT NULL,
        ExpiresAt      datetime2(7)   NOT NULL,
        CreatedAt      datetime2(7)   NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedByIp    nvarchar(45)   NULL,
        RevokedAt      datetime2(7)   NULL,
        ReplacedByToken nvarchar(256) NULL,
        CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_RefreshTokens_Token UNIQUE NONCLUSTERED (Token),
        CONSTRAINT FK_RefreshTokens_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
        CONSTRAINT CK_RefreshTokens_Expiry CHECK (ExpiresAt > CreatedAt)
    );

    CREATE NONCLUSTERED INDEX IX_RefreshTokens_UserId_ExpiresAt
        ON dbo.RefreshTokens (UserId, ExpiresAt) INCLUDE (RevokedAt);
END
GO

/* ----------------------------------------------------------------------------
   3. ACADEMIC STRUCTURE
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.GradeLevels', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GradeLevels
    (
        Id          int           IDENTITY(1,1) NOT NULL,
        [Name]      nvarchar(50)  NOT NULL,
        [Level]     int           NOT NULL,
        [Description] nvarchar(250) NULL,
        IsActive    bit           NOT NULL CONSTRAINT DF_GradeLevels_IsActive  DEFAULT (1),
        CreatedAt   datetime2(7)  NOT NULL CONSTRAINT DF_GradeLevels_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_GradeLevels PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_GradeLevels_Name  UNIQUE NONCLUSTERED ([Name]),
        CONSTRAINT UQ_GradeLevels_Level UNIQUE NONCLUSTERED ([Level]),
        CONSTRAINT CK_GradeLevels_Level CHECK ([Level] BETWEEN 9 AND 12)
    );
END
GO

IF OBJECT_ID(N'dbo.Sections', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sections
    (
        Id        int          IDENTITY(1,1) NOT NULL,
        [Name]    nvarchar(50) NOT NULL,
        Code      nvarchar(10) NOT NULL,
        Capacity  int          NOT NULL CONSTRAINT DF_Sections_Capacity DEFAULT (40),
        IsActive  bit          NOT NULL CONSTRAINT DF_Sections_IsActive  DEFAULT (1),
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Sections_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Sections PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Sections_Name UNIQUE NONCLUSTERED ([Name]),
        CONSTRAINT UQ_Sections_Code UNIQUE NONCLUSTERED (Code),
        CONSTRAINT CK_Sections_Capacity CHECK (Capacity BETWEEN 1 AND 200)
    );
END
GO

IF OBJECT_ID(N'dbo.Subjects', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Subjects
    (
        Id            int           IDENTITY(1,1) NOT NULL,
        SubjectName   nvarchar(150) NOT NULL,
        Code          nvarchar(20)  NOT NULL,
        GradeLevelId  int           NOT NULL,
        [Description] nvarchar(500) NULL,
        CreditHours   int           NOT NULL CONSTRAINT DF_Subjects_CreditHours DEFAULT (3),
        IsActive      bit           NOT NULL CONSTRAINT DF_Subjects_IsActive     DEFAULT (1),
        CreatedAt     datetime2(7)  NOT NULL CONSTRAINT DF_Subjects_CreatedAt    DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Subjects PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Subjects_Code UNIQUE NONCLUSTERED (Code),
        CONSTRAINT UQ_Subjects_Name_Grade UNIQUE NONCLUSTERED (SubjectName, GradeLevelId),
        CONSTRAINT FK_Subjects_GradeLevels_GradeLevelId FOREIGN KEY (GradeLevelId)
            REFERENCES dbo.GradeLevels (Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Subjects_CreditHours CHECK (CreditHours BETWEEN 1 AND 10)
    );

    CREATE NONCLUSTERED INDEX IX_Subjects_GradeLevelId
        ON dbo.Subjects (GradeLevelId) INCLUDE (SubjectName, Code, IsActive);
END
GO

/* ----------------------------------------------------------------------------
   4. PEOPLE
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Teachers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Teachers
    (
        Id             int           IDENTITY(1,1) NOT NULL,
        EmployeeId     nvarchar(30)  NOT NULL,
        Specialization nvarchar(150) NULL,
        UserId         nvarchar(450) NULL,
        Qualification  nvarchar(150) NULL,
        PhoneNumber    nvarchar(30)  NULL,
        HireDate       date          NULL,
        IsActive       bit           NOT NULL CONSTRAINT DF_Teachers_IsActive  DEFAULT (1),
        CreatedAt      datetime2(7)  NOT NULL CONSTRAINT DF_Teachers_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Teachers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Teachers_EmployeeId UNIQUE NONCLUSTERED (EmployeeId),
        CONSTRAINT FK_Teachers_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE SET NULL
    );

    /* One login per teacher, while still allowing records without a login yet. */
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Teachers_UserId
        ON dbo.Teachers (UserId) WHERE UserId IS NOT NULL;

    CREATE NONCLUSTERED INDEX IX_Teachers_IsActive ON dbo.Teachers (IsActive);
END
GO

IF OBJECT_ID(N'dbo.Students', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Students
    (
        Id              int           IDENTITY(1,1) NOT NULL,
        StudentIdNumber nvarchar(30)  NOT NULL,
        GradeLevelId    int           NOT NULL,
        SectionId       int           NOT NULL,
        UserId          nvarchar(450) NULL,
        DateOfBirth     date          NULL,
        Gender          nvarchar(10)  NULL,
        GuardianName    nvarchar(150) NULL,
        GuardianPhone   nvarchar(30)  NULL,
        [Address]       nvarchar(250) NULL,
        EnrollmentDate  date          NOT NULL CONSTRAINT DF_Students_EnrollmentDate DEFAULT (CAST(SYSUTCDATETIME() AS date)),
        IsActive        bit           NOT NULL CONSTRAINT DF_Students_IsActive       DEFAULT (1),
        CreatedAt       datetime2(7)  NOT NULL CONSTRAINT DF_Students_CreatedAt      DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Students PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Students_StudentIdNumber UNIQUE NONCLUSTERED (StudentIdNumber),
        CONSTRAINT FK_Students_GradeLevels_GradeLevelId FOREIGN KEY (GradeLevelId)
            REFERENCES dbo.GradeLevels (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Students_Sections_SectionId FOREIGN KEY (SectionId)
            REFERENCES dbo.Sections (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Students_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE SET NULL,
        CONSTRAINT CK_Students_Gender CHECK (Gender IS NULL OR Gender IN (N'Male', N'Female', N'Other'))
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Students_UserId
        ON dbo.Students (UserId) WHERE UserId IS NOT NULL;

    CREATE NONCLUSTERED INDEX IX_Students_GradeLevelId_SectionId
        ON dbo.Students (GradeLevelId, SectionId) INCLUDE (StudentIdNumber, IsActive);

    /* The composite above leads with GradeLevelId, so a filter on section alone - the admin
       roster filtered by section, and the FK back to Sections - could not seek on it. */
    CREATE NONCLUSTERED INDEX IX_Students_SectionId
        ON dbo.Students (SectionId) INCLUDE (GradeLevelId, StudentIdNumber, IsActive);
END
GO

/* ---------------------------------------------------------------------------
   Students: remove the earlier in-row approval columns

   Approval now lives in dbo.StudentRegistrationRequests, and a Students row is only created
   once a request has been approved - so a student can no longer be anything but approved.
   Guarded so this is a no-op on a database that never had the columns.
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Students') AND name = N'ApprovalStatus')
BEGIN
    IF EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Students_ApprovalStatus'
               AND object_id = OBJECT_ID(N'dbo.Students'))
        DROP INDEX IX_Students_ApprovalStatus ON dbo.Students;

    IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_Students_ApprovalStatus')
        ALTER TABLE dbo.Students DROP CONSTRAINT CK_Students_ApprovalStatus;

    -- The default has to go before the column it belongs to.
    IF EXISTS (SELECT * FROM sys.default_constraints WHERE name = N'DF_Students_ApprovalStatus')
        ALTER TABLE dbo.Students DROP CONSTRAINT DF_Students_ApprovalStatus;

    ALTER TABLE dbo.Students
        DROP COLUMN ApprovalStatus, ReviewedAt, ReviewedByUserId, ReviewNote;

    PRINT 'Students: dropped the in-row approval columns (superseded by StudentRegistrationRequests).';
END
GO

/* ---------------------------------------------------------------------------
   StudentRegistrationRequests

   An applicant supplies a name, a contact address and the class they want. No login and no
   Students row exists until an administrator approves the request, at which point the portal
   issues the student number, the school sign-in address and a temporary password.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.StudentRegistrationRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentRegistrationRequests
    (
        Id               int           IDENTITY(1,1) NOT NULL,
        FullName         nvarchar(150) NOT NULL,
        /* Where the applicant can be reached, and where the issued credentials are sent. This
           is not the sign-in address: that is generated at approval. */
        ContactEmail     nvarchar(256) NOT NULL,
        GradeLevelId     int           NOT NULL,
        SectionId        int           NOT NULL,
        [Status]         nvarchar(20)  NOT NULL CONSTRAINT DF_StudentRegistrationRequests_Status DEFAULT (N'Pending'),
        SubmittedAt      datetime2(7)  NOT NULL CONSTRAINT DF_StudentRegistrationRequests_SubmittedAt DEFAULT (SYSUTCDATETIME()),
        ReviewedAt       datetime2(7)  NULL,
        /* No foreign key: an audit row should outlive the reviewer's account, and AspNetUsers
           already reaches this table through the approved student. */
        ReviewedByUserId nvarchar(450) NULL,
        ReviewNote       nvarchar(300) NULL,
        /* Filled in on approval. NULL again if the student is later deleted, which keeps the
           decision on record without pretending the account still exists. */
        CreatedStudentId int           NULL,
        IssuedEmail      nvarchar(256) NULL,
        CONSTRAINT PK_StudentRegistrationRequests PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_StudentRegistrationRequests_Status
            CHECK ([Status] IN (N'Pending', N'Approved', N'Rejected')),
        CONSTRAINT FK_StudentRegistrationRequests_GradeLevels_GradeLevelId FOREIGN KEY (GradeLevelId)
            REFERENCES dbo.GradeLevels (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_StudentRegistrationRequests_Sections_SectionId FOREIGN KEY (SectionId)
            REFERENCES dbo.Sections (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_StudentRegistrationRequests_Students_CreatedStudentId FOREIGN KEY (CreatedStudentId)
            REFERENCES dbo.Students (Id) ON DELETE SET NULL
    );

    /* One open request per contact address: re-applying while a decision is outstanding is a
       duplicate, but re-applying after a rejection is legitimate. */
    CREATE UNIQUE NONCLUSTERED INDEX UQ_StudentRegistrationRequests_PendingContactEmail
        ON dbo.StudentRegistrationRequests (ContactEmail)
        WHERE [Status] = N'Pending';

    CREATE NONCLUSTERED INDEX IX_StudentRegistrationRequests_Status_SubmittedAt
        ON dbo.StudentRegistrationRequests ([Status], SubmittedAt)
        INCLUDE (FullName, ContactEmail);

    PRINT 'Created dbo.StudentRegistrationRequests.';
END
GO

/* ---------------------------------------------------------------------------
   PasswordChangeLogs

   Both outcomes are recorded: a run of failures against one account is the signal worth
   having, and a success on its own cannot show that.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.PasswordChangeLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordChangeLogs
    (
        Id            bigint        IDENTITY(1,1) NOT NULL,
        UserId        nvarchar(450) NOT NULL,
        ChangedAt     datetime2(7)  NOT NULL CONSTRAINT DF_PasswordChangeLogs_ChangedAt DEFAULT (SYSUTCDATETIME()),
        Outcome       nvarchar(20)  NOT NULL,
        FailureReason nvarchar(300) NULL,
        IpAddress     nvarchar(45)  NULL,
        UserAgent     nvarchar(400) NULL,
        CONSTRAINT PK_PasswordChangeLogs PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_PasswordChangeLogs_Outcome CHECK (Outcome IN (N'Succeeded', N'Failed')),
        /* Cascade rather than NO ACTION: removing a student deletes their login, and that must
           not be blocked by their own password history. */
        CONSTRAINT FK_PasswordChangeLogs_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_PasswordChangeLogs_UserId_ChangedAt
        ON dbo.PasswordChangeLogs (UserId, ChangedAt DESC);

    PRINT 'Created dbo.PasswordChangeLogs.';
END
GO

/* Teacher <-> Subject <-> Section assignment (a teaching load line). */
IF OBJECT_ID(N'dbo.TeacherSubjects', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TeacherSubjects
    (
        Id           int          IDENTITY(1,1) NOT NULL,
        TeacherId    int          NOT NULL,
        SubjectId    int          NOT NULL,
        SectionId    int          NOT NULL,
        AcademicYear nvarchar(9)  NOT NULL CONSTRAINT DF_TeacherSubjects_AcademicYear DEFAULT (N'2026-2027'),
        IsActive     bit          NOT NULL CONSTRAINT DF_TeacherSubjects_IsActive     DEFAULT (1),
        AssignedAt   datetime2(7) NOT NULL CONSTRAINT DF_TeacherSubjects_AssignedAt   DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_TeacherSubjects PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_TeacherSubjects_Assignment UNIQUE NONCLUSTERED (TeacherId, SubjectId, SectionId, AcademicYear),
        CONSTRAINT FK_TeacherSubjects_Teachers_TeacherId FOREIGN KEY (TeacherId)
            REFERENCES dbo.Teachers (Id) ON DELETE CASCADE,
        CONSTRAINT FK_TeacherSubjects_Subjects_SubjectId FOREIGN KEY (SubjectId)
            REFERENCES dbo.Subjects (Id) ON DELETE CASCADE,
        CONSTRAINT FK_TeacherSubjects_Sections_SectionId FOREIGN KEY (SectionId)
            REFERENCES dbo.Sections (Id) ON DELETE NO ACTION,
        CONSTRAINT CK_TeacherSubjects_AcademicYear
            CHECK (AcademicYear LIKE N'[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]')
    );

    CREATE NONCLUSTERED INDEX IX_TeacherSubjects_SubjectId ON dbo.TeacherSubjects (SubjectId);
    CREATE NONCLUSTERED INDEX IX_TeacherSubjects_SectionId ON dbo.TeacherSubjects (SectionId);
    CREATE NONCLUSTERED INDEX IX_TeacherSubjects_TeacherId_AcademicYear
        ON dbo.TeacherSubjects (TeacherId, AcademicYear) INCLUDE (SubjectId, SectionId, IsActive);
END
GO

/* ----------------------------------------------------------------------------
   5. TEACHING CONTENT
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Lessons', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Lessons
    (
        Id            int           IDENTITY(1,1) NOT NULL,
        Title         nvarchar(200) NOT NULL,
        Content       nvarchar(max) NULL,
        FileUrl       nvarchar(500) NULL,
        FileName      nvarchar(255) NULL,
        FileSizeBytes bigint        NULL,
        ContentType   nvarchar(100) NULL,
        SubjectId     int           NOT NULL,
        TeacherId     int           NOT NULL,
        SectionId     int           NULL,           -- NULL = shared with every section
        IsPublished   bit           NOT NULL CONSTRAINT DF_Lessons_IsPublished DEFAULT (1),
        CreatedAt     datetime2(7)  NOT NULL CONSTRAINT DF_Lessons_CreatedAt   DEFAULT (SYSUTCDATETIME()),
        UpdatedAt     datetime2(7)  NULL,
        CONSTRAINT PK_Lessons PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Lessons_Subjects_SubjectId FOREIGN KEY (SubjectId)
            REFERENCES dbo.Subjects (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Lessons_Teachers_TeacherId FOREIGN KEY (TeacherId)
            REFERENCES dbo.Teachers (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Lessons_Sections_SectionId FOREIGN KEY (SectionId)
            REFERENCES dbo.Sections (Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Lessons_FileSize CHECK (FileSizeBytes IS NULL OR FileSizeBytes >= 0)
    );

    CREATE NONCLUSTERED INDEX IX_Lessons_SubjectId_CreatedAt
        ON dbo.Lessons (SubjectId, CreatedAt DESC) INCLUDE (Title, IsPublished, SectionId);
    CREATE NONCLUSTERED INDEX IX_Lessons_TeacherId ON dbo.Lessons (TeacherId);
END
GO

/* ----------------------------------------------------------------------------
   6. ASSESSMENT & MARKS
      Weighting (sums to 100% of the final subject score):
        Quiz 10 | Assignment 10 | Test 20 | MidExam 30 | FinalExam 30
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.AssessmentTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AssessmentTypes
    (
        [Name]           nvarchar(20)  NOT NULL,
        DisplayName      nvarchar(50)  NOT NULL,
        WeightPercentage decimal(5,2)  NOT NULL,
        DisplayOrder     int           NOT NULL,
        IsActive         bit           NOT NULL CONSTRAINT DF_AssessmentTypes_IsActive DEFAULT (1),
        CONSTRAINT PK_AssessmentTypes PRIMARY KEY CLUSTERED ([Name]),
        CONSTRAINT CK_AssessmentTypes_Weight CHECK (WeightPercentage > 0 AND WeightPercentage <= 100)
    );
END
GO

IF OBJECT_ID(N'dbo.Assessments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Assessments
    (
        Id             int           IDENTITY(1,1) NOT NULL,
        Title          nvarchar(200) NOT NULL,
        AssessmentType nvarchar(20)  NOT NULL,   -- Quiz | Assignment | Test | MidExam | FinalExam
        MaxScore       decimal(6,2)  NOT NULL,
        SubjectId      int           NOT NULL,
        SectionId      int           NULL,        -- NULL = applies to all sections
        TeacherId      int           NULL,        -- author / owner
        AcademicYear   nvarchar(9)   NOT NULL CONSTRAINT DF_Assessments_AcademicYear DEFAULT (N'2026-2027'),
        DueDate        date          NULL,
        IsActive       bit           NOT NULL CONSTRAINT DF_Assessments_IsActive   DEFAULT (1),
        CreatedAt      datetime2(7)  NOT NULL CONSTRAINT DF_Assessments_CreatedAt  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Assessments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Assessments_Subjects_SubjectId FOREIGN KEY (SubjectId)
            REFERENCES dbo.Subjects (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Assessments_Sections_SectionId FOREIGN KEY (SectionId)
            REFERENCES dbo.Sections (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Assessments_Teachers_TeacherId FOREIGN KEY (TeacherId)
            REFERENCES dbo.Teachers (Id) ON DELETE NO ACTION,
        /* FK to the lookup table is the single source of truth for allowed types. */
        CONSTRAINT FK_Assessments_AssessmentTypes_AssessmentType FOREIGN KEY (AssessmentType)
            REFERENCES dbo.AssessmentTypes ([Name]) ON DELETE NO ACTION,
        CONSTRAINT CK_Assessments_MaxScore CHECK (MaxScore > 0 AND MaxScore <= 1000),
        CONSTRAINT CK_Assessments_AcademicYear
            CHECK (AcademicYear LIKE N'[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]')
    );

    CREATE NONCLUSTERED INDEX IX_Assessments_SubjectId_Type
        ON dbo.Assessments (SubjectId, AssessmentType) INCLUDE (Title, MaxScore, SectionId, IsActive);
    CREATE NONCLUSTERED INDEX IX_Assessments_AssessmentType ON dbo.Assessments (AssessmentType);
    CREATE NONCLUSTERED INDEX IX_Assessments_TeacherId      ON dbo.Assessments (TeacherId);
    CREATE NONCLUSTERED INDEX IX_Assessments_SectionId      ON dbo.Assessments (SectionId);
END
GO

IF OBJECT_ID(N'dbo.Marks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Marks
    (
        Id                  int           IDENTITY(1,1) NOT NULL,
        StudentId           int           NOT NULL,
        SubjectId           int           NOT NULL,
        AssessmentId        int           NOT NULL,
        Score               decimal(6,2)  NOT NULL,
        Remark              nvarchar(300) NULL,
        IsPublished         bit           NOT NULL CONSTRAINT DF_Marks_IsPublished DEFAULT (0),
        PublishedAt         datetime2(7)  NULL,
        EnteredByTeacherId  int           NOT NULL,
        CreatedAt           datetime2(7)  NOT NULL CONSTRAINT DF_Marks_CreatedAt   DEFAULT (SYSUTCDATETIME()),
        UpdatedAt           datetime2(7)  NULL,
        CONSTRAINT PK_Marks PRIMARY KEY CLUSTERED (Id),
        /* One score per student per assessment. */
        CONSTRAINT UQ_Marks_Student_Assessment UNIQUE NONCLUSTERED (StudentId, AssessmentId),
        CONSTRAINT FK_Marks_Students_StudentId FOREIGN KEY (StudentId)
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Marks_Assessments_AssessmentId FOREIGN KEY (AssessmentId)
            REFERENCES dbo.Assessments (Id) ON DELETE CASCADE,
        /* NO ACTION avoids a multiple-cascade-path conflict with Assessments. */
        CONSTRAINT FK_Marks_Subjects_SubjectId FOREIGN KEY (SubjectId)
            REFERENCES dbo.Subjects (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Marks_Teachers_EnteredByTeacherId FOREIGN KEY (EnteredByTeacherId)
            REFERENCES dbo.Teachers (Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Marks_Score CHECK (Score >= 0)
    );

    CREATE NONCLUSTERED INDEX IX_Marks_StudentId_SubjectId
        ON dbo.Marks (StudentId, SubjectId) INCLUDE (AssessmentId, Score, IsPublished);
    /* IsPublished is included, not a key: the gradebook and the published/total counts on the
       assessment list both read it for every mark of one assessment. */
    CREATE NONCLUSTERED INDEX IX_Marks_AssessmentId
        ON dbo.Marks (AssessmentId) INCLUDE (StudentId, Score, IsPublished);
    CREATE NONCLUSTERED INDEX IX_Marks_SubjectId          ON dbo.Marks (SubjectId);
    CREATE NONCLUSTERED INDEX IX_Marks_EnteredByTeacherId ON dbo.Marks (EnteredByTeacherId);
    CREATE NONCLUSTERED INDEX IX_Marks_IsPublished        ON dbo.Marks (IsPublished) WHERE IsPublished = 1;
END
GO

/* Score <= Assessment.MaxScore, and the mark's subject must match the
   assessment's subject. Cross-row rules cannot live in a CHECK constraint. */
CREATE OR ALTER TRIGGER dbo.TR_Marks_Validate
    ON dbo.Marks
    AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        INNER JOIN dbo.Assessments AS a ON a.Id = i.AssessmentId
        WHERE i.Score > a.MaxScore
    )
    BEGIN
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW 50001, N'Score cannot exceed the MaxScore of the related assessment.', 1;
    END

    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        INNER JOIN dbo.Assessments AS a ON a.Id = i.AssessmentId
        WHERE a.SubjectId <> i.SubjectId
    )
    BEGIN
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW 50002, N'Marks.SubjectId must match the subject of the related assessment.', 1;
    END
END
GO

/* ----------------------------------------------------------------------------
   7. COMMUNICATION & SETTINGS
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Announcements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Announcements
    (
        Id              int           IDENTITY(1,1) NOT NULL,
        Title           nvarchar(200) NOT NULL,
        Content         nvarchar(max) NOT NULL,
        TargetRole      nvarchar(20)  NOT NULL CONSTRAINT DF_Announcements_TargetRole  DEFAULT (N'All'),
        GradeLevelId    int           NULL,   -- NULL = every grade
        SectionId       int           NULL,   -- NULL = every section
        CreatedByUserId nvarchar(450) NULL,
        IsPublished     bit           NOT NULL CONSTRAINT DF_Announcements_IsPublished DEFAULT (1),
        IsPinned        bit           NOT NULL CONSTRAINT DF_Announcements_IsPinned    DEFAULT (0),
        ExpiresAt       datetime2(7)  NULL,
        CreatedAt       datetime2(7)  NOT NULL CONSTRAINT DF_Announcements_CreatedAt   DEFAULT (SYSUTCDATETIME()),
        UpdatedAt       datetime2(7)  NULL,
        CONSTRAINT PK_Announcements PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Announcements_GradeLevels_GradeLevelId FOREIGN KEY (GradeLevelId)
            REFERENCES dbo.GradeLevels (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Announcements_Sections_SectionId FOREIGN KEY (SectionId)
            REFERENCES dbo.Sections (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Announcements_AspNetUsers_CreatedByUserId FOREIGN KEY (CreatedByUserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE SET NULL,
        CONSTRAINT CK_Announcements_TargetRole
            CHECK (TargetRole IN (N'All', N'Admin', N'Teacher', N'Student'))
    );

    CREATE NONCLUSTERED INDEX IX_Announcements_TargetRole_CreatedAt
        ON dbo.Announcements (TargetRole, CreatedAt DESC)
        INCLUDE (Title, IsPublished, IsPinned, GradeLevelId, ExpiresAt);
END
GO

IF OBJECT_ID(N'dbo.SystemSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemSettings
    (
        [Key]         nvarchar(100) NOT NULL,
        [Value]       nvarchar(500) NULL,
        [Description] nvarchar(300) NULL,
        UpdatedAt     datetime2(7)  NOT NULL CONSTRAINT DF_SystemSettings_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SystemSettings PRIMARY KEY CLUSTERED ([Key])
    );
END
GO

/* ----------------------------------------------------------------------------
   8. REPORTING VIEWS
   ---------------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_StudentMarkDetails
AS
SELECT
    m.Id                AS MarkId,
    s.Id                AS StudentId,
    s.StudentIdNumber,
    su.FullName         AS StudentName,
    g.Id                AS GradeLevelId,
    g.[Name]            AS GradeLevelName,
    sec.Id              AS SectionId,
    sec.[Name]          AS SectionName,
    sub.Id              AS SubjectId,
    sub.SubjectName,
    sub.Code            AS SubjectCode,
    a.Id                AS AssessmentId,
    a.Title             AS AssessmentTitle,
    a.AssessmentType,
    atw.WeightPercentage,
    a.MaxScore,
    m.Score,
    CAST(m.Score / NULLIF(a.MaxScore, 0) * 100 AS decimal(6,2)) AS Percentage,
    m.IsPublished,
    m.Remark,
    t.Id                AS EnteredByTeacherId,
    tu.FullName         AS EnteredByTeacherName,
    m.CreatedAt
FROM dbo.Marks              AS m
INNER JOIN dbo.Students     AS s   ON s.Id   = m.StudentId
INNER JOIN dbo.GradeLevels  AS g   ON g.Id   = s.GradeLevelId
INNER JOIN dbo.Sections     AS sec ON sec.Id = s.SectionId
INNER JOIN dbo.Subjects     AS sub ON sub.Id = m.SubjectId
INNER JOIN dbo.Assessments  AS a   ON a.Id   = m.AssessmentId
INNER JOIN dbo.AssessmentTypes AS atw ON atw.[Name] = a.AssessmentType
INNER JOIN dbo.Teachers     AS t   ON t.Id   = m.EnteredByTeacherId
LEFT  JOIN dbo.AspNetUsers  AS su  ON su.Id  = s.UserId
LEFT  JOIN dbo.AspNetUsers  AS tu  ON tu.Id  = t.UserId;
GO

/* Weighted subject result per student, built from published marks only.
   Each assessment type contributes (earned / possible) * type weight. */
CREATE OR ALTER VIEW dbo.vw_StudentSubjectPerformance
AS
WITH TypeAgg AS
(
    SELECT
        m.StudentId,
        m.SubjectId,
        a.AssessmentType,
        SUM(m.Score)    AS Earned,
        SUM(a.MaxScore) AS Possible
    FROM dbo.Marks             AS m
    INNER JOIN dbo.Assessments AS a ON a.Id = m.AssessmentId
    WHERE m.IsPublished = 1
    GROUP BY m.StudentId, m.SubjectId, a.AssessmentType
),
Weighted AS
(
    SELECT
        t.StudentId,
        t.SubjectId,
        t.AssessmentType,
        CAST(t.Earned / NULLIF(t.Possible, 0) * atw.WeightPercentage AS decimal(6,2)) AS WeightedScore
    FROM TypeAgg AS t
    INNER JOIN dbo.AssessmentTypes AS atw ON atw.[Name] = t.AssessmentType
)
SELECT
    w.StudentId,
    w.SubjectId,
    SUM(CASE WHEN w.AssessmentType = N'Quiz'       THEN w.WeightedScore END) AS QuizScore,
    SUM(CASE WHEN w.AssessmentType = N'Assignment' THEN w.WeightedScore END) AS AssignmentScore,
    SUM(CASE WHEN w.AssessmentType = N'Test'       THEN w.WeightedScore END) AS TestScore,
    SUM(CASE WHEN w.AssessmentType = N'MidExam'    THEN w.WeightedScore END) AS MidExamScore,
    SUM(CASE WHEN w.AssessmentType = N'FinalExam'  THEN w.WeightedScore END) AS FinalExamScore,
    CAST(SUM(w.WeightedScore) AS decimal(6,2)) AS TotalScore,
    CASE
        WHEN SUM(w.WeightedScore) >= 90 THEN N'A'
        WHEN SUM(w.WeightedScore) >= 80 THEN N'B'
        WHEN SUM(w.WeightedScore) >= 70 THEN N'C'
        WHEN SUM(w.WeightedScore) >= 60 THEN N'D'
        ELSE N'F'
    END AS LetterGrade
FROM Weighted AS w
GROUP BY w.StudentId, w.SubjectId;
GO

/* ----------------------------------------------------------------------------
   9. SEED / REFERENCE DATA (idempotent)
   ---------------------------------------------------------------------------- */

/* 9.1 Roles ---------------------------------------------------------------- */
MERGE dbo.AspNetRoles AS tgt
USING (VALUES
    (N'Admin'), (N'Teacher'), (N'Student')
) AS src ([Name])
    ON tgt.NormalizedName = UPPER(src.[Name])
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, [Name], NormalizedName, ConcurrencyStamp)
    VALUES (LOWER(CONVERT(nvarchar(36), NEWID())), src.[Name], UPPER(src.[Name]),
            LOWER(CONVERT(nvarchar(36), NEWID())));
GO

/* 9.2 Assessment types and weights (total = 100%) --------------------------
   This one DOES reassert its values on re-run, unlike the grade and section
   baselines below. The weights are school grading policy with no write endpoint
   behind them, so the script stays their single source of truth and a re-run
   repairs any manual edit that would stop a report card totalling 100%.
   -------------------------------------------------------------------------- */
MERGE dbo.AssessmentTypes AS tgt
USING (VALUES
    (N'Quiz',       N'Quiz',        CAST(10.00 AS decimal(5,2)), 1),
    (N'Assignment', N'Assignment',  CAST(10.00 AS decimal(5,2)), 2),
    (N'Test',       N'Test',        CAST(20.00 AS decimal(5,2)), 3),
    (N'MidExam',    N'Mid Exam',    CAST(30.00 AS decimal(5,2)), 4),
    (N'FinalExam',  N'Final Exam',  CAST(30.00 AS decimal(5,2)), 5)
) AS src ([Name], DisplayName, WeightPercentage, DisplayOrder)
    ON tgt.[Name] = src.[Name]
WHEN MATCHED THEN
    UPDATE SET DisplayName      = src.DisplayName,
               WeightPercentage = src.WeightPercentage,
               DisplayOrder     = src.DisplayOrder
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name], DisplayName, WeightPercentage, DisplayOrder)
    VALUES (src.[Name], src.DisplayName, src.WeightPercentage, src.DisplayOrder);
GO

/* 9.3 Grade levels ---------------------------------------------------------
   Insert-only baseline, keyed on [Level] because 9-12 is fixed by
   CK_GradeLevels_Level and is the one value no API can change. Name, description
   and IsActive are editable through /api/grade-levels, so they are seeded once
   and never reasserted.
   -------------------------------------------------------------------------- */
INSERT INTO dbo.GradeLevels ([Name], [Level], [Description])
SELECT src.[Name], src.[Level], src.[Description]
FROM (VALUES
    (N'Grade 9',  9,  N'Freshman year'),
    (N'Grade 10', 10, N'Sophomore year'),
    (N'Grade 11', 11, N'Junior year'),
    (N'Grade 12', 12, N'Senior year')
) AS src ([Name], [Level], [Description])
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.GradeLevels AS g WHERE g.[Level] = src.[Level]
);
GO

/* 9.4 Sections -------------------------------------------------------------
   Seeded once, on an empty table only. Every column of a section is editable
   through /api/sections - admins may rename it, re-code it (e.g. 'SEC-A'),
   change its capacity or deactivate it - so there is no stable value to match a
   seed row against. Matching on Name or Code would re-insert a duplicate
   'Section A' as soon as an admin had renamed and re-coded the original.
   After installation, sections are owned by the admin UI, not by this script.
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Sections)
BEGIN
    INSERT INTO dbo.Sections ([Name], Code, Capacity)
    VALUES (N'Section A', N'A', 40),
           (N'Section B', N'B', 40),
           (N'Section C', N'C', 40);
END
GO

/* 9.5 Core subject catalogue for every grade ------------------------------- */
INSERT INTO dbo.Subjects (SubjectName, Code, GradeLevelId, [Description], CreditHours)
SELECT
    c.SubjectName,
    c.Prefix + N'-' + CAST(g.[Level] AS nvarchar(2)),
    g.Id,
    c.SubjectName + N' for ' + g.[Name],
    c.CreditHours
FROM dbo.GradeLevels AS g
CROSS JOIN (VALUES
    (N'English',            N'ENG',  4),
    (N'Mathematics',        N'MATH', 5),
    (N'Physics',            N'PHY',  4),
    (N'Chemistry',          N'CHEM', 4),
    (N'Biology',            N'BIO',  4),
    (N'History',            N'HIST', 3),
    (N'Geography',          N'GEO',  3),
    (N'Civics',             N'CIV',  2),
    (N'Information Technology', N'ICT', 3),
    (N'Physical Education', N'PE',   2)
) AS c (SubjectName, Prefix, CreditHours)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Subjects AS s
    WHERE s.GradeLevelId = g.Id AND s.SubjectName = c.SubjectName
);
GO

/* 9.6 System settings ------------------------------------------------------ */
MERGE dbo.SystemSettings AS tgt
USING (VALUES
    (N'SchoolName',           N'Halade High School',      N'Displayed in the portal header and reports'),
    (N'AcademicYear',         N'2026-2027',               N'Active academic year'),
    (N'PassMarkPercentage',   N'50',                      N'Minimum weighted total to pass a subject'),
    (N'AllowSelfRegistration',N'false',                   N'When false only Admins can create accounts'),
    (N'MaxUploadSizeMb',      N'25',                      N'Maximum lesson/resource upload size in MB'),
    (N'ContactEmail',         N'info@haladehighschool.edu',N'Public contact address')
) AS src ([Key], [Value], [Description])
    ON tgt.[Key] = src.[Key]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Key], [Value], [Description]) VALUES (src.[Key], src.[Value], src.[Description]);
GO

/* 9.7 Welcome announcement ------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Announcements)
BEGIN
    INSERT INTO dbo.Announcements (Title, Content, TargetRole, IsPublished, IsPinned)
    VALUES (N'Welcome to the Halade High School Portal',
            N'The new academic year is open. Students can view subjects, lesson materials and report cards here. Teachers can manage marks and upload resources.',
            N'All', 1, 1);
END
GO

/* ---------------------------------------------------------------------------
   Index maintenance for databases created by an earlier run of this script

   The CREATE INDEX statements above only execute when their table is created, so the two
   changes below are repeated here for a database that already exists. Both are shaped by the
   queries the API actually issues; the columns the portal filters on that are not listed here
   (Students.UserId, Students.GradeLevelId, Teachers.UserId, Teachers.IsActive,
   TeacherSubjects.TeacherId, Assessments.SubjectId/SectionId, Marks.AssessmentId) are already
   covered by an index created with their table.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Students', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'dbo.Students')
                     AND name = N'IX_Students_SectionId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Students_SectionId
        ON dbo.Students (SectionId) INCLUDE (GradeLevelId, StudentIdNumber, IsActive);

    PRINT 'Students: created IX_Students_SectionId.';
END
GO

/* Adds IsPublished to the include list so counting an assessment's published marks stays
   inside the index instead of looking each row up in the table. */
IF OBJECT_ID(N'dbo.Marks', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.Marks')
                 AND name = N'IX_Marks_AssessmentId')
   AND NOT EXISTS (SELECT 1
                   FROM sys.index_columns AS ic
                   JOIN sys.columns       AS c ON c.object_id = ic.object_id
                                              AND c.column_id = ic.column_id
                   JOIN sys.indexes       AS i ON i.object_id = ic.object_id
                                              AND i.index_id  = ic.index_id
                   WHERE i.object_id = OBJECT_ID(N'dbo.Marks')
                     AND i.name = N'IX_Marks_AssessmentId'
                     AND c.name = N'IsPublished')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Marks_AssessmentId
        ON dbo.Marks (AssessmentId) INCLUDE (StudentId, Score, IsPublished)
        WITH (DROP_EXISTING = ON);

    PRINT 'Marks: IX_Marks_AssessmentId now includes IsPublished.';
END
GO

/* ----------------------------------------------------------------------------
   NOTE ON THE ADMIN ACCOUNT
   The first Admin user is intentionally NOT seeded here: ASP.NET Core Identity
   password hashes must be produced by PasswordHasher<ApplicationUser> (v3
   format, HMAC-SHA512 + salt). A hand-written hash would never validate.
   The API's DbSeeder (Phase 2) creates the default admin on first start.
   ---------------------------------------------------------------------------- */

/* ----------------------------------------------------------------------------
   10. VERIFICATION SUMMARY
   ---------------------------------------------------------------------------- */
PRINT N'--- HaladeHighSchoolDb: schema created ---';

SELECT t.name AS TableName,
       (SELECT COUNT(*) FROM sys.columns      AS c WHERE c.object_id        = t.object_id) AS ColumnCount,
       (SELECT COUNT(*) FROM sys.foreign_keys AS f WHERE f.parent_object_id = t.object_id) AS ForeignKeyCount,
       (SELECT COUNT(*) FROM sys.indexes      AS i WHERE i.object_id        = t.object_id AND i.type > 0) AS IndexCount
FROM sys.tables AS t
WHERE t.is_ms_shipped = 0
ORDER BY t.name;

SELECT N'GradeLevels' AS SeededTable, COUNT(*) AS RowCountValue FROM dbo.GradeLevels
UNION ALL SELECT N'Sections',        COUNT(*) FROM dbo.Sections
UNION ALL SELECT N'Subjects',        COUNT(*) FROM dbo.Subjects
UNION ALL SELECT N'AssessmentTypes', COUNT(*) FROM dbo.AssessmentTypes
UNION ALL SELECT N'AspNetRoles',     COUNT(*) FROM dbo.AspNetRoles
UNION ALL SELECT N'SystemSettings',  COUNT(*) FROM dbo.SystemSettings;
GO
