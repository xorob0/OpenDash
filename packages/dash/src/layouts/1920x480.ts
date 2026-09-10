/**
 * 1920 x 480, the MVP: a 64 px header with the rev bar, then a 375 px body of two 3 x 2 slot
 * grids either side of a 382 px hero column, then a 40 px flag strip. Geometry from
 * design/canvas/Dash.dc.html: the body is a centred flex row 767 + 1 + 382 + 1 + 767 = 1918 wide,
 * so it starts at x 1; slots are 255 x 187 separated by 1 px rules.
 */
import { grid, gridRules, rect } from '../design/geometry.ts';
import { rungFor } from '../design/rung.ts';
import { ds } from '../tokens.ts';
import type { Layout, NamedRect } from './layout.ts';

const WIDTH = ds.screen.mvp.w;
const HEIGHT = ds.screen.mvp.h;

const REV_BAR_MARGIN = ds.space[5];
const REV_BAR_GAP = ds.space[2];
const HEADER_HEIGHT = 64;
const BODY_TOP = HEADER_HEIGHT + 1;
const FLAG_HEIGHT = ds.indicator.flagBand.height;
const BODY_HEIGHT = HEIGHT - BODY_TOP - FLAG_HEIGHT;

const SLOT = { width: 255, height: 187 } as const;
const COLS = 3;
const ROWS = 2;
const RULE = 1;
const GRID_WIDTH = COLS * SLOT.width + (COLS - 1) * RULE;
/** The layout artboard uses 382 (tokens.screen.mvp.hero says 384; the artboard wins). */
const HERO_WIDTH = 382;
const CONTENT_WIDTH = GRID_WIDTH + RULE + HERO_WIDTH + RULE + GRID_WIDTH;
const CONTENT_LEFT = Math.floor((WIDTH - CONTENT_WIDTH) / 2);

const leftGrid = { left: CONTENT_LEFT, top: BODY_TOP };
const leftSeparator = CONTENT_LEFT + GRID_WIDTH;
const heroLeft = leftSeparator + RULE;
const rightSeparator = heroLeft + HERO_WIDTH;
const rightGrid = { left: rightSeparator + RULE, top: BODY_TOP };

const PIT_LIMITER = { width: 272, height: ds.indicator.pitLimiter.height, topInset: 10 } as const;

const named = (prefix: string, rects: readonly { left: number; top: number; width: number; height: number }[]): NamedRect[] =>
  rects.map((r, i) => ({ name: `${prefix}${String(i + 1).padStart(2, '0')}`, rect: r }));

export const layout1920x480: Layout = {
  folder: 'openDash',
  width: WIDTH,
  height: HEIGHT,
  background: ds.color.surface.base,
  slotSize: { ...SLOT },
  slots: [...grid(leftGrid, COLS, ROWS, SLOT, RULE), ...grid(rightGrid, COLS, ROWS, SLOT, RULE)],
  rung: rungFor(SLOT.width, SLOT.height),
  hero: {
    revBar: { left: REV_BAR_MARGIN, top: (HEADER_HEIGHT - ds.shiftLights.height) / 2, width: WIDTH - 2 * REV_BAR_MARGIN, height: ds.shiftLights.height, gap: REV_BAR_GAP },
    column: rect(heroLeft, BODY_TOP, HERO_WIDTH, BODY_HEIGHT),
    pitLimiter: rect(heroLeft + (HERO_WIDTH - PIT_LIMITER.width) / 2, BODY_TOP + PIT_LIMITER.topInset, PIT_LIMITER.width, PIT_LIMITER.height),
    flagStrip: rect(0, HEIGHT - FLAG_HEIGHT, WIDTH, FLAG_HEIGHT),
  },
  rules: [
    { name: 'rule.header', rect: rect(0, HEADER_HEIGHT, WIDTH, RULE) },
    ...named('rule.left.', gridRules(leftGrid, COLS, ROWS, SLOT, RULE)),
    { name: 'rule.hero.left', rect: rect(leftSeparator, BODY_TOP, RULE, BODY_HEIGHT) },
    { name: 'rule.hero.right', rect: rect(rightSeparator, BODY_TOP, RULE, BODY_HEIGHT) },
    ...named('rule.right.', gridRules(rightGrid, COLS, ROWS, SLOT, RULE)),
  ],
};
