// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   App — routing and the authenticated shell.
//
// Usage:
//   Rendered by main.tsx; owns every route.
//
// Coding Instructions:
//   The guard here is a convenience, not a control. Every screen calls an
//   API that enforces the same rules server-side, and it is the server's
//   answer that matters. Hiding a link protects nothing.

// Imported from react-router rather than react-router-dom: in v8 the DOM
// bindings moved into the main package and react-router-dom is a shim. The
// version matters — 7.12 to 8.2 carry a CSRF advisory (GHSA-qwww-vcr4-c8h2).
import { useState } from 'react';
import {
  NavLink,
  Navigate,
  Outlet,
  Route,
  BrowserRouter as Router,
  Routes,
  useLocation,
  useNavigate,
} from 'react-router';
import { SessionProvider, useSession } from './session';
import { AppearanceProvider } from '../shared/appearance';
import { I18nProvider, useI18n } from '../shared/i18n';
import { AppearanceControls } from './AppearanceControls';
import { Mark, Wordmark } from './Mark';
import { NavGroup } from './NavGroup';
import { ShortcutsPanel } from './Shortcuts';
import { useHotkeys } from '../shared/useHotkeys';
import { DashboardPage } from '../features/dashboard/DashboardPage';
import { SignIn } from '../features/auth/SignIn';
import { InventoryPage } from '../features/inventory/InventoryPage';
import { TrialBalancePage } from '../features/accounting/TrialBalancePage';
import { SecondFactorSetup } from '../features/auth/SecondFactorSetup';
import { RecordsPage } from '../features/migration/RecordsPage';
import { CustomersPage } from '../features/customers/CustomersPage';
import { LeadsPage } from '../features/leads/LeadsPage';
import { DealsPage } from '../features/deals/DealsPage';
import { StaffPage } from '../features/staff/StaffPage';
import { WorkshopPage } from '../features/service/WorkshopPage';
import { LabourPage } from '../features/service/LabourPage';
import { ServiceSetupPage } from '../features/service/ServiceSetupPage';
import { PasskeysPage } from '../features/auth/PasskeysPage';
import { PartsPage } from '../features/parts/PartsPage';
import { PeriodsPage } from '../features/accounting/PeriodsPage';
import { ReportsPage } from '../features/accounting/ReportsPage';
import { AgeingPage } from '../features/receivables/AgeingPage';
import { StatementPage } from '../features/receivables/StatementPage';
import { SetFirstPassword } from '../features/auth/SetFirstPassword';
import { RecoverPassword } from '../features/auth/RecoverPassword';
import { AdminApp } from './AdminApp';

export function App() {
  return (
    // Outside the router and both sessions: the theme and the language apply to
    // the sign-in screen and the administration console as much as to the
    // shell, and a choice made on one must not be forgotten by the next.
    // Language is outermost because the sign-in screen has words on it before
    // anybody has a session to have a preference attached to.
    <I18nProvider>
      <AppearanceProvider>
        <Router>
          <Routes>
            {/* The split is above SessionProvider on purpose. An administrator
                has no dealership, so asking /auth/me on their behalf would be a
                meaningless question — and mounting both session contexts at
                once would make it possible to write a screen that does not know
                which of the two identities it is holding. */}
            <Route path="/admin/*" element={<AdminApp />} />
            <Route
              path="*"
              element={
                <SessionProvider>
                  <AppRoutes />
                </SessionProvider>
              }
            />
          </Routes>
        </Router>
      </AppearanceProvider>
    </I18nProvider>
  );
}

