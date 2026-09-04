// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   vite.config.ts — the development server and the test runner.
//
//   Two settings exist because the toolchain runs in a container while the API
//   runs on the host, and getting either wrong looks like the backend is down:
//
//     host: true   binds to 0.0.0.0 so the published port reaches the dev
//                  server. The default binds to the container's loopback,
//                  which nothing outside the container can see.
//     proxy /api   makes the browser believe the API is same-origin. Without
//                  it the session cookie is never sent — it is SameSite=Strict
//                  and would be dropped on a cross-origin request.
//
//   From inside a container, "localhost" is the container; the host machine is
//   host.docker.internal on Docker Desktop.
//
// Usage:
//   npm run dev        the server, on 5173
//   npm test           Vitest against jsdom
//   DEALERFOSS_API=... override the API origin when running on the host
//
// Coding Instructions:
//   POLLING IS NOT A PERFORMANCE MISTAKE. Vite's file watcher does not see
//   edits across a Windows bind mount, so without `usePolling` hot reload is
//   silently dead and a hard refresh still serves the previous bundle — which
//   reads as "my change did nothing" rather than as a broken watcher.
//
//   `restoreMocks` is on so a stubbed fetch cannot leak from one test into the
//   next. Tests here mount real components into a real DOM; anything needing a
//   real server is an integration test and lives in tests/Integration.

/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const api = process.env.DEALERFOSS_API ?? 'http://host.docker.internal:5080';

export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5173,
    strictPort: true,
    // The repository is bind-mounted from Windows into a Linux container, and
    // inotify events do not cross that boundary — so Vite's default watcher
    // never fires. Without polling, editing a file changes nothing in the
    // browser and even a hard reload serves the previous bundle, which reads
    // as "my change did nothing" rather than as a broken watcher.
    //
    // Polling costs a little CPU. A dead edit-refresh loop costs an afternoon.
    watch: {
      usePolling: true,
      interval: 300,
    },
    proxy: {
      '/api': {
        target: api,
        changeOrigin: false,
      },
    },
  },
  // The tests mount real components into a real DOM. jsdom is what makes
  // "does it render?" a question a machine can answer rather than one that
  // waits on somebody opening a browser.
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.tsx', 'src/**/*.test.ts'],
    restoreMocks: true,
  },
});
