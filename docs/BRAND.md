# The DealerFOSS identity

The brand as supplied on **2026-09-11**, and exactly how much of it is in the
application today.

This file exists because a colour written in two places is a colour that will be
wrong in one of them. Everything below has a single home in
[`frontend/src/theme/app.css`](../frontend/src/theme/app.css); this document says
what the values mean and why two of them are not the artwork's own.

## The name

**DealerFOSS**, set as one word with no space, in two colours: **Dealer** in
teal, **FOSS** in gold. Beneath it, in caps and letter-spaced,
**AUTOMOTIVE SOLUTIONS**.

The split is a brand constant, not a translation. `app.name` is the identical
string "DealerFOSS" in all six locales — the i18n suite lists it under "a product
name" — so the two halves are written in `Mark.tsx` rather than sliced out of a
translated string at a fixed index.

> **The two coloured halves are hidden from the accessibility tree, and the real
> name is given once as text.** The accessible-name algorithm inserts a space
> between adjacent element children, so two visible spans announced "Dealer FOSS"
> and the shell test stopped finding its heading. Remove either half of that
> arrangement and the name silently gains a space again.

The tagline is shown on the sign-in page and hidden in the application bar, where
the bar is already two rows and a third line of identity would push the
navigation off a narrow screen.

## The colours

| Token | Light | Dark | Artwork |
|---|---|---|---|
| `--brand-teal` | `#14556b` | `#63b3d4` | `#14556b` |
| `--brand-gold` | `#916f1e` | `#d9ae55` | `#be9231` |
| `--brand-gold-artwork` | `#be9231` | `#be9231` | `#be9231` |

**The teal is the artwork's own value in the light theme.** It measures 8.26:1 on
white.

**The gold is not, and that is deliberate.** The artwork's `#be9231` measures
**2.86:1 on white** — under 4.5:1 for text, and under even the 3:1 large-text
bar. Shipped as supplied, the half of the name that says FOSS would be the half
nobody could read. `#916f1e` is the same hue carried down until it measures
4.67:1.

`--brand-gold-artwork` keeps the untouched value for a graphic on a dark or
coloured ground — the carbon banner on the brand sheet, for instance — where
contrast is not the constraint. It measures 6.04:1 on the dark theme's surface.
**Never use it for text on `--surface`.**

The dark theme lifts both: the artwork teal measures 2.09:1 there, which is dim
to the point of invisible. Both halves are lifted by a similar amount on purpose
— lift one and not the other and the wordmark looks assembled from two different
logos.

### Where brand colour may be used

**Identity only: the mark, the wordmark and the tagline.** Never a chip, a
button, or a state.

`--accent` already means "in progress" on seven chips and `--fail` is already a
red. A brand colour leaking into either would make "submitted" and "failed" read
as the same thing, and colour is never allowed to carry meaning on its own in
this application (ADR-020).

## The mark

![The DealerFOSS mark](assets/dealerfoss-mark.svg)

The winged F over a D: the letter in teal, the bowl in gold, and one swept wing
with a split through it.

### It is a trace of the artwork, not a redrawing of it

Two attempts to draw this mark by hand were made and both were rejected, and
they deserved to be. Working from the picture by eye produced a wing of three
separate feathers and an F whose crossbar pointed right. **The real mark has one
swept wing and its crossbar points left.** Neither is something a person
recovers from memory of a small picture, which is why the third attempt stopped
drawing and measured instead.

The outlines in the repository were produced mechanically from the brand sheet:

1. the sheet read at its own resolution, and the mark isolated on the
   transparent-PNG tile, where nothing else is near it;
2. each pixel turned into a **coverage figure, not a verdict** — how much ink is
   in it, taken from saturation, and split between the two colours by hue. Hue
   rather than distance to a swatch because the artwork is shaded: one teal runs
   from near-black to a pale highlight, and hue is what survives that;
3. the **half-coverage contour** taken with marching squares, which interpolates
   along cell edges and therefore lands *between* pixels;
4. lightly smoothed, simplified, and re-interpolated as Catmull-Rom curves.

