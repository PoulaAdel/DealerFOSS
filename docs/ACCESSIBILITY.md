# Accessibility acceptance — WCAG 2.2 AA

**Status: measured, not independently audited. Conditionally accepted, with five
open findings and one limitation that matters.** Recorded 2026-10-03 against
commit `e07b0cc` (the frontend is unchanged since `0ded890`).

[`07-Delivery-Roadmap.md`](07-Delivery-Roadmap.md) lists a WCAG 2.2 AA review as
a stage 7 exit criterion and [`09-Implementation-Roadmap.md`](09-Implementation-Roadmap.md)
requires that accessibility acceptance be recorded. Accessibility work had been
done piecemeal and well — the contrast corrections and the 24px target floor are
in [`STATUS.md`](implementation/STATUS.md) — but nothing a reader could check.
This is that record. It says what was measured, how, and what failed.

## The limitation that matters first

**No screen reader was run.** Everything below is measured from the DOM, computed
styles, real key presses and source, in one browser (Chromium, via the local
development host). Whether a given announcement is *pleasant* to hear, and
whether NVDA, JAWS or VoiceOver read a region the way the markup suggests, is
untested. "A live region exists" is not "it is announced well." An independent
audit with assistive technology is still owed, and nothing here replaces it.

Also not covered: French, German, Russian and Spanish were not walked (Arabic
RTL was, at 320px, on the earlier presentation pass); `prefers-reduced-motion`
was confirmed from the one global rule that flattens every animation, not
emulated; Firefox and Safari were not used; the seeded development data is
small, so a 5,000-row list was not walked.

## Method — so every line can be re-checked

A scripted audit ran over **21 routes** (18 signed in, 3 signed out) in
**both themes**, switching theme in the page. It is in the working notes of the
milestone, not in the repository, because its value is the checks, which are:

| Check | How it was measured |
|---|---|
| Text contrast, 1.4.3 | Every visible text node. Foreground composited over the **real stacked background** (walking ancestors, alpha included), not the page colour. 4.5:1, or 3:1 for large text. Disabled controls exempt. |
| Control boundaries, 1.4.11 | Border of every input, select and textarea against its real parent surface, ≥ 3:1. |
| Target size, 2.5.8 | Bounding box of every link, button and field; anything under 23.5px listed, then tested against the spacing exception (a 24px circle centred on it meets no other target). |
| Reflow, 1.4.10 | Actually scrolling, `window.scrollTo(100000, 0)`, at 320px. **Not** `scrollWidth`, which lies when scroll containers exist (AGENTS.md §8). |
| Text spacing, 1.4.12 | Line height 1.5, letter spacing 0.12em, word spacing 0.16em, paragraph spacing 2em injected; clipped containers, clipped buttons and sideways scroll sought on seven screens. |
| Focus visible and not covered, 2.4.7 / 2.4.11 | **Real Tab presses** (a programmatic `.focus()` does not trigger `:focus-visible`), 24 consecutive stops on `/customers`: `:focus-visible` matched, outline width, in viewport, and `elementFromPoint` at the control's centre. |
| Names, 4.1.2 | Every link, button and field has an accessible name. |
| Structure, 1.3.1 / 2.4.1 / 2.4.6 | One `h1` per route, no skipped heading level, one `main`, a skip link, no positive `tabindex`, no duplicate ids, no image without `alt`. |
| Dialog behaviour, 2.1.2 / 2.4.3 | Real key presses on the shortcuts sheet and the typed `Confirm`: focus enters, Tab is contained, Escape returns focus to the opener. |

One lesson from doing it: the first dark-theme run reported twelve contrast
failures on the dashboard. They were **an artefact** — this pane throttles CSS
transitions while it is not drawn, so colours were read mid-transition. With
transitions and animations disabled during measurement the same page reported
none. A measurement that disagrees with itself between runs is not a finding.

## Results by criterion

**Passes, with the number that shows it**

