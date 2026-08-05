// App — routing and the authenticated shell.
//
// Use:  rendered by main.tsx; owns every route.
// Edit: the guard here is a convenience, not a control. Every screen calls an
//       API that enforces the same rules server-side, and it is the server's
//       answer that matters. Hiding a link protects nothing.

// Imported from react-router rather than react-router-dom: in v8 the DOM
// bindings moved into the main package and react-router-dom is a shim. The
// version matters — 7.12 to 8.2 carry a CSRF advisory (GHSA-qwww-vcr4-c8h2).
import { NavLink, Navigate, Outlet, Route, BrowserRouter as Router, Routes } from 'react-router';
import { SessionProvider, useSession } from './session';
import { SignIn } from '../features/auth/SignIn';
import { InventoryPage } from '../features/inventory/InventoryPage';
import { TrialBalancePage } from '../features/accounting/TrialBalancePage';
import { SecondFactorSetup } from '../features/auth/SecondFactorSetup';
import { RecordsPage } from '../features/migration/RecordsPage';
import { CustomersPage } from '../features/customers/CustomersPage';
import { LeadsPage } from '../features/leads/LeadsPage';
import { DealsPage } from '../features/deals/DealsPage';
import { AdminApp } from './AdminApp';

export function App() {
  return (
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
        <Route path="/customers" element={<CustomersPage />} />
        <Route path="/leads" element={<LeadsPage />} />
        <Route path="/deals" element={<DealsPage />} />
        <Route path="/inventory" element={<InventoryPage />} />
        <Route path="/accounting" element={<TrialBalancePage />} />
        <Route path="/records" element={<RecordsPage />} />
        <Route path="/security/second-factor" element={<SecondFactorSetup />} />
        <Route path="*" element={<Navigate to="/inventory" replace />} />
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
            <NavLink to="/customers">Customers</NavLink>
            {/* Ordered the way the work happens: an enquiry arrives, and some of
                them become deals. */}
            <NavLink to="/leads">Enquiries</NavLink>
            <NavLink to="/deals">Deals</NavLink>
            <NavLink to="/inventory">Stock</NavLink>
            <NavLink to="/accounting">Trial balance</NavLink>
            <NavLink to="/records">Records</NavLink>
            <NavLink to="/security/second-factor">Two-step sign-in</NavLink>
          </nav>
        )}

        <div className="shell__right">
          <span className="shell__tenant">{tenant}</span>
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
