<#
    Optional demo data for a development database, created entirely through the API so
    every row goes through the same validation a real user would.

    Creates one teacher with three grade 9 assignments, five students in Section A, the
    five weighted assessments per subject, and published marks for all of them.

    Safe to re-run: an account or assessment that already exists is reused instead of
    duplicated. Do not run this against a live school database - the passwords below are
    well known.

    Usage:  pwsh -File database/seed-demo-data.ps1        (with the API running)
#>
param([string]$ApiBaseUrl = 'http://localhost:5006')

$ErrorActionPreference = 'Continue'
$base = $ApiBaseUrl

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body, [string]$Token)
    $headers = @{}
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    $params = @{ Method = $Method; Uri = "$base$Path"; Headers = $headers; ErrorAction = 'Stop' }
    if ($null -ne $Body) {
        $params['Body'] = ($Body | ConvertTo-Json -Depth 6)
        $params['ContentType'] = 'application/json'
    }
    try { return [pscustomobject]@{ Ok = $true; Status = 200; Body = (Invoke-RestMethod @params) } }
    catch {
        $resp = $_.Exception.Response
        $code = if ($resp) { [int]$resp.StatusCode } else { -1 }
        $text = ''
        if ($resp) { try { $text = (New-Object System.IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } catch {} }
        return [pscustomobject]@{ Ok = $false; Status = $code; Body = $text }
    }
}

$TEACHER_EMAIL = 'k.abebe@haladehighschool.edu'
$TEACHER_PASS  = 'Teacher@12345'
$STUDENT_PASS  = 'Student@12345'

Write-Host "--- Admin login ---"
$admin = Invoke-Api POST '/api/auth/login' @{ email = 'admin@haladehighschool.edu'; password = 'Admin@12345' }
if (-not $admin.Ok) { Write-Host "FAILED: $($admin.Body)"; exit 1 }
$at = $admin.Body.accessToken
Write-Host "OK"

$grades = (Invoke-Api GET '/api/grade-levels' -Token $at).Body
$sections = (Invoke-Api GET '/api/sections' -Token $at).Body
$grade9 = $grades | Where-Object { $_.level -eq 9 } | Select-Object -First 1
$secA = $sections | Where-Object { $_.name -eq 'Section A' } | Select-Object -First 1
Write-Host "Grade: $($grade9.name) (id $($grade9.id))   Section: $($secA.name) (id $($secA.id))"

$subjects = (Invoke-Api GET "/api/subjects?gradeLevelId=$($grade9.id)" -Token $at).Body
Write-Host "Subjects in $($grade9.name): $($subjects.Count)"
$chosen = $subjects | Select-Object -First 3
if ($chosen.Count -eq 0) { Write-Host "No subjects seeded for grade 9 - cannot continue."; exit 1 }

Write-Host "`n--- Teacher account ---"
$t = Invoke-Api POST '/api/teachers' @{
    email = $TEACHER_EMAIL; password = $TEACHER_PASS
    fullName = 'Kidist Abebe'; specialization = 'Mathematics and Science'
} -Token $at

if ($t.Ok) {
    $teacherId = $t.Body.teacher.id
    Write-Host "Created teacher id $teacherId ($($t.Body.teacher.employeeId))"
} else {
    $existing = (Invoke-Api GET "/api/teachers?search=Kidist&includeInactive=true" -Token $at).Body
    $teacherId = ($existing.items | Select-Object -First 1).id
    Write-Host "Teacher already exists, id $teacherId"
}

foreach ($s in $chosen) {
    $r = Invoke-Api POST "/api/teachers/$teacherId/assignments" @{ subjectId = $s.id; sectionId = $secA.id } -Token $at
    Write-Host "  assign $($s.code) -> $(if ($r.Ok) {'OK'} else {"HTTP $($r.Status)"})"
}

Write-Host "`n--- Students ---"
$studentNames = @(
    @{ n = 'Abel Tesfaye';    e = 'abel.t@haladehighschool.edu' },
    @{ n = 'Bethel Girma';    e = 'bethel.g@haladehighschool.edu' },
    @{ n = 'Caleb Mekonnen';  e = 'caleb.m@haladehighschool.edu' },
    @{ n = 'Dina Haile';      e = 'dina.h@haladehighschool.edu' },
    @{ n = 'Eyob Solomon';    e = 'eyob.s@haladehighschool.edu' }
)

