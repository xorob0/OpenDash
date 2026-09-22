import type { Metadata } from 'next';
import Link from 'next/link';
import { Anatomy } from '../../components/Anatomy';
import { Capture } from '../../components/Capture';
import { Clip } from '../../components/Clip';
import { ScreenPicker } from '../../components/ScreenPicker';
import { Section } from '../../components/Section';
import { anatomyParts } from '../../lib/anatomy';
import { packageFile, stillFor } from '../../lib/captures';
import { clipFor } from '../../lib/clips';
import { HERO_FACE } from '../../lib/content.generated';
import { ALL, BASE_FACE, LARGE_FACE, byFolder, pickerFaces } from '../../lib/faces';
import { sizeLabel } from '../../lib/packages';
import { INSTALL, REPO_URL, issueUrl } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Screens and sizes',
  description: '10 face sizes from 1920 × 480 to a 480 round, 2 companions and 2 pit walls. Pick your screen and see it.',
};

export default function Screens() {
  const faces = pickerFaces();
  const base = BASE_FACE;
  const large = LARGE_FACE;
  const companion = byFolder('OpenDash Companion');
  const companionPortrait = byFolder('OpenDash Companion portrait');
  const pitWall = byFolder('OpenDash Pit wall');
  const pitWallPortrait = byFolder('OpenDash Pit wall portrait');
  const companionClip = companion ? clipFor(companion.folder) : undefined;
  const pitWallClip = pitWall ? clipFor(pitWall.folder) : undefined;

  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="faces"
        label="Screens"
        title="Every screen on the rig."
        lede="10 faces for the wheel or the dash, 2 companions, 2 pit walls, all in the plugin. Drawn to scale below: pick one to watch it run."
        wide
      >
        <ScreenPicker faces={faces} initial={base?.slug ?? faces[0]?.slug ?? ''} />
      </Section>

      <Section id="anatomy" label="Anatomy" title="1 face, 5 parts." lede="Every rectangular face has the same 5 parts. Hover or tap a part." wide>
        {base ? (
          <Anatomy src={stillFor(base.folder)} alt={`The ${sizeLabel(base)} face`} width={HERO_FACE.width} height={HERO_FACE.height} parts={anatomyParts(HERO_FACE)} />
        ) : null}
      </Section>

      <Section
        id="fit"
        label="Sizes"
        title="Made for a wide range of screens."
        lede="From a 1920 × 480 strip to a 480 round, and the phone and the pit wall besides. Each size is laid out for its own pixels: a small screen keeps what matters, a large one shows more."
        wide
      >
        <div className={styles.pair}>
          {base ? <Capture file={packageFile(base.folder)} alt={`The ${sizeLabel(base)} face`} width={base.width} height={base.height} caption={sizeLabel(base)} /> : null}
          {large ? <Capture file={packageFile(large.folder)} alt={`The ${sizeLabel(large)} face`} width={large.width} height={large.height} caption={sizeLabel(large)} /> : null}
        </div>
      </Section>

      <Section
        id="companion"
        label="Companion"
        title="The companion."
        lede="A phone or tablet on SimHub's network display. 1 page at a time, 21 to choose from, each with a switch. A wheel button or a tap changes the page."
        wide
      >
        <div className={styles.pair}>
          {companion ? (
            companionClip ? (
              <Clip clip={companionClip} alt="The companion, running" caption="Landscape" />
            ) : (
              <Capture file={packageFile(companion.folder)} alt="The companion showing lap times" width={companion.width} height={companion.height} caption="Landscape" />
            )
          ) : null}
          {companionPortrait ? (
            <Capture file={packageFile(companionPortrait.folder)} alt="The portrait companion" width={companionPortrait.width} height={companionPortrait.height} caption="Portrait" />
          ) : null}
        </div>
      </Section>

      <Section
        id="pit-wall"
        label="Pit wall"
        title="The pit wall."
        lede="A screen for whoever is not driving. 3 pages: Race, Tower and Telemetry. 4 data zones, each showing 1 of 11 pages, plus a web view for any address. A portrait version fits it in 1 page."
        wide
      >
        <div className={styles.pitWalls}>
          {pitWall ? (
            pitWallClip ? (
              <Clip clip={pitWallClip} alt="The pit wall's race page, running" caption="The race page" />
            ) : (
              <Capture file={packageFile(pitWall.folder)} alt="The pit wall's race page" width={pitWall.width} height={pitWall.height} caption="The race page" />
            )
          ) : null}
          {pitWallPortrait ? (
            <Capture file={packageFile(pitWallPortrait.folder)} alt="The portrait pit wall" width={pitWallPortrait.width} height={pitWallPortrait.height} caption="Portrait" scale={0.5} />
          ) : null}
        </div>
      </Section>

      <Section
        id="round"
        label="Round faces"
        title="The 2 round faces."
        lede={
          <>
            800 round and 480 round still use the old 12-slot design. What a round face does with zones is not decided (
            <a href={issueUrl(145)} className="link" rel="noopener">
              #145
            </a>
            ). Both install and work.
          </>
        }
        wide
      >
        <div className={styles.pair}>
          {ALL.filter((p) => p.round).map((p) => (
            <Capture key={p.folder} file={packageFile(p.folder)} alt={`The ${sizeLabel(p)} face`} width={p.width} height={p.height} round caption={sizeLabel(p)} />
          ))}
        </div>
      </Section>

      <Section
        id="not-listed"
        label="Another size"
        title="Not listed?"
        lede={
          <>
            SimHub scales any face to any display. Take the nearest shape. A new size is 1 layout file and a{' '}
            <a href={REPO_URL} className="link" rel="noopener">
              pull request
            </a>
            .
          </>
        }
        wide
      >
        <p className="prose">
          <Link href={INSTALL.href} className="link">
            How to install
          </Link>
        </p>
      </Section>
    </>
  );
}
