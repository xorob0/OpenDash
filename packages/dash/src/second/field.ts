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
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { ds } from '../tokens.ts';
import { densityOf, type Density, type DensitySpec } from './density.ts';

/** A small text that follows a value on its baseline: a unit ("L", "km/h") or a denominator ("/ 24"). */
export interface Follower {
  text: string;
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
  /** Label text. Empty draws no label, which is how a bare value joins a row of fields. */
  label: string;
  labelBind?: Expr;
  /** The longest label the binding can produce; what the field is measured by. Defaults to `label`. */
  labelWidest?: string;
  value: FieldValue;
  visibleBind?: Expr;
}

/** Gap between a value and the small text that follows it. */
export const FOLLOWER_GAP = ds.space[2];

/**
 * Width of a field's value, its follower included. An unbound follower is drawn upper-cased (the
 * label element does that), so it is measured upper-cased too: "s" and "S" are not the same width.
 */
export function followerWidth(follower: Follower, d: DensitySpec): number {
  const drawn = follower.bind ? follower.text : follower.text.toUpperCase();
  return Math.ceil(measureText('BarlowMedium', drawn, d.labelSm)) + 1;
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
  return width + FOLLOWER_GAP + followerWidth(follower, d);
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
  const labelPart = spec.label === '' && spec.labelBind === undefined ? 0 : d.label + d.fieldGap;
  const box = textBox(0, spec.value.fs);
  const belowTheLine = Math.max(0, box.top + box.height - spec.value.fs);
  return labelPart + spec.value.fs + belowTheLine;
}

/** Tallest of a set of fields, which is the height of the row they sit in. */
export const rowHeight = (specs: readonly FieldSpec[], density: Density): number =>
  specs.reduce((h, spec) => Math.max(h, fieldHeight(spec, density)), 0);

/**
 * One field, its value's line box bottom on `bottom`. `maxWidth` caps the value's box, which
 * matters at the right edge of a module where the box would otherwise leave the screen.
 */
export function field(spec: FieldSpec, x: number, bottom: number, density: Density, maxWidth?: number): Item[] {
  const d = densityOf(density);
  const items: Item[] = [];
  const valueY = bottom - spec.value.fs;
  const hasLabel = spec.label !== '' || spec.labelBind !== undefined;
  const width = maxWidth ?? fieldWidth(spec, density);
  if (hasLabel) {
    items.push(
      label(`${spec.name}.label`, spec.label, x, valueY - d.fieldGap - d.label, width, {
        size: d.label,
        bind: spec.labelBind,
        visibleBind: spec.visibleBind,
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
    }),
  );
  const follower = spec.value.follower;
  if (follower) {
    const followerX = x + Math.max(monoWidth(mono, spec.value.chars), monoWidth(mono, charsOfText(spec.value.sample, mono))) + FOLLOWER_GAP;
    const y = canvasYForBaseline(canvasBaseline(valueY, spec.value.fs), d.labelSm);
    items.push(
      unit(`${spec.name}.unit`, follower.text, followerX, y, Math.max(followerWidth(follower, d), x + width - followerX), {
        size: d.labelSm,
        bind: follower.bind,
        color: follower.color,
        visibleBind: follower.visibleBind ?? spec.visibleBind,
      }),
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
  opts: { gap?: number; minGap?: number } = {},
): { items: Item[]; width: number; fits: boolean } {
  const d = densityOf(density);
  const preferred = opts.gap ?? d.gapX;
  const minGap = opts.minGap ?? ds.space[2];
  const widths = specs.map((spec) => fieldWidth(spec, density));
  const total = widths.reduce((sum, w) => sum + w, 0);
  const gaps = Math.max(0, specs.length - 1);
  const gap = gaps === 0 ? 0 : Math.max(minGap, Math.min(preferred, Math.floor((width - total) / gaps)));
  const items: Item[] = [];
  let cursor = x;
  specs.forEach((spec, i) => {
    const w = widths[i] ?? 0;
    items.push(...field(spec, cursor, bottom, density, w));
    cursor += w + gap;
  });
  const used = gaps === 0 ? total : total + gap * gaps;
  return { items, width: used, fits: used <= width };
}

/**
 * Greedy line breaking for a row of fields. A companion page is 802 px wide and a portrait one is
 * 432, so the same row of three lap times is one line on the first and two on the second. Fields
 * keep their order; a field wider than the whole box gets a line of its own.
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
  opts: { gap?: number; lineGap?: number } = {},
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
    items.push(...fieldRowFitted(line, x, y + h, width, density, { gap: opts.gap }).items);
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
