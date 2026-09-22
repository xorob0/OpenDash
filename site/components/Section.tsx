/**
 * A section of a page: a rule across the top, a heading, and one paragraph under it.
 *
 * It used to open with a small uppercase label above a large uppercase heading, which read as a
 * template by the third section and as shouting by the tenth. A heading in the page's own voice,
 * with the sentence that qualifies it directly underneath, is what is left.
 */
import styles from './Section.module.css';

export interface SectionProps {
  id?: string;
  title: React.ReactNode;
  /** One paragraph under the heading. Short. */
  lede?: React.ReactNode;
  /** The first section of a page carries the page's h1. */
  level?: 1 | 2;
  /** The first section of a page has nothing above it to divide from. */
  ruled?: boolean;
  children?: React.ReactNode;
}

export function Section({ id, title, lede, level = 2, ruled = true, children }: SectionProps) {
  const Heading = level === 1 ? 'h1' : 'h2';
  return (
    <section id={id} className={`section ${ruled ? 'ruled' : ''} ${styles.section}`}>
      <div className="page">
        <header className={styles.head}>
          <Heading className={level === 1 ? 'h1' : 'h2'}>{title}</Heading>
          {lede ? <p className={`prose ${styles.lede}`}>{lede}</p> : null}
        </header>
        {children ? <div className={styles.body}>{children}</div> : null}
      </div>
    </section>
  );
}
