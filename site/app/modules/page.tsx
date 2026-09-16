import type { Metadata } from 'next';
import Link from 'next/link';
import { Reveal } from '../../components/Reveal';
import { SectionHead } from '../../components/SectionHead';
import { Shot } from '../../components/Shot';
import { MODULES } from '../../lib/content.generated';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'The twenty-one pages',
  description:
    'Lap times, delta, sectors, speedo, fuel, tyres, pit view, car settings, inputs, session, radar, track, leaderboard, relative, opponents, gear, stint and lap history — the pages every openDash zone can show, and the companion shows one at a time.',
};

/** Why a module that ships off ships off. Three of them, and each has a different reason. */
const OFF_REASON: Record<string, string> = {
  energy: 'Virtual energy is a Le Mans Ultimate value. iRacing publishes none, so the page would draw zeros.',
  damage: 'iRacing reports no per-panel damage over the SDK, so there is nothing for the drawing to colour.',
  trackRivals: 'Segment-by-segment comparison against the field is not a SimHub value; it would have to be computed from a history openDash does not keep.',
};

export default function Modules() {
  const on = MODULES.filter((m) => m.enabled);
  const off = MODULES.filter((m) => !m.enabled);

  return (
    <>
      <section className={`section ${styles.top}`}>
        <div className="page">
          <SectionHead level="h1" label="The catalogue" title={<>{MODULES.length} pages, one button.</>}>
            <p>
              A page is not a widget you place. It is one of a catalogue that a zone can show, and a
              wheel button cycles it. The same {MODULES.length} are available to zone B and zone C of
              every face, and to the companion, which shows one at a time on a phone or a tablet.
            </p>
            <p>
              Each has its own switch in the plugin, so the cycle is only as long as you want it —
              three pages on a button you press mid-corner is a different instrument from twenty-one.
            </p>
          </SectionHead>
        </div>
      </section>

      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <div className={styles.grid}>
            {on.map((m, i) => (
              <Reveal key={m.id} delay={Math.min(i % 3, 3) * 70} className={styles.card}>
                <Shot
                  src={`/shots/module-${m.id}.png`}
                  alt={`The ${m.name} module on the openDash companion`}
                  width={850}
                  height={480}
                  sizes="(min-width: 75rem) 26rem, (min-width: 48rem) 40vw, 100vw"
                />
                <div className={styles.text}>
                  <p className={`num ${styles.number}`}>
                    {String(m.number).padStart(2, '0')}
                  </p>
                  <h2 className="h3">{m.name}</h2>
                  <p className={styles.desc}>{m.description}</p>
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ------------------------------------------------------------ the three that ship off */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <SectionHead label="Switched off" title={<>Three say so rather than drawing zeros.</>}>
            <p>
              They are built, they are in the catalogue, and they are off by default. A page that
              draws a confident 0.0 for a value the sim never sent is worse than no page at all, so
              these wait for a sim that publishes them. Turn any of them on in the plugin.
            </p>
          </SectionHead>

          {/*
            These are photographed too, and they earn it: the picture is the claim. A page that
            writes "not available in iRacing" across itself is the argument for shipping it off,
            and it makes it better than the paragraph above ever could.
          */}
          <div className={styles.offGrid}>
            {off.map((m, i) => (
              <Reveal key={m.id} delay={i * 70} className={styles.off}>
                <Shot
                  src={`/shots/module-${m.id}.png`}
                  alt={`The ${m.name} module, saying it has no data rather than drawing zeros`}
                  width={850}
                  height={480}
                  sizes="(min-width: 48rem) 30vw, 100vw"
                />
                <div className={styles.offText}>
                  <h3 className="h3">{m.name}</h3>
                  <p className={styles.desc}>{OFF_REASON[m.id] ?? m.description}</p>
                </div>
              </Reveal>
            ))}
          </div>

          <Reveal delay={220}>
            <p className={styles.after}>
              The pit wall draws from the same parts: eleven of these pages fit its data zones and
              six more are cut for its wide zone.{' '}
              <Link href="/second-screens" className="link">
                The companion and the pit wall
              </Link>
              .
            </p>
          </Reveal>
        </div>
      </section>
    </>
  );
}
