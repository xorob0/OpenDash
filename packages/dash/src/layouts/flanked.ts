/**
 * The rectangular family: a header with the rev bar over a rule, a body of two slot grids
 * flanking the hero column (each separated from it by a 1 px rule), and a flag strip along the
 * bottom edge. Slots are numbered left grid row-major, then right grid row-major. Every
 * rectangular DDU size is this builder with its own numbers from the design canvas.
 */
import type { Hex } from '../generator.ts';
import { grid, gridRules, rect, type Size } from '../design/geometry.ts';
import type { RevBarFrame } from '../components/revBar.ts';
import type { FlagStripStyle } from '../components/flagStrip.ts';
import type { GearSpeedSizes } from '../components/gearSpeed.ts';
import type { Padding } from '../design/rung.ts';
import { ds } from '../tokens.ts';
import { layoutDescription, namedRects, type Layout } from './layout.ts';

/** Thickness of every separator. */
export const RULE = 1;

export interface FlankedSpec {
  folder: string;
  width: number;
  height: number;
  revBar: RevBarFrame;
  /** y of the rule under the header; the body starts one pixel below it. */
  headerRule: number;
  /** Height of the flag strip along the bottom edge. */
  flagHeight: number;
  /** Strip dressing when not the standard labelled one (the nano's 12 px strip). */
  flagStyle?: FlagStripStyle;
  /** Each side's grid: cell size, columns and rows, and the left edge of the left grid. */
  grid: { cell: Size; cols: number; rows: number; left: number };
  heroWidth: number;
  /** Pit limiter block, centred in the hero column, `topInset` below the body top. */
  pitLimiter: { width: number; height: number; topInset: number };
  /** Gear and speed sizes when not the standard 260 / 116. */
  gearSpeedSizes?: GearSpeedSizes;
  /** Card padding when not the rung's own. */
  cardPadding?: Padding;
  background?: Hex;
}

export function flankedLayout(spec: FlankedSpec): Layout {
  const { cell, cols, rows } = spec.grid;
  const bodyTop = spec.headerRule + RULE;
  const bodyHeight = spec.height - bodyTop - spec.flagHeight;
  const gridWidth = cols * cell.width + (cols - 1) * RULE;
  const leftGrid = { left: spec.grid.left, top: bodyTop };
  const leftSeparator = leftGrid.left + gridWidth;
  const heroLeft = leftSeparator + RULE;
  const rightSeparator = heroLeft + spec.heroWidth;
  const rightGrid = { left: rightSeparator + RULE, top: bodyTop };
  const column = rect(heroLeft, bodyTop, spec.heroWidth, bodyHeight);
  const slots = [...grid(leftGrid, cols, rows, cell, RULE), ...grid(rightGrid, cols, rows, cell, RULE)];
  const pit = spec.pitLimiter;
  return {
    folder: spec.folder,
    description: layoutDescription('rect', spec.width, spec.height, slots.length),
    width: spec.width,
    height: spec.height,
    shape: 'rect',
    background: spec.background ?? ds.color.surface.base,
    slotSize: { ...cell },
    slots,
    hero: {
      rev: { kind: 'revBar', ...spec.revBar },
      gearSpeed: { kind: 'gearSpeedRow', rect: column, ...(spec.gearSpeedSizes ? { sizes: spec.gearSpeedSizes } : {}) },
      pitLimiter: rect(heroLeft + (spec.heroWidth - pit.width) / 2, bodyTop + pit.topInset, pit.width, pit.height),
      flags: { kind: 'flagStrip', rect: rect(0, spec.height - spec.flagHeight, spec.width, spec.flagHeight), ...(spec.flagStyle ? { style: spec.flagStyle } : {}) },
    },
    rules: [
      { name: 'rule.header', rect: rect(0, spec.headerRule, spec.width, RULE) },
      ...namedRects('rule.left.', gridRules(leftGrid, cols, rows, cell, RULE)),
      { name: 'rule.hero.left', rect: rect(leftSeparator, bodyTop, RULE, bodyHeight) },
      { name: 'rule.hero.right', rect: rect(rightSeparator, bodyTop, RULE, bodyHeight) },
      ...namedRects('rule.right.', gridRules(rightGrid, cols, rows, cell, RULE)),
    ],
    ...(spec.cardPadding ? { cardPadding: { ...spec.cardPadding } } : {}),
  };
}
