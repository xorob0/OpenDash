import type { Metadata } from 'next';
import Link from 'next/link';
import { Changelog } from '../../components/Changelog';
import { Reveal } from '../../components/Reveal';
import { SectionHead } from '../../components/SectionHead';
import { DOWNLOADS, RELEASES, SIMHUB_VERSION, VERSION } from '../../lib/content.generated';
import { NOTES, sizeLabel } from '../../lib/packages';
import { ALL as ORDERED } from '../../lib/faces';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Download',
  description:
    'The OpenDash SimHub plugin, which installs all fourteen dashboards, and every .simhubdash on its own. Windows, SimHub 9.12.6 or later.',
};

const PLUGIN_ZIP = 'OpenDash-plugin.zip';

/** `102400` -> `100 KB`. Sizes are stated because a download whose size is a surprise is a worry. */
const weigh = (bytes: number): string =>
  bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.round(bytes / 1024)} KB`;

export default function Download() {
  const plugin = DOWNLOADS.find((d) => d.file === PLUGIN_ZIP);
  const byFile = new Map(DOWNLOADS.map((d) => [d.file, d]));
  const preRelease = VERSION.includes('-');

  return (
    <>
      <section className={`section ${styles.top}`}>
        <div className="page">
          <SectionHead
            level="h1"
            label={`Version ${VERSION}`}
            title={
              <>
                Two ways in.
                <br />
                One file each.
              </>
            }
          >
            <p>
              You need <strong>Windows</strong> and <strong>SimHub {SIMHUB_VERSION} or later</strong>
              . Everything below is built from this version’s source by the same command that runs in
              CI, so what you download is what the code here produces.
            </p>
            {preRelease ? (
              <p className={styles.pre}>
                <span className={styles.preTag}>Pre-release.</span> A version ending in a suffix such
                as <span className="num">-rc.1</span> is a release candidate, which is what to expect
                while OpenDash is alpha.
              </p>
            ) : null}
          </SectionHead>
        </div>
      </section>

      {/* ------------------------------------------------------------ the two routes */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <div className={styles.routes}>
            <Reveal className={`${styles.route} ${styles.recommended}`}>
              <p className="label">Recommended</p>
              <h2 className="h2">The plugin</h2>
              <p className="prose">
                One file. It carries all {ORDERED.length} dashboards inside it, extracts them into
                SimHub, and adds an OpenDash page to the left menu where every setting lives — which
                zone shows what, the rev bar, the flag box, the LED profiles.
              </p>
              {plugin ? (
                <a className={styles.primary} href={`/downloads/${PLUGIN_ZIP}`} download>
                  {PLUGIN_ZIP}
                  <span className={`num ${styles.weight}`}>{weigh(plugin.bytes)}</span>
                </a>
              ) : (
                <p className={styles.missing}>
                  Not in this build. The plugin zip is produced by <code>bun run package</code>,
                  which the site’s image runs in an earlier stage.
                </p>
              )}
              <p className={styles.hint}>
                Close SimHub, unzip, copy <code>OpenDash.dll</code> next to{' '}
                <code>SimHubWPF.exe</code>, and unblock it.{' '}
                <Link href="/install" className="link">
                  The full procedure
                </Link>
                .
              </p>
            </Reveal>

            <Reveal delay={90} className={styles.route}>
              <p className="label">Or</p>
              <h2 className="h2">One dashboard</h2>
              <p className="prose">
                Double-click a <code>.simhubdash</code> and SimHub imports it. You get the default
                layout and the default pages, and no settings page. Nothing else is needed, so this
                is the shortest way to see OpenDash on a display.
              </p>
              <p className={styles.hint}>
                <strong>850 × 480</strong> is the base size and the one to take if nothing matches
                your display exactly.{' '}
                <Link href="/dashes#resolutions" className="link">
                  The table of sizes
                </Link>
                .
              </p>
            </Reveal>
          </div>
        </div>
      </section>

      {/* ------------------------------------------------------------ every package */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <SectionHead label="Every package" title={<>One file per screen.</>}>
            <p>
              Take the one that matches your display. SimHub will scale a mismatched size rather than
              refuse it, so if nothing is exact, take the nearest shape rather than the biggest
              number.
            </p>
          </SectionHead>

          <Reveal>
            <ul className={styles.files}>
              {ORDERED.map((p) => {
                const file = byFile.get(`${p.folder}.simhubdash`);
                return (
                  <li key={p.folder} className={styles.file}>
                    <div className={styles.fileText}>
                      <p className={`num ${styles.fileSize}`}>{sizeLabel(p)}</p>
                      <p className={styles.fileName}>{p.folder}</p>
                      <p className={styles.fileNote}>{NOTES[p.folder]?.what}</p>
                    </div>
                    {file ? (
                      <a className={styles.get} href={`/downloads/${encodeURIComponent(file.file)}`} download>
                        Download
                        <span className={`num ${styles.weight}`}>{weigh(file.bytes)}</span>
                      </a>
                    ) : (
                      <span className={styles.absent}>Not built</span>
                    )}
                  </li>
                );
              })}
            </ul>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------------------------------------ history */}
      <section className={`section ruled ${styles.block}`} id="changelog">
        <div className="page">
          <SectionHead label="Release history" title={<>What each version changed.</>}>
            <p>
              This site serves the version it was built from, so only {VERSION} is downloadable here.
              The history below is the whole changelog, newest first.
            </p>
          </SectionHead>

          <Changelog releases={RELEASES} />
        </div>
      </section>
    </>
  );
}
