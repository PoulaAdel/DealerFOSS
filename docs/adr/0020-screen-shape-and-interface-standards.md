# ADR-0020 — The shape of a screen, and the standards every screen is held to

**Status:** Accepted · **Date:** 2026-08-09

## Context

The product's screens were built one capability at a time, each following the one
before it. That produced a consistent house style by imitation rather than by
rule, and imitation drifts: six class names were being written in JSX with no CSS
behind them for weeks, because nothing said what a screen is made of.

The maintainer has now set the interface direction explicitly — consolidated
screens rather than page-hopping, the fewest clicks that do the job, tight and
even layout, dual theme, both text directions, instant search, considered motion,
smart defaults, and full keyboard reach at WCAG AA. This ADR turns that direction
into a shape and a set of rules, so a new screen is *checked* against something
rather than eyeballed against the last one.

It also records honestly which of those rules the build already keeps, which it
keeps partly, and which it does not keep yet. A standards document that describes
an aspiration as a fact is worse than none.

## Decision — the shape of a screen

Every dealership screen is one route and is built from five bands, in this order.
The vocabulary is the existing CSS, not a parallel system.

```
section.page
├── header.page__head      TITLE · instant filter · the one primary action
├── .panel--signal   0..n  WHAT NEEDS A PERSON NOW  — omitted entirely when empty
├── .panel--context  0..n  THE NEXT THING, in place  — the diary above the ramps
├── .scroll > table    1   THE RECORD LIST          — scrolls inside its own box
└── .panel--detail   0..1  THE SELECTED RECORD, inline — never a second route
```

**The bands are ordered by urgency, not by data model.** `panel--signal` is the
work somebody owes right now: the workshop's calls waiting on a customer, an
import needing attention. It is drawn from data the list already carries, costs
no extra request, and **disappears when there is nothing in it** — a permanently
present "0 items need attention" panel trains people to stop reading that spot.

**A detail is a band, not a route.** Selecting a row reveals `panel--detail`
below the list with the list still on screen. This is the zero-jump rule: the
operator keeps their place, their filter and their scroll position, and going
"back" is not an operation. Routes are for *areas* — stock, workshop, deals — and
never for *records within an area*.

**A modal is permitted for exactly one thing:** confirming an act that is hard to
undo and that the server will happily perform (suspending a dealership, deleting
a line). Everything else is a band. Wizards are not used; a form that needs three
steps is a form that is asking for things it does not need.

### The rules a band obeys

| Rule | Why |
|---|---|
| One `h1` per route, one `h2` per band | The heading tree is how a screen reader user navigates; a band with no heading is invisible to them |
| Wide content lives in `.scroll` | The **page** must never scroll sideways. Measured by scrolling, because `scrollWidth` lies when scroll containers exist |
| Every band renders loading, empty, denied, failed and retry | A band that only handles the happy path is not finished. `InventoryPage` is the reference |
| "Denied" for a *secondary* band renders nothing at all | Plenty of people who may see the workshop have no business taking bookings. A red panel tells them they did something wrong |
| Spacing comes from tokens, never from a literal | Even rhythm is a consequence of a shared scale, not of care at each site |
| Logical properties only | `margin-inline-start`, never `margin-left`. One physical property is the single thing that would not mirror |

## Decision — the eleven standards, and where each stands

**Already held, and enforced by something.**

1. **Dual theme.** 36 tokens in `:root`, redeclared under
   `[data-theme='dark']`. Light, dark and follow-the-machine, applied before
   React mounts so the page never flashes the wrong one. Colour pairs were
   measured for contrast and the failing ones corrected; the reasoning is in the
   token comments so it is not silently undone.
2. **Both text directions.** The stylesheet is written in logical properties, so
   one `dir` on `<html>` mirrors the whole application. Direction derives from
   the language and is not separately selectable (ADR-019). Verified in a real
   browser in all five languages.
3. **No visible string in a component.** All 28 files that render words read from
   the catalogue. English is the schema, so an untranslated key fails
   `npm run typecheck`.
4. **Motion is tokenised and respects the reader.** One `--beat` (140 ms) drives
   every transition, and `prefers-reduced-motion: reduce` cuts them all to
   0.01 ms. This is the WCAG 2.3.3 obligation, and it is already met.
5. **Keyboard reach.** A skip link ahead of the navigation, a visible focus ring
   on everything interactive, labels tied to inputs, `role="alert"` on errors,
   focus moved to the code field when the second-factor step appears, and status
   given as a word rather than only a colour.

**Held in part.**

6. **Zero-jump consolidation.** The workshop is the reference: the diary of cars
   still to come sits above the jobs on the ramps, and marking a car in opens
   its job in one click without leaving the screen. The deal desk is close.
   Other screens are single-purpose lists and have not been consolidated.
