using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Data;

/*  ===========================================================================
    DEVELOPMENT SEED CREDENTIALS
    ===========================================================================
    Every login this file creates is listed here. Nothing else in the codebase
    creates an account on start-up, so this block is the complete picture.

      Role      Email                              Password        Source
      -------   --------------------------------   -------------   ----------------------
      Admin     admin@haladehighschool.edu         Admin@12345     SeedAdmin, all envs
      Teacher   k.abebe@haladehighschool.edu        Teacher@12345   SeedDemoAccounts, dev
      Student   abel.t@haladehighschool.edu         Student@12345   SeedDemoAccounts, dev

    The values come from configuration, never from constants in this file:
      SeedAdmin        - appsettings.json,             created in every environment
      SeedDemoAccounts - appsettings.Development.json, created in Development only

    Two safeguards keep these out of a real deployment:
      1. The demo cohort is skipped unless IHostEnvironment.IsDevelopment().
      2. Its configuration section lives only in appsettings.Development.json, so
         even a mis-set environment name finds nothing to create.

    Change the administrator password before the first production start-up, and
    prefer a user secret or environment variable over appsettings.json:
      dotnet user-secrets set "SeedAdmin:Password" "<strong-password>"

    See README_CREDENTIALS.md for the full reference, including the accounts that
    database/seed-demo-data.ps1 adds on top of these.
    =========================================================================== */

/// <summary>
/// Creates the artefacts the Phase 1 T-SQL script cannot: Identity password hashes must be
/// produced by <see cref="IPasswordHasher{TUser}"/>, so accounts are created here on
/// start-up instead. Idempotent - an account that already exists is left untouched.
/// </summary>
public static class DbSeeder
{
    /// <summary>One seeded login, for the Development credential summary.</summary>
    private sealed record SeededAccount(string Role, string Email, string Password, bool WasCreated);

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await SeedRolesAsync(roleManager, logger);

        var accounts = new List<SeededAccount>();

        if (await SeedAdminAsync(userManager, configuration, logger) is { } admin)
        {
            accounts.Add(admin);
        }

