// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Mark.test — the logo is drawn in six places and this is what stops them
//   becoming six different logos.
//
//   Five of the six are static files that cannot import anything: the browser
//   tab icon and the four drawings under docs/assets. They hold copies of the
//   path data, and a copy with nothing watching it is a copy that goes stale.
//
//   THE COLOURS ARE READ OUT OF app.css RATHER THAN WRITTEN HERE. Putting the
//   hexes in this file would make it a third place a brand colour lives, which
//   is the exact thing BRAND.md exists to prevent — and a test that carries its
//   own copy of the thing it is checking cannot fail when that thing is wrong.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   IF A NEW STATIC DRAWING OF THE MARK IS ADDED, ADD IT TO `copies`. That list
//   is the whole point of this file. `dealerfoss-wordmark.svg` is deliberately
//   NOT in it: it is the name only and carries no path data.
//
//   THE BANNER IS THE ONE FILE THAT WEARS THE DARK PAIR unconditionally, and it
//   is asserted separately. Its ground is dark in both themes, so the artwork's
//   own teal would measure 1.8:1 against it.
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
const read = (file: string) => readFileSync(join(root, file), 'utf8');

/** Both declarations of a token, light theme first, dark second. */
function token(name: string): { light: string; dark: string } {
  const css = read('frontend/src/theme/app.css');
  const [light, dark] = [...css.matchAll(new RegExp(`--${name}:\\s*(#[0-9a-f]{6})`, 'g'))].map(
    (m) => m[1],
  );
  // Checked rather than asserted: a length test does not narrow an index, and
  // the token genuinely can be missing — that is the failure worth reporting.
  if (!light || !dark) throw new Error(`--${name} is not declared in both themes`);
  return { light, dark };
}

const teal = token('brand-teal');
const gold = token('brand-mark-gold');

/** Every static drawing that carries the path data. The banner is one of them. */
const copies = [
  'frontend/public/favicon.svg',
  'docs/assets/dealerfoss-mark.svg',
  'docs/assets/dealerfoss-badge.svg',
  'docs/assets/dealerfoss-lockup.svg',
  'docs/assets/dealerfoss-banner.svg',
];

/** The ones that sit on a light ground and therefore carry both themes. */
const lightGround = copies.filter((f) => !f.endsWith('banner.svg'));

describe('the static drawings of the mark', () => {
  it.each(copies)('%s carries the geometry from markPaths.ts', (file) => {
    const svg = read(file);

    expect(svg, `${file} has lost the D`).toContain(GOLD_PATH);
    expect(svg, `${file} has lost the winged F`).toContain(TEAL_PATH);
  });

  it('paints the D before the F, everywhere', () => {
    // The letter overlaps the bowl. Reversed, the gold is painted over the teal
    // and the F disappears behind the D.
    for (const file of copies) {
      const svg = read(file);
      expect(svg.indexOf(GOLD_PATH), file).toBeLessThan(svg.indexOf(TEAL_PATH));
    }
  });

  it('wears the same brand colours app.css declares', () => {
    // Markdown renderers strip <style> out of an SVG, so each shape needs a
    // literal fill as well as a class. This checks the literal is the RIGHT
    // colour, against the stylesheet rather than against a copy kept here.
    for (const file of lightGround) {
      const svg = read(file);
      expect(svg, `${file} is not using the artwork teal`).toContain(`fill="${teal.light}"`);
      expect(svg, `${file} is not using the artwork gold`).toContain(`fill="${gold.light}"`);
      expect(svg, `${file} has no dark theme`).toContain(teal.dark);
      expect(svg, `${file} has no dark theme`).toContain(gold.dark);
    }
  });

  it('the banner wears the lifted pair, because its ground is dark in both themes', () => {
    const svg = read('docs/assets/dealerfoss-banner.svg');

    expect(svg).toContain(`fill="${teal.dark}"`);
    expect(svg).toContain(`fill="${gold.dark}"`);
    expect(svg, 'the banner must not use the artwork teal: 1.8:1 on its own weave')
      .not.toContain(`fill="${teal.light}"`);
  });

  it('the badge drawings place the mark the way markPaths.ts says to', () => {
    for (const file of ['frontend/public/favicon.svg', 'docs/assets/dealerfoss-badge.svg']) {
      const svg = read(file);

      expect(svg).toContain(BADGE_MARK_TRANSFORM);
      expect(svg).toContain(`r="${BADGE_RING_RADIUS}"`);
    }
  });

  it('the standalone mark is cropped to the ink and nothing else', () => {
    expect(read('docs/assets/dealerfoss-mark.svg')).toContain(MARK_VIEWBOX);
  });
});

describe('the icon pack', () => {
  // It has no component: these files and the links to them ARE how it is used.
  const icons = [
    'frontend/public/favicon.svg',
    'frontend/public/favicon.ico',
    'frontend/public/apple-touch-icon.png',
    'frontend/public/icon-192.png',
    'frontend/public/icon-512.png',
  ];

  it.each(icons)('%s exists', (file) => {
    expect(existsSync(join(root, file)), `${file} is missing`).toBe(true);
  });

  it('is linked from the page head', () => {
    const html = read('frontend/index.html');

    expect(html).toContain('href="/favicon.svg"');
    expect(html).toContain('href="/favicon.ico"');
    expect(html).toContain('rel="apple-touch-icon"');
    expect(html).toContain('href="/manifest.webmanifest"');
  });

  it('is declared in the manifest, at the sizes an installed copy asks for', () => {
    const manifest = JSON.parse(read('frontend/public/manifest.webmanifest'));
    const srcs = manifest.icons.map((i: { src: string }) => i.src);

    expect(srcs).toContain('/favicon.svg');
    expect(srcs).toContain('/icon-192.png');
    expect(srcs).toContain('/icon-512.png');
    expect(manifest.theme_color).toBe(teal.light);
  });

  it('does not offer to install itself', () => {
    // A `display` member is what makes a browser prompt. Installing a
    // dealership's DMS is the operator's decision, not ours.
    const manifest = JSON.parse(read('frontend/public/manifest.webmanifest'));

    expect(manifest.display).toBeUndefined();
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
    // artwork teal measures 1.8:1 against the dark surface.
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
