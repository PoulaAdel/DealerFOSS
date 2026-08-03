// Vite configuration.
//
// Two settings here exist because the toolchain runs in a container while the
// API runs on the host, and getting either wrong looks like the backend is down.
//
//   host: true          — bind to 0.0.0.0 so the published port reaches the dev
//                         server. The default binds to the container's loopback,
//                         which nothing outside the container can see.
//   proxy /api          — the browser must believe the API is same-origin, or
//                         the session cookie is never sent: it is SameSite=Strict
//                         and would be dropped on a cross-origin request.
//
// From inside a container, "localhost" is the container. The host machine is
// reachable as host.docker.internal on Docker Desktop. Override with ODMS_API
// when running the toolchain directly on the host instead.

/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const api = process.env.ODMS_API ?? 'http://host.docker.internal:5080';

export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5173,
    strictPort: true,
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
