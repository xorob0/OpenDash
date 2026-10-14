/**
 * What each part of the base face is, in a sentence. The rectangles come from the generated
 * HERO_FACE, which is read from the layout module, so the highlight lands on the part it names and
 * a layout change moves it. Only the words live here.
 */
import type { FacePartId, SiteFace, SiteFaceParts } from '../scripts/content';
import { and, type Counts } from './counts';
import { sizeLabel } from './packages';

export interface AnatomyPart {
  id: FacePartId;
  /** What the canvas calls it: the letter for a zone, the name for everything else. */
  tag: string;
  name: string;
  body: string;
  rect: { left: number; top: number; width: number; height: number };
}

const copy = (c: Counts): Record<FacePartId, Omit<AnatomyPart, 'id' | 'rect'>> => ({
  revBar: {
    tag: 'Rev bar',
    name: 'The car’s own shift lights.',
    body: 'SimHub’s bands for a car that publishes none. Switch it off for a wheel with LEDs, and the zones take the room.',
  },
  bar: {
    tag: 'The bar',
    name: 'Settled values.',
    body: `2 fields at each end, chosen from ${c.barFields}, and the car settings between them. It never cycles.`,
  },
  zoneB: {
    tag: 'Zone B',
    name: `1 of ${c.pages} pages.`,
    body: 'A wheel button cycles it. Here: lap times.',
  },
  zoneA: {
    tag: 'Zone A',
    name: 'The gear, read by reflex.',
    body: `${c.zoneAPages} pages: gear with speed and revs, gear alone, speed, or the track.`,
  },
  zoneC: {
    tag: 'Zone C',
    name: `A second page from the same ${c.pages}.`,
    body: 'On its own button. Here: the relative.',
  },
  band: {
    tag: 'Band D',
    name: 'Fuel by default.',
    body: `${c.bandDPages} pages in all, and a car theme adds its own. A flag takes the band for three seconds, then both ends of it.`,
  },
});

export const anatomyParts = (face: SiteFace, c: Counts): AnatomyPart[] => {
  const words = copy(c);
  return face.parts.map((p) => ({ id: p.id, rect: p.rect, ...words[p.id] }));
};

/** A part as a sentence names it: the face `has no bar`. */
const NOUN: Record<FacePartId, string> = {
  revBar: 'rev bar',
  bar: 'bar',
  zoneB: 'zone B',
  zoneA: 'zone A',
  zoneC: 'zone C',
  band: 'band D',
};

const or = (words: readonly string[]): string =>
  words.length < 2 ? (words[0] ?? '') : `${words.slice(0, -1).join(', ')} or ${words[words.length - 1]}`;

/**
 * The anatomy's lede, held to every rectangular face rather than to the one it shows. A face that
 * lacks a part the anatomy offers is named with what it lacks, and then the sentence no longer says
 * every: the 800 × 286 face has no room for the bar (#1094).
 */
export function anatomyLede(shown: SiteFace, faces: readonly SiteFaceParts[]): string {
  const offered = shown.parts.map((p) => p.id);
  const short = faces
    .map((f) => ({ face: f, missing: offered.filter((id) => !f.parts.includes(id)) }))
    .filter((f) => f.missing.length > 0)
    .map(({ face, missing }) => `the ${sizeLabel({ ...face, round: false })} face has no ${or(missing.map((id) => NOUN[id]))}`);
  const built = short.length === 0 ? 'Every rectangular face is built the same way.' : `The rectangular faces are built the same way, but ${and(short)}.`;
  return `${built} Hover or tap a part.`;
}
