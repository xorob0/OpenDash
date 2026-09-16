/**
 * A section heading: the label, the title, and the paragraph that qualifies it.
 *
 * On a wide screen the title and the paragraph sit side by side rather than the paragraph hanging
 * under the title's left edge. A measure of 62ch under a four-word condensed headline leaves most
 * of a 1440 px page empty, and empty space that is not doing anything is just a long scroll.
 */
import type { ReactNode } from 'react';
import { Reveal } from './Reveal';
import styles from './SectionHead.module.css';

export function SectionHead({
  label,
  title,
  children,
  level: Level = 'h2',
}: {
  label: string;
  title: ReactNode;
  children?: ReactNode;
  level?: 'h1' | 'h2';
}) {
  return (
    <Reveal className={styles.head}>
      <div className={styles.left}>
        <p className="label">{label}</p>
        <Level className={`h1 ${styles.title}`}>{title}</Level>
      </div>
      {children ? <div className={`prose ${styles.body}`}>{children}</div> : null}
    </Reveal>
  );
}