| Criterion | Result |
|---|---|
| 1.4.3 Contrast (minimum) | **0 failures on all 21 routes in both themes.** (Prior corrections: STATUS.md.) |
| 1.4.11 Non-text contrast | **0 field borders under 3:1 on all 21 routes in both themes**; measured 4.12:1 light, 3.99:1 dark on the page. |
| 1.4.10 Reflow | **0px horizontal scroll at 320px on all 17 signed-in routes** and at 768px on the six checked. Tables scroll inside their own container. |
| 1.4.12 Text spacing | No clipped container, no clipped button, 0px sideways scroll on seven screens. |
| 1.4.4 Resize text | Viewport meta does not restrict zoom; 320px reflow above is the 400% case. |
| 2.4.7 Focus visible | 24 of 24 real Tab stops matched `:focus-visible`, every outline ≥ 2px (3px measured). |
| 2.4.11 Focus not obscured | 0 of 24 covered. No `position: fixed` or `sticky` element exists outside the two modal dialogs. |
| 2.1.1 / 2.1.2 Keyboard, no trap | Order was header → search → primary action → pager → rows. Both dialogs contain Tab and release on Escape; navigation disclosures close when Tab leaves them. |
| 2.4.3 Focus order | Pager before rows is deliberate (ADR-020: 116 Tabs to reach Next otherwise). Dialog focus returns to the opener. |
| 2.4.1 Bypass blocks | Skip link is the first Tab stop on every signed-in route. |
| 2.4.6 / 1.3.1 Headings, landmarks | One `h1` and no skipped level on all 21; one `main`; tables carry a caption and `scope`. |
| 4.1.2 Name, role, value | 0 unnamed controls on all 21; disclosures expose `aria-expanded` and a matching `aria-controls`; dialogs are `role="dialog"` / `"alertdialog"` with a name. |
| 3.1.1 Language of page | `lang` follows the chosen language and `dir` follows it (Arabic is RTL). Each language's own name is marked with its `lang`. |
| 2.5.8 Target size (minimum) | Every target is ≥ 24px except two, both passing an exception: the checkboxes (13–18px glyph, but the wrapping `<label>` is 80×24 and 162×24), and standalone links such as *I have forgotten my password* (179×18, nearest other target 26.5px away, over the 12px the spacing exception needs). |
| 2.5.7 Dragging movements | Not applicable: no draggable element and no drop handler exists in the source. |
| 3.2.6 Consistent help | The shortcuts control sits in the same place in the same header on every signed-in route. |
| 3.3.8 Accessible authentication | No cognitive test; no paste blocking; `autocomplete` is `username`, `current-password` and `one-time-code`; passkeys are offered. |

**Not walked.** 3.3.7 Redundant entry was reasoned about, not walked end to end:
the deal desk picks a customer from a record picker rather than re-asking for
their details, but no multi-step flow was traced for repeated fields.

## Open findings

None of these is fixed by this record. Each needs a code change, and the
repository lock was held by another session when this was written, so they are
queued rather than done. Severity is the assessor's judgement.

1. **A — 2.4.2 Page titled.** `document.title` is `DealerFOSS` on **all 21
   routes**; it is set once in `frontend/index.html` and no code changes it.
   Somebody with several tabs open, or a screen reader announcing the tab,
   cannot tell *Customers* from *The books*. Likeliest to be marked a failure.
2. **A/AA — 4.1.3 Status messages.** Eight *page failed to load* messages are
   plain `<p className="error">` with no `role="alert"` or live region: Periods
   (two), Reports, Ageing, Statements, the workshop diary, Service setup, Staff.
   A screen-reader user is told nothing when a screen fails to load. The
   out-of-balance message on *Record an entry* is likewise unannounced while it
   changes as the lines are typed. (The other 45 error elements are live.)
3. **Best practice, bordering 3.3.1 — errors are not tied to their field.** There
   are 54 error elements and **zero** `aria-invalid` or field `aria-describedby`.
   The text identifies the error and, where live, is announced; it is not
   programmatically associated with the input it is about. Passes the letter of
   3.3.1 through the live region; does not meet the recognised technique.
4. **Consistency, not a WCAG failure.** The *Only mine* checkbox on the
   enquiries screen draws **13px wide; the workshop's draws 18px.**
   `.filter label.check input { width: auto }` overrides the 18px rule for any
   checkbox inside a `.filter`. Same control, two sizes.
5. **Judgement call, 3.2.2 On input.** The language picker applies the moment
   it changes, with no confirm button. It is labelled and in a fixed place, and
   changing language is what a person opening it intends, but it is the one
   control that changes the whole page on input. Left as designed; recorded so a
   reviewer who disagrees can reopen it.

**Pre-existing, unrelated:** `/accounting/reports` renders a blank page for an
empty currency code — see the 2026-10-03 entry in STATUS.md. It is a data and
formatting fault, but a blank screen is also the least accessible one there is.

## What would make this a real acceptance

1. Fix findings 1–3 and re-measure; 4 is a one-line CSS correction.
2. A pass with a screen reader on the five highest-traffic flows (sign in,
   find a customer, open a deal, take a payment, close a month), in English and
   Arabic.
3. An independent reviewer, since the author of the markup cannot audit it.

Until then the honest statement is: **the measurable criteria pass; two are
open as failures (1, 2); the rest is untested with assistive technology.**
