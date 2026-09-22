import Link from 'next/link';
import { Attribution } from '../components/Attribution';
import { Actions, Primary, Secondary } from '../components/Buttons';
import { Capture, CaptureNote } from '../components/Capture';
import { Clip } from '../components/Clip';
import { ScreenPicker } from '../components/ScreenPicker';
import { Section } from '../components/Section';
import { StripGlyph } from '../components/StripGlyph';
import { packageFile } from '../lib/captures';
import { clipFor } from '../lib/clips';
import { MODULES, SIMHUB_VERSION, VERSION } from '../lib/content.generated';
import { BASE_FACE, byFolder, pickerFaces } from '../lib/faces';
import { sizeLabel } from '../lib/packages';
import { CLAIMED_SIM, DIFFERENTIATORS, FREE_FOREVER, FREE_HEADLINE, NOTHING_TO_UNLOCK, issueUrl } from '../lib/site';
import styles from './page.module.css';

/** The shapes the lights teaser draws: a common wheel, a wide wheel, a brow. */
const STRIP_EXAMPLES = [
  { left: 3, centre: 9, right: 3, name: '3 / 9 / 3' },
  { left: 4, centre: 14, right: 4, name: '4 / 14 / 4' },
  { left: 0, centre: 25, right: 0, name: 'A brow of 25' },
];

