/**
 * 1280 x 480, the 10 inch DDU: the 1920 anatomy with a 2 x 2 grid of 223 x 187 slots per side
 * (rung M) around the same 382 px hero column and a 6 px rev bar gap. Geometry from
 * design/canvas/Dash1280x480.dc.html: the body is a centred flex row 447 + 1 + 382 + 1 + 447 =
 * 1278 wide, so it starts at x 1; two rows of 187 and a rule fill the 375 px body exactly.
 */
import { ds } from '../tokens.ts';
import { flankedLayout, RULE } from './flanked.ts';
import type { Layout } from './layout.ts';

const WIDTH = 1280;
const HEIGHT = 480;

const REV_BAR_MARGIN = ds.space[4];
const HEADER_HEIGHT = 64;

const SLOT = { width: 223, height: 187 } as const;
const COLS = 2;
const ROWS = 2;
const GRID_WIDTH = COLS * SLOT.width + (COLS - 1) * RULE;
const HERO_WIDTH = 382;
const CONTENT_WIDTH = GRID_WIDTH + RULE + HERO_WIDTH + RULE + GRID_WIDTH;

export const layout1280x480: Layout = flankedLayout({
  folder: 'openDash slots 1280x480',
  width: WIDTH,
  height: HEIGHT,
  revBar: { left: REV_BAR_MARGIN, top: (HEADER_HEIGHT - ds.shiftLights.height) / 2, width: WIDTH - 2 * REV_BAR_MARGIN, height: ds.shiftLights.height, gap: 6 },
  headerRule: HEADER_HEIGHT,
  flagHeight: ds.indicator.flagBand.height,
  grid: { cell: { ...SLOT }, cols: COLS, rows: ROWS, left: Math.floor((WIDTH - CONTENT_WIDTH) / 2) },
  heroWidth: HERO_WIDTH,
  pitLimiter: { width: 272, height: ds.indicator.pitLimiter.height, topInset: 10 },
});
