// RecordsPage — bringing a dealership's records in from a file, and taking them
// out again.
//
// Use:  reachable at /records.
// Edit: one rule here is a safeguard rather than a preference. **The real import
//       is not reachable until a practice run has finished on this exact file.**
//       Somebody is about to write thousands of rows into their own business's
//       history; making them look at what would happen first costs one click and
//       prevents the mistake nobody can undo. The button re-locks whenever the
//       file or the kind changes, because a trial of a different file says
//       nothing about this one.
//
//       The exception list is the other half. After nine thousand rows, what a
//       person needs is the twelve that did not work — by the line number they
//       can see in their own spreadsheet, with the row quoted back to them.

import { useCallback, useRef, useState } from 'react';
import { ApiError, api, download, post } from '../../shared/api';
import type {
  ImportJobView,
  ImportKind,
  ImportMode,
  ImportRowView,
} from '../../shared/contracts';

/** How long to keep asking before giving up on a job. */
const PollTimeoutMs = 120_000;
const PollEveryMs = 400;

interface Loaded {
  name: string;
  content: string;
}

export function RecordsPage() {
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
      setError('That file could not be read. Is it a text CSV?');
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
        setError(messageFor(failure));
      } finally {
        setBusy(null);
      }
    },
    [],
  );

  async function exportKind(which: ImportKind) {
    setError(null);
    setBusy('export');

    try {
      await download(`/migration/exports/${which}`, `${which.toLowerCase()}.csv`);
    } catch (failure) {
      setError(messageFor(failure));
    } finally {
      setBusy(null);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>Records</h1>
      </header>

      <section className="panel">
        <h2>Bring records in</h2>
        <p>
          A spreadsheet exported from your old system, saved as CSV. Nothing is
          written until you have run it as a practice first.
        </p>

        <label htmlFor="kind">What is in the file</label>
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
          <option value="Customers">Customers</option>
          <option value="Vehicles">Vehicles</option>
        </select>

        <label htmlFor="file">The file</label>
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
            {busy === 'Trial' ? 'Trying it…' : 'Practice run'}
          </button>

          <button
            type="button"
            className="primary"
            disabled={!mayApply || busy !== null}
            onClick={() => file && void run('Apply', file, kind)}
          >
            {busy === 'Apply' ? 'Importing…' : 'Import for real'}
          </button>
        </div>

        {file !== null && !mayApply && busy === null ? (
          <p className="note">
            Run the practice first. It changes nothing and tells you exactly what
            the real one will do.
          </p>
        ) : null}
      </section>

      {job === null ? null : <Report job={job} problems={problems} />}

      <section className="panel">
        <h2>Take records out</h2>
        <p>
          Downloads everything of that kind as a CSV. It is the same shape this
          page accepts back, so you can move it anywhere — including into another
          system entirely.
        </p>

        <div className="actions">
          <button type="button" disabled={busy !== null} onClick={() => void exportKind('Customers')}>
            Download customers
          </button>
          <button type="button" disabled={busy !== null} onClick={() => void exportKind('Vehicles')}>
            Download vehicles
          </button>
        </div>
      </section>
    </>
  );
}

function Report({ job, problems }: { job: ImportJobView; problems: ImportRowView[] }) {
  const practice = job.mode === 'Trial';

  if (job.status === 'Failed') {
    return (
      <section className="panel">
        <p className="verdict verdict--bad" role="alert">
          That import could not be run. {job.failureReason ?? ''}
        </p>
      </section>
    );
  }

  return (
    <section className="panel">
      <h2>{practice ? 'What would happen' : 'What happened'}</h2>

      <p
        className={`verdict ${job.rowsFailed > 0 ? 'verdict--bad' : 'verdict--ok'}`}
        role="status"
      >
        {practice
          ? `Of ${job.rowsTotal} row(s): ${job.rowsCreated} would be added, `
            + `${job.rowsUpdated} already here, ${job.rowsSkipped} skipped, `
            + `${job.rowsFailed} could not be read.`
          : `Of ${job.rowsTotal} row(s): ${job.rowsCreated} added, `
            + `${job.rowsUpdated} already here, ${job.rowsSkipped} skipped, `
            + `${job.rowsFailed} refused.`}
      </p>

      {practice ? (
        <p className="note">Nothing has been written. This was a practice run.</p>
      ) : null}

      {problems.length === 0 ? null : (
        <>
          <h3>Rows to look at</h3>
          <p className="note">
            The line number is the one you see in your spreadsheet, and the row is
            quoted exactly as it arrived. Fix the file and run it again — nothing
            here edits what you sent.
          </p>

          <div className="scroll">
            <table>
              <caption className="visually-hidden">
                {problems.length} rows needing attention
              </caption>
              <thead>
                <tr>
                  <th scope="col">Line</th>
                  <th scope="col">What happened</th>
                  <th scope="col">The row</th>
                </tr>
              </thead>
              <tbody>
                {problems.map((row) => (
                  <tr key={row.rowNumber}>
                    <td className="mono num">{row.rowNumber}</td>
                    <td>
                      <span className={`chip chip--${row.outcome.toLowerCase()}`}>
                        {row.outcome}
                      </span>{' '}
                      {row.message}
                    </td>
                    <td className="mono raw">{row.raw}</td>
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

function messageFor(failure: unknown): string {
  return failure instanceof ApiError
    ? failure.message
    : 'Something went wrong. Try again.';
}
