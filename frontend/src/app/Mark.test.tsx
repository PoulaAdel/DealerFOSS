// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Mark.test — the logo is drawn in five places and this is what stops them
//   becoming five different logos.
//
//   Four of the five are static .svg files that cannot import the geometry: the
//   browser tab icon and the three documentation assets. They hold copies of
//   the path data, and a copy with nothing watching it is a copy that goes
//   stale. Both outlines from markPaths.ts are asserted to appear in every one
//   of those files, so retracing the mark and forgetting the rest names the
//   files that fell behind instead of shipping a logo that is subtly wrong on
//   the tab and right in the application.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   IF A NEW STATIC DRAWING OF THE MARK IS ADDED, ADD IT TO `copies`. That list
//   is the whole point of this file.
//
//   THE WORDMARK'S ACCESSIBLE NAME MUST HAVE NO SPACE IN IT. The accname
//   algorithm inserts one between adjacent element children, so two coloured
//   spans announce "Dealer FOSS". The test below is the tripwire; it has caught
//   this once already.

import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { render, screen } from '../test/render';
import { describe, expect, it } from 'vitest';
import { Mark, Wordmark } from './Mark';
import {
  BADGE_MARK_TRANSFORM,
  BADGE_RING_RADIUS,
  GOLD_PATH,
  MARK_RATIO,
  MARK_VIEWBOX,
  TEAL_PATH,
} from './markPaths';

/**
 * The repository root, found by walking up from wherever the runner started.
 *
 * NOT from `import.meta.url`. Vitest transforms modules through its own graph
 * and that URL is not a `file:` one, so `fileURLToPath` throws before a single
 * assertion runs — which reads as "the whole suite is broken" rather than as
 * "the path helper is wrong". Counting `../` up from a source file would also
 * break the day this test moves; looking for the thing being looked for does
 * not.
 */
function repoRoot(): string {
  let dir = process.cwd();
  for (let up = 0; up < 6; up++) {
    if (existsSync(join(dir, 'frontend', 'package.json')) && existsSync(join(dir, 'docs'))) {
      return dir;
    }
    dir = dirname(dir);
  }
  throw new Error(`no repository root above ${process.cwd()}`);
}

const root = repoRoot();

const copies = [
  'frontend/public/favicon.svg',
  'docs/assets/dealerfoss-mark.svg',
  'docs/assets/dealerfoss-badge.svg',
  'docs/assets/dealerfoss-lockup.svg',
  'docs/assets/dealerfoss-banner.svg',
];

describe('the static drawings of the mark', () => {
  it.each(copies)('%s carries the geometry from markPaths.ts', (file) => {
    const svg = readFileSync(join(root, file), 'utf8');

    expect(svg, `${file} has lost the D`).toContain(GOLD_PATH);
    expect(svg, `${file} has lost the winged F`).toContain(TEAL_PATH);
  });

  it('paints the D before the F, everywhere', () => {
    // The letter overlaps the bowl. Reversed, the gold is painted over the teal
    // and the F disappears behind the D.
    for (const file of copies) {
      const svg = readFileSync(join(root, file), 'utf8');
      expect(svg.indexOf(GOLD_PATH), file).toBeLessThan(svg.indexOf(TEAL_PATH));
    }
  });

  it('the badge drawings place the mark the way markPaths.ts says to', () => {
    for (const file of ['frontend/public/favicon.svg', 'docs/assets/dealerfoss-badge.svg']) {
      const svg = readFileSync(join(root, file), 'utf8');

      expect(svg).toContain(BADGE_MARK_TRANSFORM);
      expect(svg).toContain(`r="${BADGE_RING_RADIUS}"`);
    }
  });

  it('the standalone mark is cropped to the ink and nothing else', () => {
    expect(readFileSync(join(root, 'docs/assets/dealerfoss-mark.svg'), 'utf8')).toContain(MARK_VIEWBOX);
  });

  it('the tab icon is the badge, and the manifest points at the same file', () => {
    // The icon pack has no component: this pair of links is how it is used.
    expect(readFileSync(join(root, 'frontend/index.html'), 'utf8')).toContain('href="/favicon.svg"');

    const manifest = JSON.parse(readFileSync(join(root, 'frontend/public/manifest.webmanifest'), 'utf8'));

    expect(manifest.icons.map((i: { src: string }) => i.src)).toContain('/favicon.svg');
  });

  it('every drawing keeps a literal fill beside its class', () => {
    // Markdown renderers strip <style> out of an SVG. Without the attribute the
    // logo renders black in the documentation and nobody notices for a month.
    for (const file of copies) {
      const svg = readFileSync(join(root, file), 'utf8');
      expect(svg, file).toMatch(/fill="#(14556b|63b3d4)"/);
      expect(svg, file).toMatch(/fill="#(be9231|d9ae55)"/);
    }
  });
});

describe('Mark', () => {
  it('is decorative unless it is given a title', () => {
    const { container } = render(<Mark />);
    const svg = container.querySelector('svg');

    expect(svg).toHaveAttribute('aria-hidden', 'true');
    expect(svg).not.toHaveAttribute('role');
  });

  it('becomes an image with a name when it stands alone', () => {
    render(<Mark size={44} title="DealerFOSS" />);

    expect(screen.getByRole('img', { name: 'DealerFOSS' })).toBeInTheDocument();
  });

  it('is twice as wide as it is tall, because the wing makes it so', () => {
    // `size` is the HEIGHT. A near-square stand-in lived here for a while, so
    // anything written against that assumption is wrong rather than merely old.
    const { container } = render(<Mark size={100} />);
    const svg = container.querySelector('svg');

    expect(svg).toHaveAttribute('height', '100');
    expect(svg).toHaveAttribute('width', String(Math.round(100 * MARK_RATIO)));
    expect(MARK_RATIO).toBeGreaterThan(1.9);
  });

  it('leaves its colours to the stylesheet', () => {
    // A hex code on a path would be wrong in one of the two themes: the
    // artwork teal measures 2.09:1 against the dark surface.
    const { container } = render(<Mark />);

    for (const path of container.querySelectorAll('path')) {
      expect(path).not.toHaveAttribute('fill');
    }
  });

  it('draws both halves with evenodd', () => {
    const { container } = render(<Mark />);

    expect(container.querySelector('.mark__bowl')).toHaveAttribute('fill-rule', 'evenodd');
    expect(container.querySelector('.mark__letter')).toHaveAttribute('fill-rule', 'evenodd');
  });
});

describe('Wordmark', () => {
  it('announces the name once, with no space in the middle', () => {
    const { container } = render(<Wordmark tagline />);

    expect(container.textContent).toContain('DealerFOSS');
    expect(container.textContent).not.toContain('Dealer FOSS');
  });

  it('hides the two coloured halves and the tagline from assistive technology', () => {
    const { container } = render(<Wordmark tagline />);

    for (const cls of ['wordmark__lead', 'wordmark__tail', 'wordmark__tagline']) {
      expect(container.querySelector(`.${cls}`)).toHaveAttribute('aria-hidden', 'true');
    }
  });
});
