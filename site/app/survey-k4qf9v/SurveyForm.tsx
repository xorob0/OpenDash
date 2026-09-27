'use client';

/**
 * The form is rendered from `lib/survey.ts`, so the questions here are the questions the API
 * accepts and the admin panel counts, with nothing retyped.
 */
import { useState } from 'react';
import { OTHER_PREFIX, QUESTIONS, SURVEY, type Question } from '../../lib/survey';
import styles from './SurveyForm.module.css';

type Draft = Record<string, string | string[]>;

const asArray = (v: string | string[] | undefined): string[] => (Array.isArray(v) ? v : []);
const asText = (v: string | string[] | undefined): string => (typeof v === 'string' ? v : '');

export function SurveyForm() {
  const [draft, setDraft] = useState<Draft>({});
  const [otherText, setOtherText] = useState<Record<string, string>>({});
  const [state, setState] = useState<'editing' | 'sending' | 'done' | 'failed'>('editing');

  const set = (id: string, value: string | string[]) => setDraft((d) => ({ ...d, [id]: value }));

  const toggle = (q: Question, option: string) => {
    const current = asArray(draft[q.id]);
    set(q.id, current.includes(option) ? current.filter((v) => v !== option) : [...current, option]);
  };

  /** The submission: blanks dropped, and a ticked "Another one" carrying its free line. */
  const payload = (): Draft => {
    const out: Draft = {};
    for (const q of QUESTIONS) {
      const v = draft[q.id];
      if (q.kind === 'checks') {
        let values = asArray(v);
        if (q.other && values.includes(q.options![q.options!.length - 1]!)) {
          const line = (otherText[q.id] ?? '').trim();
          if (line) values = values.map((x) => (x === q.options![q.options!.length - 1] ? OTHER_PREFIX + line : x));
        }
        if (values.length > 0) out[q.id] = values;
      } else {
        const text = asText(v).trim();
        if (text) out[q.id] = text;
      }
    }
    return out;
  };

  const send = async () => {
    setState('sending');
    try {
      const res = await fetch('/api/survey', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(payload()),
      });
      setState(res.ok ? 'done' : 'failed');
    } catch {
      setState('failed');
    }
  };

  if (state === 'done') {
    return (
      <p className={`prose ${styles.thanks}`}>
        Sent. Thank you: this is the kind of answer a roadmap gets built from. If a friend races too, the address of this page is yours to pass on.
      </p>
    );
  }

  const answered = Object.keys(payload()).length;

  return (
    <div className={styles.form}>
      {SURVEY.map((section, s) => (
        <section key={section.title} className={styles.part}>
          <h2 className={`h2 ${styles.partTitle}`}>
            <span className={styles.partNumber}>{String(s + 1).padStart(2, '0')}</span>
            {section.title}
          </h2>
          {section.note ? <p className={`prose ${styles.partNote}`}>{section.note}</p> : null}
          {section.questions.map((q) => (
            <fieldset key={q.id} className={styles.question}>
              <legend className={styles.prompt}>
                <span className={styles.number}>{q.id.slice(1)}</span>
                {q.prompt}
                {q.hint ? <span className={styles.hint}>{q.hint}</span> : null}
              </legend>

              {q.kind === 'checks' || q.kind === 'radio' ? (
                <div className={styles.options}>
                  {q.options!.map((option) => (
                    <label key={option} className={styles.option}>
                      <input
                        type={q.kind === 'checks' ? 'checkbox' : 'radio'}
                        name={q.id}
                        checked={q.kind === 'checks' ? asArray(draft[q.id]).includes(option) : draft[q.id] === option}
                        onChange={() => (q.kind === 'checks' ? toggle(q, option) : set(q.id, option))}
                      />
                      <span>{option}</span>
                    </label>
                  ))}
                  {q.other && asArray(draft[q.id]).includes(q.options![q.options!.length - 1]!) ? (
                    <input
                      type="text"
                      className={styles.text}
                      placeholder="Which one?"
                      aria-label="Which one?"
                      value={otherText[q.id] ?? ''}
                      onChange={(e) => setOtherText((o) => ({ ...o, [q.id]: e.target.value }))}
                    />
                  ) : null}
                </div>
              ) : null}

              {q.kind === 'scale' ? (
                <>
                  <div className={styles.scale}>
                    {['1', '2', '3', '4', '5'].map((v) => (
                      <label key={v} className={styles.step}>
                        <input type="radio" name={q.id} checked={draft[q.id] === v} onChange={() => set(q.id, v)} />
                        <span>{v}</span>
                      </label>
                    ))}
                  </div>
                  <div className={styles.ends}>
                    <span>{q.scaleEnds![0]}</span>
                    <span>{q.scaleEnds![1]}</span>
                  </div>
                </>
              ) : null}

              {q.kind === 'text' ? (
                <input type="text" className={styles.text} value={asText(draft[q.id])} onChange={(e) => set(q.id, e.target.value)} />
              ) : null}
              {q.kind === 'paragraph' ? (
                <textarea className={styles.paragraph} value={asText(draft[q.id])} onChange={(e) => set(q.id, e.target.value)} />
              ) : null}
            </fieldset>
          ))}
        </section>
      ))}

      <div className={styles.submit}>
        <button type="button" className={styles.send} onClick={send} disabled={state === 'sending' || answered === 0}>
          {state === 'sending' ? 'Sending…' : 'Send my answers'}
        </button>
        <p className={styles.tally}>
          {answered} of {QUESTIONS.length} answered. Blanks are allowed.
        </p>
        {state === 'failed' ? <p className={styles.failed}>That did not go through. Your answers are still here: try again.</p> : null}
      </div>
    </div>
  );
}
