/**
 * Which sim shows what: one block per supported sim, the count of readings that work, and the rest
 * grouped by where they are drawn, read from docs/sim-support.md at build time (#102, #1191). The
 * sims that are not claimed get one line between them, and the document is linked for the whole
 * table; `lib/sims.ts` says what is kept of each cell.
 */
import { Fragment, type ReactNode } from 'react';
import type { SimMark, SimName, SimSection } from '../scripts/content';
import { toInline, type Inline } from '../lib/markdown';
import { MARK_WORD, summarise } from '../lib/sims';
import { SIM_SUPPORT_URL } from '../lib/site';
import styles from './SimSummary.module.css';

function span(node: Inline, key: number): ReactNode {
  switch (node.kind) {
    case 'strong':
      return <strong key={key}>{node.text}</strong>;
    case 'em':
      return <em key={key}>{node.text}</em>;
    case 'code':
      return <code key={key}>{node.text}</code>;
    default:
      return <Fragment key={key}>{node.text}</Fragment>;
  }
}

const MARK_CLASS: Record<SimMark, string> = {
  Works: styles.works ?? '',
  Partly: styles.partly ?? '',
  Absent: styles.absent ?? '',
  Wrong: styles.wrong ?? '',
  Untested: styles.untested ?? '',
};

function Supported({ sim, sections }: { sim: SimName; sections: readonly SimSection[] }) {
  const { works, total, groups } = summarise(sections, sim);
  return (
    <section className={styles.sim}>
      <header className={styles.head}>
        <h3 className="h3">{sim}</h3>
        <p className={`prose ${styles.count}`}>
          {works} of {total} readings work.{groups.length > 0 ? ' The rest, by where they are drawn:' : ''}
        </p>
      </header>
      {groups.length > 0 ? (
        <div className={styles.groups}>
          {groups.map((group) => (
            <div key={group.title} className={styles.group}>
              <p className={`label ${styles.title}`}>{group.title}</p>
              <ul className={`rows ${styles.items}`}>
                {group.items.map((item) => (
                  <li key={item.reading} className={styles.item}>
                    <p className={styles.line}>
                      <span className={styles.reading}>{toInline(item.reading).map(span)}</span>
                      <span className={styles.mark}>
                        <span className={`${styles.square} ${MARK_CLASS[item.mark]}`} aria-hidden="true" />
                        <span className={styles.word}>{MARK_WORD[item.mark]}</span>
                      </span>
                    </p>
                    {item.note ? <p className={styles.note}>{toInline(item.note).map(span)}</p> : null}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      ) : null}
    </section>
  );
}

export function SimSummary({ sims, supported, sections }: { sims: readonly SimName[]; supported: readonly SimName[]; sections: readonly SimSection[] }) {
  const unclaimed = sims.filter((sim) => !supported.includes(sim));
  return (
    <div className={styles.wrap}>
      {sims.filter((sim) => supported.includes(sim)).map((sim) => (
        <Supported key={sim} sim={sim} sections={sections} />
      ))}
      {unclaimed.length > 0 ? (
        <section className={styles.sim}>
          <header className={styles.head}>
            <h3 className="h3">{unclaimed.join(' and ')}</h3>
            <p className={`prose ${styles.count}`}>Untested. Nobody has driven them with the packages yet.</p>
          </header>
        </section>
      ) : null}
      <p className={`prose ${styles.more}`}>
        <a href={SIM_SUPPORT_URL} className="link" rel="noopener">
          The whole table, reading by reading and sim by sim
        </a>
      </p>
    </div>
  );
}
