/**
 * The BMW register (#203): the colours the Bosch display adds, and the boxes every reading on it
 * stands in, each the canvas's CSS box drawn in SimHub's items.
 *
 * The colours are `palette.bmw` in this theme's overlay and are read at draw time rather than when the
 * module loads, because `themes/drawings.ts` imports this file in every process and only the BMW's
 * has those tokens.
 *
 * The canvas draws three kinds of box, all square, all with a shadow down and to the right:
 *
 *   - the outlined box, 2 px of grey on the glass, in two writings: the capitals (`BRAKE BAL`, `FUEL`,
 *     `POS`), a bold label along the top and the value at the right, and the sentence (`Time Diff`,
 *     `Lap Time`), a medium label on a line of 1.4 and the value at the right or centred;
 *   - the filled box, the tiles, the pills, the tyres' cells and the alarms, a solid colour lit from
 *     above: the canvas's gradient from a tenth of white at the top to three tenths of black at the
 *     foot, which SimHub draws no gradient for, is laid over it as strips of the same two inks.
 *
 * WPF draws no blur, so a shadow is a solid rectangle of near-black under the box, offset by `SHADOW`;
 * a box is drawn that much smaller than the room it is given, so that its shadow stays inside the room.
 * Positions follow the canvas's line boxes: a run on a line of `lh` has its baseline where the browser
 * puts it, half the leading over a 1.2 em content box.
 */
import type { Hex, Item, Rect } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { measureText } from '../../design/advances.ts';
import { rect } from '../../design/geometry.ts';
import { boxSlack } from '../../design/metrics.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { resolveToken } from '../../tokens.ts';
import { SHADOW } from './geometry.ts';

export type BmwColour =
  | 'face' | 'ground' | 'ink' | 'tileInk' | 'edge' | 'shadow' | 'off' | 'green' | 'yellow' | 'red' | 'magenta'
  | 'tc' | 'abs' | 'map' | 'arb' | 'lit' | 'litEdge' | 'pillDim' | 'pillInk' | 'head' | 'alarm';

