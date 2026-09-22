/**
 * 1280 x 720, the 4 to 6.8 inch wheel and dash screens: the 1280 x 480 anatomy with a third row,
 * so a 2 x 3 grid of 223 x 204 slots per side (rung M, twelve slots like the MVP) around the
 * 382 px hero column, and the 1920's 24 px rev bar margin. Geometry from
 * design/canvas/Dash1280x720.dc.html: the grid is 3 x 204 + 2 rules = 614 high inside the 615 body,
 * so the last pixel above the flag strip stays background.
 */
import { ds } from '../tokens.ts';
import { flankedLayout, RULE } from './flanked.ts';
import type { Layout } from './layout.ts';

const WIDTH = 1280;
const HEIGHT = 720;

const REV_BAR_MARGIN = ds.space[5];
const HEADER_HEIGHT = 64;

const SLOT = { width: 223, height: 204 } as const;
const COLS = 2;
const ROWS = 3;
const GRID_WIDTH = COLS * SLOT.width + (COLS - 1) * RULE;
const HERO_WIDTH = 382;
const CONTENT_WIDTH = GRID_WIDTH + RULE + HERO_WIDTH + RULE + GRID_WIDTH;

export const layout1280x720: Layout = flankedLayout({
  folder: 'OpenDash slots 1280x720',
  width: WIDTH,
  height: HEIGHT,
  revBar: { left: REV_BAR_MARGIN, top: (HEADER_HEIGHT - ds.shiftLights.height) / 2, width: WIDTH - 2 * REV_BAR_MARGIN, height: ds.shiftLights.height, gap: 6 },
  headerRule: HEADER_HEIGHT,
  flagHeight: ds.indicator.flagBand.height,
  grid: { cell: { ...SLOT }, cols: COLS, rows: ROWS, left: Math.floor((WIDTH - CONTENT_WIDTH) / 2) },
  heroWidth: HERO_WIDTH,
  pitLimiter: { width: 272, height: ds.indicator.pitLimiter.height, topInset: 10 },
});
