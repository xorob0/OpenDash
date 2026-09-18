import Link from 'next/link';
import { Anatomy } from '../components/Anatomy';
import { Reveal } from '../components/Reveal';
import { SectionHead } from '../components/SectionHead';
import { Shot } from '../components/Shot';
import { MODULES, SIMHUB_VERSION, VERSION } from '../lib/content.generated';
import { ORDERED } from '../lib/packages';
import styles from './page.module.css';

const faces = ORDERED.filter((p) => p.kind === 'dash');

export default function Home() {
  return (
    <>
      {/* ------------------------------------------------------------------ hero */}
      <section className={styles.hero}>
        <div className="page">
          <p className={`label ${styles.eyebrow}`}>
            <span className={styles.alpha}>Alpha</span>
            <span aria-hidden="true">·</span>
            <span>SimHub {SIMHUB_VERSION}+</span>
            <span aria-hidden="true">·</span>
            <span>MIT</span>
          </p>

          <h1 className={`display ${styles.headline}`}>
            Every screen
            <br />
            on the rig.
          </h1>

          <p className={`prose ${styles.lede}`}>
            openDash is not one dashboard. It is the face on your wheel, the phone or tablet beside
            it, and a pit wall screen for whoever is not driving — {faces.length} sizes from a{' '}
            <span className="num">1920 × 480</span> ultrawide down to a{' '}
            <span className="num">480 px</span> round DDU, and one SimHub plugin that installs all of
            them.
          </p>

          <div className={styles.actions}>
            <Link href="/download" className={styles.primary}>
              Download {VERSION}
            </Link>
            <Link href="/dashes" className={styles.secondary}>
              See every size
            </Link>
          </div>
        </div>

        <div className={`page ${styles.heroShot}`}>
          <Shot
            src="/shots/opendash-green.png"
            alt="The openDash face at 1920 by 480: rev bar, the bar of settled values, lap times, the gear, the relative, and the fuel band"
            width={1920}
            height={480}
            priority
          />
          <p className={styles.note}>
            Every picture on this site is the package itself, photographed through SimHub’s own
            renderer at its own size while a telemetry emulator replayed a lap at Spa. None of them
            is a mock-up, and no value in them was typed by hand.
          </p>
        </div>
      </section>

      {/* ------------------------------------------------------------------ anatomy */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <SectionHead label="The face" title={<>Five parts, and only one of them moves slowly.</>}>
            <p>
              A driver does not configure a dashboard. They change it mid-stint, with a thumb, and
              the layout has to survive that. So the face is a small number of named regions, each
              showing one page at a time from its own catalogue and each bound to a wheel button.
            </p>
          </SectionHead>

          <Reveal delay={80} className={styles.anatomy}>
            <Anatomy src="/shots/opendash-green.png" />
          </Reveal>
        </div>
      </section>

      {/* ------------------------------------------------------------------ pages */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <SectionHead
            label="The catalogue"
            title={<>{MODULES.length} pages. A wheel button, not a menu.</>}
          >
            <p>
              Zones B and C each choose from the same catalogue, the band from eight that suit a
              wide, shallow strip, and zone A from four. Hold a button instead of tapping it and you
              get a glance: one page shows while you hold, and the zone returns to what it was when
              you let go.
            </p>
          </SectionHead>

          <Reveal delay={80}>
            <ul className={styles.chips}>
              {MODULES.map((m) => (
                <li key={m.id} className={styles.chip} data-off={!m.enabled || undefined}>
                  {m.name}
                </li>
              ))}
            </ul>
            <p className={styles.chipNote}>
              Three ship switched off because iRacing publishes none of their data. They say so
              rather than drawing zeros.{' '}
              <Link href="/modules" className="link">
                Every page, with a photograph
              </Link>
              .
            </p>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------------------------------------------ three kinds */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <SectionHead
            label="Three kinds of screen"
            title={<>Built from the same parts, drawn for different rooms.</>}
          >
            <p>
              All three are assembled from the same elements, components and pages, and each is
              redrawn for the rectangle it is given rather than scaled into it.
            </p>
          </SectionHead>

          <div className={styles.kinds}>
            <Reveal delay={0} className={styles.kind}>
              <Shot
                src="/shots/opendash-850x480-green.png"
                alt="The openDash face at 850 by 480, the base size"
                width={850}
                height={480}
                sizes="(min-width: 62rem) 40rem, 100vw"
              />
              <h3 className="h3">The face</h3>
              <p className="prose">
                On the wheel or on the dash. Ten sizes, and the base is{' '}
                <span className="num">850 × 480</span> — the common wheel-mounted DDU, and the
                tightest face that still carries all five parts.
              </p>
            </Reveal>

            <Reveal delay={90} className={styles.kind}>
              <Shot
                src="/shots/opendash-companion-green.png"
                alt="The openDash companion at 850 by 480, showing one module at a time"
                width={850}
                height={480}
                sizes="(min-width: 62rem) 40rem, 100vw"
              />
              <h3 className="h3">The companion</h3>
              <p className="prose">
                A phone or a tablet beside the wheel, showing one of the {MODULES.length} modules at
                a time and paged with the same wheel button. Portrait, for a phone stood on end, is
                a second package.
              </p>
            </Reveal>

            <Reveal delay={180} className={styles.kind}>
              <Shot
                src="/shots/opendash-pit-wall-green.png"
                alt="The openDash pit wall at 1920 by 1080, showing the whole field"
                width={1920}
                height={1080}
                sizes="(min-width: 62rem) 40rem, 100vw"
              />
              <h3 className="h3">The pit wall</h3>
              <p className="prose">
                <span className="num">1920 × 1080</span> for somebody who is not driving: the whole
                field with gaps, intervals, sectors, stints and stops, your own lap beside it, and
                four data zones that are plugin settings.
              </p>
            </Reveal>
          </div>

          <Reveal delay={240}>
            <p className={styles.chipNote}>
              <Link href="/second-screens" className="link">
                More on the companion and the pit wall
              </Link>
              .
            </p>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------------------------------------------ fit */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <SectionHead label="Fit" title={<>A page is never scaled. It is redrawn.</>}>
            <p>
              A bigger screen does not buy you the same dashboard, larger. It buys you more in each
              zone. The relative lists seven drivers at <span className="num">850 × 480</span> and
              eighteen at <span className="num">1280 × 720</span>; a page sheds its secondary rows
              before it shrinks its numerals, and it grows to fill a box it does not fill until it
              meets the height, the width, or the next size up its own ramp.
            </p>
          </SectionHead>

          <Reveal delay={80}>
            <ul className={styles.sizes}>
              {ORDERED.map((p) => (
                <li key={p.folder} className={styles.size}>
                  <span className={`num ${styles.sizeNum}`}>
                    {p.round ? `${p.width} round` : `${p.width}×${p.height}`}
                  </span>
                  <span className={styles.sizeKind}>
                    {p.kind === 'dash' ? 'Face' : p.kind === 'companion' ? 'Companion' : 'Pit wall'}
                  </span>
                </li>
              ))}
            </ul>
            <p className={styles.chipNote}>
              <Link href="/dashes" className="link">
                Every size, photographed, with the table of what to pick
              </Link>
              .
            </p>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------------------------------------------ close */}
      <section className={`section ruled ${styles.close}`}>
        <div className="page">
          <Reveal>
            <h2 className={`h1 ${styles.title}`}>Nothing has to be configured to work.</h2>
          </Reveal>
          <Reveal delay={60} className={styles.closeBody}>
            <p className="prose">
              Every package carries a default layout by itself, so the shortest way to see openDash
              on a display is to double-click one file. The plugin is what makes that layout yours —
              and it exposes its settings as SimHub properties, so your other dashboards and LED
              profiles can read them too.
            </p>
            <div className={styles.actions}>
              <Link href="/download" className={styles.primary}>
                Download {VERSION}
              </Link>
              <Link href="/install" className={styles.secondary}>
                How to install it
              </Link>
            </div>
          </Reveal>
        </div>
      </section>
    </>
  );
}
