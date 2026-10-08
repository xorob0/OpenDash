import type { Metadata } from 'next';
import Link from 'next/link';
import { Demo } from '../../components/Demo';
import { Section } from '../../components/Section';
import { DEMO_BUILD_VERSION, DEMO_FACES, DEMO_TRACES, PANEL_CATALOGUE } from '../../lib/demo.generated';
import { issueUrl } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Try it in your browser',
  description: 'The dashboards, the companion and the pit wall drawn in your browser from the files the plugin installs, replaying a recorded session, with the settings panel beside them.',
};

/** The 850 x 480, the common wheel display, which the rest of the site opens on as well. */
const INITIAL = 'opendash-850x480';

export default function DemoPage() {
  const initial = DEMO_FACES.some((f) => f.slug === INITIAL) ? INITIAL : (DEMO_FACES[0]?.slug ?? '');
  const themed = DEMO_FACES.some((f) => f.group === 'theme');

  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="demo"
        title="Try it in your browser"
        lede="These are the dashboards drawn in your browser from the same files the plugin installs, replaying a session SimHub recorded. The panel beside them writes what the plugin writes: change a zone's page, hold the glance, page the companion, pick the pit wall's zones, and the dashboard does the rest."
      >
        {DEMO_FACES.length > 0 && DEMO_TRACES.length > 0 ? (
          <Demo faces={DEMO_FACES} traces={DEMO_TRACES} catalogue={PANEL_CATALOGUE} initial={initial} />
        ) : (
          <p className="prose">This copy of the site was built without the dashboards or the recorded sessions, so there is nothing to draw. Build the repository and generate the site again.</p>
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

      <Section id="empty" title="What is empty here" lede="Some of what the screens show needs data a recorded session does not carry yet, and the demo leaves it empty rather than make it up.">
        <ul className={`rows ${styles.empty}`}>
          <li>
            <strong>The relative, the leaderboard, the opponents and the sectors pages, and the best splits on lap history,</strong> on a face, on the companion and in the pit
            wall&apos;s zones, and the pit wall&apos;s own timing board. SimHub computes them from the other cars it holds in memory, and the traces record properties only. They
            fill in when the traces carry those calls too (
            <a className="link" href={issueUrl(257)}>
              #257
            </a>
            ).
          </li>
          <li>
            <strong>The radar, the track map and the web view</strong> are drawn as labelled boxes. SimHub draws the first two from the cars around you and the track it has
            recorded, neither of which a browser has, and the web view is a browser of its own: give the pit wall an address and its box names it.
          </li>
          {themed ? (
            <li>
              <strong>The Porsche&apos;s crest</strong> is a picture of your own that the plugin points the face at, so the demo shows the placeholder shield the face draws
              without one.
            </li>
          ) : null}
        </ul>
      </Section>
    </>
  );
}
