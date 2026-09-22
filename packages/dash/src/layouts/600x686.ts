/**
 * 600 x 686, the Heusinkveld DisplayDash: a portrait face where the hero is a full-width band
 * instead of a column. The rev bar header over a rule, a 300 px band with the gear and speed row
 * (and the pit limiter above the gear), a second rule, then a 3 x 2 grid of 199 x 139 slots
 * (rung M, numbered row-major) and the flag strip along the bottom edge. Geometry from
 * design/canvas/DashDisplayDash600x686.dc.html: the grid is 3 x 199 + 2 rules = 599 wide and
 * 2 x 139 + 1 = 279 high in a 600 x 280 area, so the last column and the row above the flag
 * strip stay background.
 */
import { grid, gridRules, rect } from '../design/geometry.ts';
import { ds } from '../tokens.ts';
import { RULE } from './flanked.ts';
import { layoutDescription, namedRects, type Layout } from './layout.ts';

const WIDTH = 600;
const HEIGHT = 686;

const REV_BAR_MARGIN = ds.space[4];
const HEADER_HEIGHT = 64;
const HERO_TOP = HEADER_HEIGHT + RULE;
const HERO_HEIGHT = 300;
const HERO_RULE = HERO_TOP + HERO_HEIGHT;
const FLAG_HEIGHT = ds.indicator.flagBand.height;

const SLOT = { width: 199, height: 139 } as const;
const COLS = 3;
const ROWS = 2;
const GRID = { left: 0, top: HERO_RULE + RULE } as const;

/** Centred in the band, 8 px below its top on this canvas (10 on the flanked faces). */
const PIT_LIMITER = { width: 272, height: ds.indicator.pitLimiter.height, topInset: 8 } as const;

const slots = grid(GRID, COLS, ROWS, SLOT, RULE);

export const layout600x686: Layout = {
  folder: 'OpenDash slots 600x686',
  description: layoutDescription('rect', WIDTH, HEIGHT, slots.length),
  width: WIDTH,
  height: HEIGHT,
  shape: 'rect',
  background: ds.color.surface.base,
  slotSize: { ...SLOT },
  slots,
  hero: {
    rev: { kind: 'revBar', left: REV_BAR_MARGIN, top: (HEADER_HEIGHT - ds.shiftLights.height) / 2, width: WIDTH - 2 * REV_BAR_MARGIN, height: ds.shiftLights.height, gap: ds.space[1] },
    gear: { rect: rect(0, HERO_TOP, WIDTH, HERO_HEIGHT) },
    pitLimiter: rect((WIDTH - PIT_LIMITER.width) / 2, HERO_TOP + PIT_LIMITER.topInset, PIT_LIMITER.width, PIT_LIMITER.height),
    flags: { kind: 'flagStrip', rect: rect(0, HEIGHT - FLAG_HEIGHT, WIDTH, FLAG_HEIGHT) },
  },
  rules: [
    { name: 'rule.header', rect: rect(0, HEADER_HEIGHT, WIDTH, RULE) },
    { name: 'rule.hero', rect: rect(0, HERO_RULE, WIDTH, RULE) },
    ...namedRects('rule.grid.', gridRules(GRID, COLS, ROWS, SLOT, RULE)),
  ],
};
