---
name: Tax pack review
about: Check a jurisdiction's sales tax rates and rules against the official source
title: 'Tax pack: '
labels: compliance, tax-pack
---

<!-- Read docs/REVIEWING-A-TAX-PACK.md first. It is written for a tax
     professional rather than a programmer, and it explains what you are and are
     not signing. Nothing here is tax advice and the project gives none: you are
     checking that a file matches what an authority publishes.

     You do not need an account on anyone's installation. THIS ISSUE IS THE
     RECORD OF YOUR REVIEW: it carries your name, and a pack file will quote it
     and link back here. -->

## The jurisdiction

<!-- Country, and the state, province or region. If the rate varies by county or
     city, say so here — a pack carrying only a national or state rate will
     undercharge every deal in a place that levies its own. -->

## Official sources

<!-- Link the authority's OWN pages, not a summary site, and give the date you
     read each one. Somebody has to be able to go back later and find the same
     figure. One line each:

       https://example.gov/sales-tax   read 2026-11-05   state rate
-->

## The rates

<!-- For each: the percentage, which authority levies it (state / county / city /
     special district), the area it applies to, and the dates it is in force.

     In the file a rate is a FRACTION — seven percent is 0.07 — but write it here
     however the authority writes it and say which. -->

## The three flags

<!-- These decide what the rate is charged ON, and they are the heart of the
     review. Answer each yes or no, and link the publication that says so. -->

- **Does the trade-in allowance come off the taxed amount?** (`tradeInReducesBasis`)
  <!-- Most US states: yes. California: NO — it taxes the full price. This one is
       worth the most money of the three. -->

- **Is the dealer's documentation fee taxed?** (`documentationFeeIsTaxable`)

- **Are registration, title and delivery fees taxed?** (`otherFeesAreTaxable`)
  <!-- Usually no, because they are collected on somebody else's behalf. -->

## Anything the flags cannot express

<!-- THE MOST USEFUL BOX ON THIS FORM. Please do not pick the nearest flag if
     none of them fits — write what the jurisdiction actually does.

     Michigan is the worked example: it CAPS the trade-in credit rather than
     allowing or disallowing it, and a true/false flag has nowhere to put a cap.
     So Michigan is a place we say we cannot yet describe, instead of
     approximating it.

     Other things that do not fit today: a tiered rate, a rate that changes above
     a price threshold, and whether a service contract sold with the car is
     taxable.

     "This jurisdiction needs something you do not have" is a result, not a
     failure. Leave this blank only if the three flags genuinely cover it. -->

## Who checked this, and when

<!-- Your name, and your professional qualification or role if you would like it
     on the record. The date you checked the sources above.

     What happens next: a pack file is written from this issue, quoting this
     attestation and linking back here, and the file's fingerprint covers those
     comments — so if anybody later edits your name or a rate you signed off, the
     fingerprint changes and the acceptance visibly no longer covers that file.

     An administrator on an installation then records that a named outside review
     exists and that the file is the one it covers. They are not claiming to have
     read your sources themselves, and marking a pack reviewed does not start
     charging anybody: each dealership must separately turn it on. -->
