import { Outlet } from 'react-router-dom';
import TopNavBar from './TopNavBar';
import SiteFooter from './SiteFooter';

/**
 * Shell for the pages anyone can read. Identical chrome to the signed-in areas, so following
 * a link from Home into a dashboard does not feel like arriving at a different site.
 */
export default function PublicLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      <TopNavBar />
      <main className="flex-1">
        <Outlet />
      </main>
      <SiteFooter />
    </div>
  );
}
