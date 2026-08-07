// Shortcuts — the keyboard map, and the panel that admits it exists.
//
// Use:  rendered by the shell; opened with "?" or the button beside it.
// Edit: an undiscoverable shortcut is a shortcut nobody uses. The list below is
//       the only description of the bindings, and it is generated from the same
//       table the shell binds — so a shortcut cannot be added without appearing
//       here, and cannot be removed while still being advertised.

import { useEffect, useRef } from 'react';

export interface Shortcut {
  /** As useHotkeys binds it: "g d", "?", "[". */
  keys: string;
  says: string;
}

/**
 * Every binding, in the order they are worth learning. The shell turns the `keys`
 * into handlers; this panel turns them into instructions.
 */
export const shortcuts: Shortcut[] = [
  { keys: 'g d', says: 'Go to the dashboard' },
  { keys: 'g s', says: 'Go to stock' },
  { keys: 'g c', says: 'Go to customers' },
  { keys: 'g e', says: 'Go to enquiries' },
  { keys: 'g l', says: 'Go to deals' },
  { keys: 'g w', says: 'Go to the workshop' },
  { keys: 'g p', says: 'Go to parts' },
  { keys: 'g b', says: 'Go to the books' },
  { keys: '[', says: 'On the dashboard: the month before' },
  { keys: ']', says: 'On the dashboard: the month after' },
  { keys: 't', says: 'On the dashboard: back to this month' },
  { keys: '?', says: 'Show this list' },
];

/** How a key is printed. "g d" is two keys pressed one after the other. */
function Keys({ keys }: { keys: string }) {
  return (
    <span>
      {keys.split(' ').map((key, index) => (
        <kbd key={key}>
          {index > 0 ? ' ' : ''}
          {key === ' ' ? 'Space' : key}
        </kbd>
      ))}
    </span>
  );
}

export function ShortcutsPanel({ onClose }: { onClose: () => void }) {
  const dialog = useRef<HTMLDivElement>(null);

  // Focus moves into the panel, so Escape and Tab do what somebody who never
  // touched the mouse expects. Without this the panel opens behind the keyboard
  // rather than in front of it.
  useEffect(() => dialog.current?.focus(), []);

  return (
    <div
      className="sheet"
      role="dialog"
      aria-modal="true"
      aria-label="Keyboard shortcuts"
      tabIndex={-1}
      ref={dialog}
      onKeyDown={(event) => {
        if (event.key === 'Escape') {
          onClose();
        }
      }}
    >
      <div className="sheet__head">
        <h2>Keyboard shortcuts</h2>
        <button type="button" onClick={onClose}>
          Close
        </button>
      </div>

      <dl className="keys">
        {shortcuts.map((shortcut) => (
          <div key={shortcut.keys}>
            <dt>
              <Keys keys={shortcut.keys} />
            </dt>
            <dd>{shortcut.says}</dd>
          </div>
        ))}
      </dl>

      <p className="note">
        Shortcuts are ignored while you are typing in a field, so they never eat a
        character you meant to write.
      </p>
    </div>
  );
}
