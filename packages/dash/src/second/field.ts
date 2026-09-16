/**
 * A field: a label with a value under it, which is what every second-screen module is built from.
 *
 * Fields in a row share their bottom edge, not their top, because a row mixes sizes (a 116 px
 * fuel level beside a 46 px time) and the design aligns them on the baseline of the largest. So a
 * field is placed by its bottom and reports the height it needs above that line.
 *
 * Width is measured, never guessed: the label through the font's advance table and the value
 * through its monospace cells, so `fieldRow` can lay fields out without a renderer and a test can
 * prove the row fits its module.
 */
import type { Hex, Item, Monospace, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { SPECIAL_CHARS, canvasBaseline, canvasYForBaseline, cells, monoWidth, textBox, type Chars, type DataWeight } from '../design/metrics.ts';
import { denominator } from '../elements/denominator.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { ds } from '../tokens.ts';
import { densityOf, nextOnRamp, type Density, type DensitySpec } from './density.ts';
import { rank } from './rank.ts';

/** A small text that follows a value on its baseline: a unit ("L", "km/h") or a denominator ("/ 24"). */
export interface Follower {
  text: string;
  /**
   * Which of the two the canvas draws. A unit is a 13 px label six pixels after the value; a
   * denominator is a numeral at a proportion of the value, eight pixels after it. Units are the
   * common case and the default.
   */
  kind?: 'unit' | 'denominator';
  /**
   * The gap this follower takes, where the canvas asks for one of its own: the delta's reference
   * caption sits ten pixels after its value where a unit sits six. Defaults to the kind's.
   */
  gap?: number;
  /** The size it is drawn at, for a caption that is neither a unit nor a proportion of the value. */
  size?: number;
  bind?: Expr;
  color?: Hex;
  visibleBind?: Expr;
}

export interface FieldValue {
  /** Design-time text, and what the field is drawn with in DashStudio. */
  sample: string;
  bind?: Expr;
  /** Character budget: how wide the value can get. */
  chars: Chars;
  /** Font size; a density size, e.g. `d.big`. */
  fs: number;
  color?: Hex;
  colorBind?: Expr;
  weight?: DataWeight;
  follower?: Follower;
}

export interface FieldSpec {
  /** Item name prefix, unique within the screen. */
  name: string;
  /**
   * What the field is called in its page's shedding order, which is the name without the screen's
   * prefix. `modules/shedding.ts` is the table it is looked up in.
   */
  id?: string;
  /** Label text. Empty draws no label, which is how a bare value joins a row of fields. */
  label: string;
  labelBind?: Expr;
  /** The longest label the binding can produce; what the field is measured by. Defaults to `label`. */
  labelWidest?: string;
  /**
   * Draws the label under the value rather than above it, which is how the catalogue captions a
   * delta in a narrow zone: the number, then two pixels, then "vs session best". The field is still
   * placed by its bottom edge, and with the label below that edge is the label's.
   */
  labelBelow?: boolean;
  value: FieldValue;
  visibleBind?: Expr;
}

/**
 * Gap between a value and a label drawn under it. Two pixels on the catalogue, which is off the
 * `space` scale, so the literal stays here with the canvas as its citation. A caption under a
 * number is not the label above the next one: the pair reads as one thing and is set tight.
 */
export const LABEL_BELOW_GAP = 2;

/**
 * Gap between a value and the unit after it. Six pixels on the canvas, which is off the `space`
 * scale (it goes 4 then 8), so the literal stays here with the canvas as its citation.
 */
export const UNIT_GAP = 6;

/** Gap between a value and the denominator after it, which the canvas draws wider than a unit's. */
export const DENOMINATOR_GAP = ds.space[2];

/**
 * The size a denominator is drawn at beside a value: the canvas scales it with the value rather
 * than fixing it at the density's small label. 32 beside 46, 44 beside 64, 23 beside 34 and 53
 * beside 76, which is 0.7 of the value floored at each of them.
 */
export const denominatorSize = (valueFs: number): number => Math.floor(0.7 * valueFs);

/** Gap a follower of this kind takes between itself and the value it follows. */
export const followerGap = (follower: Follower): number => follower.gap ?? (follower.kind === 'denominator' ? DENOMINATOR_GAP : UNIT_GAP);

/** The size a follower is drawn at: its own, the value's proportion, or the density's small label. */
export const followerSize = (follower: Follower, d: DensitySpec, valueFs: number): number =>
  follower.size ?? (follower.kind === 'denominator' ? denominatorSize(valueFs) : d.labelSm);

/**
 * Width of a field's follower. A unit is a proportional label, and an unbound one is drawn
 * upper-cased (the label element does that), so it is measured upper-cased too: "s" and "S" are not
 * the same width. A denominator is a numeral, so it is measured in the monospace cells its size
 * cuts, which is wider than its advances and never clips.
 */
export function followerWidth(follower: Follower, d: DensitySpec, valueFs: number): number {
  const fs = followerSize(follower, d, valueFs);
  if (follower.kind === 'denominator') {
    const mono = cells('SemiBold', fs);
    return monoWidth(mono, charsOfText(follower.text, mono));
  }
  const drawn = follower.bind ? follower.text : follower.text.toUpperCase();
  return Math.ceil(measureText('BarlowMedium', drawn, fs)) + 1;
}

/** The cells a literal string takes: `.,:` get the narrow cell and everything else the wide one. */
export function charsOfText(text: string, mono: Monospace): Chars {
  const specials = [...text].filter((c) => (mono.specialChars ?? SPECIAL_CHARS).includes(c)).length;
  return { digits: text.length - specials, specials };
}

export function valueWidth(spec: FieldSpec, d: DensitySpec): number {
  const mono = cells(spec.value.weight ?? 'SemiBold', spec.value.fs);
  // The budget is what the binding can grow to; the sample is what DashStudio draws today. The box
  // takes whichever is wider, so a sample longer than its budget is never clipped in the editor.
  const width = Math.max(monoWidth(mono, spec.value.chars), monoWidth(mono, charsOfText(spec.value.sample, mono)));
  const follower = spec.value.follower;
  if (!follower) return width;
  return width + followerGap(follower) + followerWidth(follower, d, spec.value.fs);
}

/** Width a field needs: the wider of its label and its value. */
export function fieldWidth(spec: FieldSpec, density: Density): number {
  const d = densityOf(density);
  const text = spec.labelWidest ?? spec.label;
  const labelW = text === '' ? 0 : measureText('BarlowMedium', text.toUpperCase(), d.label);
  return Math.ceil(Math.max(labelW, valueWidth(spec, d)));
}

/**
 * Height a field needs above its bottom edge: the label line, the gap, the value line, and the
 * tail the value's line box hangs below it.
 *
 * That tail is the part this used to omit. A WPF line box runs about a fifth of the font size
 * below the baseline row it sits on, so a row declaring `label + gap + fs` really draws a tenth of
 * `fs` further down than it said. On a companion page nobody noticed, because the box is 336 px
 * tall and the slack absorbs it. On a 237 by 160 zone of the nano it is what put a lap time
 * twenty-two pixels past the bottom edge.
 */
export function fieldHeight(spec: FieldSpec, density: Density): number {
  const d = densityOf(density);
  const hasLabel = spec.label !== '' || spec.labelBind !== undefined;
  const labelPart = hasLabel ? d.label + (spec.labelBelow ? LABEL_BELOW_GAP : d.fieldGap) : 0;
  return labelPart + spec.value.fs + fieldTail(spec, density);
}

/**
 * How far a field's last line hangs below the bottom edge it is placed on.
 *
 * The same tail `fieldHeight` counts, named on its own because a stack has to reserve it twice
 * over: a block is centred, so the room it may take is its box less this at each end. Two pixels
 * covered it while every value was a ramp size in a box with slack; a value that has grown into its
 * box is exactly where a constant stops covering it.
 *
 * The last line is the label where the label is below, so a caption's small tail is what is
 * reserved rather than the tail of the number above it.
 */
export function fieldTail(spec: FieldSpec, density: Density): number {
  const hasLabel = spec.label !== '' || spec.labelBind !== undefined;
  const fs = spec.labelBelow && hasLabel ? densityOf(density).label : spec.value.fs;
  const box = textBox(0, fs);
  return Math.max(0, box.top + box.height - fs);
}

/** The deepest tail of a set of fields. */
export const fieldsTail = (specs: readonly FieldSpec[], density: Density): number =>
  specs.reduce((tail, spec) => Math.max(tail, fieldTail(spec, density)), 0);

/** Tallest of a set of fields, which is the height of the row they sit in. */
export const rowHeight = (specs: readonly FieldSpec[], density: Density): number =>
  specs.reduce((h, spec) => Math.max(h, fieldHeight(spec, density)), 0);

/**
 * One field, its value's line box bottom on `bottom`. `maxWidth` caps the value's box, which
 * matters at the right edge of a module where the box would otherwise leave the screen.
 *
 * `leftAt` is the rank's: when the row closes over a field the sim does not publish, every item of
 * every field left has to move, and the unit after a value has to move with its own offset kept.
 */
export function field(spec: FieldSpec, x: number, bottom: number, density: Density, maxWidth?: number, leftAt?: (dx?: number) => Expr | undefined): Item[] {
  const d = densityOf(density);
  const items: Item[] = [];
  const hasLabel = spec.label !== '' || spec.labelBind !== undefined;
  const below = hasLabel && spec.labelBelow === true;
  const valueY = bottom - spec.value.fs - (below ? d.label + LABEL_BELOW_GAP : 0);
  const width = maxWidth ?? fieldWidth(spec, density);
  if (hasLabel) {
    items.push(
      label(`${spec.name}.label`, spec.label, x, below ? bottom - d.label : valueY - d.fieldGap - d.label, width, {
        size: d.label,
        bind: spec.labelBind,
        visibleBind: spec.visibleBind,
        leftBind: leftAt?.(),
      }),
    );
  }
  const mono = cells(spec.value.weight ?? 'SemiBold', spec.value.fs);
  items.push(
    numeral(`${spec.name}.value`, spec.value.sample, x, valueY, spec.value.fs, spec.value.chars, {
      weight: spec.value.weight,
      bind: spec.value.bind,
      color: spec.value.color,
      colorBind: spec.value.colorBind,
      visibleBind: spec.visibleBind,
      maxWidth: width,
      leftBind: leftAt?.(),
    }),
  );
  const follower = spec.value.follower;
  if (follower) {
    const followerX = x + Math.max(monoWidth(mono, spec.value.chars), monoWidth(mono, charsOfText(spec.value.sample, mono))) + followerGap(follower);
    const fs = followerSize(follower, d, spec.value.fs);
    const y = canvasYForBaseline(canvasBaseline(valueY, spec.value.fs), fs);
    const box = Math.max(followerWidth(follower, d, spec.value.fs), x + width - followerX);
    const opts = {
      bind: follower.bind,
      visibleBind: follower.visibleBind ?? spec.visibleBind,
      leftBind: leftAt?.(followerX - x),
    };
    items.push(
      follower.kind === 'denominator'
        ? denominator(`${spec.name}.denominator`, follower.text, followerX, y, fs, box, opts)
        : unit(`${spec.name}.unit`, follower.text, followerX, y, box, { size: fs, color: follower.color, ...opts }),
    );
  }
  return items;
}

/**
 * A row of fields from `x`, bottom-aligned on `bottom`, `gap` apart. Returns the items and the
 * width the row took, so a caller can centre it or check it against the module's box.
 */
export function fieldRow(specs: readonly FieldSpec[], x: number, bottom: number, density: Density, gap?: number): { items: Item[]; width: number } {
  const d = densityOf(density);
  const step = gap ?? d.gapX;
  const items: Item[] = [];
  let cursor = x;
  for (const spec of specs) {
    const width = fieldWidth(spec, density);
    items.push(...field(spec, cursor, bottom, density, width));
    cursor += width + step;
  }
  return { items, width: specs.length === 0 ? 0 : cursor - x - step };
}

/**
 * How a line of fields is set in the width and the height it is given.
 *
 * `align` is the vertical one. Fields share the baseline of the largest by default, which is what
 * a row mixing a 116 px level with a 46 px time asks for; `top` shares the top edge instead, which
 * is what the companion's laps and estimate are drawn on.
 *
 * `justify` is the horizontal one, and the default is the caller's business: a zone narrow enough
 * for one column centres what is in it, and a wider one draws from its left edge.
 *
 * `columns` replaces the packed widths with that many equal cells, which is the catalogue's grid.
 */
export interface LineOptions {
  gap?: number;
  minGap?: number;
  align?: 'baseline' | 'top';
  justify?: 'left' | 'centre';
  columns?: number;
}

/** The width of one cell of an equal-column grid, gaps taken out first. */
export const cellWidth = (width: number, columns: number, gap: number): number => Math.floor((width - gap * Math.max(0, columns - 1)) / Math.max(1, columns));

/**
 * A row of fields that is made to fit `width`: the gap shrinks (never below `minGap`) before
 * anything is dropped, and the row reports whether it still overflows so a module can choose a
 * shorter set of fields for a narrow zone.
 */
export function fieldRowFitted(
  specs: readonly FieldSpec[],
  x: number,
  bottom: number,
  width: number,
  density: Density,
  opts: LineOptions = {},
): { items: Item[]; width: number; fits: boolean } {
  const d = densityOf(density);
  const preferred = opts.gap ?? d.gapX;
  const minGap = opts.minGap ?? ds.space[2];
  const natural = specs.map((spec) => fieldWidth(spec, density));
  const grid = opts.columns !== undefined && opts.columns > 0;
  const cell = grid ? cellWidth(width, opts.columns ?? 1, preferred) : 0;
  const widths = grid ? natural.map(() => cell) : natural;
  const total = widths.reduce((sum, w) => sum + w, 0);
  const gaps = Math.max(0, specs.length - 1);
  const gap = grid ? preferred : gaps === 0 ? 0 : Math.max(minGap, Math.min(preferred, Math.floor((width - total) / gaps)));
  // A top-aligned line hangs each field from the line's own top edge rather than from the baseline
  // of the largest, so a 34 px value beside a 46 px one starts where it does rather than sitting
  // on its line.
  const top = bottom - rowHeight(specs, density);
  const bottomOf = (spec: FieldSpec): number => (opts.align === 'top' ? top + fieldHeight(spec, density) : bottom);
  // Placed as a rank so that a field the sim does not publish takes its space with it rather than
  // leaving a hole in the row. `atLeast` is every field: what this row keeps was decided by the
  // page's shedding order before it got here, and the gap above is how it answers a narrow box.
  const { items } = rank(
    specs.map((spec, i) => ({
      id: spec.id ?? spec.name,
      width: widths[i] ?? 0,
      present: spec.visibleBind,
      draw: (at) => field(spec, at.x, bottomOf(spec), density, widths[i] ?? 0, at.leftAt),
    })),
    { left: x, width, gap, when: 'close', align: opts.justify === 'centre' ? 'centre' : 'left', atLeast: specs.length },
  );
  const used = gaps === 0 ? total : total + gap * gaps;
  return { items, width: used, fits: used <= width };
}

/**
 * Greedy line breaking for a row of fields. A companion page is 802 px wide and a portrait one is
 * 432, so the same row of three lap times is one line on the first and two on the second. Fields
 * keep their order; a field wider than the whole box gets a line of its own.
 *
 * Greedy and not capped by `columnsAt`, which is a question worth answering here because the shape
 * model declares a column count and this ignores it. Wiring it as a cap was tried and cost car
 * settings two of its cells at 1280 x 400 and the sectors page its three lap times: a rank capped
 * narrower than it fits is a taller rank, and a taller rank is one `rowsThatFit` takes a row off.
 * What actually stacks a narrow zone is rule 20 -- two lap times fit side by side at 34 px and do
 * not at 46, so the rank wraps to one column on its way up.
 */
export function wrapFields(specs: readonly FieldSpec[], width: number, density: Density, gap?: number): FieldSpec[][] {
  const step = gap ?? densityOf(density).gapX;
  const lines: FieldSpec[][] = [];
  let line: FieldSpec[] = [];
  let used = 0;
  for (const spec of specs) {
    const w = fieldWidth(spec, density);
    const needed = line.length === 0 ? w : used + step + w;
    if (line.length > 0 && needed > width) {
      lines.push(line);
      line = [spec];
      used = w;
    } else {
      line.push(spec);
      used = needed;
    }
  }
  if (line.length > 0) lines.push(line);
  return lines;
}

/**
 * How a rank is broken into lines.
 *
 * `wrap` is the greedy break above and the default. `perLine` gives every field a line of its own,
 * which is how a page asks for one reading under another rather than beside it. `grid` fills lines
 * of `columns` equal cells, which is what the catalogue draws a settings page with: cells that
 * share an x down the block rather than lines packed to their own widths.
 */
export type LinePlan = 'wrap' | 'perLine' | 'grid';

/** Fields in lines of `columns`, the last line short. */
const chunk = (specs: readonly FieldSpec[], columns: number): FieldSpec[][] => {
  const lines: FieldSpec[][] = [];
  for (let i = 0; i < specs.length; i += columns) lines.push(specs.slice(i, i + columns));
  return lines;
};

/**
 * The lines a rank takes, by the plan it was asked for.
 *
 * A grid falls back to the greedy wrap when a field would be wider than the cell the grid cuts:
 * a module is a function of its rectangle, so a grid that does not fit is a grid that is not drawn
 * rather than one drawn over the edge.
 */
export function planLines(specs: readonly FieldSpec[], width: number, density: Density, opts: { plan?: LinePlan; columns?: number; gap?: number } = {}): FieldSpec[][] {
  const gap = opts.gap ?? densityOf(density).gapX;
  if (opts.plan === 'perLine') return specs.map((spec) => [spec]);
  if (opts.plan === 'grid') {
    const columns = Math.max(1, opts.columns ?? 1);
    const cell = cellWidth(width, columns, gap);
    if (specs.every((spec) => fieldWidth(spec, density) <= cell)) return chunk(specs, columns);
  }
  return wrapFields(specs, width, density, gap);
}

/**
 * How uneven a wrapped block is: the longest line less the shortest, and zero for one line.
 *
 * The measure rule 20 grows against. Almost every rank on the catalogue artboard is an even grid --
 * `repeat(3, minmax(0, 1fr))` over three or nine cells -- and growing a rank that fits one line
 * into two lines of two and one is a page that looks broken for the sake of a larger digit. The
 * companion's three lap times are the case: at 64 px they are one row of three, at 75 they are two
 * and one, and at 72 they are still one row of three.
 */
export const raggedness = (lines: readonly (readonly FieldSpec[])[]): number =>
  lines.length <= 1 ? 0 : Math.max(...lines.map((l) => l.length)) - Math.min(...lines.map((l) => l.length));

/**
 * How far a set of fields may grow before the smallest step on the ramp would carry one of them
 * past the next named size. **Rule 20's third edge.**
 *
 * The minimum over the fields rather than over the largest of them, because a rank grows by one
 * factor: letting the 46 px value reach 64 while the 34 px one beside it passed 46 would be two
 * sizes doing one job, and the hierarchy the ramp exists to express is the thing that would go.
 */
export function growthCeiling(specs: readonly FieldSpec[], density: Density): number {
  let ceiling = Number.POSITIVE_INFINITY;
  for (const spec of specs) {
    const fs = spec.value.fs;
    if (fs <= 0) continue;
    ceiling = Math.min(ceiling, nextOnRamp(fs, density) / fs);
  }
  return Number.isFinite(ceiling) ? Math.max(1, ceiling) : 1;
}

/** The largest value size in a set of fields, which is what a stack steps when it grows one. */
export const leadSize = (specs: readonly FieldSpec[]): number => specs.reduce((fs, spec) => Math.max(fs, spec.value.fs), 0);

/** Height a wrapped block of fields takes: its lines and the gaps between them. */
export const fieldBlockHeight = (lines: readonly FieldSpec[][], density: Density, lineGap: number): number =>
  lines.reduce((h, line) => h + rowHeight(line, density), 0) + lineGap * Math.max(0, lines.length - 1);

/** Draws a wrapped block of fields, its last line's bottom on `bottom`. */
export function drawFieldBlock(
  lines: readonly FieldSpec[][],
  x: number,
  bottom: number,
  width: number,
  density: Density,
  opts: LineOptions & { lineGap?: number } = {},
): Item[] {
  const d = densityOf(density);
  const lineGap = opts.lineGap ?? d.gapY;
  const heights = lines.map((line) => rowHeight(line, density));
  const total = fieldBlockHeight(lines, density, lineGap);
  const top = bottom - total;
  const items: Item[] = [];
  let y = top;
  lines.forEach((line, i) => {
    const h = heights[i] ?? 0;
    items.push(...fieldRowFitted(line, x, y + h, width, density, opts).items);
    y += h + lineGap;
  });
  return items;
}

/** The same fields at a smaller size: value sizes scale, labels keep theirs. */
export const scaleFields = (specs: readonly FieldSpec[], factor: number): FieldSpec[] =>
  specs.map((spec) => ({ ...spec, value: { ...spec.value, fs: Math.max(12, Math.round(spec.value.fs * factor)) } }));

/** How far a block of fields will shrink before it gives up and draws at the smallest size. */
export const FIT_LADDER = [1, 0.85, 0.72, 0.6, 0.5] as const;

/**
 * The longest prefix of `specs` whose wrapped block fits `box`, at full size.
 *
 * Fields are listed in importance order, so dropping from the tail drops the least important
 * thing. This is the first response to a box that is too small, not the last.
 */
export function rowsThatFit(specs: readonly FieldSpec[], box: Rect, density: Density, opts: { gap?: number; lineGap?: number } = {}): FieldSpec[] {
  const lineGap = opts.lineGap ?? Math.round(densityOf(density).gapY / 2);
  const kept = [...specs];
  while (kept.length > 1) {
    const lines = wrapFields(kept, box.width, density, opts.gap);
    if (fieldBlockHeight(lines, density, lineGap) <= box.height) break;
    kept.pop();
  }
  return kept;
}

/**
 * Fields drawn inside `box`, top-aligned.
 *
 * A pit wall panel is a fixed rectangle and the fields in it are whatever the module asked for, so
 * something has to give when the two disagree. **What gives is the least important field, not the
 * size of the most important one.** That is rule 17: a page sheds its secondary rows before it
 * shrinks its numerals, and nothing is ever scaled down.
 *
 * This used to go the other way round. `FIT_LADDER` was the first response, so a box one pixel too
 * short shrank every value in it — including the one the page exists to show — while keeping a
 * field nobody would miss. A driver reading a delta at half size because a stint count would not
 * fit underneath it is the exact failure the rule is about.
 *
 * The ladder survives as the floor. A single field that does not fit its box on its own cannot be
 * shed, because then the page draws nothing; that one shrinks.
 */
export function fitFields(specs: readonly FieldSpec[], box: Rect, density: Density, opts: { gap?: number; lineGap?: number } = {}): Item[] {
  const lineGap = opts.lineGap ?? Math.round(densityOf(density).gapY / 2);
  const draw = (kept: readonly FieldSpec[], factor: number): Item[] => {
    const scaled = factor === 1 ? [...kept] : scaleFields(kept, factor);
    const lines = wrapFields(scaled, box.width, density, opts.gap);
    const height = fieldBlockHeight(lines, density, lineGap);
    return drawFieldBlock(lines, box.left, box.top + Math.min(height, box.height), box.width, density, { gap: opts.gap, lineGap });
  };

  // Shed first, at full size.
  const kept = rowsThatFit(specs, box, density, opts);
  const lines = wrapFields(kept, box.width, density, opts.gap);
  if (fieldBlockHeight(lines, density, lineGap) <= box.height) return draw(kept, 1);

  // One field left and it still does not fit: shrink it, because shedding it leaves nothing.
  for (const factor of FIT_LADDER) {
    const scaled = scaleFields(kept, factor);
    const scaledLines = wrapFields(scaled, box.width, density, opts.gap);
    if (fieldBlockHeight(scaledLines, density, lineGap) <= box.height || factor === FIT_LADDER[FIT_LADDER.length - 1]) {
      return draw(kept, factor);
    }
  }
  return [];
}

/**
 * The longest prefix of `specs` that fits `width` on one line. Fields are listed in importance
 * order, so a row that cannot hold everything drops its tail rather than running off the edge.
 */
export function fieldsThatFit(specs: readonly FieldSpec[], width: number, density: Density, gap?: number): FieldSpec[] {
  const kept = [...specs];
  while (kept.length > 1) {
    const step = gap ?? densityOf(density).gapX;
    const total = kept.reduce((sum, spec) => sum + fieldWidth(spec, density), 0) + ds.space[2] * (kept.length - 1);
    if (total <= width) break;
    kept.pop();
    void step;
  }
  return kept;
}