7. **Smart defaults.** Present where somebody thought of it — the short name
   suggested from a dealership's name, the workshop inferred rather than asked
   for, the last dealer group remembered at sign-in. Not systematic, and nothing
   yet adapts to historical patterns.
8. **Micro-interactions.** Five transitions exist. Autosave, background sync and
   transaction confirmation have no motion vocabulary because none of those
   behaviours exists yet — inventing the animation before the behaviour would be
   decoration.

**Also held, added 2026-08-09.**

9. **Instant search.** The list follows the box on Customers and Parts; Enter is
   no longer how you search. Debounced at 250 ms, and — the part that made this
   more than a two-line change — the superseded request is **aborted**, so the
   list can never show results for a query the box no longer contains.
   Debouncing alone would only make that race rarer, which is worse than leaving
   it obvious. `useDebounced` carries the reasoning; the test stub had to learn
   delays and cancellation before the property could be proven at all.
10. **Inline editing.** `shared/InlineEdit.tsx`, first used on the diary's
    estimate — the most repeated edit in a service diary, and the one that stops
    being kept current the moment it needs a form. Idle state is a **button**,
    not a div with an `onClick`, so it is reachable by Tab and announces what
    pressing it does; Escape restores; blur saves; the confirmation is a word,
    not a colour; and a refused save puts the record's value back rather than
    leaving the typed one looking accepted. Quick-action toolbars are still to
    come.
11. **A measured AA pass**, run at 320 px and at 640 px (1280 at 200 %), in both
    directions, across all nine dealership screens:

    | Criterion | Result |
    |---|---|
    | 1.4.10 Reflow, 320 px | Pass — 0 of 9 screens scroll sideways, measured by scrolling rather than by trusting `scrollWidth` |
    | 1.4.4 Resize text, 200 % | Pass — LTR and RTL |
    | 2.5.8 Target size | **5 failures found and fixed**: the inline-edit value at 22 px wide, a parts row button at 22 px tall, three dashboard account links at 15 px tall, and two bare checkboxes at 13×13. Floored at 24 px; re-measured clean |
    | 2.1.1 Keyboard | Pass — nothing unreachable, and no `div` with a click handler pretending to be a control |
    | 2.4.7 Focus visible | Pass — 3 px outline, verified with **real Tab presses**; a first attempt using programmatic `.focus()` reported every control as failing, which was the measurement being wrong, not the application |
    | 2.3.3 Animation | Pass — one `--beat` token, `prefers-reduced-motion` cuts every transition |
    | 1.4.3 Contrast | Pass — measured previously, two token pairs corrected |

    What this pass does **not** cover, and so is not claimed: a screen reader
    driven end to end by a person, and any judgement about whether the wording
    is understandable to somebody who has not used the product.

## Alternatives rejected

**A component library.** Rejected on the same grounds as `i18next` in ADR-019 and
the PDF library in ADR-018: a dependency needs a licence review and an advisory
history, `npm audit --audit-level=high` fails the build on a high advisory, and
the whole visual language is currently 1,200 lines of CSS with no runtime cost.
A library would also fight the logical-property rule, which is what makes RTL
free here.

**A tab per band.** Tabs are page-hopping with the jumps hidden, and they cost a
click before anything is visible. The bands stack; the signal band is at the top
because it is what somebody needs first.

**Refactoring every screen to the shape at once.** Rejected. The shape is written
down so new work follows it and existing screens are brought over when they are
next touched for another reason. A twenty-screen restyle in one change is
unreviewable, and the tests assert on what a person reads rather than on
structure, so it would move fastest exactly where it is least checked.

## Consequences

**Good.** A new screen has something to be checked against, and the five bands
map onto the five states every band must render, so "did you handle empty" has an
obvious place to look. Writing the standards down also found the six unstyled
class names, which had been shipping as unstyled stacked divs since the console
was built.

**Cost.** Eight of the eleven are now met and three are partly met, so this
document's job has shifted from listing intentions to holding the line. The
remaining work is named in items 6, 7 and 8 — consolidation beyond the workshop,
systematic smart defaults, and a motion vocabulary for behaviours (autosave,
background sync) that do not exist yet. Writing 9–11 down is what got them built
within the day; if 6–8 are still partial in three months, this document has
become the thing it was written to prevent.

**Worth keeping.** Two of the three closed items were closed by *measuring*
rather than by inspection, and both measurements were wrong the first time — the
target-size sweep initially ran against pages that had not rendered, and the
focus-ring check reported every control as failing because programmatic `.focus()`
does not trigger `:focus-visible`. A measurement that has not itself been checked
is not evidence.

## Validation / review trigger

Revisit when inline editing lands (it will change what `panel--detail` is for),
or when a screen genuinely cannot be expressed in the five bands — the second is
evidence about the shape, not about the screen.
