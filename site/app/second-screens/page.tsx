import type { Metadata } from 'next';
import Link from 'next/link';
import { Reveal } from '../../components/Reveal';
import { SectionHead } from '../../components/SectionHead';
import { Shot } from '../../components/Shot';
import { MODULES } from '../../lib/content.generated';
import { SECOND_SCREENS, shotFor, sizeLabel } from '../../lib/packages';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'The companion and the pit wall',
  description:
    'A phone or tablet beside the wheel showing one module at a time, and a 1920 by 1080 pit wall for whoever is not driving: the whole field with gaps, intervals, sectors, stints and stops.',
};

const companions = SECOND_SCREENS.filter((p) => p.kind === 'companion');
const pitWalls = SECOND_SCREENS.filter((p) => p.kind === 'pitwall');

export default function SecondScreens() {
  return (
    <>
      <section className={`section ${styles.top}`}>
        <div className="page">
          <SectionHead level="h1" label="Second screens" title={<>The rig has more than one screen.</>}>
            <p>
              Two more kinds of screen are built from the same parts as the face and installed by the
              same plugin. Neither is a smaller dashboard: each is drawn for the room it sits in and
              for the person looking at it.
            </p>
          </SectionHead>
        </div>
      </section>

      {/* ------------------------------------------------------------ the companion */}
      <section className={`section ruled ${styles.block}`} id="companion">
        <div className="page">
          <SectionHead label="The companion" title={<>One module, as large as the screen allows.</>}>
            <p>
              A phone or a tablet propped beside the wheel, on SimHub’s network display. It shows one
              of the {MODULES.length} modules at a time, with a header carrying the module name, the
              page counter, your position and your lap, and a compact flag band at the foot.
            </p>
            <p>
              Paging is the same wheel button the face uses, through SimHub’s own screen navigation.
              A module you switch off in the plugin leaves the ring entirely, so turning eight off
              means paging through thirteen rather than skipping past eight blanks.
            </p>
          </SectionHead>

          <div className={styles.pair}>
            {companions.map((p, i) => (
              <Reveal key={p.folder} delay={i * 80} className={styles.item}>
                <Shot
                  src={shotFor(p.folder)}
                  alt={`The openDash companion at ${sizeLabel(p)}`}
                  width={p.width}
                  height={p.height}
                  caption={`${p.folder} · ${sizeLabel(p)}`}
                  sizes="(min-width: 62rem) 40rem, 100vw"
                />
              </Reveal>
            ))}
          </div>

          <Reveal>
            <p className={styles.after}>
              No module has a portrait variant. A row of fields shrinks its gaps and then wraps, so
              three lap times are one line on the 850 px companion and two on the 480 px portrait
              one — the module is a function of the rectangle it is handed.{' '}
              <Link href="/modules" className="link">
                Every module, photographed
              </Link>
              .
            </p>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------------------------------------ the pit wall */}
      <section className={`section ruled ${styles.block}`} id="pit-wall">
        <div className="page">
          <SectionHead label="The pit wall" title={<>For whoever is not driving.</>}>
            <p>
              A <span className="num">1920 × 1080</span> screen with three pages: the whole field
              with gaps, intervals, sectors, stints and stops, your own lap beside it, and four data
              zones whose contents are plugin settings — eleven pages fit a zone and six more are
              cut for the wide one.
            </p>
            <p>
              It is the slot mechanism of the face applied to a bigger screen, and it works for the
              same reason: a widget’s screen index can be bound, so changing a setting changes what a
              zone shows without touching the file.
            </p>
          </SectionHead>

          <div className={styles.stack}>
            {pitWalls.map((p, i) => (
              <Reveal key={p.folder} delay={i * 80} className={styles.item}>
                <Shot
                  src={shotFor(p.folder)}
                  alt={`The openDash pit wall at ${sizeLabel(p)}`}
                  width={p.width}
                  height={p.height}
                  caption={`${p.folder} · ${sizeLabel(p)}`}
                  sizes="(min-width: 88rem) 60rem, 100vw"
                />
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ------------------------------------------------------------ what they won't show */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <SectionHead label="Honest gaps" title={<>What they will not show.</>}>
            <p>
              Three modules ship switched off because iRacing publishes none of their data: virtual
              energy, per-panel damage, and segment comparison against the field. They say so rather
              than drawing zeros, which is the only version of those pages worth having.
            </p>
            <p>
              openDash is tested against iRacing. The telemetry is SimHub’s, so other sims will very
              probably work — but iRacing is the one it claims.
            </p>
          </SectionHead>
        </div>
      </section>
    </>
  );
}
