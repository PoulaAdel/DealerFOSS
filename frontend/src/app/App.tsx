// App — routing and the authenticated shell.
//
// Use:  rendered by main.tsx; owns every route.
// Edit: the guard here is a convenience, not a control. Every screen calls an
//       API that enforces the same rules server-side, and it is the server's
//       answer that matters. Hiding a link protects nothing.

import { NavLink, Navigate, Outlet, Route, BrowserRouter as Router, Routes } from 'react-router-dom';
import { SessionProvider, useSession } from './session';
import { SignIn } from '../features/auth/SignIn';
import { InventoryPage } from '../features/inventory/InventoryPage';

export function App() {
  return (
    <SessionProvider>
      <Router>
        <AppRoutes />
      </Router>
    </SessionProvider>
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

  return (
    <Routes>
      <Route element={<Shell />}>
        <Route path="/inventory" element={<InventoryPage />} />
        <Route path="*" element={<Navigate to="/inventory" replace />} />
      </Route>
    </Routes>
  );
}

function Shell() {
  const { tenant, signOut } = useSession();

  return (
    <div className="shell">
      {/* First thing in the tab order, so a keyboard user is not walked
          through the whole navigation on every page. */}
      <a className="skip" href="#main">
        Skip to content
      </a>

      <header className="shell__bar">
        <span className="shell__brand">OpenDealer360</span>

        <nav aria-label="Main">
          <NavLink to="/inventory">Stock</NavLink>
        </nav>

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
