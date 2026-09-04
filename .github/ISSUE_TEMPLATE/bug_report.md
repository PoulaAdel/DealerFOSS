---
name: Bug report
about: Something behaves differently from what the documentation or the code claims
title: ''
labels: bug
---

<!--
Not for security vulnerabilities. Those go through private reporting —
see .github/SECURITY.md. A public issue is the wrong place to publish one.
-->

## What happened

<!-- What you saw. Include the exact error code if the API returned one: every
     refusal in this product has a stable code, and it is more useful than the
     message. -->

## What you expected, and what said so

<!-- Name the document, file header, or test. This project's documents separate
     the specification from what is built (docs/00-Workbook.md), so
     "docs/06 §6 says commands accept an idempotency key" is a documentation bug
     rather than a code bug — and both are worth reporting, just differently. -->

## How to reproduce

<!-- Ideally the commands. Which dealer organization, which account: the
     development seed has accounts with deliberately different permissions
     (docs/ONBOARDING.md), and "it returned 403" is expected for some of them. -->

## Environment

- Commit:
- Database: LocalDB / SQL Server container / other
- Browser (if a screen): 
- Language the interface was in: <!-- Six are supported; direction and plural
     rules differ, and defects have been specific to one language before. -->

## Verification

<!-- Please say whether these pass on your checkout, because it separates
     "broken here" from "broken everywhere": -->

- [ ] `dotnet build DealerFOSS.slnx -c Release`
- [ ] `dotnet test DealerFOSS.slnx -c Release`
- [ ] `& .\deploy\verify-e2e.ps1` prints PASS
