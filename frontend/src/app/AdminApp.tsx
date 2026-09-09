// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AdminApp — the console for whoever runs the installation.
//
// Usage:
//   Mounted at /admin/* by App.tsx, above the dealership's session provider
//   so the two never exist at once.
//
// Coding Instructions:
//   The shell here is visually distinct from the dealership's on purpose.
//   During a support visit one browser holds both sets of cookies, and the
//   most valuable thing this screen can do is leave nobody in any doubt
//   about which of the two they are looking at.

import { NavLink, Navigate, Outlet, Route, Routes } from 'react-router';
import { useI18n } from '../shared/i18n';
import { AdminSessionProvider, useAdminSession } from './adminSession';
import { AdminSignIn } from '../features/admin/AdminSignIn';
import { AdminSecondFactorSetup } from '../features/admin/AdminSecondFactorSetup';
import { TenantsPage } from '../features/admin/TenantsPage';
import { SupportAccessPage } from '../features/admin/SupportAccessPage';
import { AppearanceControls } from './AppearanceControls';
import { Mark, Wordmark } from './Mark';

export function AdminApp() {
  return (
    <AdminSessionProvider>
      <AdminRoutes />
    </AdminSessionProvider>
  );
}

function AdminRoutes() {
  const { administrator } = useAdminSession();
  const { t } = useI18n();

  if (administrator === undefined) {
    return (
      <main className="state" aria-live="polite">
        <p>{t('common.loading')}</p>
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
  const { t } = useI18n();

  return (
    <div className="shell shell--admin">
      <a className="skip" href="#main">
        {t('shell.skipToContent')}
      </a>

      <header className="shell__bar">
        <span className="shell__brand">
          <Mark />
          <Wordmark /> <span className="shell__badge">{t('admin.badge')}</span>
        </span>

        {restricted ? null : (
          <nav aria-label={t('shell.mainNavigation')}>
            <NavLink to="tenants">{t('admin.navDealerships')}</NavLink>
            <NavLink to="support-access">{t('admin.navSupportAccess')}</NavLink>
          </nav>
        )}

        <div className="shell__right">
          <AppearanceControls />
          {/* An email address is a record, and its parts read left to right even
              inside a mirrored bar. */}
          <span className="shell__tenant" dir="ltr">
            {administrator?.email}
          </span>
          <button type="button" onClick={() => void signOut()}>
            {t('shell.signOut')}
          </button>
        </div>
      </header>

      <main id="main" className="shell__main" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  );
}
