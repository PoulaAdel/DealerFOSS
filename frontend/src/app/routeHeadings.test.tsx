// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   routeHeadings.test — every routed screen draws an <h1>, so no tab is left
//   titled just "DealerFOSS".
//
// Usage:
//   npm test
//
// Coding Instructions:
//   PageTitle takes the tab's title from the page's own <h1>, so a routed screen
//   with no heading would silently fall back to the product name (WCAG 2.4.2).
//   This reads the router's source and checks each routed component's file for
//   a heading. It is a source check on purpose: mounting twenty screens with
//   their data would test the data, not the rule. AdminApp is a nested router
//   with its own screens and is covered by AdminApp.test.

import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const here = dirname(fileURLToPath(import.meta.url));
const app = readFileSync(resolve(here, 'App.tsx'), 'utf8');

/** Component name -> the file it is imported from. */
function importedFiles(): Map<string, string> {
  const found = new Map<string, string>();
  for (const match of app.matchAll(/import \{([^}]+)\} from '(\.[^']+)';/g)) {
    for (const name of match[1]!.split(',').map((n) => n.trim())) {
      found.set(name, match[2]!);
    }
  }
  return found;
}

describe('routed screens', () => {
  const files = importedFiles();
  const routed = [...app.matchAll(/<Route [^>]*element=\{<(\w+)/g)]
    .map((m) => m[1]!)
    .filter((name) => name !== 'Shell' && name !== 'AdminApp' && name !== 'Navigate');

  it('finds the routes it is meant to check', () => {
    expect(new Set(routed).size).toBeGreaterThan(20);
  });

  it.each([...new Set(routed)])('%s draws a heading, so its tab has a title', (name) => {
    const path = files.get(name);
    expect(path, `${name} is not imported from a relative path`).toBeDefined();

    const file = ['.tsx', '/index.tsx']
      .map((suffix) => resolve(here, `${path}${suffix}`))
      .find((candidate) => {
        try {
          readFileSync(candidate);
          return true;
        } catch {
          return false;
        }
      });

    expect(file, `${name}'s file was not found`).toBeDefined();
    expect(readFileSync(file!, 'utf8')).toMatch(/<h1[\s>]/);
  });
});
