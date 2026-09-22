import type { Metadata } from 'next';
import Link from 'next/link';
import { Anatomy } from '../../components/Anatomy';
import { Capture, CaptureNote } from '../../components/Capture';
import { Clip } from '../../components/Clip';
import { ScreenPicker } from '../../components/ScreenPicker';
import { Section } from '../../components/Section';
import { SizeList } from '../../components/SizeList';
import { anatomyParts } from '../../lib/anatomy';
import { packageFile, stillFor } from '../../lib/captures';
import { clipFor } from '../../lib/clips';
import { HERO_FACE, VERSION } from '../../lib/content.generated';
import { ALL, BASE_FACE, LARGE_FACE, byFolder, pickerFaces } from '../../lib/faces';
import { sizeLabel } from '../../lib/packages';
import { REPO_URL, issueUrl } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Screens and sizes',
  description: '10 face sizes from 1920 × 480 to a 480 round, 2 companions and 2 pit walls. Pick your screen and download that file.',
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
        lede="10 faces for the wheel or the dash, 2 companions, 2 pit walls. Each is drawn for its exact pixels, never scaled."
        wide
      >
        <h2 className="h3">Find your face size</h2>
        <p className={`prose ${styles.under}`}>10 face sizes, drawn to scale. Pick yours to see it and download that file. If nothing matches, take the nearest shape. 850 × 480 is the base size.</p>
        <div className={styles.picker}>
          <ScreenPicker faces={faces} initial={base?.slug ?? faces[0]?.slug ?? ''} />
        </div>
        <h2 className={`h3 ${styles.listHead}`}>All 14 screens</h2>
        <SizeList packages={ALL} />
        <div className={styles.note}>
          <CaptureNote served={VERSION} />
        </div>
      </Section>

      <Section id="anatomy" label="Anatomy" title="1 face, 5 parts." lede="Every rectangular face has the same 5 parts. Hover or tap a part." wide>
        {base ? (
          <Anatomy src={stillFor(base.folder)} alt={`The ${sizeLabel(base)} face`} width={HERO_FACE.width} height={HERO_FACE.height} parts={anatomyParts(HERO_FACE)} />
        ) : null}
      </Section>

      <Section
        id="fit"
        label="Fit"
        title="A page is laid out, never scaled."
        lede="The same page in the base face and in the large one. The base stacks it; the large tabulates it. A page sheds rows before it shrinks numerals. Every text is measured against its box in tests, so nothing clips."
        wide
      >
        <div className={styles.pair}>
          {base ? <Capture file={packageFile(base.folder)} alt={`The ${sizeLabel(base)} face`} width={base.width} height={base.height} caption={`${sizeLabel(base)}, the base size`} /> : null}
          {large ? <Capture file={packageFile(large.folder)} alt={`The ${sizeLabel(large)} face`} width={large.width} height={large.height} caption={`${sizeLabel(large)}, the large size`} /> : null}
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
              <Clip clip={companionClip} alt="The companion, running" caption={`${sizeLabel(companion)}, landscape`} />
            ) : (
              <Capture file={packageFile(companion.folder)} alt="The companion showing lap times" width={companion.width} height={companion.height} caption={`${sizeLabel(companion)}, landscape`} />
            )
          ) : null}
          {companionPortrait ? (
            <Capture file={packageFile(companionPortrait.folder)} alt="The portrait companion" width={companionPortrait.width} height={companionPortrait.height} caption={`${sizeLabel(companionPortrait)}, portrait`} />
          ) : null}
        </div>
      </Section>

      <Section
        id="pit-wall"
        label="Pit wall"
        title="The pit wall."
        lede="A 1920 × 1080 screen for whoever is not driving. 3 pages: Race, Tower and Telemetry. 4 data zones, each showing 1 of 11 pages, plus a web view for any address. The portrait 1080 × 1920 fits it in 1 page."
        wide
      >
        <div className={styles.pitWalls}>
          {pitWall ? (
            pitWallClip ? (
              <Clip clip={pitWallClip} alt="The pit wall's race page, running" caption={`${sizeLabel(pitWall)}, the race page`} />
            ) : (
              <Capture file={packageFile(pitWall.folder)} alt="The pit wall's race page" width={pitWall.width} height={pitWall.height} caption={`${sizeLabel(pitWall)}, the race page`} />
            )
          ) : null}
          {pitWallPortrait ? (
            <Capture file={packageFile(pitWallPortrait.folder)} alt="The portrait pit wall" width={pitWallPortrait.width} height={pitWallPortrait.height} caption={`${sizeLabel(pitWallPortrait)}, portrait`} scale={0.5} />
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
          <Link href="/install" className="link">
            How to install a face
          </Link>
        </p>
      </Section>
    </>
  );
}
