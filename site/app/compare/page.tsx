import type { Metadata } from 'next';
import { CompareTable } from '../../components/CompareTable';
import { Section } from '../../components/Section';
import { CHECKED_ON, rows } from '../../lib/compare';
import { longDate } from '../../lib/captures';
import { and } from '../../lib/counts';
import { COUNTS, LISTED } from '../../lib/liveCounts';
import { REPO_URL } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'OpenDash compared with Lovely and Daniel Newman Racing',
  description: 'Sims, price, licence, sizes, LEDs and more, with what is coming and what is not built.',
};

/** The rows read again after the rest of the table, which the note under it dates on their own. */
const READ_LATER = rows(COUNTS, LISTED).filter((r) => r.readOn !== undefined);

const LEGEND = [
  ['yes', 'Included'],
  ['partial', 'With a condition'],
  ['paid', 'Paid'],
  ['soon', 'Coming soon'],
  ['notBuilt', 'Not built, and why'],
  ['no', 'No'],
] as const;

export default function Compare() {
  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="table"
        title="OpenDash beside Lovely and Daniel Newman Racing"
        lede="Both have shipped longer and cover more sims than OpenDash. This is what you would gain and lose by switching."
      >
        <ul className={styles.legend}>
          {LEGEND.map(([mark, word]) => (
            <li key={mark} className={styles.key}>
              <span className={`${styles.square} ${styles[mark]}`} aria-hidden="true" />
              {word}
            </li>
          ))}
        </ul>
        <CompareTable />
        <div className={styles.notes}>
          <p className="prose">
            Every line about Lovely and DNR was read from their own pages, release notes or files, last on {longDate(CHECKED_ON)}
            {READ_LATER.length > 0 ? `, and ${and(READ_LATER.map((r) => `the ${r.label.toLowerCase()} row on ${longDate(r.readOn!)}`))}` : ''}. Tell us{' '}
            <a href={REPO_URL} className="link" rel="noopener">
              on GitHub
            </a>{' '}
            if one is wrong and it gets fixed.
          </p>
          <p className="prose">No competitor screenshots appear on this site. Lovely’s licence forbids using its interface in marketing, and the same rule is applied to both.</p>
        </div>
      </Section>
    </>
  );
}
