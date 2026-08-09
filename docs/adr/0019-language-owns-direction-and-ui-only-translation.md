# ADR-0019 — The language owns the direction, and only the UI is translated

**Status:** Accepted · **Date:** 2026-08-09

## Context

The application ships in English, French, German, Russian and Arabic. Two
questions had to be settled before any of it was written, and both had a
plausible-looking wrong answer that would have been expensive to undo.

**Which way the page runs.** The build already had a light/dark theme control
and, beside it, a separate LTR/RTL switcher. Both were stored independently and
applied to `<html>` as attributes. That made "Arabic, left to right" a state a
user could select, and "English, right to left" as well.

**What gets translated.** A dealer management system is a system of record. It
holds names, vehicle descriptions, part numbers, chart-of-accounts names, notes
staff typed, and rows a dealership imported from their previous system. It also
holds protocol values — `OnHold`, `Submitted`, `Invoiced` — that are stored in
database columns but that nobody typed.

## Decision

**Direction is a property of the language, not a setting.** `LANGUAGES` in
`shared/i18n/languages.ts` carries `direction` alongside the code and the native
name. Choosing a language sets both `lang` and `dir` on `<html>`. There is no
independent direction control, and the old one was removed.

**Only UI vocabulary is translated. Records are rendered exactly as stored.**
The boundary is *who wrote the string*:

- In `shared/i18n/locales/*` → ours → translated.
- Arrived in an API response as record content → printed verbatim.

**API status enums are UI vocabulary, not records.** They get a translated
display label via `useEnumLabel()`. The stored value is unchanged and CSS class
names still key off the raw value, so nothing about behaviour depends on the
reader's language.

**Server error messages split by who they are about.** Codes about the *reader*
— your session ended, sign in again, this browser's token no longer matches —
are mapped to catalogue keys in `shared/i18n/apiMessage.ts`. Codes about a
*record* keep the server's wording.

## Consequences

**Good.** The stylesheet was already written in logical properties
(`margin-inline`, `inline-start`), so one `dir` attribute mirrors every margin,
border and table column at once — RTL cost almost no CSS. `lang` being set with
it is what lets a screen reader pick the right voice. English is the schema:
`MessageKey` is `keyof typeof en` and the other four catalogues are typed
`Catalogue`, so a key added and not translated fails `npm run typecheck`.

**Accepted cost, one.** Some server refusals are English on an otherwise
translated screen. A blanket mapping of every `*.forbidden` code to one generic
translated sentence was tried and **reverted**: it turned "you cannot add
customers" into "you do not have permission to see this", which says the
opposite of what happened. Precision beat uniformity. Closing this properly
means the server learning the reader's language — a backend change, not a
frontend one.

**Accepted cost, two.** Codes and identifiers need `dir="ltr"` individually —
VINs, stock numbers, account codes, recovery codes, the TOTP enrolment secret,
email addresses, and raw CSV rows. Without it the bidirectional algorithm
reorders their groups inside an Arabic paragraph. A VIN read out in the wrong
order is a different car; a mis-transcribed enrolment secret locks somebody out
of their own account. This is a rule contributors must remember, and the reason
is written at each site.

**Accepted cost, three.** A sentence with one emphasised value cannot be built
from JSX fragments, because German and Arabic move the value within the
sentence. `shared/i18n/Emphasised.tsx` splits the *finished* translation around
the substituted value instead. Slightly indirect; the alternative is emphasis
that lands on the wrong words in two of five languages.

## Alternatives rejected

**Keep the direction toggle and let Arabic default to RTL.** Rejected: it leaves
a broken combination reachable, and "why is my Arabic left-aligned" becomes a
support question with a settings answer rather than a bug that cannot happen.

**Take `i18next`.** Rejected on the same grounds as the PDF library in ADR-018:
a dependency needs a licence review and an advisory history, and `NuGetAudit`'s
frontend counterpart (`npm audit --audit-level=high`) fails the build on a high
advisory. The in-house module is about 250 lines, and the typed-key guarantee —
a missing translation is a compile error — is stronger than what a runtime
lookup gives. Plurals go to `Intl.PluralRules`, which ships in the browser and
is maintained against CLDR by the engine, so no plural tables live here.

**Hand-write plural pairs.** Rejected. Russian has four categories and Arabic
six; `n === 1` is wrong for most numbers in both. It was already wrong in
English: the stock list rendered "1 vehicles in stock" on a route the dashboard
links to.

**Translate everything, including records.** Rejected by the maintainer,
2026-08-09. A record that changed with the reader's language would disagree with
the paperwork and with what staff typed.
