/**
 * What each part of the base face is, in a sentence. The rectangles come from the generated
 * HERO_FACE, which is read from the layout module, so the highlight lands on the part it names and
 * a layout change moves it. Only the words live here.
 */
import type { FacePartId, SiteFace } from '../scripts/content';
import type { Counts } from './counts';

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
