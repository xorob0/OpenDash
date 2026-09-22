/**
 * A section of a page: a rule across the top, a label, a heading and one paragraph, then whatever
 * the section shows. Every page is a stack of these, so the rhythm is the same everywhere.
 */
import styles from './Section.module.css';

export interface SectionProps {
  id?: string;
  label?: string;
  title: React.ReactNode;
  /** One paragraph under the heading. Short. */
  lede?: React.ReactNode;
  /** The first section of a page carries the page's h1. */
  level?: 1 | 2;
  /** The first section of a page has nothing above it to divide from. */
  ruled?: boolean;
  /** Put the lede beside the heading rather than under it, on a wide screen. */
  wide?: boolean;
  children?: React.ReactNode;
}

export function Section({ id, label, title, lede, level = 2, ruled = true, wide = false, children }: SectionProps) {
  const Heading = level === 1 ? 'h1' : 'h2';
  return (
    <section id={id} className={`section ${ruled ? 'ruled' : ''} ${styles.section}`}>
      <div className="page">
        <header className={`${styles.head} ${wide ? styles.wide : ''}`}>
          <div className={styles.titles}>
            {label ? <p className="label">{label}</p> : null}
            <Heading className={level === 1 ? 'h1' : 'h2'}>{title}</Heading>
          </div>
          {lede ? <p className={`prose ${styles.lede}`}>{lede}</p> : null}
        </header>
        {children ? <div className={styles.body}>{children}</div> : null}
      </div>
    </section>
  );
}
