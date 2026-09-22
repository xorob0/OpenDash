/**
 * The two kinds of button the site has. Primary is the one cyan fill on a surface, and it is
 * always the download or the one action the section exists for; secondary is a ruled box.
 */
import Link from 'next/link';
import styles from './Buttons.module.css';

interface ButtonProps {
  href: string;
  children: React.ReactNode;
  /** A file to save rather than a page to open. */
  download?: boolean;
  className?: string;
}

const isExternal = (href: string) => /^https?:/.test(href);

function Button({ href, children, download, className }: ButtonProps) {
  if (download || isExternal(href)) {
    return (
      <a href={href} className={className} download={download || undefined} rel={isExternal(href) ? 'noopener' : undefined}>
        {children}
      </a>
    );
  }
  return (
    <Link href={href} className={className}>
      {children}
    </Link>
  );
}

export const Primary = (props: ButtonProps) => <Button {...props} className={`${styles.button} ${styles.primary}`} />;
export const Secondary = (props: ButtonProps) => <Button {...props} className={`${styles.button} ${styles.secondary}`} />;

/** A row of buttons that wraps on a phone. */
export const Actions = ({ children }: { children: React.ReactNode }) => <div className={styles.actions}>{children}</div>;