        // Demo teacher and student, so a fresh clone can sign in as all three roles.
        if (environment.IsDevelopment())
        {
            accounts.AddRange(await SeedDemoAccountsAsync(services, configuration, userManager, logger, cancellationToken));
            LogCredentialSummary(accounts, environment, logger);
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created missing role {Role}", role);
            }
        }
    }

    private static async Task<SeededAccount?> SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        var fullName = configuration["SeedAdmin:FullName"] ?? "System Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SeedAdmin configuration is missing; no administrator was created.");
            return null;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return new SeededAccount(Roles.Admin, email, password, WasCreated: false);
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Failed to create the seed administrator: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return null;
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Seeded administrator account {Email}", email);

        return new SeededAccount(Roles.Admin, email, password, WasCreated: true);
    }

    /// <summary>
    /// Development-only teacher and student. Both go through
    /// <see cref="IAccountProvisioningService"/> so they get the same generated employee and
    /// student numbers, transaction handling and validation as an account created by an admin.
    /// </summary>
    private static async Task<List<SeededAccount>> SeedDemoAccountsAsync(
        IServiceProvider services,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var seeded = new List<SeededAccount>();
        var section = configuration.GetSection("SeedDemoAccounts");

        if (!section.Exists() || !section.GetValue("Enabled", false))
        {
            logger.LogInformation("SeedDemoAccounts is absent or disabled; only the administrator was seeded.");
            return seeded;
        }

        var teacherEmail = section["Teacher:Email"];
        var studentEmail = section["Student:Email"];

        // Per-role passwords, falling back to a shared one so either style of config works.
        var teacherPassword = section["Teacher:Password"] ?? section["Password"];
        var studentPassword = section["Student:Password"] ?? section["Password"];

        if (string.IsNullOrWhiteSpace(teacherEmail) ||
            string.IsNullOrWhiteSpace(studentEmail) ||
            string.IsNullOrWhiteSpace(teacherPassword) ||
            string.IsNullOrWhiteSpace(studentPassword))
        {
            logger.LogWarning("SeedDemoAccounts is incomplete; no demo accounts were created.");
            return seeded;
        }

        var provisioning = services.GetRequiredService<IAccountProvisioningService>();

        // ---- Teacher -------------------------------------------------------
        if (await userManager.FindByEmailAsync(teacherEmail) is not null)
        {
            seeded.Add(new SeededAccount(Roles.Teacher, teacherEmail, teacherPassword, WasCreated: false));
        }
        else
        {
            var result = await provisioning.CreateTeacherAsync(new ProvisionTeacherRequest
            {
                Email = teacherEmail,
                Password = teacherPassword,
                FullName = section["Teacher:FullName"] ?? "Demo Teacher",
                Specialization = section["Teacher:Specialization"]
            }, cancellationToken);

            if (result.Succeeded)
            {
                logger.LogInformation("Seeded demo teacher {Email}", teacherEmail);
                seeded.Add(new SeededAccount(Roles.Teacher, teacherEmail, teacherPassword, WasCreated: true));
            }
            else
            {
                logger.LogWarning(
                    "Could not seed the demo teacher: {Errors}",
                    string.Join("; ", result.Errors));
            }
        }

        // ---- Student -------------------------------------------------------
        if (await userManager.FindByEmailAsync(studentEmail) is not null)
        {
            seeded.Add(new SeededAccount(Roles.Student, studentEmail, studentPassword, WasCreated: false));
            return seeded;
        }

        var db = services.GetRequiredService<ApplicationDbContext>();

        /* A student needs a class. Both are matched on the values no API can change -
           the lowest grade Level and the alphabetically first section Code - because an
           admin may have renamed 'Grade 9' or 'Section A' by now. */
        var gradeLevelId = await db.GradeLevels
            .Where(g => g.IsActive)
            .OrderBy(g => g.Level)
            .Select(g => (int?)g.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var sectionId = await db.Sections
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (gradeLevelId is null || sectionId is null)
        {
            logger.LogWarning(
                "No active grade level or section exists, so the demo student was skipped. " +
                "Run database/01_Create_HaladeHighSchoolDb.sql to seed the academic structure.");
            return seeded;
        }

        var studentResult = await provisioning.CreateStudentAsync(new ProvisionStudentRequest
        {
            Email = studentEmail,
            Password = studentPassword,
            FullName = section["Student:FullName"] ?? "Demo Student",
            GradeLevelId = gradeLevelId.Value,
            SectionId = sectionId.Value
        }, cancellationToken);

        if (studentResult.Succeeded)
        {
            logger.LogInformation("Seeded demo student {Email}", studentEmail);
            seeded.Add(new SeededAccount(Roles.Student, studentEmail, studentPassword, WasCreated: true));
        }
        else
        {
            logger.LogWarning(
                "Could not seed the demo student: {Errors}",
                string.Join("; ", studentResult.Errors));
        }

        return seeded;
    }

    /// <summary>
    /// Prints the sign-in details to the console. Called only when the host environment is
    /// Development, because it deliberately writes passwords in clear text.
    /// </summary>
    private static void LogCredentialSummary(
        List<SeededAccount> accounts,
        IHostEnvironment environment,
        ILogger logger)
    {
        if (accounts.Count == 0)
        {
            return;
        }

        var rows = accounts
            .OrderBy(a => Array.IndexOf(Roles.All, a.Role))
            .Select(a => string.Format(
                "  {0,-8} {1,-34} {2,-15} {3}",
                a.Role,
                a.Email,
                a.Password,
                a.WasCreated ? "created now" : "already existed"));

        logger.LogWarning(
            """
            ==========================================================================
             DEVELOPMENT SEED CREDENTIALS - {Environment} environment
             Passwords are printed in clear text and are never logged outside
             Development. A password shown for an account that already existed is the
             configured value, which is wrong if someone has since changed it.
            --------------------------------------------------------------------------
              ROLE     EMAIL                              PASSWORD        STATUS
            {Rows}
            --------------------------------------------------------------------------
             Reference: README_CREDENTIALS.md
             Classes, assessments and marks: database/seed-demo-data.ps1
            ==========================================================================
            """,
            environment.EnvironmentName,
            string.Join(Environment.NewLine, rows));
    }
}
