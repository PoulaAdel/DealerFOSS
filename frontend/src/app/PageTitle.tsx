// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PageTitle — keeps the browser tab's title naming the screen the reader is on.
//
// Usage:
//   Rendered once inside the router, beside <Routes>. It draws nothing.
//
// Coding Instructions:
//   WCAG 2.4.2 (Page Titled, level A). The title was "DealerFOSS" on every one of
//   21 routes — set once in index.html — so somebody with several tabs open, or a
//   screen reader announcing the tab, could not tell Customers from The books.
//
//   THE TITLE IS READ FROM THE PAGE'S OWN <h1>, not from a table of routes. A
//   table would name today's twenty screens and silently miss tomorrow's, which
//   is how this was missed the first time; the heading is already translated and
//   already what each screen calls itself, so no catalogue string is added and a
//   new screen is covered the moment it has a heading. A MutationObserver is used
//   because several screens draw their <h1> only after their data arrives.
//
//   Two screens cannot use their heading as it is. The dashboard's <h1> is the
//   month ("October 2026") — data, not a name — so it takes its navigation label.
//   The signed-out screens' <h1> is the brand, so they take their submit button's
//   words ("Sign in") plus anything the heading adds (the administration badge).
//   Text hidden from assistive technology is dropped, so the wordmark reads once
//   as "DealerFOSS" rather than as its two coloured halves.

import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { useI18n } from '../shared/i18n';

/** The heading's words as an assistive technology would read them. */
function spokenText(element: Element): string {
  const copy = element.cloneNode(true) as Element;
  copy.querySelectorAll('[aria-hidden="true"]').forEach((hidden) => hidden.remove());
  return (copy.textContent ?? '').replace(/\s+/g, ' ').trim();
}

export function PageTitle() {
  const { pathname } = useLocation();
  const { t } = useI18n();

  useEffect(() => {
    const brand = t('app.name');
    const dashboard = t('nav.dashboard');
    let timer: ReturnType<typeof setTimeout> | undefined;

    function apply() {
      const heading = document.querySelector('h1');
      const spoken = heading ? spokenText(heading) : '';
      let page = spoken;

      if (pathname === '/dashboard') {
        page = dashboard;
      } else if (spoken === brand || spoken.startsWith(`${brand} `)) {
        const extra = spoken.slice(brand.length).trim();
        const action = document.querySelector('main button[type="submit"]')?.textContent?.trim();
        page = [action, extra].filter(Boolean).join(' — ');
      }

      document.title = page && page !== brand ? `${page} — ${brand}` : brand;
    }

    // Coalesced: a list rendering fifty rows is one change to this. A timer and
    // not requestAnimationFrame, which does not fire in a tab that is not being
    // drawn — measured: the title stayed "DealerFOSS" on a heading that had
    // arrived, and a tab opened in the background would have kept it.
    const schedule = () => {
      clearTimeout(timer);
      timer = setTimeout(apply, 50);
    };

    apply();
    const observer = new MutationObserver(schedule);
    observer.observe(document.body, { childList: true, subtree: true, characterData: true });

    return () => {
      observer.disconnect();
      clearTimeout(timer);
    };
  }, [pathname, t]);

  return null;
}
