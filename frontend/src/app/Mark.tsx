// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Mark — the DealerFOSS logo from the brand sheet: a winged F over a D, the
//   letter in teal and the bowl in gold.
//
//   The geometry is not here. It lives in markPaths.ts because four other
//   drawings of the same shape are static .svg files that cannot import it: the
//   browser tab icon, and the mark, badge and banner under docs/assets. A test
//   keeps all five in step.
//
//   There is no badge component. The icon-pack treatment on the brand sheet is
//   an ICON — its job in a web application is the tab and the installed app
//   icon, both of which are files rather than components, so it is drawn in
//   public/favicon.svg and pointed at from the manifest. A React version would
//   have had no caller.
//
// Usage:
//   <Mark />                 in the shell bar, 26 tall
//   <Mark size={44} />       on the sign-in panel
//   <Mark size={44} title="DealerFOSS" />   when no text name is beside it
//   <Wordmark tagline />     the name, with AUTOMOTIVE SOLUTIONS under it
//
// Coding Instructions:
//   `size` IS THE HEIGHT, NOT THE WIDTH, and the mark is twice as wide as it is
//   tall. Passing `size` to both squashes it to half width. A near-square
//   stand-in lived here for a while, so any caller written against that needs
//   looking at rather than trusting.
//
//   TWO COLOURS, AND THEY COME FROM app.css. The fills are classes rather than
//   attributes so the mark follows the theme: the artwork's own teal measures
//   2.09:1 on the dark surface, which is invisible, so both halves are lifted
//   there. A hex code in this file would be wrong in one of the two themes.
//
//   GOLD FIRST. The teal F overlaps the D; swap the order and the bowl is
//   painted over the letter.
//
//   IT IS DRAWN, NOT IMPORTED. An <img> would need a second request for the one
//   element on every screen, could not follow the theme, and would soften on a
//   scaled display.

import { GOLD_PATH, MARK_RATIO, MARK_VIEWBOX, TEAL_PATH } from './markPaths';

/** Decorative by default: the brand name is beside it as real text. */
export function Mark({ size = 26, title }: { size?: number; title?: string }) {
  return (
    <svg
      className="mark"
      width={Math.round(size * MARK_RATIO)}
      height={size}
      viewBox={MARK_VIEWBOX}
      role={title ? 'img' : undefined}
      aria-hidden={title ? undefined : true}
    >
      {title ? <title>{title}</title> : null}
      <path className="mark__bowl" fillRule="evenodd" d={GOLD_PATH} />
      <path className="mark__letter" fillRule="evenodd" d={TEAL_PATH} />
    </svg>
  );
}

/**
 * The name, in the logo's two colours: "Dealer" teal, "FOSS" gold, with
 * AUTOMOTIVE SOLUTIONS beneath it — the lockup as the brand sheet sets it.
 *
 * Split here rather than in the message catalogue on purpose. `app.name` is the
 * identical string "DealerFOSS" in all six locales — the i18n test calls it "a
 * product name" — so this is a brand constant, not a translation, and slicing a
 * translated string at a fixed index would be the fragile version of the same
 * thing.
 *
 * THE HIDDEN COPY IS NOT BELT AND BRACES. Two coloured spans looked like they
 * would concatenate to "DealerFOSS"; they do not. The accessible name algorithm
 * puts a SPACE between adjacent element children, so assistive technology
 * announced "Dealer FOSS" and the shell test stopped finding its heading. The
 * visible halves are therefore hidden from the accessibility tree and the real
 * name is given once, as text. Remove either half of that arrangement and the
 * name silently gains a space again.
 */
export function Wordmark({ tagline = false }: { tagline?: boolean }) {
  return (
    <span className="wordmark">
      <span aria-hidden="true" className="wordmark__lead">
        Dealer
      </span>
      <span aria-hidden="true" className="wordmark__tail">
        FOSS
      </span>
      <span className="visually-hidden">DealerFOSS</span>

      {/* Hidden from the accessibility tree for the same reason the two halves
          are: it is part of the picture of the name, and announcing "DealerFOSS
          automotive solutions" after every heading would be noise. The real
          name is the visually-hidden span above, once. */}
      {tagline ? (
        <span aria-hidden="true" className="wordmark__tagline">
          Automotive Solutions
        </span>
      ) : null}
    </span>
  );
}
