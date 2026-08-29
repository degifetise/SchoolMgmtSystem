import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { homeRouteFor, useAuth } from '../context/AuthContext';
import { LoadingPanel } from '../components/ui/Feedback';

/**
 * Gate for authenticated areas. `allowedRoles` omitted means "any signed-in user".
 *
 * A signed-in user who lacks the role is sent to their own home page rather than to
 * a dead end, because the only way to hit this is a hand-typed URL or a stale bookmark.
 */
export default function ProtectedRoute({ allowedRoles }) {
  const { isAuthenticated, initialising, roles } = useAuth();
  const location = useLocation();

  // Deciding before /api/auth/me resolves would bounce a valid session to the login screen.
  if (initialising) {
    return (
      <div className="grid min-h-screen place-items-center bg-slate-100 p-6">
        <LoadingPanel label="Restoring your session…" />
      </div>
    );
  }

  if (!isAuthenticated) {
    // `state.from` lets the login screen return the user to where they were heading.
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (allowedRoles?.length && !allowedRoles.some((role) => roles.includes(role))) {
    return <Navigate to={homeRouteFor(roles)} replace />;
  }

  return <Outlet />;
}
