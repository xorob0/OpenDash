/**
 * 800 x 480, the 5 inch DDU at 800 wide: the 850 x 480 layout with 25 px less per side, so one
 * column of three 208 x 124 slots per side (rung M) around the same 382 px hero column. There is
 * no canvas of its own (design/canvas/Dash850x480.dc.html says so in its title); as at 850 the
 * grid is 3 x 124 + 2 rules = 374 high inside the 375 body and the last pixel stays background.
 */
import { ds } from '../tokens.ts';
import { flankedLayout } from './flanked.ts';
import type { Layout } from './layout.ts';

const WIDTH = 800;
const HEIGHT = 480;

const REV_BAR_MARGIN = ds.space[4];
const HEADER_HEIGHT = 64;

const SLOT = { width: 208, height: 124 } as const;
const HERO_WIDTH = 382;

export const layout800x480: Layout = flankedLayout({
  folder: 'openDash slots 800x480',
  width: WIDTH,
  height: HEIGHT,
  revBar: { left: REV_BAR_MARGIN, top: (HEADER_HEIGHT - ds.shiftLights.height) / 2, width: WIDTH - 2 * REV_BAR_MARGIN, height: ds.shiftLights.height, gap: ds.space[1] },
  headerRule: HEADER_HEIGHT,
  flagHeight: ds.indicator.flagBand.height,
  grid: { cell: { ...SLOT }, cols: 1, rows: 3, left: 0 },
  heroWidth: HERO_WIDTH,
  pitLimiter: { width: 272, height: ds.indicator.pitLimiter.height, topInset: 10 },
});