function AppRoutes() {
  const { user } = useSession();
  const { t } = useI18n();

  // Undefined means "still asking the server". Rendering the sign-in form here
  // would flash it at somebody who is already signed in.
  if (user === undefined) {
    return (
      <main className="state" aria-live="polite">
        <p>{t('common.loading')}</p>
      </main>
    );
  }

  if (user === null) {
    return (
      <Routes>
        <Route path="/sign-in" element={<SignIn />} />
        {/*
          A starter has no password, so they can never reach a signed-in route.
          This is the one place they can get to, and it has to live out here with
          sign-in rather than behind the session.
        */}
        <Route path="/set-password" element={<SetFirstPassword />} />
        {/* Anonymous by necessity, like /set-password: not having a session is
            the state this screen exists to fix. */}
        <Route path="/recover" element={<RecoverPassword />} />

        {/* Sign-in is rendered WHERE THEY ASKED TO BE, rather than by
            redirecting to /sign-in.

            This used to be `<Navigate to="/sign-in" replace />`, which threw the
            requested address away: signing in then landed on the dashboard. It
            did not matter while every record lived in component state, because
            no address named one. It matters now — the first thing a shared link
            meets is often somebody who is signed out or whose session lapsed
            overnight, and dropping them on the dashboard is exactly the failure
            "send them the link" was meant to end.

            Keeping the address is also the version with no state to carry:
            once the session exists, this whole table is replaced and the
            location is still /workshop/ro1, so the record simply opens. */}
        <Route path="*" element={<SignIn />} />
      </Routes>
    );
  }

  // Somebody who owes a second factor holds a real session, but the server will
  // answer 403 to everything except enrolment. Showing them a shell whose every
  // link fails would be technically honest and practically useless, so the
  // routing table shrinks to the one screen that can actually help them.
  if (user.mustEnrolSecondFactor) {
    return (
      <Routes>
        <Route element={<Shell restricted />}>
          <Route path="/security/second-factor" element={<SecondFactorSetup />} />
          <Route path="*" element={<Navigate to="/security/second-factor" replace />} />
        </Route>
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<Shell />}>
        <Route path="/dashboard" element={<DashboardPage />} />

        {/* `:id?` is an OPTIONAL segment on the SAME route, and that is load
            bearing. Two routes — one for the list, one for the record — would
            be two different matches, so React Router would unmount and remount
            the page on every open and every close, taking the filter, the page
            number and the scroll position with it. That is precisely the
            zero-jump rule ADR-020 exists to protect, and this is how a record
            gets an address without breaking it. See `useRecordRoute`. */}
        <Route path="/customers/:id?" element={<CustomersPage />} />
        <Route path="/leads/:id?" element={<LeadsPage />} />
        <Route path="/deals/:id?" element={<DealsPage />} />
        <Route path="/inventory/:id?" element={<InventoryPage />} />

        <Route path="/accounting" element={<TrialBalancePage />} />
        <Route path="/accounting/periods" element={<PeriodsPage />} />
        <Route path="/accounting/reports" element={<ReportsPage />} />
        <Route path="/receivables/ageing" element={<AgeingPage />} />
        <Route path="/receivables/statements" element={<StatementPage />} />
        <Route path="/records" element={<RecordsPage />} />

        {/* Areas, not records: a different question over a different period.
            They are listed BEFORE the job route for a reader's benefit only —
            React Router ranks a static segment above a dynamic one whatever
            the order here, and `App.test` proves it rather than trusting it,
            because the failure would be the labour report becoming
            unreachable. */}
        <Route path="/workshop/labour" element={<LabourPage />} />
        <Route path="/workshop/setup" element={<ServiceSetupPage />} />
        <Route path="/workshop/:id?" element={<WorkshopPage />} />
        <Route path="/parts" element={<PartsPage />} />
        <Route path="/staff" element={<StaffPage />} />
        <Route path="/security/second-factor" element={<SecondFactorSetup />} />
        {/* Deliberately NOT reachable while somebody owes a second factor: the
            server allows such a session to reach enrolment and nothing else, so
            this screen would answer 403 on every call it makes. */}
        <Route path="/security/passkeys" element={<PasskeysPage />} />
        {/* The dashboard is the landing screen: "how did we do" is the question
            somebody opening this at 8am is actually asking. */}
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Route>
    </Routes>
  );
}

/**
 * `restricted` hides the navigation for somebody who owes a second factor.
 * Sign-out stays: locking a person into a screen with no way out would turn a
 * security measure into a trap, and they must be able to leave a shared machine.
 */
/** Whether the current route is one this nav group is responsible for, so its
 * trigger can carry the same "you are here" highlight a top-level link gets. */
