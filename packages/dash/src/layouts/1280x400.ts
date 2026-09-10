/**
 * 1280 x 400, the 7.8 inch DDU: the 1280 x 480 anatomy squeezed by 80 px. The rev bar and the
 * flag band drop to 32 px, the header rule sits at 54, the pit limiter is the small 28 px block,
 * and the 2 x 2 grids per side hold 223 x 156 slots (rung M) in the 313 px body. Geometry from
 * design/canvas/Dash1280x400.dc.html: the rev bar sits at y 10, not centred in its 54 px header.
 */
import { ds } from '../tokens.ts';
import { flankedLayout, RULE } from './flanked.ts';
import type { Layout } from './layout.ts';

const WIDTH = 1280;
const HEIGHT = 400;

const REV_BAR_MARGIN = ds.space[4];
/** The canvas draws the rev bar 32 high at y 10 on this face; neither is a token. */
const REV_BAR = { top: 10, height: 32 } as const;
const HEADER_HEIGHT = 54;

const SLOT = { width: 223, height: 156 } as const;
const COLS = 2;
const ROWS = 2;
const GRID_WIDTH = COLS * SLOT.width + (COLS - 1) * RULE;
const HERO_WIDTH = 382;
const CONTENT_WIDTH = GRID_WIDTH + RULE + HERO_WIDTH + RULE + GRID_WIDTH;

export const layout1280x400: Layout = flankedLayout({
  folder: 'openDash 1280x400',
  width: WIDTH,
  height: HEIGHT,
  revBar: { left: REV_BAR_MARGIN, top: REV_BAR.top, width: WIDTH - 2 * REV_BAR_MARGIN, height: REV_BAR.height, gap: 6 },
  headerRule: HEADER_HEIGHT,
  flagHeight: ds.indicator.flagBand.heightSm,
  grid: { cell: { ...SLOT }, cols: COLS, rows: ROWS, left: Math.floor((WIDTH - CONTENT_WIDTH) / 2) },
  heroWidth: HERO_WIDTH,
  pitLimiter: { width: 272, height: ds.indicator.pitLimiter.heightSm, topInset: 10 },
});
