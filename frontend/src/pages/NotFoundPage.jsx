import { Link } from 'react-router-dom';
import { Compass } from 'lucide-react';
import { homeRouteFor, useAuth } from '../context/AuthContext';
import { EmptyState } from '../components/ui/Feedback';

export default function NotFoundPage() {
  const { roles, isAuthenticated } = useAuth();

  // A signed-out visitor is sent to the public home page, not to a dashboard they cannot open.
  const target = isAuthenticated ? homeRouteFor(roles) : '/';

  return (
    <div className="mx-auto max-w-3xl px-4 py-16 sm:px-6 lg:px-8">
      <EmptyState
        icon={Compass}
        title="Page not found"
        description="That address does not exist in the portal, or your role cannot reach it."
        action={
          <Link to={target} className="btn-primary">
            {isAuthenticated ? 'Back to my dashboard' : 'Back to home'}
          </Link>
        }
      />
    </div>
  );
}
