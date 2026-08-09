// InlineEdit — changing one value where it is written, instead of opening a form
// to change it.
//
// Use:  <InlineEdit
//         label={t('diary.colHours')}
//         value={hours}
//         display={hours === null ? t('diary.unestimated') : format.number(hours)}
//         onSave={async (next) => post(...)}
//       />
//
// Edit: five things here are the reason this is a component and not a pattern
//       people re-type.
//
//       IDLE IS A BUTTON, NOT A DIV WITH AN onClick. A value you can change has
//       to be reachable by keyboard and has to announce itself as something that
//       does anything. A div with a click handler is invisible to a screen
//       reader and unreachable by Tab, which is how "inline editing" usually
//       ships as a mouse-only feature.
//
//       ESCAPE CANCELS AND RESTORES. Somebody who starts typing in the wrong row
//       needs a way out that is not "work out what it said before".
//
//       BLUR SAVES, and so does Enter. Clicking away from a half-typed value and
//       losing it is the single most annoying thing an inline editor does.
//
//       THE SAVE IS CONFIRMED, BRIEFLY AND IN WORDS. A value that changes back
//       into text with no acknowledgement leaves somebody wondering whether it
//       took. The tick carries role="status" so it is announced, and fades on
//       the shared --beat so it reads as a change rather than a flicker.
//
//       A FAILED SAVE PUTS THE VALUE BACK AND SAYS WHY. It does not keep the
//       typed value on screen as though it had been accepted — the record did
//       not change, and the screen must not imply it did.

import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { useI18n } from './i18n';
import { useApiMessage } from './i18n/apiMessage';

type State =
  | { kind: 'idle' }
  | { kind: 'editing' }
  | { kind: 'saving' }
  | { kind: 'saved' }
  | { kind: 'failed'; message: string };

/** How long the confirmation stays before fading. Long enough to read. */
const ConfirmationMs = 2000;

export function InlineEdit({
  label,
  value,
  display,
  onSave,
  inputMode = 'text',
  disabled = false,
}: {
  /** What this value IS, for somebody who cannot see the column heading. */
  label: string;
  /** The current value, as it should appear in the box when editing starts. */
  value: string;
  /** How it reads when not being edited — formatted, or a placeholder. */
  display: string;
  /** Returns when the change is saved. Throwing leaves the old value showing. */
  onSave: (next: string) => Promise<void>;
  inputMode?: 'text' | 'numeric' | 'decimal';
  disabled?: boolean;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [state, setState] = useState<State>({ kind: 'idle' });
  const [draft, setDraft] = useState(value);
  const input = useRef<HTMLInputElement>(null);

  // Focus lands in the box when editing opens, so the keyboard route is one
  // key rather than one key and then a hunt.
  useEffect(() => {
    if (state.kind === 'editing') {
      input.current?.focus();
      input.current?.select();
    }
  }, [state.kind]);

  useEffect(() => {
    if (state.kind !== 'saved') {
      return;
    }

    const timer = setTimeout(() => setState({ kind: 'idle' }), ConfirmationMs);
    return () => clearTimeout(timer);
  }, [state.kind]);

  // A value changed elsewhere — another edit, a reload — must not be overwritten
  // by a stale draft the next time editing opens.
  useEffect(() => {
    if (state.kind === 'idle' || state.kind === 'saved') {
      setDraft(value);
    }
  }, [value, state.kind]);

  async function commit() {
    if (draft === value) {
      setState({ kind: 'idle' });
      return;
    }

    setState({ kind: 'saving' });

    try {
      await onSave(draft);
      setState({ kind: 'saved' });
    } catch (failure) {
      // The record did not change, so neither does what is on screen.
      setDraft(value);
      setState({ kind: 'failed', message: describe(failure) });
    }
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.preventDefault();
      void commit();
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      setDraft(value);
      setState({ kind: 'idle' });
    }
  }

  if (state.kind === 'editing' || state.kind === 'saving') {
    return (
      <span className="inline-edit inline-edit--open">
        <input
          ref={input}
          className="inline-edit__box"
          aria-label={label}
          value={draft}
          inputMode={inputMode}
          disabled={state.kind === 'saving'}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={onKeyDown}
          onBlur={() => void commit()}
          dir="ltr"
        />
      </span>
    );
  }

  return (
    <span className="inline-edit">
      <button
        type="button"
        className="inline-edit__value"
        disabled={disabled}
        // Says what it is and what pressing it does, because "2.5" alone tells
        // a screen-reader user nothing about either.
        aria-label={t('inline.changeThis', { label, value: display })}
        onClick={() => setState({ kind: 'editing' })}
      >
        {display}
      </button>

      {state.kind === 'saved' ? (
        <span className="inline-edit__saved" role="status">
          {t('inline.saved')}
        </span>
      ) : null}

      {state.kind === 'failed' ? (
        <span className="inline-edit__failed" role="alert">
          {state.message}
        </span>
      ) : null}
    </span>
  );
}
