// AdminApp — the console for whoever runs the installation.
//
// Use:  mounted at /admin/* by App.tsx, above the dealership's session provider
//       so the two never exist at once.
// Edit: the shell here is visually distinct from the dealership's on purpose.
//       During a support visit one browser holds both sets of cookies, and the
//       most valuable thing this screen can do is leave nobody in any doubt
//       about which of the two they are looking at.

import { NavLink, Navigate, Outlet, Route, Routes } from 'react-router';
import { AdminSessionProvider, useAdminSession } from './adminSession';
import { AdminSignIn } from '../features/admin/AdminSignIn';
import { AdminSecondFactorSetup } from '../features/admin/AdminSecondFactorSetup';
import { TenantsPage } from '../features/admin/TenantsPage';
import { SupportAccessPage } from '../features/admin/SupportAccessPage';

export function AdminApp() {
  return (
    <AdminSessionProvider>
      <AdminRoutes />
    </AdminSessionProvider>
  );
}

function AdminRoutes() {
  const { administrator } = useAdminSession();

  if (administrator === undefined) {
    return (
      <main className="state" aria-live="polite">
        <p>Loading…</p>
      </main>
    );
  }

  if (administrator === null) {
    return <AdminSignIn />;
  }

  // Mandatory here, not a policy choice: this account can enter any dealership
  // on the installation. Sign-out stays reachable so nobody is trapped.
  if (administrator.mustEnrolSecondFactor) {
    return (
      <Routes>
        <Route element={<AdminShell restricted />}>
          <Route path="second-factor" element={<AdminSecondFactorSetup />} />
          <Route path="*" element={<Navigate to="second-factor" replace />} />
        </Route>
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<AdminShell />}>
        <Route path="tenants" element={<TenantsPage />} />
        <Route path="support-access" element={<SupportAccessPage />} />
        <Route path="*" element={<Navigate to="tenants" replace />} />
      </Route>
    </Routes>
  );
}

function AdminShell({ restricted = false }: { restricted?: boolean }) {
  const { administrator, signOut } = useAdminSession();

  return (
    <div className="shell shell--admin">
      <a className="skip" href="#main">
        Skip to content
      </a>

      <header className="shell__bar">
        <span className="shell__brand">
          OpenDealer360 <span className="shell__badge">Administration</span>
        </span>

        {restricted ? null : (
          <nav aria-label="Main">
            <NavLink to="tenants">Dealerships</NavLink>
            <NavLink to="support-access">Support access</NavLink>
          </nav>
        )}

        <div className="shell__right">
          <span className="shell__tenant">{administrator?.email}</span>
          <button type="button" onClick={() => void signOut()}>
            Sign out
          </button>
        </div>
      </header>

      <main id="main" className="shell__main" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  );
}
