/**
 * The one run on a face that is set in Bold.
 *
 * Every artboard says the same thing twice over: a numeral is Barlow Condensed 600, and the current
 * gear alone is 700. The speed page was Bold as well, and zone A's catalogue is embedded in every
 * zone face, so the second Bold was on every screen the build emits rather than on one drawing.
 *
 * Asserted over the built packages rather than over a module, because that is where the mistake
 * showed: the page looked right on its own and wrong on eight faces. The flag band's name is the
 * other 700 the design asks for and is a label rather than a numeral, which is why it is named here
 * rather than swept in: the artboards set it in the label family at 700 against the 500 of every
 * other label, and `flagBand.test.ts` is where that is held.
 */
import { describe, expect, test } from 'bun:test';
import { buildZoneFace, sizeOf, ZONE_FACES } from '../src/zones/index.ts';
import { buildPackage } from '../src/dashboard.ts';
import { LAYOUTS } from '../src/layouts/index.ts';
import { walkItems } from '../src/walk.ts';
import type { Dashboard, TextItem } from '../src/generator.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

const textsOf = (dashboards: readonly Dashboard[]): TextItem[] =>
  dashboards.flatMap((d) => d.screens.flatMap((s) => [...walkItems(s.items)])).filter((i): i is TextItem => i.kind === 'text');

/** The gear, and the two neighbours ghosted either side of it, which are the same glyph. */
const isGear = (name: string): boolean => /(^|\.)gear$/.test(name) || /(^|\.)gear\.(above|below)$/.test(name);

/** The flag band's name, which is the design's other 700 and is a label rather than a numeral. */
const isFlagName = (name: string): boolean => name.startsWith('flag.');

describe('only the gear is Bold', () => {
  for (const face of ZONE_FACES) {
    test(`${face.folder} sets every other numeral in the numeral weight`, () => {
      const built = buildZoneFace(face, OPTS);
      const bold = textsOf([built.main, ...built.zones]).filter((i) => i.fontWeight === 'Bold');
      expect(bold.length).toBeGreaterThan(0);
      const stray = [...new Set(bold.map((i) => i.name))].filter((n) => !isGear(n) && !isFlagName(n));
      expect({ folder: face.folder, stray }).toEqual({ folder: face.folder, stray: [] });
    });
  }

  test('and the card layouts say the same, at every size', () => {
    for (const layout of LAYOUTS) {
      const pkg = buildPackage(layout, { version: OPTS.version, simHubVersion: OPTS.simHubVersion, strategy: 'widget' as const });
      const bold = textsOf(pkg.dashboards).filter((i) => i.fontWeight === 'Bold');
      // The flag band's name is the other Bold the design asks for, and it is a label rather than a
      // numeral: the artboards set it in the label family at 700 against the 500 of every other.
      const stray = [...new Set(bold.map((i) => i.name))].filter((n) => !isGear(n) && !isFlagName(n));
      expect({ folder: pkg.folderName, stray }).toEqual({ folder: pkg.folderName, stray: [] });
    }
  });

  test('the ghosts are drawn at the 0.42 of the gear the sheets draw', () => {
    // Three artboards draw a gear with its neighbours, at 204 over 85, 198 over 83 and 194 over 81,
    // which is 0.417, 0.419 and 0.418. They were drawn at 0.34, a fifth short.
    const face = ZONE_FACES.find((f) => sizeOf(f) === '1280x720') ?? ZONE_FACES[0]!;
    const built = buildZoneFace(face, OPTS);
    const texts = textsOf([built.main, ...built.zones]);
    const gear = texts.find((i) => i.name === 'gearSpeedRevs.main.gear');
    const ghost = texts.find((i) => i.name === 'gearSpeedRevs.gear.below');
    if (!gear || !ghost) throw new Error('no ghosted gear on this face');
    expect(ghost.fontSize / gear.fontSize).toBeCloseTo(0.42, 2);
  });
});