> **The first attempt thresholded the image into a hard mask first, and that is
> the mistake to avoid on a retrace.** A threshold rounds every edge to the
> nearest whole pixel and throws away the anti-aliasing — which is exactly the
> information that says where the edge really is. What comes back is a
> staircase, and no amount of smoothing afterwards recovers what the rounding
> discarded; it looked chewed. On a source this small that rounding is worth
> about a percent of the mark's width on every edge.

> **The source is about 138 pixels wide.** That is the ceiling on how crisp this
> can be. The *shapes* are the designer's; the exact edges are a reading of a
> small raster and wobble very slightly against a true vector. At 26px in the
> application bar the difference does not exist. At 160px on the sign-in panel a
> designer would see it.

**To replace it with the real thing**, put the artwork's outlines into the two
constants in:

```
frontend/src/app/markPaths.ts
```

That file is the only copy of the geometry. Everything else — the component, the
tab icon, and the three drawings under `docs/assets/` — is drawn from those two
strings, and `Mark.test.tsx` fails by name on any file that has not been updated
to match. There is no second place to remember.

### Why the numbers live in a `.ts` file and not an `.svg`

The mark is drawn five times and only one of them can import anything: the React
component. The browser tab icon and the three documentation assets are static
files, loaded through `<img>` or by the browser's icon fetcher, and a file loaded
that way inherits nothing from the page around it. They hold copies. The test is
what stops the copies drifting into five slightly different logos.

The component's fills are CSS classes rather than attributes for the same reason
the colour table above exists — see the dark-theme row. The static files carry
**both** a class and a literal `fill`, because Markdown renderers strip `<style>`
out of an SVG and the attribute is what survives that.

### The parts, and where each one is used

| File | What it is | Used by |
|---|---|---|
| `frontend/src/app/markPaths.ts` | the geometry, and the only copy | everything below |
| `frontend/src/app/Mark.tsx` | `<Mark>` and `<Wordmark>` | the app bar, sign-in, the admin shell |
| `frontend/public/favicon.svg` | the icon-pack badge | the browser tab |
| `frontend/public/manifest.webmanifest` | name, icon, theme colour | an installed copy |
| `docs/assets/dealerfoss-mark.svg` | the mark alone | this file |
| `docs/assets/dealerfoss-badge.svg` | the badge | documentation |
| `docs/assets/dealerfoss-lockup.svg` | mark, name and tagline | documentation |
| `docs/assets/dealerfoss-banner.svg` | the 1200×300 banner | `README.md` |

**The mark is twice as wide as it is tall.** Every caller gives a height and
derives the width. The rejected stand-in was nearly square, so any code written
against that assumption is wrong rather than merely old.

**There is no badge component.** The icon-pack treatment is an *icon*, and an
icon's job in a web application is the browser tab and the installed app icon —
both files, not components. A React version would have had no caller.

**The banner drops the brand sheet's GET A QUOTE button.** It sits on the README
of a repository, where a sales call to action is addressed to nobody: the reader
is a contributor or an operator, not a lead. The ground, the lockup and the
proportions are the sheet's.

## The printed paperwork is deliberately unbranded

The order, the invoice and the job sheet carry the **dealership's** name, not
ours. A dealer management system prints the dealership's documents; putting the
vendor's colours on a customer's invoice would be branding somebody else's
paperwork with our identity. `DocumentHtml.Stylesheet` is neutral greys on white
and stays that way.

## What is done and what is not

| | |
|---|---|
| Colour tokens, both themes, contrast measured | done |
| Wordmark in the two brand colours | done |
| AUTOMOTIVE SOLUTIONS tagline | done, on sign-in |
| The mark, on screen in both themes | done — **as a trace** |
| The icon-pack badge, as the tab icon and the app icon | done |
| The lockup and the banner, in the documentation | done |
| The mark replaced by the supplied vector | **still open** |
| PNG raster exports | not needed — every use above is a vector |

The one row still open is one file, and it is a quality question rather than a
missing feature: the mark is on screen and it is the right mark. Dropping the
designer's own outlines into `markPaths.ts` sharpens it and changes nothing
else.
