/**
 * 800 x 286, the nano: the smallest rectangular face. A 32 px rev bar over a 54 px header, one
 * column of two 250 x 109 slots per side around a 298 px hero column where the gear steps down
 * to 180 and the speed to 64 (gap 20), the small pit limiter, and a 12 px flag strip with no
 * label. The slots are rung M by width but only 109 high, so the cards take the S padding.
 * Geometry from design/canvas/DashNano800x286.dc.html: the rev bar sits at y 10, two rows of
 * 109 and a rule fill the 219 px body exactly.
 */
import { FLAG_STRIP_STYLES } from '../components/flagStrip.ts';
import { GEAR_SIZES } from '../components/gear.ts';
import { rungSpec } from '../design/rung.ts';
import { ds } from '../tokens.ts';
import { flankedLayout } from './flanked.ts';
import type { Layout } from './layout.ts';

const WIDTH = 800;
const HEIGHT = 286;

const REV_BAR_MARGIN = ds.space[4];
/** The canvas draws the rev bar 32 high at y 10 on this face; neither is a token. */
const REV_BAR = { top: 10, height: 32 } as const;
const HEADER_HEIGHT = 54;
/** The canvas draws a 12 px strip, too thin for a label; no token. */
const FLAG_HEIGHT = 12;

const SLOT = { width: 250, height: 109 } as const;
const HERO_WIDTH = 298;

export const layout800x286: Layout = flankedLayout({
  folder: 'openDash slots 800x286',
  width: WIDTH,
  height: HEIGHT,
  revBar: { left: REV_BAR_MARGIN, top: REV_BAR.top, width: WIDTH - 2 * REV_BAR_MARGIN, height: REV_BAR.height, gap: ds.space[1] },
  headerRule: HEADER_HEIGHT,
  flagHeight: FLAG_HEIGHT,
  flagStyle: FLAG_STRIP_STYLES.nano,
  grid: { cell: { ...SLOT }, cols: 1, rows: 2, left: 0 },
  heroWidth: HERO_WIDTH,
  pitLimiter: { width: 252, height: ds.indicator.pitLimiter.heightSm, topInset: 10 },
  gearSize: GEAR_SIZES.nano,
  cardPadding: rungSpec('S').padding,
});
