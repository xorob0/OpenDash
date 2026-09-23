import type { Metadata } from 'next';
import Link from 'next/link';
import { Releases } from '../../components/Releases';
import { Section } from '../../components/Section';
import { SizeList } from '../../components/SizeList';
import { DOWNLOADS, RELEASES, SIMHUB_VERSION, VERSION } from '../../lib/content.generated';
import { COMPANIONS, FACES, PIT_WALLS } from '../../lib/faces';
import { weigh } from '../../lib/packages';
import { FREE_FOREVER, REPO_URL } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: `Download OpenDash ${VERSION}`,
  description: `The plugin with all 14 dashboards, or 1 file per screen. Windows, SimHub ${SIMHUB_VERSION} or later.`,
};

const PLUGIN_ZIP = 'OpenDash-plugin.zip';

export default function Download() {
  const plugin = DOWNLOADS.find((d) => d.file === PLUGIN_ZIP);
  const preRelease = VERSION.includes('-');

  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="plugin"
        title={`Download ${VERSION}`}
        lede={`${FREE_FOREVER} Windows, SimHub ${SIMHUB_VERSION} or later. Built from this commit’s source by the same command CI runs.`}
      >
        {preRelease ? (
          <p className={`prose ${styles.pre}`}>
            <span className={styles.preTag}>Pre-release.</span> Every release so far is a candidate. Expect rough edges and report them{' '}
            <a href={REPO_URL} className="link" rel="noopener">
              on GitHub
            </a>
            .
          </p>
        ) : null}
        <div className={styles.plugin}>
          <h2 className="h3">The plugin</h2>
          <p className="prose">Installs all 14 dashboards and 63 LED profiles. Adds an OpenDash page to SimHub’s left menu. Updates itself from there.</p>
          {plugin ? (
            <a href={`/downloads/${PLUGIN_ZIP}`} download className={styles.primary}>
              {PLUGIN_ZIP} <span className={`num ${styles.weight}`}>{weigh(plugin.bytes)}</span>
            </a>
          ) : (
            <p className={styles.missing}>Not in this build. The plugin zip is produced by the packaging step the site’s image runs first.</p>
          )}
          <p className="prose">
            Close SimHub, unzip, copy <code>OpenDash.dll</code> next to <code>SimHubWPF.exe</code>, unblock it.{' '}
            <Link href="/install" className="link">
              The 5 steps
            </Link>
            .
          </p>
        </div>
      </Section>

      <Section id="packages" title="One dashboard at a time" lede="Double-click to import: default pages, no settings page, no updates. The LED profiles come with the plugin.">
        <div className={styles.groups}>
          <div className={styles.group}>
            <h3 className="h3">Faces</h3>
            <SizeList packages={FACES} downloads />
          </div>
          <div className={styles.group}>
            <h3 className="h3">Companion</h3>
            <SizeList packages={COMPANIONS} downloads />
          </div>
          <div className={styles.group}>
            <h3 className="h3">Pit wall</h3>
            <SizeList packages={PIT_WALLS} downloads />
          </div>
        </div>
      </Section>

      <Section
        id="source"
        title="Build it yourself"
        lede={
          <>
            MIT. Clone{' '}
            <a href={REPO_URL} className="link" rel="noopener">
              the repository
            </a>
            , run <code>bun install</code>, then <code>bun run build</code>. Every package comes out of the build directory.
          </>
        }
      />

      <Section id="releases" title="Release notes" lede={`This site serves the version it was built from, so only ${VERSION} is downloadable here. The whole changelog, newest first.`}>
        <Releases releases={RELEASES} />
      </Section>
    </>
  );
}