foreach ($s in $studentNames) {
    $r = Invoke-Api POST '/api/students' @{
        email = $s.e; password = $STUDENT_PASS; fullName = $s.n
        gradeLevelId = $grade9.id; sectionId = $secA.id
    } -Token $at
    Write-Host "  $($s.n): $(if ($r.Ok) {"created $($r.Body.student.studentIdNumber)"} else {"HTTP $($r.Status) (probably exists)"})"
}

Write-Host "`n--- Teacher login ---"
$teacher = Invoke-Api POST '/api/auth/login' @{ email = $TEACHER_EMAIL; password = $TEACHER_PASS }
if (-not $teacher.Ok) { Write-Host "FAILED: $($teacher.Body)"; exit 1 }
$tt = $teacher.Body.accessToken
$classes = (Invoke-Api GET '/api/teachers/me/classes' -Token $tt).Body
Write-Host "Teacher classes: $($classes.Count)"
$classes | ForEach-Object { Write-Host "  $($_.subjectCode) $($_.subjectName) - $($_.gradeLevelName) $($_.sectionName) ($($_.studentCount) students)" }

Write-Host "`n--- Assessments and marks ---"
$plan = @(
    @{ type = 'Quiz';       title = 'Quiz 1';        max = 10 },
    @{ type = 'Assignment'; title = 'Assignment 1';  max = 10 },
    @{ type = 'Test';       title = 'Unit test 1';   max = 15 },
    @{ type = 'MidExam';    title = 'Mid exam';      max = 25 },
    @{ type = 'FinalExam';  title = 'Final exam';    max = 40 }
)

$rnd = New-Object System.Random 20260829

foreach ($class in $classes) {
    Write-Host "  $($class.subjectCode):"
    foreach ($p in $plan) {
        $a = Invoke-Api POST '/api/assessments' @{
            title = "$($p.title) - $($class.subjectCode)"
            assessmentType = $p.type
            maxScore = $p.max
            subjectId = $class.subjectId
            sectionId = $class.sectionId
        } -Token $tt

        if (-not $a.Ok) {
            $found = (Invoke-Api GET "/api/assessments?subjectId=$($class.subjectId)&sectionId=$($class.sectionId)" -Token $tt).Body |
                Where-Object { $_.title -eq "$($p.title) - $($class.subjectCode)" } | Select-Object -First 1
            if (-not $found) { Write-Host "    $($p.title): HTTP $($a.Status) skipped"; continue }
            $assessment = $found
        } else {
            $assessment = $a.Body
        }

        $book = (Invoke-Api GET "/api/marks/assessment/$($assessment.id)" -Token $tt).Body
        $entries = @()
        foreach ($row in $book.rows) {
            # 55% - 98% of the maximum, so some subjects land near the pass mark.
            $pct = $rnd.Next(55, 99) / 100.0
            $entries += @{ studentId = $row.studentId; score = [math]::Round($p.max * $pct, 2) }
        }

        if ($entries.Count -eq 0) { Write-Host "    $($p.title): no students"; continue }

        $m = Invoke-Api POST '/api/marks/bulk' @{
            assessmentId = $assessment.id; entries = $entries; isPublished = $true
        } -Token $tt
        Write-Host "    $($p.title) (out of $($p.max)): $(if ($m.Ok) {"$($m.Body.created) created, $($m.Body.updated) updated, published"} else {"HTTP $($m.Status) $($m.Body)"})"
    }
}

Write-Host "`n--- Student view check ---"
$student = Invoke-Api POST '/api/auth/login' @{ email = $studentNames[0].e; password = $STUDENT_PASS }
if ($student.Ok) {
    $st = $student.Body.accessToken
    $card = (Invoke-Api GET '/api/marks/me/report-card' -Token $st).Body
    Write-Host "Report card for $($card.studentName) ($($card.gradeLevelName) $($card.sectionName)), average $($card.averageTotal)"
    $card.subjects | ForEach-Object {
        Write-Host ("  {0,-22} total {1,6}  grade {2}  {3}" -f $_.subjectName, $_.totalScore, $_.letterGrade, $(if ($_.isPass) {'PASS'} else {'FAIL'}))
    }
} else {
    Write-Host "Student login failed: $($student.Body)"
}

Write-Host "`n=== Credentials for the browser check ==="
Write-Host "  admin@haladehighschool.edu / Admin@12345"
Write-Host "  $TEACHER_EMAIL / $TEACHER_PASS"
Write-Host "  $($studentNames[0].e) / $STUDENT_PASS"