function groupContains(pathname: string, prefixes: string[]) {
  return prefixes.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

function Shell({ restricted = false }: { restricted?: boolean }) {
  const { tenant, signOut } = useSession();
  const { t } = useI18n();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [showingShortcuts, setShowingShortcuts] = useState(false);

  // Bound here rather than on each screen, so "g s" works from wherever you are.
  // The list is in Shortcuts.tsx and the panel is generated from it, which is
  // what stops a binding existing that nothing tells anybody about.
  useHotkeys(
    {
      'g d': () => navigate('/dashboard'),
      'g s': () => navigate('/inventory'),
      'g c': () => navigate('/customers'),
      'g e': () => navigate('/leads'),
      'g l': () => navigate('/deals'),
      'g w': () => navigate('/workshop'),
      'g p': () => navigate('/parts'),
      'g b': () => navigate('/accounting/periods'),
      '?': () => setShowingShortcuts(true),
    },
    !restricted,
  );

  return (
    <div className="shell">
      {/* First thing in the tab order, so a keyboard user is not walked
          through the whole navigation on every page. */}
      <a className="skip" href="#main">
        {t('shell.skipToContent')}
      </a>

      <header className="shell__bar">
        <span className="shell__brand">
          <Mark />
          <Wordmark />
        </span>

        {restricted ? null : (
          <nav aria-label={t('shell.mainNavigation')}>
            <NavLink to="/dashboard">{t('nav.dashboard')}</NavLink>

            {/* Ordered the way the work happens: an enquiry arrives, and some of
                them become deals. */}
            <NavGroup
              label={t('nav.groupSales')}
              active={groupContains(pathname, ['/customers', '/leads', '/deals', '/inventory'])}
            >
              <NavLink to="/customers">{t('nav.customers')}</NavLink>
              <NavLink to="/leads">{t('nav.leads')}</NavLink>
              <NavLink to="/deals">{t('nav.deals')}</NavLink>
              <NavLink to="/inventory">{t('nav.stock')}</NavLink>
            </NavGroup>

            <NavGroup
              label={t('nav.groupService')}
              active={groupContains(pathname, ['/workshop', '/parts'])}
            >
              <NavLink to="/workshop">{t('nav.workshop')}</NavLink>
              <NavLink to="/parts">{t('nav.parts')}</NavLink>
            </NavGroup>

            <NavGroup
              label={t('nav.groupAccounting')}
              active={groupContains(pathname, ['/accounting', '/receivables'])}
            >
              <NavLink to="/accounting" end>
                {t('nav.trialBalance')}
              </NavLink>
              <NavLink to="/accounting/periods">{t('nav.books')}</NavLink>
              <NavLink to="/accounting/reports">{t('nav.reports')}</NavLink>
              <NavLink to="/receivables/ageing">{t('nav.ageing')}</NavLink>
              <NavLink to="/receivables/statements">{t('nav.statements')}</NavLink>
            </NavGroup>

            <NavGroup
              label={t('nav.groupPeople')}
              active={groupContains(pathname, ['/staff', '/security'])}
            >
              <NavLink to="/staff">{t('nav.staff')}</NavLink>
              <NavLink to="/security/second-factor">{t('nav.secondFactor')}</NavLink>
              <NavLink to="/security/passkeys">{t('nav.passkeys')}</NavLink>
            </NavGroup>

            <NavLink to="/records">{t('nav.records')}</NavLink>
          </nav>
        )}

        <div className="shell__right">
          <AppearanceControls />
          {restricted ? null : (
            <button
              type="button"
              className="link"
              onClick={() => setShowingShortcuts(true)}
              title={t('shell.shortcutsTitle')}
            >
              {t('shell.shortcuts')}
            </button>
          )}
          <span className="shell__tenant">{tenant}</span>
          <button type="button" onClick={() => void signOut()}>
            {t('shell.signOut')}
          </button>
        </div>
      </header>

      {showingShortcuts ? <ShortcutsPanel onClose={() => setShowingShortcuts(false)} /> : null}

      <main id="main" className="shell__main" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  );
}
