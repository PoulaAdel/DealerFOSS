// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   The browser entry point, and deliberately the smallest file in the
//   frontend. It mounts App into #root and imports the one stylesheet; every
//   decision worth making — routing, providers, theme, language — is made
//   inside App, one level down, where it can be tested.
//
//   It throws rather than falling back when #root is missing. A silent no-op
//   would present as "the page is blank" with nothing in the console, which is
//   among the least diagnosable failures a frontend can have.
//
// Usage:
//   Referenced by index.html as the module entry. Nothing imports it.
//
// Coding Instructions:
//   Keep it this small. A provider added here instead of in App is one the
//   tests never mount, so the tree under test stops matching the tree that
//   ships — which is exactly the divergence test/render.tsx exists to prevent.

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './app/App';
import './theme/app.css';

const root = document.getElementById('root');
if (root === null) {
  throw new Error('index.html is missing #root.');
}

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
