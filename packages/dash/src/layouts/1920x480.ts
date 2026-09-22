/**
 * 1920 x 480, the MVP: a 64 px header with the rev bar, then a 375 px body of two 3 x 2 slot
 * grids either side of a 382 px hero column, then a 40 px flag strip. Geometry from
 * design/canvas/Dash.dc.html: the body is a centred flex row 767 + 1 + 382 + 1 + 767 = 1918 wide,
 * so it starts at x 1; slots are 255 x 187 separated by 1 px rules.
 */
import { ds } from '../tokens.ts';
import { flankedLayout, RULE } from './flanked.ts';
import type { Layout } from './layout.ts';

const WIDTH = ds.screen.mvp.w;
const HEIGHT = ds.screen.mvp.h;

const REV_BAR_MARGIN = ds.space[5];
const HEADER_HEIGHT = 64;

const SLOT = { width: 255, height: 187 } as const;
const COLS = 3;
const ROWS = 2;
const GRID_WIDTH = COLS * SLOT.width + (COLS - 1) * RULE;
/** The layout artboard uses 382 (tokens.screen.mvp.hero says 384; the artboard wins). */
const HERO_WIDTH = 382;
const CONTENT_WIDTH = GRID_WIDTH + RULE + HERO_WIDTH + RULE + GRID_WIDTH;

export const layout1920x480: Layout = flankedLayout({
  folder: 'OpenDash slots 1920x480',
  width: WIDTH,
  height: HEIGHT,
  revBar: { left: REV_BAR_MARGIN, top: (HEADER_HEIGHT - ds.shiftLights.height) / 2, width: WIDTH - 2 * REV_BAR_MARGIN, height: ds.shiftLights.height, gap: ds.space[2] },
  headerRule: HEADER_HEIGHT,
  flagHeight: ds.indicator.flagBand.height,
  grid: { cell: { ...SLOT }, cols: COLS, rows: ROWS, left: Math.floor((WIDTH - CONTENT_WIDTH) / 2) },
  heroWidth: HERO_WIDTH,
  pitLimiter: { width: 272, height: ds.indicator.pitLimiter.height, topInset: 10 },
});
