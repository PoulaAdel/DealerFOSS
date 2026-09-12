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

### The artwork's own values, measured

| | Hex | On white | On `--ground` | On the dark surface |
|---|---|---|---|---|
| **teal** | `#054b60` | 9.63:1 | 8.43:1 | 1.80:1 |
| **gold** | `#b38524` | 3.33:1 | 2.92:1 | 5.18:1 |

**These were measured, not read off a swatch.** The brand sheet is a JPEG, so
the ink was sampled from the **core** of each shape — eroded two pixels so that
bevels, glows and anti-aliased edges cannot drag the answer toward the
background — and the commonest value taken. An earlier pass guessed `#14556b`
and `#be9231` by eye and both were too light. If these are ever re-derived,
erode first.

### The tokens

| Token | Light | Dark | What it is |
|---|---|---|---|
| `--brand-teal` | `#054b60` | `#3fa5c4` | the teal, as text and as ink |
| `--brand-gold` | `#8b671c` | `#c2922b` | the gold, **as text** |
| `--brand-mark-gold` | `#b38524` | `#c2922b` | the gold, **as the mark** |
| `--brand-gold-artwork` | `#b38524` | `#b38524` | the untouched value, for reference |

**The teal needs no help in the light theme**: 9.63:1 on `--surface`.

**The gold does, and only as text.** `#b38524` measures 3.33:1 on white and
2.92:1 on `--ground` — below the 4.5:1 bar. Shipped as supplied, the half of the
name that says FOSS would be the half nobody could read. `#8b671c` is the same
hue carried down until it clears the bar on the worse of the two grounds: 5.18:1
on white, 4.54:1 on `--ground`.

**The mark keeps the artwork's gold, because it is a picture.** It is
`aria-hidden` with the name beside it as real text, so it carries nothing a
reader could lose and the text bar does not apply. Darkening the D would make
the logo a different logo. The standalone logo **files** under `docs/assets` are
the same case and carry the artwork values throughout.

### The dark theme keeps the hue and gives up saturation

The artwork teal measures **1.80:1** on the dark surface — invisible.

> **The brand sheet's own banner leaves it there** and gets away with it, because
> a logo is not text and the eye forgives a large shape. A whole interface does
> not get that licence.

`#3fa5c4` is **194 degrees exactly — the artwork's own hue** — eased from 0.95
saturated to 0.68 as it lifts. Lifting a 0.95-saturated colour without easing it
gives electric cyan: `#0ba5d3` was tried and looked like a different brand.

The gold is lifted to **match**, not to what it needs. `#b38524` would already
pass on the dark surface at 5.18:1, but two halves of one word lifted by
different amounts make the wordmark look assembled from two logos.

| Dark pair | On `--surface` | On `--ground` | On the banner weave |
|---|---|---|---|
| `#3fa5c4` | 6.08:1 | 6.61:1 | 6.33:1 |
| `#c2922b` | 6.12:1 | 6.65:1 | 6.37:1 |

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

1. the sheet read at its own resolution, and the **primary logo** copy used — at
   147×73 source pixels it is the largest of the four that sit on a light
   ground;
2. that crop upscaled bicubically, then each pixel turned into a **coverage
   figure, not a verdict** — how much ink is in it, taken from saturation, and
   split between the two colours by hue. Hue rather than distance to a swatch
   because the artwork is shaded: one teal runs from near-black to a pale
   highlight, and hue is what survives that;
3. the field **blurred very slightly**, to take the JPEG's ringing out *before*
   the contour exists — which is not the same as smoothing the polygon
   afterwards: that pulls corners in, this does not;
4. the **half-coverage contour** taken with marching squares, which interpolates
   along cell edges and therefore lands *between* pixels;
5. simplified hard and re-interpolated as Catmull-Rom curves.

**Three things were tried and are not worth retrying.**

> **Thresholding into a hard mask first.** It rounds every edge to the nearest
> whole pixel and throws away the anti-aliasing — which is exactly the
> information that says where the edge really is. What comes back is a
> staircase, and no amount of smoothing afterwards recovers what the rounding
> discarded; it looked chewed.

