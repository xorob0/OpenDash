import type { Metadata } from 'next';
import Link from 'next/link';
import { Demo } from '../../components/Demo';
import { Section } from '../../components/Section';
import { DEMO_BUILD_VERSION, DEMO_FACES, DEMO_TRACE, PANEL_CATALOGUE } from '../../lib/demo.generated';
import { issueUrl } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Try it in your browser',
  description: 'The dashboards drawn in your browser from the files the plugin installs, replaying a recorded lap, with the settings panel beside them.',
};

/** The 850 x 480, the common wheel display, which the rest of the site opens on as well. */
const INITIAL = 'opendash-850x480';

export default function DemoPage() {
  const initial = DEMO_FACES.some((f) => f.slug === INITIAL) ? INITIAL : (DEMO_FACES[0]?.slug ?? '');

  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="demo"
        title="Try it in your browser"
        lede="These are the dashboards drawn in your browser from the same files the plugin installs, replaying a lap SimHub recorded. The panel beside them writes what the plugin writes: change a zone's page, hold the glance, turn the rev bar off, and the dashboard does the rest."
      >
        {DEMO_FACES.length > 0 && DEMO_TRACE ? (
          <Demo faces={DEMO_FACES} trace={DEMO_TRACE} catalogue={PANEL_CATALOGUE} initial={initial} />
        ) : (
          <p className="prose">This copy of the site was built without the dashboards or the recorded lap, so there is nothing to draw. Build the repository and generate the site again.</p>
        )}
        <p className={`prose ${styles.proof}`}>
          The drawing is the browser&apos;s, so it is close to SimHub and not the same.{' '}
          <Link href="/#screen" className="link">
            The clips are the proof
          </Link>
          : those are SimHub itself, filmed on a Windows machine running the emulator.
          {DEMO_BUILD_VERSION ? ` Drawn from build ${DEMO_BUILD_VERSION}.` : ''}
        </p>
      </Section>

      <Section id="empty" title="What is empty here" lede="Some of the face needs data a recorded lap does not carry yet, and the demo leaves it empty rather than make it up.">
        <ul className={`rows ${styles.empty}`}>
          <li>
            <strong>The relative, the leaderboard, the opponents and the sectors pages, and the best splits on lap history.</strong> SimHub computes them from the other cars it
            holds in memory, and the traces record properties only. They fill in when the traces carry those calls too (
            <a className="link" href={issueUrl(257)}>
              #257
            </a>
            ).
          </li>
          <li>
            <strong>The radar and the track map</strong> are drawn as labelled boxes. SimHub draws them from the cars around you and the track it has recorded, neither of which a
            browser has.
          </li>
          <li>
            <strong>Only the default look, on the ten faces.</strong> The companion, the pit wall and the car themes come later (
            <a className="link" href={issueUrl(395)}>
              #395
            </a>
            ).
          </li>
        </ul>
      </Section>
    </>
  );
}
