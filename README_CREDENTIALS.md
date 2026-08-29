# Development seed credentials

Every account the Halade High School Portal creates for you, where it comes from, and how to
change it. **Development only** — see [Before deploying](#before-deploying).

## Accounts

| Role | Email | Password | Created by | Environments |
| --- | --- | --- | --- | --- |
| Admin | `admin@haladehighschool.edu` | `Admin@12345` | `DbSeeder` ← `SeedAdmin` | All |
| Teacher | `k.abebe@haladehighschool.edu` | `Teacher@12345` | `DbSeeder` ← `SeedDemoAccounts` | Development |
| Student | `abel.t@haladehighschool.edu` | `Student@12345` | `DbSeeder` ← `SeedDemoAccounts` | Development |

All three are created on API start-up and are idempotent: an account that already exists is
left exactly as it is, including its password.

Seeded accounts sign in straight away. An application from the public registration form on the
login screen has no account at all until it is approved: the form takes a name, an email address
and a class, and stores that in `StudentRegistrationRequests` as `Pending`. No login and no
`Students` row exist yet, so trying to sign in reports an incorrect email or password because
there is genuinely nothing to sign in to.

An administrator works the queue under **Accounts → Registration approvals**. Approving generates
the student number (`HHS-{year}-{sequence}`), a school sign-in address derived from it
(`hhs-2026-0007@haladehighschool.edu`, domain from `Provisioning:StudentEmailDomain`) and a
temporary password, then provisions the login and the student record in one transaction. **The
temporary password is shown once, in the panel that appears after approving** - it is stored only
as a hash, so if it is lost the only way back is a password reset. Send it, with the sign-in
address, to the applicant's own address. Declining provisions nothing, and the applicant is free
to apply again.

Password changes go through `POST /api/account/change-password` and every attempt, successful or
not, is recorded in `PasswordChangeLogs` with the outcome, the reason it was refused, the caller's
IP address and user agent.

`database/seed-demo-data.ps1` optionally adds four more students
(`bethel.g@`, `caleb.m@`, `dina.h@`, `eyob.s@haladehighschool.edu`, all `Student@12345`) plus
the teaching assignments, assessments and published marks that make the dashboards worth
looking at. Run it after the API is up:

```powershell
pwsh -File database/seed-demo-data.ps1
```

## Where the values live

| Setting | File | Notes |
| --- | --- | --- |
| `SeedAdmin:Email` / `:Password` / `:FullName` | `backend/HaladeHighSchool.Api/appsettings.json` | Created in every environment |
| `SeedDemoAccounts:Enabled` | `appsettings.Development.json` | Set `false` to skip the demo cohort |
| `SeedDemoAccounts:Teacher:*` | `appsettings.Development.json` | Email, password, name, specialization |
| `SeedDemoAccounts:Student:*` | `appsettings.Development.json` | Email, password, name |

Nothing is hard-coded in `DbSeeder.cs`; it only reads configuration. The demo student is
placed in the lowest active grade level and the first active section, matched on `Level` and
`Code` rather than on name, so a renamed "Grade 9" or "Section A" does not break start-up.

## Seeing them at start-up

When the host environment is Development, `DbSeeder` writes a summary table to the console:

```text
==========================================================================
 DEVELOPMENT SEED CREDENTIALS - Development environment
 ...
  ROLE     EMAIL                              PASSWORD        STATUS
  Admin    admin@haladehighschool.edu         ...             already existed
  Teacher  k.abebe@haladehighschool.edu       ...             created now
  Student  abel.t@haladehighschool.edu        ...             created now
--------------------------------------------------------------------------
```

It is written at `Warning` level so it survives a raised minimum log level, and it is guarded
by `IHostEnvironment.IsDevelopment()`, so no password is ever written to a deployed log. A
password shown next to `already existed` is the configured value, which will be wrong if
somebody has since changed that account's password through the portal.

## When a documented password stops working

The seeder never touches an account that already exists, so if somebody changes a password
through the portal — including with **Reset password** on the Admin Console, which issues a
random one — the table above is out of date for that account and the seeder cannot know it.

Two ways back:

- **Reset it again** from the Admin Console. The temporary password is shown once, on screen,
  and never returned by the API a second time.
- **Let the seeder rebuild it.** Delete the account from the Admin Console and restart the API;
  it is recreated with the configured password. Deletion is refused for anybody carrying marks
  or teaching history, so this only works for an account with no record attached to it.

> As of the last run, `abel.t@haladehighschool.edu` no longer accepts `Student@12345` — its
> password was changed inside the portal. The other demo students seeded by
> `seed-demo-data.ps1` still use `Student@12345`.

## Two safeguards

1. `DbSeeder` skips the demo cohort unless `IHostEnvironment.IsDevelopment()`.
2. `SeedDemoAccounts` exists only in `appsettings.Development.json`, so even a mis-set
   `ASPNETCORE_ENVIRONMENT` finds no section to act on.

The administrator is deliberately not behind that guard: every environment needs one account
to bootstrap from.

## Before deploying

1. Move the administrator password out of `appsettings.json`:

```bash
dotnet user-secrets set "SeedAdmin:Password" "<strong-password>"
# or
setx SeedAdmin__Password "<strong-password>"
```

2. Sign in as the administrator and change the password through the portal, then confirm
   `GET /api/system-settings` still works with the new one.
3. Replace `Jwt:Key` in `appsettings.json` with a 32-byte-plus secret from a secret store.
4. Check the start-up log for the credential table. If you can see it, the app is running in
   Development and should not be serving real users.
