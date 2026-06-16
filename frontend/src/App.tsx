import { useEffect, useState } from 'react';
import { CompaniesPage } from './pages/CompaniesPage';
import { DashboardPage } from './pages/DashboardPage';
import { JobsPage } from './pages/JobsPage';
import { MapPage } from './pages/MapPage';
import { ProfilePage } from './pages/ProfilePage';
import { ReportPage } from './pages/ReportPage';

const routes = [
  { path: '/dashboard', label: 'Dashboard' },
  { path: '/map', label: 'Carte' },
  { path: '/companies', label: 'Entreprises' },
  { path: '/jobs', label: 'Offres' },
  { path: '/profile', label: 'Profil' },
  { path: '/report', label: 'Rapport' }
];

export function App() {
  const [path, setPath] = useState(normalizePath(window.location.pathname));

  useEffect(() => {
    if (window.location.pathname === '/') {
      window.history.replaceState({}, '', '/dashboard');
      setPath('/dashboard');
    }

    const onPop = () => setPath(normalizePath(window.location.pathname));
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, []);

  function navigate(event: React.MouseEvent<HTMLAnchorElement>, nextPath: string) {
    event.preventDefault();
    window.history.pushState({}, '', nextPath);
    setPath(nextPath);
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">JR</span>
          <div>
            <strong>Job Radar Local</strong>
            <small>V0.2.0</small>
          </div>
        </div>
        <nav>
          {routes.map((route) => (
            <a key={route.path} href={route.path} className={path === route.path ? 'active' : ''} onClick={(event) => navigate(event, route.path)}>
              {route.label}
            </a>
          ))}
        </nav>
      </aside>
      <main className="content">{renderPage(path)}</main>
    </div>
  );
}

function normalizePath(path: string) {
  return routes.some((route) => route.path === path) ? path : '/dashboard';
}

function renderPage(path: string) {
  switch (path) {
    case '/map':
      return <MapPage />;
    case '/companies':
      return <CompaniesPage />;
    case '/jobs':
      return <JobsPage />;
    case '/profile':
      return <ProfilePage />;
    case '/report':
      return <ReportPage />;
    default:
      return <DashboardPage />;
  }
}
