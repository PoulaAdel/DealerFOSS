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

**Not in the repository, and what is on screen is a placeholder.**

The mark is the winged F+D monogram on the brand sheet. It is artwork: it cannot
be reproduced from a picture of itself, and hand-tracing it was attempted twice
and rejected twice. What ships today is a geometric stand-in in
`frontend/src/app/Mark.tsx`, wearing `--brand-teal` so that it does not fight the
wordmark beside it.

**To finish this**, save the vector from the brand sheet's *SVG vector files*
panel to:

```
frontend/src/assets/dealerfoss-mark.svg
```

Then the component is replaced rather than adjusted: the mark alone at 26px in
the application bar, the full lockup on sign-in. An SVG is preferable to the PNG
because the bar renders it at 26px and the sign-in page at several times that.

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
| The mark itself | **waiting on the SVG** |
| Favicon and application icons | **waiting on the SVG** |
| The icon-pack circular badge treatments | not started |