/** A colour this theme adds under `palette.bmw`, refused unless it resolves to `#RRGGBB`. */
export function bmwColour(name: BmwColour): Hex {
  const value = resolveToken(`palette.bmw.${name}`);
  if (typeof value !== 'string' || !/^#[0-9A-F]{6}$/.test(value)) throw new Error(`bmw: palette.bmw.${name} is not a #RRGGBB colour (${String(value)})`);
  return value as Hex;
}

/** An outlined box's border. */
export const EDGE = 2;
/** Where a filled box's shadow falls: the canvas's `3px 5px`, a step further than an outlined box's. */
export const FILLED_SHADOW = { x: 3, y: 5 } as const;

/** The width a run of Barlow takes, with the slack WPF needs to draw its last glyph whole. */
export const runWidth = (text: string, size: number, bold = false): number => Math.ceil(measureText(bold ? 'BarlowBold' : 'BarlowMedium', text, size)) + boxSlack(size);

/** The canvas `y` (`label`'s line box of 1) of a run of `size` on a line of `lh` whose top is `top`. */
export const lineY = (top: number, size: number, lh = 1.2): number => top + ((lh - 1) / 2) * size;

/** A line of `size` centred on the height of `box`. */
export const centredY = (box: Rect, size: number, lh = 1.2): number => lineY(box.top + (box.height - size * lh) / 2, size, lh);

/** The largest size not over `wanted`, nor under 8, at which every one of `texts` fits `room`. */
export function fitSize(wanted: number, room: number, texts: readonly string[], bold = false): number {
  let size = Math.max(8, Math.floor(wanted));
  while (size > 8 && texts.some((t) => runWidth(t, size, bold) > room)) size--;
  return size;
}

/** One reading a box shows: the design-time sample, its binding and the widest the binding can draw. */
export interface Reading {
  sample: string;
  bind?: Expr;
  widest: string;
  /** The value's ink bound, as the time difference's green and red. */
  colourBind?: Expr;
}

/**
 * The canvas's gradient over a filled box: `linear-gradient(180deg, rgba(255,255,255,.10), rgba(0,0,0,.30))`
 * as strips, each the gradient's colour at its middle, interpolated as the browser does, premultiplied.
 */
export function gradient(name: string, r: Rect, visible?: Expr): Item[] {
  const count = Math.max(3, Math.min(8, Math.round(r.height / 8)));
  const hex = (v: number): string => Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0').toUpperCase();
  return Array.from({ length: count }, (_, i) => {
    const top = Math.round(r.top + (i * r.height) / count);
    const bottom = Math.round(r.top + ((i + 1) * r.height) / count);
    const t = (i + 0.5) / count;
    const alpha = 0.1 + 0.2 * t;
    const white = (255 * 0.1 * (1 - t)) / alpha;
    const colour = `#${hex(alpha * 255)}${hex(white)}${hex(white)}${hex(white)}` as Hex;
    return band(`${name}.light${i}`, rect(r.left, top, r.width, bottom - top), colour, { visibleBind: visible });
  });
}

/** A shadow under `r` at `offset`, cut to `within` where it would leave it. */
export function shadow(name: string, r: Rect, offset: { x: number; y: number }, within?: Rect, visible?: Expr): Item[] {
  let left = r.left + offset.x;
  let top = r.top + offset.y;
  let right = left + r.width;
  let bottom = top + r.height;
  if (within) {
    left = Math.max(left, within.left);
    top = Math.max(top, within.top);
    right = Math.min(right, within.left + within.width);
    bottom = Math.min(bottom, within.top + within.height);
  }
  return right > left && bottom > top ? [band(`${name}.shadow`, rect(left, top, right - left, bottom - top), bmwColour('shadow'), { visibleBind: visible })] : [];
}

/** A room less the shadow its box casts at `offset`. */
export const shrunk = (room: Rect, offset: { x: number; y: number } = SHADOW): Rect => rect(room.left, room.top, room.width - offset.x, room.height - offset.y);

export interface BoxStyle {
  /** A filled box's colour; an outlined box when absent. */
  fill?: Hex;
  /** The fill bound, for a box that changes colour with a state; the static `fill` is its design-time colour. */
  fillBind?: Expr;
  visible?: Expr;
  /** The text's colour where it is not the one the fill implies, as on an unlit tile. */
  ink?: Hex;
  /** A filled box's top edge, the tyres' cells' 3 px of a lighter green. */
  topEdge?: { colour: Hex; width: number };
  /**
   * Where the shadow may fall. A box given one fills its room and lets its shadow out into the gap,
   * cut to this rectangle, as the cells of a grid do; a box without one keeps its shadow in its room.
   */
  within?: Rect;
  /** A filled outlined box, the alarm on the FUEL box: the fill to the border and no light over it. */
  flat?: boolean;
}

/**
 * The box in `room`, its shadow kept inside it: the shadow, the box and, filled, its light; and the
 * rectangle inside the border, or under a filled box's top edge, that its text is laid in.
 */
export function box(name: string, room: Rect, style: BoxStyle = {}): { items: Item[]; inner: Rect } {
  const lit = style.fill !== undefined && !style.flat;
  const offset = lit ? FILLED_SHADOW : SHADOW;
  const r = style.within ? room : shrunk(room, offset);
  const under = shadow(name, r, offset, style.within, style.visible);
  if (style.fill && style.flat) {
    const face = withMoreBindings(band(`${name}.box`, r, style.fill, { visibleBind: style.visible }), { BackgroundColor: style.fillBind });
    return { items: [...under, face], inner: rect(r.left + EDGE, r.top + EDGE, r.width - 2 * EDGE, r.height - 2 * EDGE) };
  }
  if (!style.fill) {
    // The canvas's outlined box is see-through on the glass; here it is the glass, so that it hides the
    // shadow under it and only the offset shows.
    const face = band(`${name}.box`, r, bmwColour('ground'), { border: { color: bmwColour('edge'), width: EDGE }, visibleBind: style.visible });
    return { items: [...under, face], inner: rect(r.left + EDGE, r.top + EDGE, r.width - 2 * EDGE, r.height - 2 * EDGE) };
  }
  const face = withMoreBindings(band(`${name}.box`, r, style.fill, { visibleBind: style.visible }), { BackgroundColor: style.fillBind });
  const edge = style.topEdge ? [band(`${name}.edge`, rect(r.left, r.top, r.width, style.topEdge.width), style.topEdge.colour, { visibleBind: style.visible })] : [];
  const skip = style.topEdge?.width ?? 0;
  return {
    items: [...under, face, ...gradient(name, rect(r.left, r.top + skip, r.width, r.height - skip), style.visible), ...edge],
    inner: rect(r.left, r.top + skip, r.width, r.height - skip),
  };
}

/** The ink a box's text takes: white on a filled box, the display's grey on an outlined one. */
export const inkOf = (style: BoxStyle): Hex => style.ink ?? (style.fill ? bmwColour('tileInk') : bmwColour('ink'));

/** How a box writes its label and its value: the canvas's two writings of an outlined box. */
export interface Writing {
  /** The label's size and weight, and the line it stands on. */
  label: number;
  bold: boolean;
  lh: number;
  /** The box's padding: top, sides and bottom. */
  pad: { top: number; x: number; bottom: number };
  value: number;
  /** The value's line, centred in what the label leaves. */
  valueLh: number;
  align: 'right' | 'center';
}

/** The capitals: `padding: 2px 10px 4px`, a bold label on its normal line, the value at the right. */
export const caps = (label: number, value: number, pad = { top: 2, x: 10, bottom: 4 }, valueLh = 1.2): Writing => ({ label, bold: true, lh: 1.2, pad, value, valueLh, align: 'right' });

/** The sentence: `padding: 0 10px 4px`, a medium label on a line of 1.4, the value at the right or centred. */
export const sentence = (label: number, value: number, align: 'right' | 'center', bottom = 4): Writing => ({ label, bold: false, lh: 1.4, pad: { top: 0, x: 10, bottom }, value, valueLh: 1.2, align });

/** The writing made to fit `inner`: the value, then the label, smaller until each holds its widest. */
export function fitted(w: Writing, inner: Rect, title: string, widest: string): Writing {
  const across = inner.width - 2 * w.pad.x;
  const label = fitSize(w.label, across, [title], w.bold);
  const room = inner.height - w.pad.top - w.pad.bottom - label * w.lh;
  const value = Math.min(fitSize(w.value, across, [widest]), Math.max(8, Math.floor(room / Math.max(w.valueLh, 1.1))));
  return { ...w, label, value };
}

/**
 * One reading in its box, the canvas's way: the label along the top, centred, and the value in the
 * room under it, centred on its height and set at the right or centred across.
 */
export function readingBox(name: string, room: Rect, title: string, reading: Reading, writing: Writing, style: BoxStyle = {}): Item {
  const { items, inner } = box(name, room, style);
  const w = fitted(writing, inner, title, reading.widest);
  const ink = inkOf(style);
  const left = inner.left + w.pad.x;
  const across = inner.width - 2 * w.pad.x;
  const labelTop = inner.top + w.pad.top;
  const areaTop = labelTop + w.label * w.lh;
  const area = rect(inner.left, areaTop, inner.width, inner.top + inner.height - w.pad.bottom - areaTop);
  const children: Item[] = [
    ...items,
    label(`${name}.name`, title, left, lineY(labelTop, w.label, w.lh), across, { size: w.label, weight: w.bold ? 'Bold' : 'Medium', color: ink, hAlign: 'center' }),
    label(`${name}.value`, reading.sample, left, centredY(area, w.value, w.valueLh), across, {
      size: w.value,
      color: ink,
      colorBind: reading.colourBind,
      hAlign: w.align,
      bind: reading.bind,
      widest: reading.widest,
    }),
  ];
  return withMoreBindings({ kind: 'layer', name, children }, { Visible: style.visible });
}

/**
 * A filled box with its runs stacked and centred on both axes, as the tiles, the tyres' cells and a
 * change notification are: each run its size, weight and line, the column centred on the box's height.
 */
export interface Run {
  id: string;
  text: string;
  size: number;
  bold?: boolean;
  lh: number;
  bind?: Expr;
  widest?: string;
}

export function stackedRuns(name: string, inner: Rect, runs: readonly Run[], ink: Hex, visible?: Expr): Item[] {
  const height = runs.reduce((sum, r) => sum + r.size * r.lh, 0);
  let top = inner.top + (inner.height - height) / 2;
  return runs.map((r) => {
    const item = label(`${name}.${r.id}`, r.text, inner.left + 2, lineY(top, r.size, r.lh), inner.width - 4, {
      size: r.size,
      weight: r.bold ? 'Bold' : 'Medium',
      color: ink,
      hAlign: 'center',
      bind: r.bind,
      widest: r.widest,
      visibleBind: visible,
    });
    top += r.size * r.lh;
    return item;
  });
}