> **Averaging all four copies** to beat the compression noise. It is the right
> instinct — four renderings at four sizes, each with its own sampling phase —
> but their bounding boxes are whole pixels, so their scales disagree by about
> 1.5% and no translation can align them. The average tore the D's shoulder off.

> **The banner copy**, which is the largest on the sheet at 158×81. Coverage
> comes from saturation, and that only works over a **light** ground: scaling
> every channel toward black leaves saturation unchanged, so a half-covered
> pixel over the dark weave reads as solid and the whole shape fattens.

> **The source is 147 pixels wide.** That is the ceiling on how crisp this can
> be. The *shapes* are the designer's; the exact edges are a reading of a small
> raster and wobble very slightly against a true vector. At 26px in the
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

### The kit

**The geometry and the layout numbers**

| File | What it is |
|---|---|
| `frontend/src/app/markPaths.ts` | the outlines, and **the only copy** |
| `frontend/src/app/Mark.tsx` | `<Mark>` and `<Wordmark>` — the app bar, sign-in, the admin shell |

**Vector — the masters. Every raster below is an export of one of these.**

| File | What it is |
|---|---|
| `docs/assets/dealerfoss-mark.svg` | the mark alone, cropped to the ink |
| `docs/assets/dealerfoss-lockup.svg` | the primary logo: mark, name, tagline |
| `docs/assets/dealerfoss-wordmark.svg` | the name and tagline, no mark |
| `docs/assets/dealerfoss-badge.svg` | the icon-pack badge |
| `docs/assets/dealerfoss-banner.svg` | the 1200×300 banner |

**Raster — for the places that cannot take a vector**

| File | For |
|---|---|
| `dealerfoss-mark-512.png`, `-1024.png` | transparent, any light ground |
| `dealerfoss-badge-512.png` | avatars, tiles |
| `dealerfoss-lockup-1024.png` | transparent |
| `dealerfoss-lockup-1024.jpg` | on white, for anything that refuses alpha |
| `dealerfoss-banner-1200x300.png` / `.jpg` | the README banner |
| `dealerfoss-social-1200x630.png` | the repository's social preview card |

**The web application's icons**

| File | Asked for by |
|---|---|
| `frontend/public/favicon.svg` | the browser tab |
| `frontend/public/favicon.ico` | anything that will not read an SVG icon — 16/32/48/64 in one file |
| `frontend/public/apple-touch-icon.png` | iOS home screen, which ignores the manifest |
| `frontend/public/icon-192.png`, `icon-512.png` | an installed copy |
| `frontend/public/manifest.webmanifest` | name, icons, theme colour |

**The rasters are drawn from the path data, not screenshotted from the SVGs.**
There is no SVG rasteriser on this machine and adding one would be a dependency
for a job GDI+ already does — so a PNG cannot disagree with the vector it is an
export of. The rasteriser understands `M`, `C` and `Z` only, which is all the
tracer emits, and throws on anything else rather than drawing it wrong.

`dealerfoss-social-1200x630.png` is a **file, not a setting**: GitHub's social
preview is uploaded in the repository's settings and cannot be committed into
place. Upload it there once.

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
| Colours measured off the artwork, both themes, contrast checked | done |
| Wordmark and AUTOMOTIVE SOLUTIONS tagline | done |
| The mark, on screen in both themes | done — **as a trace** |
| The icon pack: tab icon, `.ico`, Apple touch icon, installed-app icons | done |
| Logo, wordmark, badge, banner as SVG | done |
| PNG, JPEG and social-card exports | done |
| The application, the Coming Soon page, and the README | done |
| The mark replaced by the supplied vector | **still open** |
| GitHub social preview *uploaded* in repository settings | **needs a person** |

Only two rows are open, and neither is a missing feature.

**The vector** is a quality question: the mark is on screen and it is the right
mark. Dropping the designer's own outlines into `markPaths.ts` sharpens the
edges and changes nothing else — the component, the five static drawings and
every raster are all regenerated from those two strings.

**The social preview** is a setting rather than a file. The image is committed
at `docs/assets/dealerfoss-social-1200x630.png`; somebody has to upload it under
*Settings → General → Social preview* once.
