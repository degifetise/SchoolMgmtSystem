# Halade High School Portal — frontend

React 18 + Vite 6 + Tailwind CSS v4 client for the Halade High School Portal API.
Lucide React supplies the icons and Framer Motion the transitions.

## Prerequisites

1. `HaladeHighSchoolDb` created by `database/01_Create_HaladeHighSchoolDb.sql`.
2. The API running on <http://localhost:5006> (`dotnet run` in `backend/HaladeHighSchool.Api`).
   Its CORS policy whitelists `http://localhost:5173`, so the Vite port is pinned in
   `vite.config.js`; change both together if you move it.

## Running

```bash
npm install
cp .env.example .env.local   # then edit VITE_API_BASE_URL if the API is elsewhere
npm run dev                  # http://localhost:5173
npm run build                # production bundle in dist/
```

## Structure

| Path | Responsibility |
| --- | --- |
| `src/services/api.js` | Axios instance, bearer-token attachment, single-flight 401 refresh, error flattening |
| `src/services/endpoints.js` | Every API route the UI calls, grouped by resource |
| `src/context/AuthContext.jsx` | Session state, login/logout, JWT decoding and expiry checks |
| `src/context/SchoolInfoContext.jsx` | Anonymous school name / academic year for the header and login screen |
| `src/routes/AppRoutes.jsx` | Route table; `ProtectedRoute` enforces the role on each branch |
| `src/hooks/useApiResource.js` | Shared loading / error / retry state for a fetch, used by every read-only page |
| `src/components/layout/TopNavBar.jsx` | The one navigation bar: brand, role links, profile badges, sign out, mobile drawer |
| `src/components/layout/PublicLayout.jsx` | Shell for the pages anyone can read |
| `src/components/layout/DashboardLayout.jsx` | Shell for the signed-in areas, adds the page heading strip |
| `src/pages/HomePage.jsx` | Public landing page: live figures, grading policy, latest notices |
| `src/pages/AboutPage.jsx` | Public profile: curriculum per grade, grading weights, contact details |
| `src/pages/EventsPage.jsx` | Public noticeboard from the school-wide announcements |
| `src/pages/admin` | Dashboard metrics, student and teacher management, school settings |
| `src/pages/teacher/EnterMarks.jsx` | Class → assessment → bulk score entry and publishing |
| `src/pages/student/MyResults.jsx` | Weighted report card, GPA and per-component breakdown |

## Routes

| Route | Who | Data source |
| --- | --- | --- |
| `/` | Anyone | `GET /api/public/overview`, `GET /api/public/events?take=3` |
| `/about` | Anyone | `GET /api/public/overview` |
| `/events` | Anyone | `GET /api/public/events?take=50` |
| `/login` | Anyone | `GET /api/public/overview` for the grading panel |
| `/admin`, `/admin/settings` | Admin | grade levels, sections, student summary, teachers, system settings |
| `/teacher/marks` | Teacher | `GET /api/teachers/me/classes`, assessments, gradebook |
| `/student/results` | Student | `GET /api/marks/me/report-card`, `GET /api/marks/weights` |

Nothing is hard-coded from the school's data: the name, academic year, contact address, grade
list, subject counts and grading weights are all read from the API, so changing them in
**Settings** changes every page, including the ones a visitor sees without signing in.

## Authentication

`POST /api/auth/login` returns an access token, a rotating refresh token and the user
profile; all three go to `localStorage` under the `halade.` prefix. On a 401 the Axios
interceptor refreshes once and replays the original request. Concurrent 401s share one
refresh, because the API rotates the refresh token and parallel attempts would invalidate
each other. When the refresh fails the session is cleared and the login screen explains why.

Roles come from the profile's `roles` array and decide both the navigation entries and the
routes `ProtectedRoute` will admit. Signing in goes to the role's own landing page: the admin
console, mark entry or results. `/` stays a public page for everyone, and offers a signed-in
visitor a link straight to their dashboard.

## Loading and error states

Every read-only page runs through `useApiResource`, which exposes `loading`, `error` and a
`reload`. Pages show skeletons shaped like the content they replace rather than a bare spinner,
and a failure that leaves nothing to display renders `ErrorState` with a working **Try again**
button. A failed request is never presented as an empty result — that distinction matters most
on mark entry, where "no classes assigned" and "could not reach the API" mean very different
things to a teacher.

## Demo data

`database/seed-demo-data.ps1` creates a teacher, five students, the five weighted
assessments per subject and published marks, all through the API. Development only — the
passwords in it are well known.

Sign-in details for every seeded account are in [`README_CREDENTIALS.md`](../README_CREDENTIALS.md);
the API also prints them to its console on start-up when it runs in Development.

## Checking the backend

`GET /api/health/db-check` needs no token and reports connectivity, the active academic year
and whether the grading policy still totals 100%. It answers `200` when the database is
reachable and `503` when it is not, which makes it usable as a probe.
