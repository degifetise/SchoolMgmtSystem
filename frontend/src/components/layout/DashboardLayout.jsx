import { Outlet, useLocation } from 'react-router-dom';
import TopNavBar from './TopNavBar';
import SiteFooter from './SiteFooter';
import { useAuth } from '../../context/AuthContext';
import { pageHeadingFor } from './navigation';

/**
 * Shell for the signed-in areas. Shares TopNavBar with the public pages and adds a page
 * heading strip, so each dashboard states where you are without repeating the title itself.
 */
export default function DashboardLayout() {
  const { roles } = useAuth();
  const location = useLocation();

  const current = pageHeadingFor(location.pathname, roles);

  return (
    <div className="flex min-h-screen flex-col">
      <TopNavBar />

      {current && (
        <div className="border-b border-slate-200 bg-white">
          <div className="mx-auto max-w-7xl px-4 py-3 sm:px-6 lg:px-8">
            <p className="text-sm font-semibold text-slate-800">{current.label}</p>
            {current.description && <p className="text-xs text-slate-500">{current.description}</p>}
          </div>
        </div>
      )}

      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6 lg:px-8">
        <Outlet />
      </main>

      <SiteFooter />
    </div>
  );
}
