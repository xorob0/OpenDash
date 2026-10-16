import type { Metadata } from 'next';
import Link from 'next/link';
import { Anatomy } from '../../components/Anatomy';
import { Capture } from '../../components/Capture';
import { Clip } from '../../components/Clip';
import { ScreenPicker } from '../../components/ScreenPicker';
import { Section } from '../../components/Section';
import { anatomyLede, anatomyParts } from '../../lib/anatomy';
import { packageFile, stillFor } from '../../lib/captures';
import { clipFor } from '../../lib/clips';
import { HERO_FACE, RECTANGULAR_FACES } from '../../lib/content.generated';
import { ALL, BASE_FACE, LARGE_FACE, NARROWEST_FACE, WIDEST_FACE, byFolder, pickerFaces, type PackageOption } from '../../lib/faces';
import { and, counted } from '../../lib/counts';
import { COUNTS } from '../../lib/liveCounts';
import { sizeLabel } from '../../lib/packages';
import { INSTALL, REPO_URL } from '../../lib/site';
import styles from './page.module.css';

/** A face as a sentence names it: a round one is `480 round`, a strip `1920 × 480 strip`, any other `1280 × 720 face`. */
const named = (p: PackageOption): string => (p.round ? sizeLabel(p) : `${sizeLabel(p)} ${p.width >= 3 * p.height ? 'strip' : 'face'}`);

/** `from a 1920 × 480 strip to a 480 round`, read from the faces the build makes, or nothing when it has made none. */
const RANGE = WIDEST_FACE && NARROWEST_FACE ? `from a ${named(WIDEST_FACE)} to a ${named(NARROWEST_FACE)}` : '';

/** The round faces, which still draw the card layout. */
const ROUND = ALL.filter((p) => p.round);

export const metadata: Metadata = {
  title: 'Screens and sizes',
  description: `${counted(COUNTS.faces, 'face size')}${RANGE ? ` ${RANGE}` : ''}, ${counted(COUNTS.companions, 'companion')} and ${counted(COUNTS.pitWalls, 'pit wall')}. Pick your screen and see it.`,
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
        title="Every screen on the rig"
        lede={`${counted(COUNTS.faces, 'face')} for the wheel or the dash, ${counted(COUNTS.companions, 'companion')} and ${counted(COUNTS.pitWalls, 'pit wall')}, all in the plugin. Drawn to scale below: pick one to watch it run.`}
      >
        <ScreenPicker faces={faces} initial={base?.slug ?? faces[0]?.slug ?? ''} />
      </Section>

      <Section id="anatomy" title="One face, part by part" lede={anatomyLede(HERO_FACE, RECTANGULAR_FACES)}>
        {base ? (
          <Anatomy src={stillFor(base.folder)} alt={`The ${sizeLabel(base)} face`} width={HERO_FACE.width} height={HERO_FACE.height} parts={anatomyParts(HERO_FACE, COUNTS)} />
        ) : null}
      </Section>

      <Section
        id="fit"
        title="Made for a wide range of screens"
        lede={`${RANGE ? `${RANGE.charAt(0).toUpperCase()}${RANGE.slice(1)}. ` : ''}Each size is laid out for its own pixels: a small screen keeps what matters, a large one shows more.`}
      >
        <div className={styles.pair}>
          {base ? <Capture file={packageFile(base.folder)} alt={`The ${sizeLabel(base)} face`} width={base.width} height={base.height} caption={sizeLabel(base)} /> : null}
          {large ? <Capture file={packageFile(large.folder)} alt={`The ${sizeLabel(large)} face`} width={large.width} height={large.height} caption={sizeLabel(large)} /> : null}
        </div>
      </Section>

      <Section
        id="companion"
        title="The companion"
        lede="A phone or tablet on SimHub's network display, showing one page at a time. A wheel button or a tap changes it."
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
        title="The pit wall"
        lede={`For whoever is not driving: the whole field with gaps, sectors and stops, your own lap beside it, and zones you fill from ${counted(COUNTS.pitWallZonePages, 'page')}, a web view among them, or in the wide zone from ${counted(COUNTS.pitWallWideZonePages, 'page')} of its own. ${counted(COUNTS.pitWallPages, 'page')} to choose between, and a portrait version.`}
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
        title="The round faces"
        lede={`${and(ROUND.map(sizeLabel))} draw a card layout of their own rather than zones: a disc has no room for them. Both install and work like any other face.`}
      >
        <div className={styles.pair}>
          {ROUND.map((p) => (
            <Capture key={p.folder} file={packageFile(p.folder)} alt={`The ${sizeLabel(p)} face`} width={p.width} height={p.height} round caption={sizeLabel(p)} />
          ))}
        </div>
      </Section>

      <Section
        id="not-listed"
        title="Not listed?"
        lede={
          <>
            SimHub scales any face to any display, so take the nearest shape. If your size deserves a face of its own, ask for it{' '}
            <a href={REPO_URL} className="link" rel="noopener">
              on GitHub
            </a>
            .
          </>
        }
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
