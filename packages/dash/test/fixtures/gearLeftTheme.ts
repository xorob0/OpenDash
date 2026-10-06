/**
 * A theme no build ships, for anatomy.test.ts: the gear at the left edge, then zones B and C, at
 * 1280 x 480 and 1920 x 480 alone.
 *
 * Its regions are the house face's moved rather than redrawn, so that every zone keeps its size and
 * the modules are handed only rectangles they are already built into and measured against: a theme
 * that wants a new shape pays for it in `secondScreens.test.ts`, and this one is not about that.
 * Its overlay is empty, so it draws in the default's colours and builds in a default process.
 */
import type { Rect } from '../../src/generator.ts';
import type { Theme } from '../../src/themes/index.ts';
import { zoneRegions } from '../../src/zones/layout.ts';

export const GEAR_LEFT_THEME_ID = 'test-gear-left';

export const gearLeftTheme: Theme = {
  overlay: {},
  anatomy: {
    sizes: [
      { width: 1280, height: 480 },
      { width: 1920, height: 480 },
    ],
    regions: (face) => {
      const { zoneA, zoneB } = face.zones;
      const gear: Rect = { ...zoneA, left: zoneB.left };
      const shift = gear.left - zoneA.left;
      return zoneRegions(face).map((region) => {
        if ((region.role === 'zone' && region.zone === 'A') || region.role === 'hero') return { ...region, rect: gear };
        if (region.role === 'zone' && region.zone === 'B') return { ...region, rect: { ...zoneB, left: gear.left + gear.width + 1 } };
        if (region.role === 'pitAlert') return { ...region, rect: { ...region.rect, left: region.rect.left + shift } };
        return region;
      });
    },
  },
};
