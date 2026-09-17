// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   NavGroup — one disclosure in the shell's top navigation. The bar used to
//   render sixteen NavLinks flat in one row, wrapping into two or three
//   ragged lines depending on viewport width, with no signal that any of
//   them were related. This groups them by the work they belong to (Sales,
//   Service, Accounting, People & security) so the bar reads as five or six
//   named destinations instead of a list to be scanned word by word.
//
// Usage:
//   <NavGroup label={t('nav.groupSales')} active={somePathMatches}>
//     <NavLink to="/customers">...</NavLink>
//     ...
//   </NavGroup>
//
//   The caller decides `active` (whether the current route is inside this
//   group) so the trigger can carry the same highlight a top-level NavLink
//   gets, rather than every group looking unselected while a child page is
//   open.
//
// Coding Instructions:
//   CLOSES ON OUTSIDE CLICK, ESCAPE, AND ON NAVIGATING — a menu that outlives
//   the click that was supposed to use it is the thing this component exists
//   to not be. Escape also returns focus to the trigger, the same contract
//   ShortcutsPanel and Confirm already keep.
//
//   NOT A ROVING TABINDEX MENU. The links inside stay in normal Tab order;
//   ArrowDown from the trigger only moves focus to the first one, because a
//   full arrow-key menu is more machinery than five or six links justify and
//   Tab already reaches every one of them.

import { useEffect, useRef, useState, type ReactNode } from 'react';

export function NavGroup({
  label,
  active,
  children,
}: {
  label: string;
  active: boolean;
  children: ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    function onPointerDown(event: MouseEvent) {
      if (!root.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setOpen(false);
        trigger.current?.focus();
      }
    }

    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  return (
    <div className="navgroup" ref={root}>
      <button
        type="button"
        ref={trigger}
        className={active ? 'navgroup__trigger navgroup__trigger--active' : 'navgroup__trigger'}
        aria-haspopup="true"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown') {
            event.preventDefault();
            setOpen(true);
            requestAnimationFrame(() => {
              const first = menu.current?.querySelector('a');
              (first as HTMLElement | null)?.focus();
            });
          }
        }}
      >
        {label}
        <svg
          className="navgroup__caret"
          width="10"
          height="6"
          viewBox="0 0 10 6"
          aria-hidden="true"
        >
          <path d="M1 1l4 4 4-4" fill="none" stroke="currentColor" strokeWidth="1.5" />
        </svg>
      </button>

      {open ? (
        <div className="navgroup__menu" ref={menu} onClick={() => setOpen(false)}>
          {children}
        </div>
      ) : null}
    </div>
  );
}
