import type { Release } from '../scripts/content';
import styles from './Changelog.module.css';
import { renderInline, toBlocks } from '../lib/markdown';

/**
 * The release history, rendered from CHANGELOG.md.
 *
 * The changelog is one of the two long-form documents this site shows verbatim rather than
 * paraphrasing, because a release note rewritten for a web page is a release note that no longer
 * matches the release. `lib/markdown.ts` renders the narrow subset the file actually uses.
 */
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
                        {renderInline(item)}
                      </li>
                    ))}
                  </ul>
                );
              }
              return (
                <p key={i} className={styles.para}>
                  {renderInline(block.text)}
                </p>
              );
            })}
          </div>
        </article>
      ))}
    </div>
  );
}