export default function Home() {
  const hero = BASE_FACE;
  const clip = hero ? clipFor(hero.folder) : undefined;
  const companion = byFolder('OpenDash Companion');
  const pitWall = byFolder('OpenDash Pit wall');
  const faces = pickerFaces();
  const off = MODULES.filter((m) => !m.enabled);

  return (
    <>
      <section className={`section ${styles.hero}`}>
        <div className={`page ${styles.heroGrid}`}>
          <div className={styles.heroText}>
            <p className="label">
              iRacing · SimHub {SIMHUB_VERSION}+ · Windows · <span className={styles.alpha}>Alpha {VERSION}</span>
            </p>
            <h1 className="display">{FREE_HEADLINE}</h1>
            <p className={`prose ${styles.lede}`}>
              OpenDash is a set of SimHub dashboards for iRacing on Windows. 14 screens, 21 pages and 63 LED profiles, generated from source.
            </p>
            <p className="prose">
              <strong>{FREE_FOREVER}</strong> {NOTHING_TO_UNLOCK}
            </p>
            <Actions>
              <Primary href="/download">Download {VERSION}</Primary>
              <Secondary href="#screen">Find your screen</Secondary>
            </Actions>
          </div>

          {hero ? (
            <div className={styles.heroShot}>
              {clip ? (
                <Clip clip={clip} alt={`The ${sizeLabel(hero)} face, running`} caption={`OpenDash ${sizeLabel(hero)}, the base size`} priority />
              ) : (
                <Capture file={packageFile(hero.folder)} alt={`The ${sizeLabel(hero)} face`} width={hero.width} height={hero.height} caption={`OpenDash ${sizeLabel(hero)}, the base size`} priority />
              )}
              <CaptureNote served={VERSION} />
            </div>
          ) : null}
        </div>
      </section>

      <section id="why" className={`section ruled`}>
        <div className="page">
          <ul className={styles.why}>
            {DIFFERENTIATORS.map((d) => (
              <li key={d.id} className={styles.reason}>
                <h2 className="h3">{d.title}</h2>
                <p className="prose">{d.body}</p>
                {d.id === 'lights' ? <Attribution className={styles.credit} /> : null}
              </li>
            ))}
          </ul>
        </div>
      </section>

      <Section
        id="screen"
        label="Screens"
        title="Find your screen."
        lede="10 face sizes, drawn to scale. Pick yours to see it and download that file. If nothing matches, take the nearest shape. 850 × 480 is the base size."
        wide
      >
        <ScreenPicker faces={faces} initial={hero?.slug ?? faces[0]?.slug ?? ''} />
        <p className={`prose ${styles.more}`}>
          <Link href="/screens" className="link">
            All 14 screens, with the companion and the pit wall
          </Link>
        </p>
      </Section>

      <Section
        id="pages"
        label="Pages"
        title="21 pages. 1 button per zone."
        lede="Zone B and zone C each show 1 page. A wheel button cycles it. Hold a button to glance at another page, release to return."
        wide
      >
        <ul className={styles.chips}>
          {MODULES.map((m) => (
            <li key={m.id} className={`${styles.chip} ${m.enabled ? '' : styles.off}`}>
              <span className={`num ${styles.chipNumber}`}>{String(m.number).padStart(2, '0')}</span>
              {m.name}
            </li>
          ))}
        </ul>
        <p className={`prose ${styles.more}`}>
          {off.length} pages ship off because iRacing publishes no data for them: {off.map((m) => m.name).join(', ')}.{' '}
          <Link href="/pages" className="link">
            Every page, captured
          </Link>
          .
        </p>
      </Section>

      <Section id="second-screens" label="Second screens" title="A phone beside the wheel. A screen for the pit wall." wide>
        <div className={styles.seconds}>
          {companion ? (
            <div className={styles.second}>
              <h3 className="h3">The companion</h3>
              <p className="prose">1 page at a time on a phone or tablet. 21 pages, each with a switch. Landscape 850 × 480 or portrait 480 × 850.</p>
              <Capture file={packageFile(companion.folder)} alt="The companion showing lap times" width={companion.width} height={companion.height} caption={sizeLabel(companion)} />
            </div>
          ) : null}
          {pitWall ? (
            <div className={styles.second}>
              <h3 className="h3">The pit wall</h3>
              <p className="prose">1920 × 1080 for whoever is not driving. 3 pages: Race, Tower and Telemetry. A 1080 × 1920 portrait version in 1 page.</p>
              <Capture file={packageFile(pitWall.folder)} alt="The pit wall's race page" width={pitWall.width} height={pitWall.height} caption={sizeLabel(pitWall)} />
            </div>
          ) : null}
        </div>
        <p className={`prose ${styles.more}`}>
          <Link href="/screens#companion" className="link">
            More on the companion and the pit wall
          </Link>
        </p>
      </Section>

      <Section
        id="lights"
        label="Lights"
        title="Lights that match the car."
        lede="62 strip shapes and an 8 × 8 flag box, all generated. The strip shows your car's own shift lights. Flags, spotter, pit states and warnings light it too."
        wide
      >
        <ul className={styles.strips}>
          {STRIP_EXAMPLES.map((s) => (
            <li key={s.name} className={styles.strip}>
              <StripGlyph left={s.left} centre={s.centre} right={s.right} led={12} gap={4} title={`A ${s.name} strip`} />
              <span className={`num ${styles.stripName}`}>{s.name}</span>
            </li>
          ))}
        </ul>
        <Attribution className={`prose ${styles.credit}`} />
        <p className={`prose ${styles.more}`}>
          <Link href="/lights" className="link">
            The lights, the strip shapes and the flag box
          </Link>
        </p>
      </Section>

      <Section id="status" label="Status" title="Alpha. Here is what is missing." wide>
        <ul className={`rows ${styles.status}`}>
          <li>Every release so far is a pre-release.</li>
          <li>{CLAIMED_SIM}</li>
          <li>
            No idle screen yet (
            <a href={issueUrl(113)} className="link" rel="noopener">
              #113
            </a>
            ).
          </li>
          <li>
            Night mode is for the lights only. Screens are coming (
            <a href={issueUrl(128)} className="link" rel="noopener">
              #128
            </a>
            ).
          </li>
          <li>The 800 and 480 round faces are still the old 12-slot design.</li>
        </ul>
        <p className={`prose ${styles.more}`}>
          <Link href="/compare" className="link">
            The full comparison with Lovely and Daniel Newman Racing
          </Link>
        </p>
      </Section>

      <Section id="get" label="Get it" title="Get it." lede={`${FREE_FOREVER} Windows, SimHub ${SIMHUB_VERSION} or later.`} wide>
        <Actions>
          <Primary href="/download">Download {VERSION}</Primary>
          <Secondary href="/install">How to install</Secondary>
        </Actions>
      </Section>
    </>
  );
}
