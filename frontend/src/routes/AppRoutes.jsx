import { Navigate, Route, Routes } from 'react-router-dom';
import { ROLES } from '../context/AuthContext';
import ProtectedRoute from './ProtectedRoute';
import PublicLayout from '../components/layout/PublicLayout';
import DashboardLayout from '../components/layout/DashboardLayout';
import HomePage from '../pages/HomePage';
import AboutPage from '../pages/AboutPage';
import EventsPage from '../pages/EventsPage';
import LoginPage from '../pages/LoginPage';
import SettingsPage from '../pages/SettingsPage';
import NotFoundPage from '../pages/NotFoundPage';
import AdminStudents from '../pages/admin/AdminStudents';
import AdminAccounts from '../pages/admin/AdminAccounts';
import TeacherStudents from '../pages/teacher/TeacherStudents';
import EnterMarks from '../pages/teacher/EnterMarks';
import MyResults from '../pages/student/MyResults';

export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      {/* Anyone, signed in or not. Home is a real landing page rather than a redirect, so a
          visitor can read about the school before being asked for a password. */}
      <Route element={<PublicLayout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/about" element={<AboutPage />} />
        <Route path="/events" element={<EventsPage />} />
      </Route>

      <Route element={<ProtectedRoute />}>
        <Route element={<DashboardLayout />}>
          {/* Every role: the page shows their own account, and the school-wide settings to an
              administrator, which is why the navigation bar needs only one Settings link. */}
          <Route path="/settings" element={<SettingsPage />} />

          <Route element={<ProtectedRoute allowedRoles={[ROLES.admin]} />}>
            <Route path="/admin/students" element={<AdminStudents />} />
            <Route path="/admin/accounts" element={<AdminAccounts />} />
            {/* Kept alive so older links and bookmarks still land somewhere useful. */}
            <Route path="/admin" element={<Navigate to="/admin/students" replace />} />
            <Route path="/admin/settings" element={<Navigate to="/settings" replace />} />
          </Route>

          {/* Teacher only: both pages are driven by /api/teachers/me/..., which resolves the
              caller's own teaching load and is not available to an admin token. */}
          <Route element={<ProtectedRoute allowedRoles={[ROLES.teacher]} />}>
            <Route path="/teacher/students" element={<TeacherStudents />} />
            {/* Absent from the navigation bar by design: reached from a class list, which
                passes the class along in the query string. */}
            <Route path="/teacher/marks" element={<EnterMarks />} />
          </Route>

          <Route element={<ProtectedRoute allowedRoles={[ROLES.student]} />}>
            <Route path="/student/results" element={<MyResults />} />
          </Route>
        </Route>
      </Route>

      {/* Outside the protected tree: an unknown URL should say so, not demand a sign-in. */}
      <Route element={<PublicLayout />}>
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
