// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Mark — the DealerFOSS logo. A mirrored F and a D sharing one spine: three
//   bars sweep LEFT off a vertical spine, each cut on the same diagonal and
//   each one step shorter than the last, with the D's bowl closing the right.
//   The bars read as the air coming off a spoiler; the spine is both the F's
//   stem and the D's straight side.
//
// Usage:
//   <Mark />               in the shell bar, 26px
//   <Mark size={44} />     on the sign-in panel
//
// Coding Instructions:
//   TWO NUMBERS HOLD THE WHOLE MARK, and getting either wrong is what makes a
//   logo look drawn rather than designed:
//
//   - ONE STROKE WEIGHT, 64, everywhere. Spine, both bars, and the bowl at its
//     widest. The first draft inherited four different weights from the
//     reference artwork (47, 50, 52, 57) and that alone read as amateur.
//   - ONE DIAGONAL, 32 across over 64 down — a clean 1:2 — on both bar ends. An
//     earlier version had 27 and 31.6 degrees, which reads as a mistake at any
//     size above a favicon.
//
//   THE BARS ARE THE SAME LENGTH, AND THAT WAS THE SECOND THING TO GO WRONG.
//   A draft tapered them by 28 each, so the mark read as speed lines at 170px
//   and as an unreadable blob at 26 — where it actually lives, in the shell bar.
//   The shortest bar was 2px wide on screen. Equal bars, checked at 26px in a
//   real browser: legibility at the size it is used beats a flourish at a size
//   it never appears in.
//
//   The grid is a 288 box divided into bands of 64: bar, gap, bar, then spine
//   alone. Keep the bands and keep both ends on the slope.
//
//   IT IS DRAWN, NOT IMPORTED. An <img> would need a second request for the one
//   element on every screen, could not follow the theme, and would soften on a
//   scaled display. Inline SVG costs about 400 bytes in a bundle it is already
//   part of.
//
//   currentColor, deliberately: the colour lives in app.css with every other
//   colour, so the mark follows the theme. A hex code here would be wrong in
//   one of the two themes, which is the standing rule in that file.

/** Decorative by default: the brand name is beside it as real text. */
export function Mark({ size = 26, title }: { size?: number; title?: string }) {
  return (
    <svg
      className="mark"
      width={size}
      height={size}
      viewBox="0 0 480 480"
      fill="currentColor"
      role={title ? 'img' : undefined}
      aria-hidden={title ? undefined : true}
    >
      {title ? <title>{title}</title> : null}
      {/* the spine: the mirrored F's stem, and the D's straight side */}
      <path d="M184 96 H248 V384 H184 Z" />
      {/* the bowl */}
      <path d="M248 96 A136 144 0 0 1 248 384 L248 320 A72 80 0 0 0 248 160 Z" />
      {/* two bars sweeping left, equal length, both cut on the same 1:2 slope */}
      <path d="M96 96 H184 V160 H128 Z" />
      <path d="M96 224 H184 V288 H128 Z" />
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
