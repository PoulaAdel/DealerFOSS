// App — routing and the authenticated shell.
//
// Use:  rendered by main.tsx; owns every route.
// Edit: the guard here is a convenience, not a control. Every screen calls an
//       API that enforces the same rules server-side, and it is the server's
//       answer that matters. Hiding a link protects nothing.

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
  useNavigate,
} from 'react-router';
import { SessionProvider, useSession } from './session';
import { AppearanceProvider } from '../shared/appearance';
import { AppearanceControls } from './AppearanceControls';
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
import { PartsPage } from '../features/parts/PartsPage';
import { PeriodsPage } from '../features/accounting/PeriodsPage';
import { SetFirstPassword } from '../features/auth/SetFirstPassword';
import { AdminApp } from './AdminApp';

export function App() {
  return (
    // Outside the router and both sessions: the theme applies to the sign-in
    // screen and the administration console as much as to the shell, and a
    // choice made on one must not be forgotten by the next.
    <AppearanceProvider>
      <Router>
        <Routes>
          {/* The split is above SessionProvider on purpose. An administrator has
              no dealership, so asking /auth/me on their behalf would be a
              meaningless question — and mounting both session contexts at once
              would make it possible to write a screen that does not know which
              of the two identities it is holding. */}
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
  );
}

function AppRoutes() {
  const { user } = useSession();

  // Undefined means "still asking the server". Rendering the sign-in form here
  // would flash it at somebody who is already signed in.
  if (user === undefined) {
    return (
      <main className="state" aria-live="polite">
        <p>Loading…</p>
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
        <Route path="*" element={<Navigate to="/sign-in" replace />} />
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
        <Route path="/customers" element={<CustomersPage />} />
        <Route path="/leads" element={<LeadsPage />} />
        <Route path="/deals" element={<DealsPage />} />
        <Route path="/inventory" element={<InventoryPage />} />
        <Route path="/accounting" element={<TrialBalancePage />} />
        <Route path="/accounting/periods" element={<PeriodsPage />} />
        <Route path="/records" element={<RecordsPage />} />
        <Route path="/workshop" element={<WorkshopPage />} />
        <Route path="/parts" element={<PartsPage />} />
        <Route path="/staff" element={<StaffPage />} />
        <Route path="/security/second-factor" element={<SecondFactorSetup />} />
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
function Shell({ restricted = false }: { restricted?: boolean }) {
  const { tenant, signOut } = useSession();
  const navigate = useNavigate();
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
        Skip to content
      </a>

      <header className="shell__bar">
        <span className="shell__brand">DealerFOSS</span>

        {restricted ? null : (
          <nav aria-label="Main">
            <NavLink to="/dashboard">This month</NavLink>
            <NavLink to="/customers">Customers</NavLink>
            {/* Ordered the way the work happens: an enquiry arrives, and some of
                them become deals. */}
            <NavLink to="/leads">Enquiries</NavLink>
            <NavLink to="/deals">Deals</NavLink>
            <NavLink to="/inventory">Stock</NavLink>
            <NavLink to="/workshop">Workshop</NavLink>
            <NavLink to="/parts">Parts</NavLink>
            <NavLink to="/accounting" end>
              Trial balance
            </NavLink>
            <NavLink to="/accounting/periods">The books</NavLink>
            <NavLink to="/records">Records</NavLink>
            <NavLink to="/staff">People</NavLink>
            <NavLink to="/security/second-factor">Two-step sign-in</NavLink>
          </nav>
        )}

        <div className="shell__right">
          <AppearanceControls />
          {restricted ? null : (
            <button
              type="button"
              className="link"
              onClick={() => setShowingShortcuts(true)}
              title="Keyboard shortcuts ( ? )"
            >
              Shortcuts
            </button>
          )}
          <span className="shell__tenant">{tenant}</span>
          <button type="button" onClick={() => void signOut()}>
            Sign out
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
