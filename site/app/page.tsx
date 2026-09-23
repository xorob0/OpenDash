import Link from 'next/link';
import { Attribution } from '../components/Attribution';
import { Actions, Primary, Secondary } from '../components/Buttons';
import { Capture } from '../components/Capture';
import { Clip } from '../components/Clip';
import { LedStrip } from '../components/LedStrip';
import { FRAMES } from '../lib/stripFrames';
import { ScreenPicker } from '../components/ScreenPicker';
import { Section } from '../components/Section';
import { packageFile } from '../lib/captures';
import { clipFor } from '../lib/clips';
import { MODULES, SIMHUB_VERSION, VERSION } from '../lib/content.generated';
import { BASE_FACE, byFolder, pickerFaces } from '../lib/faces';
import { sizeLabel } from '../lib/packages';
import { CLAIMED_SIM, DIFFERENTIATORS, FREE_FOREVER, FREE_HEADLINE, INSTALL, NOTHING_TO_UNLOCK, issueUrl } from '../lib/site';
import styles from './page.module.css';

/** The states the lights teaser shows, drawn by the site. */
const TEASER = [FRAMES.shift!, FRAMES.yellow!, FRAMES.spotter!];

export default function Home() {
  const hero = BASE_FACE;
  const clip = hero ? clipFor(hero.folder) : undefined;
  const companion = byFolder('OpenDash Companion');
  const pitWall = byFolder('OpenDash Pit wall');
  const faces = pickerFaces();

  return (
    <>
      <section className={`section ${styles.hero}`}>
        <div className={`page ${styles.heroGrid}`}>
          <div className={styles.heroText}>
            <p className="label">
              SimHub {SIMHUB_VERSION}+ · iRacing first · <span className={styles.alpha}>Alpha {VERSION}</span>
            </p>
            <h1 className="display">{FREE_HEADLINE}</h1>
            <p className={`prose ${styles.lede}`}>OpenDash is a free set of dashboards for SimHub, built for iRacing first.</p>
            <p className="prose">
              <strong>{FREE_FOREVER}</strong> {NOTHING_TO_UNLOCK}
            </p>
            <Actions>
              <Primary href={INSTALL.href}>{INSTALL.label} OpenDash</Primary>
              <Secondary href="#screen">Find your screen</Secondary>
            </Actions>
          </div>

          {hero ? (
            <div className={styles.heroShot}>
              {clip ? (
                <Clip clip={clip} alt={`The ${sizeLabel(hero)} face, running`} caption={`OpenDash ${sizeLabel(hero)}`} priority />
              ) : (
                <Capture file={packageFile(hero.folder)} alt={`The ${sizeLabel(hero)} face`} width={hero.width} height={hero.height} caption={`OpenDash ${sizeLabel(hero)}`} priority />
              )}
            </div>
          ) : null}
        </div>
      </section>

      <section id="why" className="section ruled">
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

      <Section id="screen" title="Find your screen" lede="Ten faces, drawn to scale and showing what they draw. Pick one to watch it run; if nothing matches your panel, take the nearest shape.">
        <ScreenPicker faces={faces} initial={hero?.slug ?? faces[0]?.slug ?? ''} />
        <p className={`prose ${styles.more}`}>
          <Link href="/screens" className="link">
            All 14 screens, with the companion and the pit wall
          </Link>
        </p>
      </Section>

      <Section id="pages" title="21 pages, one button per zone" lede="Zone B and zone C each show one page, and a wheel button cycles it. Hold the button to glance at another, release to go back.">
        <ul className={styles.chips}>
          {MODULES.map((m) => (
            <li key={m.id} className={`${styles.chip} ${m.enabled ? '' : styles.off}`}>
              <span className={`num ${styles.chipNumber}`}>{String(m.number).padStart(2, '0')}</span>
              {m.name}
            </li>
          ))}
        </ul>
        <p className={`prose ${styles.more}`}>
          <Link href="/pages" className="link">
            Every page, captured
          </Link>
        </p>
      </Section>

      <Section id="second-screens" title="A phone beside the wheel, a screen for the pit wall">
        <div className={styles.seconds}>
          {companion ? (
            <div className={styles.second}>
              <h3 className="h3">The companion</h3>
              <p className="prose">One page at a time on a phone or tablet, landscape or portrait. All 21, each with a switch.</p>
              <Capture file={packageFile(companion.folder)} alt="The companion showing lap times" width={companion.width} height={companion.height} caption="Companion" />
            </div>
          ) : null}
          {pitWall ? (
            <div className={styles.second}>
              <h3 className="h3">The pit wall</h3>
              <p className="prose">For whoever is not driving: the field with gaps and stops, your lap beside it, and a portrait version.</p>
              <Capture file={packageFile(pitWall.folder)} alt="The pit wall's race page" width={pitWall.width} height={pitWall.height} caption="Pit wall" />
            </div>
          ) : null}
        </div>
        <p className={`prose ${styles.more}`}>
          <Link href="/screens#companion" className="link">
            More on the companion and the pit wall
          </Link>
        </p>
      </Section>

      <Section id="lights" title="LEDs, for more than revs" lede="Shift lights in your car's own colours and order, and sides that carry the flags, a car alongside, the limiter and the warnings. 62 strip shapes and an 8 × 8 flag box.">
        <ul className={styles.strips}>
          {TEASER.map((f) => (
            <li key={f.label} className={styles.strip}>
              <LedStrip left={3} centre={9} right={3} frame={f} />
            </li>
          ))}
        </ul>
        <p className={`prose ${styles.aside}`}>Drawn by the site to show the idea. The Lights page has the whole story.</p>
        <p className={`prose ${styles.more}`}>
          <Link href="/lights" className="link">
            The lights, the strip shapes and the flag box
          </Link>
        </p>
      </Section>

      <Section id="status" title="Alpha: what is missing">
        <ul className={`rows ${styles.status}`}>
          <li>Every release so far is a candidate.</li>
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

      <Section id="get" title="Install it" lede={`Windows, SimHub ${SIMHUB_VERSION} or later. ${FREE_FOREVER}`}>
        <Actions>
          <Primary href={INSTALL.href}>{INSTALL.label} OpenDash</Primary>
          <Secondary href="/download">Download the files</Secondary>
        </Actions>
      </Section>
    </>
  );
}
