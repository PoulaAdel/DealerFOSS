// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RecordsPage — bringing a dealership's records in from a file, and taking them
//   out again.
//
// Usage:
//   Reachable at /records.
//
// Coding Instructions:
//   One rule here is a safeguard rather than a preference. **The real import
//   is not reachable until a practice run has finished on this exact file.**
//   Somebody is about to write thousands of rows into their own business's
//   history; making them look at what would happen first costs one click and
//   prevents the mistake nobody can undo. The button re-locks whenever the
//   file or the kind changes, because a trial of a different file says
//   nothing about this one.
//
//   The exception list is the other half. After nine thousand rows, what a
//   person needs is the twelve that did not work — by the line number they
//   can see in their own spreadsheet, with the row quoted back to them.

import { useCallback, useRef, useState } from 'react';
import { ApiError, api, download, post } from '../../shared/api';
import type {
  ImportJobView,
  ImportKind,
  ImportMode,
  ImportRowView,
} from '../../shared/contracts';
import { useI18n, type MessageKey } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';

/** How long to keep asking before giving up on a job. */
const PollTimeoutMs = 120_000;
const PollEveryMs = 400;

interface Loaded {
  name: string;
  content: string;
}

export function RecordsPage() {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [kind, setKind] = useState<ImportKind>('Customers');
  const [file, setFile] = useState<Loaded | null>(null);

  const [job, setJob] = useState<ImportJobView | null>(null);
  const [problems, setProblems] = useState<ImportRowView[]>([]);
  const [busy, setBusy] = useState<ImportMode | 'export' | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Which file has been rehearsed, so choosing a different one re-locks the
  // real run. Held as the content itself rather than a flag: a flag would
  // survive a change nobody noticed.
  const rehearsed = useRef<string | null>(null);

  const mayApply = file !== null && rehearsed.current === file.content + kind;

  async function choose(chosen: File | null) {
    setError(null);
    setJob(null);
    setProblems([]);
    rehearsed.current = null;

    if (chosen === null) {
      setFile(null);
      return;
    }

    try {
      setFile({ name: chosen.name, content: await readText(chosen) });
    } catch {
      setFile(null);
      setError(t('records.unreadableFile'));
    }
  }

  const run = useCallback(
    async (mode: ImportMode, loaded: Loaded, forKind: ImportKind) => {
      setError(null);
      setBusy(mode);
      setProblems([]);

      try {
        const submitted = await post<ImportJobView>('/migration/imports', {
          kind: forKind,
          mode,
          sourceName: loaded.name,
          content: loaded.content,
        });

        const finished = await waitFor(submitted.id);
        setJob(finished);

        if (mode === 'Trial' && finished.status === 'Completed') {
          rehearsed.current = loaded.content + forKind;
        }

        if (finished.rowsFailed > 0 || finished.rowsSkipped > 0) {
          setProblems(
            await api<ImportRowView[]>(
              `/migration/imports/${finished.id}/rows?problemsOnly=true&limit=200`,
            ),
          );
        }
      } catch (failure) {
        setError(describe(failure));
      } finally {
        setBusy(null);
      }
    },
    [describe],
  );

  async function exportKind(which: ImportKind) {
    setError(null);
    setBusy('export');

    try {
      await download(`/migration/exports/${which}`, `${which.toLowerCase()}.csv`);
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(null);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>{t('records.title')}</h1>
      </header>

      <section className="panel">
        <h2>{t('records.bringIn')}</h2>
        <p>{t('records.bringInLede')}</p>

        <label htmlFor="kind">{t('records.whatIsInIt')}</label>
        <select
          id="kind"
          value={kind}
          onChange={(e) => {
            setKind(e.target.value as ImportKind);
            rehearsed.current = null;
            setJob(null);
            setProblems([]);
          }}
        >
          <option value="Customers">{t('enum.importKind.Customers')}</option>
          <option value="Vehicles">{t('enum.importKind.Vehicles')}</option>
        </select>

        <label htmlFor="file">{t('records.file')}</label>
        <input
          id="file"
          type="file"
          accept=".csv,text/csv"
          onChange={(e) => void choose(e.target.files?.[0] ?? null)}
        />

        <p className="error" aria-live="polite">
          {error ?? ''}
        </p>

        <div className="actions">
          <button
            type="button"
            disabled={file === null || busy !== null}
            onClick={() => file && void run('Trial', file, kind)}
          >
            {busy === 'Trial' ? t('records.practising') : t('records.practice')}
          </button>

          <button
            type="button"
            className="primary"
            disabled={!mayApply || busy !== null}
            onClick={() => file && void run('Apply', file, kind)}
          >
            {busy === 'Apply' ? t('records.importing') : t('records.importForReal')}
          </button>
        </div>

        {file !== null && !mayApply && busy === null ? (
          <p className="note">{t('records.practiseFirst')}</p>
        ) : null}
      </section>

      {job === null ? null : <Report job={job} problems={problems} />}

      <section className="panel">
        <h2>{t('records.takeOut')}</h2>
        <p>{t('records.takeOutLede')}</p>

        <div className="actions">
          <button type="button" disabled={busy !== null} onClick={() => void exportKind('Customers')}>
            {t('records.downloadCustomers')}
          </button>
          <button type="button" disabled={busy !== null} onClick={() => void exportKind('Vehicles')}>
            {t('records.downloadVehicles')}
          </button>
        </div>
      </section>
    </>
  );
}

function Report({ job, problems }: { job: ImportJobView; problems: ImportRowView[] }) {
  const { t } = useI18n();
  const label = useEnumLabel();
  const practice = job.mode === 'Trial';

  if (job.status === 'Failed') {
    return (
      <section className="panel">
        <p className="verdict verdict--bad" role="alert">
          {t('records.couldNotRun')} {job.failureReason ?? ''}
        </p>
      </section>
    );
  }

  // One catalogue sentence with the counts substituted in, rather than five
  // fragments concatenated here. The old version read "Of 1 row(s)" — and
  // "row(s)" is not a thing German, Russian or Arabic can be written in.
  const summary: MessageKey = practice ? 'records.summaryPractice' : 'records.summaryReal';

  return (
    <section className="panel">
      <h2>{practice ? t('records.whatWouldHappen') : t('records.whatHappened')}</h2>

      <p
        className={`verdict ${job.rowsFailed > 0 ? 'verdict--bad' : 'verdict--ok'}`}
        role="status"
      >
        {t(summary, {
          count: job.rowsTotal,
          created: job.rowsCreated,
          updated: job.rowsUpdated,
          skipped: job.rowsSkipped,
          failed: job.rowsFailed,
        })}
      </p>

      {practice ? <p className="note">{t('records.nothingWritten')}</p> : null}

      {problems.length === 0 ? null : (
        <>
          <h3>{t('records.rowsToLookAt')}</h3>
          <p className="note">{t('records.rowsToLookAtLede')}</p>

          <div className="scroll">
            <table>
              <caption className="visually-hidden">
                {t('records.problemCount', { count: problems.length })}
              </caption>
              <thead>
                <tr>
                  <th scope="col">{t('records.colLine')}</th>
                  <th scope="col">{t('records.colWhatHappened')}</th>
                  <th scope="col">{t('records.colTheRow')}</th>
                </tr>
              </thead>
              <tbody>
                {problems.map((row) => (
                  <tr key={row.rowNumber}>
                    <td className="mono num">{row.rowNumber}</td>
                    <td>
                      <span className={`chip chip--${row.outcome.toLowerCase()}`}>
                        {label('importOutcome', row.outcome)}
                      </span>{' '}
                      {row.message}
                    </td>
                    {/* The raw row is quoted exactly as the dealership's file
                        had it — CSV, so left to right whatever the page does. */}
                    <td className="mono raw" dir="ltr">
                      {row.raw}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </section>
  );
}

/**
 * Reads a chosen file as text.
 *
 * `FileReader` rather than the tidier `Blob.text()` on purpose: jsdom does not
 * implement `text()`, so using it would make this path untestable — and
 * polyfilling it in the test setup would mean the tests exercise the polyfill
 * instead of this code. `FileReader` is universal and genuinely runs.
 */
function readText(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error ?? new Error('unreadable'));
    reader.onload = () => resolve(String(reader.result ?? ''));
    reader.readAsText(file);
  });
}

/**
 * Submitting only stages the rows; a background worker does the work. Poll
 * until it has finished rather than assuming a fixed wait is long enough.
 */
async function waitFor(jobId: string): Promise<ImportJobView> {
  const deadline = Date.now() + PollTimeoutMs;

  for (;;) {
    const job = await api<ImportJobView>(`/migration/imports/${jobId}`);
    if (job.status === 'Completed' || job.status === 'Failed') {
      return job;
    }

    if (Date.now() > deadline) {
      // The code is what matters: `useApiMessage` maps it to the reader's
      // language, so the English here is only ever a fallback for a catalogue
      // that somehow lacks the key.
      throw new ApiError(
        0,
        'migration.timeout',
        'That import is taking longer than expected. It is still running — '
          + 'this page just stopped waiting.',
      );
    }

    await new Promise((resolve) => setTimeout(resolve, PollEveryMs));
  }
}
