import type { Metadata } from 'next';
import { CompareTable } from '../../components/CompareTable';
import { Section } from '../../components/Section';
import { SCHEDULED } from '../../lib/compare';
import { REPO_URL, issueUrl } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'OpenDash compared with Lovely and Daniel Newman Racing',
  description: 'Sims, price, licence, sizes, LEDs and more, with what is coming and what is not built.',
};

const LEGEND = [
  ['yes', 'Free, included or compatible'],
  ['partial', 'With a condition'],
  ['paid', 'Paid'],
  ['soon', 'Coming soon, with the issue'],
  ['notBuilt', 'Not built, with the reason'],
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
            Every line about Lovely and DNR was read from their own pages, release notes or shipped files. Tell us{' '}
            <a href={REPO_URL} className="link" rel="noopener">
              on GitHub
            </a>{' '}
            if one is wrong and it gets fixed.
          </p>
          <p className="prose">No competitor screenshots appear on this site. Lovely’s licence forbids using its interface in marketing, and the same rule is applied to both.</p>
        </div>
      </Section>

      <Section id="scheduled" title="What is coming" lede="Each is an open issue, read from the table above so the two cannot disagree.">
        <ul className={`rows ${styles.scheduled}`}>
          {SCHEDULED.map((row) => (
            <li key={row.id} className={styles.item}>
              <span className={styles.itemLabel}>{row.label}</span>
              <span className={styles.itemText}>{row.cells.opendash.text}</span>
              <span className={styles.itemRefs}>
                {(row.cells.opendash.issues ?? []).map((n, i) => (
                  <span key={n}>
                    {i > 0 ? ', ' : ''}
                    <a href={issueUrl(n)} className="link" rel="noopener">
                      #{n}
                    </a>
                  </span>
                ))}
              </span>
            </li>
          ))}
        </ul>
      </Section>
    </>
  );
}
