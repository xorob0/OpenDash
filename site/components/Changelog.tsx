import { Fragment, type ReactNode } from 'react';
import type { Release } from '../scripts/content';
import styles from './Changelog.module.css';
import { toBlocks, toInline, type Inline } from '../lib/markdown';

/**
 * The release history, rendered from CHANGELOG.md.
 *
 * The changelog is the one long-form document this site shows verbatim rather than paraphrasing,
 * because a release note rewritten for a web page is a release note that no longer matches the
 * release. `lib/markdown.ts` parses the narrow subset the file actually uses into spans; turning
 * those into elements is this file's job, which is why no part of the site needs
 * `dangerouslySetInnerHTML`.
 */

/** One parsed span as an element. A link without an href lost a repository-relative target. */
function span(node: Inline, key: number): ReactNode {
  switch (node.kind) {
    case 'strong':
      return <strong key={key}>{node.text}</strong>;
    case 'em':
      return <em key={key}>{node.text}</em>;
    case 'code':
      return <code key={key}>{node.text}</code>;
    case 'link':
      return node.href ? (
        <a key={key} href={node.href} className="link" rel="noopener">
          {node.text}
        </a>
      ) : (
        <Fragment key={key}>{node.text}</Fragment>
      );
    default:
      return <Fragment key={key}>{node.text}</Fragment>;
  }
}

/** A line of markdown as elements. */
const inline = (text: string): ReactNode => toInline(text).map(span);
export function Changelog({ releases }: { releases: Release[] }) {
  return (
    <div className={styles.list}>
      {releases.map((release) => (
        <article key={release.version} className={styles.release} id={`v${release.version}`}>
          <header className={styles.head}>
            <h3 className={`num ${styles.version}`}>{release.version}</h3>
            <div className={styles.meta}>
              {release.date ? <time dateTime={release.date}>{release.date}</time> : null}
              {release.unreleased ? <span className={styles.badgeWork}>On main, untagged</span> : null}
              {release.preRelease && !release.unreleased ? (
                <span className={styles.badgePre}>Pre-release</span>
              ) : null}
            </div>
          </header>

          <div className={styles.body}>
            {toBlocks(release.body).map((block, i) => {
              if (block.kind === 'heading') {
                return (
                  <h4 key={i} className={`label ${styles.group}`}>
                    {block.text}
                  </h4>
                );
              }
              if (block.kind === 'list') {
                return (
                  <ul key={i} className={styles.items}>
                    {block.items.map((item, j) => (
                      <li key={j} className={styles.item}>
                        {inline(item)}
                      </li>
                    ))}
                  </ul>
                );
              }
              return (
                <p key={i} className={styles.para}>
                  {inline(block.text)}
                </p>
              );
            })}
          </div>
        </article>
      ))}
    </div>
  );
}
