/**
 * The AiM register (#204): a backlit LCD whose figures are all one ink on one ground, set in the two
 * segment faces, with the unlit segments faintly visible behind every value.
 *
 * The ground and the ink are `palette.aim` in this theme's overlay, read at draw time rather than
 * when the module loads, because `themes/drawings.ts` imports this file in every process and only the
 * AiM's has those tokens. The ghost is the ink drawn at a ninth of its strength, an opacity rather
 * than a third colour, so that the backlight a later ticket offers changes the two colours and
 * nothing else.
 *
 * The faces are DSEG7 Classic for the figures, Bold for the readings and Regular in the tacho and the
 * foot, and DSEG14 Classic for the captions. Every glyph of either is the same 0.816 em cell, the
 * full stop has no advance at all, since it lights the point of the digit before it, and the colon a
 * fifth of a cell; what a run of them takes is therefore known from its characters, which is
 * `segmentWidth`.
 */
import type { Hex, Item, Rect, TextItem } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { measureText, type SegmentFace } from '../../design/advances.ts';
import { boxSlack, LINE_SPACING, BOX_SLACK } from '../../design/metrics.ts';
import { roundRect } from '../../design/geometry.ts';
import { resolveToken, TRANSPARENT } from '../../tokens.ts';

/** The family names the two faces declare, which is what WPF resolves them by. */
export const SEGMENT_FAMILY = { DSEG7Regular: 'DSEG7 Classic', DSEG7Bold: 'DSEG7 Classic', DSEG14Regular: 'DSEG14 Classic' } as const satisfies Record<SegmentFace, string>;

/** The ghost's strength, out of SimHub's hundred: the canvas draws the ink at 0x1C of 0xFF. */
export const GHOST_OPACITY = 11;

/** A colour of the LCD, refused unless it resolves to `#RRGGBB`. */
export function lcdColour(name: 'ground' | 'ink'): Hex {
  const value = resolveToken(`palette.aim.${name}`);
  if (typeof value !== 'string' || !/^#[0-9A-F]{6}$/.test(value)) throw new Error(`aim: palette.aim.${name} is not a #RRGGBB colour (${String(value)})`);
  return value as Hex;
}

/**
 * The 14-segment face's space is a fifth of a cell, about the gap its letters already leave between
 * them, so a caption of two words set with one reads as one word (`SPEEDKM/H`). Every space of a run
 * in that face is therefore drawn as two, which is close to half a cell, and measured as two.
 */
export const WORD_SPACE = '  ';

/** A run as the 14-segment face draws it, its words two spaces apart; the 7-segment faces' runs as they are. */
export const spacedWords = (face: SegmentFace, text: string): string => (face === 'DSEG14Regular' ? text.replaceAll(' ', WORD_SPACE) : text);

/** The same for a bound run, the spacing applied to whatever the expression writes. */
export const spacedWordsExpr = (face: SegmentFace, expr: Expr): Expr => (face === 'DSEG14Regular' ? ncalc.replace(expr, ' ', WORD_SPACE) : expr);

/** The width a run of a segment face takes at `fs`, with the slack WPF needs to draw its last cell whole. */
export const segmentWidth = (face: SegmentFace, text: string, fs: number): number => Math.ceil(measureText(face, spacedWords(face, text), fs)) + boxSlack(fs);

/**
 * The house's minus is U+2212, which the seven-segment face does not carry, so a signed reading would
 * be drawn with its sign in a fallback face. The segment face's own minus is the hyphen.
 */
export const segmentText = (expr: Expr): Expr => ncalc.replace(expr, '−', '-');

/** The height of a run's box: the house's line and a pixel, of which the cell's ink is the top em. */
export const segmentLine = (size: number): number => Math.ceil(LINE_SPACING * size) + BOX_SLACK;

export interface SegmentOptions {
  size: number;
  hAlign?: 'left' | 'center' | 'right';
  bind?: Expr;
  widest?: string;
  visibleBind?: Expr;
  /** The ink by default; the ghost is the ink at {@link GHOST_OPACITY}. */
  ghost?: boolean;
  color?: Hex;
}

/**
 * A run of a segment face whose ink starts at `y`. The faces' ascent is the em and their descent
 * nothing, so WPF puts the top of a cell at the top of the box; the box is the house's line and a
 * pixel taller than the cell, which the fit checks ask of every text.
 */
export function segment(name: string, face: SegmentFace, text: string, x: number, y: number, width: number, opts: SegmentOptions): TextItem {
  return withMoreBindings({
    kind: 'text',
    name,
    rect: roundRect({ left: x, top: y, width, height: segmentLine(opts.size) }),
    text: spacedWords(face, text),
    font: SEGMENT_FAMILY[face],
    fontWeight: face === 'DSEG7Bold' ? 'Bold' : 'Normal',
    fontSize: opts.size,
    textColor: opts.color ?? lcdColour('ink'),
    hAlign: opts.hAlign ?? 'left',
    vAlign: 'top',
    backgroundColor: TRANSPARENT,
    ...(opts.ghost ? { opacity: GHOST_OPACITY } : {}),
    ...(opts.widest ? { widest: spacedWords(face, opts.widest) } : {}),
  } satisfies TextItem, { Text: opts.bind === undefined ? undefined : spacedWordsExpr(face, opts.bind), Visible: opts.visibleBind });
}

/**
 * The seven-segment faces' blank cell: `!` lights no segment and is a cell wide, where the space is
 * a quarter of one. A reading of words is written with it between them, so that every letter after
 * the first word stays on the cells its ghost lays; with a space, every cell left of it sits a
 * fraction of a cell off its unlit `8` (#1029).
 */
export const BLANK_CELL = '!';

/**
 * The unlit segments of a reading: every cell of its widest string as an `8`, the points and the
 * colons where they fall, and blank where the reading is a space. What a seven-segment cell cannot
 * draw (a sign it has no glyph for, a letter) is a cell all the same and is ghosted as one, and so is
 * the {@link BLANK_CELL}.
 */
export const ghostOf = (widest: string): string => [...widest].map((ch) => (ch === '.' || ch === ':' || ch === ' ' ? ch : '8')).join('');

/**
 * A reading right-aligned against `right`, over its ghost: the widest string the binding can draw,
 * in `8`s, which is the box the reading is measured in too.
 */
export function reading(name: string, sample: string, right: number, y: number, size: number, opts: { bind?: Expr; widest: string; face?: SegmentFace; visibleBind?: Expr }): Item[] {
  const face = opts.face ?? 'DSEG7Bold';
  const ghost = ghostOf(opts.widest);
  const width = segmentWidth(face, ghost, size);
  const left = right - width;
  return [
    segment(`${name}.ghost`, face, ghost, left, y, width, { size, hAlign: 'right', ghost: true, visibleBind: opts.visibleBind }),
    segment(`${name}.value`, face, sample, left, y, width, {
      size,
      hAlign: 'right',
      bind: opts.bind === undefined ? undefined : segmentText(opts.bind),
      widest: ghost,
      visibleBind: opts.visibleBind,
    }),
  ];
}

/** A filled rectangle of the ink: a rule, a tick, a segment. */
export const inkRect = (name: string, r: Rect, visibleBind?: Expr): Item =>
  withMoreBindings({ kind: 'rect', name, rect: roundRect(r), backgroundColor: lcdColour('ink') }, { Visible: visibleBind });
