'use client';

/**
 * One tab, the survey. The panel counts what `lib/survey.ts` defines: an option's bar is its
 * share of the responses, free text is listed as written, and a "Other: ..." line is counted
 * under its option and quoted with the texts.
 *
 * The key lives in this component's state and in every request's Authorization header, nowhere
 * else: a refresh forgets it, which is the cheapest possible session to reason about.
 */
import { useState } from 'react';
import { OTHER_PREFIX, SURVEY, type Answers, type Question } from '../../lib/survey';
import styles from './Results.module.css';

interface StoredResponse {
  id: number;
  submittedAt: string;
  answers: Answers;
}

const values = (r: StoredResponse, q: Question): string[] => {
  const v = r.answers[q.id];
  if (Array.isArray(v)) return v;
  return typeof v === 'string' ? [v] : [];
};

function OptionCounts({ q, responses }: { q: Question; responses: StoredResponse[] }) {
  const options = q.kind === 'scale' ? ['1', '2', '3', '4', '5'] : (q.options ?? []);
  const total = responses.filter((r) => values(r, q).length > 0).length;
  const others = responses.flatMap((r) => values(r, q).filter((v) => v.startsWith(OTHER_PREFIX)));
  const count = (option: string) =>
    responses.filter((r) => values(r, q).some((v) => v === option || (q.other && option === q.options![q.options!.length - 1] && v.startsWith(OTHER_PREFIX)))).length;
  return (
    <>
      <ul className={styles.counts}>
        {options.map((option) => {
          const n = count(option);
          return (
            <li key={option} className={styles.count}>
              <span className={styles.bar} style={{ width: total ? `${(100 * n) / total}%` : 0 }} />
              <span className={styles.option}>{option}</span>
              <span className={styles.n}>{n}</span>
            </li>
          );
        })}
      </ul>
      {others.length > 0 ? <p className={styles.others}>{others.map((v) => v.slice(OTHER_PREFIX.length)).join(' · ')}</p> : null}
    </>
  );
}

function Texts({ q, responses }: { q: Question; responses: StoredResponse[] }) {
  const texts = responses.flatMap((r) => values(r, q));
  if (texts.length === 0) return <p className={styles.none}>No answers.</p>;
  return (
    <ul className={styles.texts}>
      {texts.map((t, i) => (
        <li key={i} className={styles.textAnswer}>
          {t}
        </li>
      ))}
    </ul>
  );
}

export function Results() {
  const [key, setKey] = useState('');
  const [responses, setResponses] = useState<StoredResponse[] | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    setError(undefined);
    try {
      const res = await fetch('/api/survey', { headers: { authorization: `Bearer ${key}` } });
      const body = (await res.json()) as { responses?: StoredResponse[]; error?: string };
      if (!res.ok || !body.responses) {
        setError(body.error ?? `The server answered ${res.status}.`);
      } else {
        setResponses(body.responses);
      }
    } catch {
      setError('The request did not go through.');
    }
    setLoading(false);
  };

  if (responses === undefined) {
    return (
      <form
        className={styles.gate}
        onSubmit={(e) => {
          e.preventDefault();
          void load();
        }}
      >
        <input
          type="password"
          className={styles.key}
          value={key}
          onChange={(e) => setKey(e.target.value)}
          placeholder="Admin key"
          aria-label="Admin key"
          autoFocus
        />
        <button type="submit" className={styles.enter} disabled={loading || key.length === 0}>
          {loading ? 'Checking…' : 'Open'}
        </button>
        {error ? <p className={styles.error}>{error}</p> : null}
      </form>
    );
  }

  return (
    <div>
      <div className={styles.tabs} role="tablist">
        <button role="tab" aria-selected="true" className={styles.tab} type="button">
          Sim racer survey
        </button>
        <button type="button" className={styles.refresh} onClick={() => void load()} disabled={loading}>
          {loading ? 'Loading…' : 'Refresh'}
        </button>
      </div>
      <p className={styles.total}>
        {responses.length} response{responses.length === 1 ? '' : 's'}
      </p>
      {SURVEY.map((section) => (
        <section key={section.title} className={styles.part}>
          <h2 className="h2">{section.title}</h2>
          {section.questions.map((q) => (
            <div key={q.id} className={styles.question}>
              <h3 className={`h3 ${styles.prompt}`}>
                <span className={styles.number}>{q.id.slice(1)}</span>
                {q.label}
              </h3>
              {q.kind === 'text' || q.kind === 'paragraph' ? <Texts q={q} responses={responses} /> : <OptionCounts q={q} responses={responses} />}
            </div>
          ))}
        </section>
      ))}
    </div>
  );
}
