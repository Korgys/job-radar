import { useEffect, useState } from 'react';
import { CompaniesPage } from './pages/CompaniesPage';
import { DashboardPage } from './pages/DashboardPage';
import { JobsPage } from './pages/JobsPage';
import { MapPage } from './pages/MapPage';
import { ProfilePage } from './pages/ProfilePage';
import { ReportPage } from './pages/ReportPage';

const routes = [
  { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
  { path: '/map', label: 'Carte', icon: 'map' },
  { path: '/companies', label: 'Entreprises', icon: 'companies' },
  { path: '/jobs', label: 'Offres', icon: 'jobs' },
  { path: '/profile', label: 'Profil', icon: 'profile' },
  { path: '/report', label: 'Rapport', icon: 'report' }
];

export function App() {
  const [path, setPath] = useState(normalizePath(window.location.pathname));
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

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
    <div className={`app-shell ${sidebarCollapsed ? 'sidebar-collapsed' : ''}`}>
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">JR</span>
          <div className="brand-text">
            <strong>Job Radar Local</strong>
            <small>V0.2.0</small>
          </div>
        </div>
        <button
          type="button"
          className="sidebar-toggle"
          onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
          aria-label={sidebarCollapsed ? 'Afficher le menu complet' : 'Réduire le menu'}
          title={sidebarCollapsed ? 'Afficher le menu complet' : 'Réduire le menu'}
        >
          <Icon name={sidebarCollapsed ? 'expand' : 'collapse'} />
        </button>
        <nav>
          {routes.map((route) => (
            <a key={route.path} href={route.path} className={path === route.path ? 'active' : ''} onClick={(event) => navigate(event, route.path)} title={route.label}>
              <Icon name={route.icon} />
              <span>{route.label}</span>
            </a>
          ))}
        </nav>
      </aside>
      <main className="content">{renderPage(path)}</main>
    </div>
  );
}

function Icon({ name }: { name: string }) {
  const paths: Record<string, string> = {
    dashboard: 'M4 13h7V4H4v9Zm9 7h7V4h-7v16ZM4 20h7v-5H4v5Z',
    map: 'M9 18 3 20V6l6-2 6 2 6-2v14l-6 2-6-2Zm0-2 6 2V8L9 6v10Z',
    companies: 'M4 20V6l8-3 8 3v14h-5v-6H9v6H4Zm5-9h2V8H9v3Zm4 0h2V8h-2v3Z',
    jobs: 'M8 6V4h8v2h4v14H4V6h4Zm2 0h4V5h-4v1Zm-4 5h12V8H6v3Z',
    profile: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm-7 8a7 7 0 0 1 14 0H5Z',
    report: 'M6 3h9l3 3v15H6V3Zm8 1.5V7h2.5L14 4.5ZM8 11h8V9H8v2Zm0 4h8v-2H8v2Zm0 4h5v-2H8v2Z',
    collapse: 'M15.5 5 8.5 12l7 7-1.5 1.5L5.5 12 14 3.5 15.5 5Z',
    expand: 'M8.5 5 15.5 12l-7 7L10 20.5l8.5-8.5L10 3.5 8.5 5Z'
  };

  return (
    <svg aria-hidden="true" className="nav-icon" viewBox="0 0 24 24" focusable="false">
      <path d={paths[name]} />
    </svg>
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
