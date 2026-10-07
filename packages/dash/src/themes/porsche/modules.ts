/**
 * The modules of zones B and C in the car's register, drawn as the `After` component of #205 draws
 * its race page: the zone's ground is the panel grey, the labels stand in a column 164 px wide at its
 * left, padded 10 from the edge, in Barlow 500 at 23 px, and every value of the page is right-aligned
 * at 32 px, 14 px from the right edge, in ONE dark inset of radius 5 that fills the rest of the width
 * and the whole height left to the readings. The rows are spread down that height with equal space
 * round each, so a value sits beside its label wherever the page has room for fewer than the four
 * rows the car draws.
 *
 * A page with one reading draws it as the component draws `Laptime`: the label centred over one inset
 * filling the room, and the value centred in it at up to 56 px. Whatever is not a reading, a gauge, a
 * bar or a drawing, goes under the readings in an inset of its own, and a page that is one table or
 * one drawing (`INSET_PAGES`) is drawn on one inset the size of its body by `porscheModuleGround`.
 *
 * The modules keep their fields, their bindings, their `widest` declarations and their order. A page
 * with more readings than its height holds sheds them in that order, least important first, which is
 * the stack's rule, and a value too wide for the inset is drawn smaller rather than clipped.
 *
 * Opponents is the one page drawn here in full, because it lays its two cars out itself rather than
 * through `fieldsRow`: its rows are the same as every other page's, `Ahead · P3` against the gap and
 * the driver's name against the last lap or the number, from the same pieces in the same order.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { measureText } from '../../design/advances.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { fld, pageKeeps, type ModuleContext } from '../../modules/module.ts';
import { keepsAt, archetypeFor } from '../../modules/shedding.ts';
import { field, fieldWidth, scaleFields, type FieldSpec } from '../../second/field.ts';
import type { Density } from '../../second/density.ts';
import type { StackRow } from '../../second/layout.ts';
import { shapeOf } from '../../second/shape.ts';
import { nameText } from '../../second/table.ts';
import { MINUS } from '../../design/metrics.ts';
import { CHARS, carLastLap, carNumber, carRelativeGap, listNeighbour, positionLabelled } from '../../second/values.ts';
import { ds } from '../../tokens.ts';
import type { ModuleRegister } from '../drawing.ts';
import { carColour, INSET_RADIUS } from './register.ts';

const { concat, str } = ncalc;

/** The component's numbers: the label column, its padding, the sizes, the inset's padding, the gap under the readings. */
const PANEL = { column: 164, labelPad: 10, label: 23, value: 32, hero: 56, heroLabel: 23, heroHeader: 30, valuePad: 14, gap: 6 };

/** The share of the width the label column takes at most, which is the car's 164 of 315. */
const LABEL_SHARE = 0.52;

/** The pages that are one table or one drawing, drawn on a single inset rather than in rows. */
const INSET_PAGES: ReadonlySet<string> = new Set(['radar', 'track', 'inputs', 'leaderboard', 'relative', 'lapHistory', 'damage', 'trackRivals']);

/** A rank of readings, as `fieldsRow` hands it over: its fields and the page's order. */
interface Readings {
  specs: readonly FieldSpec[];
  order: readonly string[];
  density: Density;
}

/** What `fieldsRow` returned for each rank, so that the stack can read the readings back out of its rows. */
const READINGS = new WeakMap<StackRow, Readings>();

const idOf = (spec: FieldSpec): string => spec.id ?? spec.name;

const bare = (spec: FieldSpec): FieldSpec => ({ ...spec, label: '', labelBind: undefined, labelWidest: undefined, labelBelow: false });

const labelled = (spec: FieldSpec): boolean => spec.label !== '' || spec.labelBind !== undefined;

/** The value at `size`, the component's size whatever the module's own ramp asked, or the largest under it that fits `room`. */
function valueAt(spec: FieldSpec, size: number, room: number, density: Density): FieldSpec {
  const at = (fs: number): FieldSpec => scaleFields([bare(spec)], fs / spec.value.fs)[0]!;
  for (let fs = size; fs > 12; fs--) if (fieldWidth(at(fs), density) <= room) return at(fs);
  return at(12);
}

/** The height one row needs: the value's WPF line box at the component's size. */
const ROW_HEIGHT = Math.ceil(1.2 * PANEL.value) + 2;

/** The readings that fit `rows` places, shed least important first in the page's order. */
function readingsThatFit(specs: readonly FieldSpec[], order: readonly string[], rows: number): FieldSpec[] {
  const kept = [...specs];
  const rank = (spec: FieldSpec): number => {
    const i = order.indexOf(idOf(spec));
    return i === -1 ? order.length : i;
  };
  while (kept.length > Math.max(1, rows)) {
    const worst = kept.reduce((a, b) => (rank(b) >= rank(a) ? b : a));
    kept.splice(kept.indexOf(worst), 1);
  }
  return kept;
}

/**
 * The label column's width: the car's 164, at most its share of a narrower panel, and narrower still,
 * down to two fifths of the width, where the values need the room to be drawn at 32 px.
 */
function columnFor(specs: readonly FieldSpec[], width: number, density: Density): number {
  if (!specs.some(labelled)) return 0;
  const most = Math.min(PANEL.column, Math.round(LABEL_SHARE * width));
  const values = Math.max(...specs.map((spec) => fieldWidth(scaleFields([bare(spec)], PANEL.value / spec.value.fs)[0]!, density)));
  return Math.max(Math.round(0.4 * width), Math.min(most, width - values - 2 * PANEL.valuePad));
}

/** The largest size, at most the car's 23 px, at which a label fits its column. */
function labelSize(spec: FieldSpec, room: number): number {
  let size = PANEL.label;
  while (size > 12 && Math.ceil(measureText('BarlowMedium', spec.labelWidest ?? spec.label, size)) + 2 > room) size--;
  return size;
}

/** The two columns: the labels at the left, and the one inset with every value right-aligned in it. */
function list(specs: readonly FieldSpec[], area: Rect, density: Density, inset: string): Item[] {
  const column = columnFor(specs, area.width, density);
  const box = rect(area.left + column, area.top, area.width - column, area.height);
  const slot = area.height / specs.length;
  const items: Item[] = [band(inset, box, carColour('inset'), { radius: INSET_RADIUS })];
  specs.forEach((spec, i) => {
    const middle = area.top + (i + 0.5) * slot;
    if (labelled(spec)) {
      const size = labelSize(spec, column - PANEL.labelPad);
      items.push(
        label(`${spec.name}.label`, spec.label, area.left + PANEL.labelPad, middle - size / 2, column - PANEL.labelPad, {
          size,
          color: ds.color.text.label,
          bind: spec.labelBind,
          widest: spec.labelWidest,
          visibleBind: spec.visibleBind,
        }),
      );
    }
    const value = valueAt(spec, PANEL.value, box.width - 2 * PANEL.valuePad, density);
    const width = fieldWidth(value, density);
    items.push(...field(value, box.left + box.width - PANEL.valuePad - width, middle + value.value.fs / 2, density, width));
  });
  return items;
}

/** One reading, as the component draws `Laptime`: its label centred above one inset, the value centred in it. */
function hero(spec: FieldSpec, area: Rect, density: Density, inset: string): Item[] {
  const header = labelled(spec) ? PANEL.heroHeader : 0;
  const box = rect(area.left, area.top + header, area.width, area.height - header);
  const tall = Math.min(PANEL.hero, Math.floor((box.height - 4) / 1.2));
  const value = valueAt(spec, tall, box.width - 2 * PANEL.valuePad, density);
  const width = fieldWidth(value, density);
  return [
    ...(header
      ? [
          label(`${spec.name}.label`, spec.label, area.left, area.top + (header - PANEL.heroLabel) / 2, area.width, {
            size: PANEL.heroLabel,
            color: ds.color.text.label,
            hAlign: 'center',
            bind: spec.labelBind,
            widest: spec.labelWidest,
            visibleBind: spec.visibleBind,
          }),
        ]
      : []),
    band(inset, box, carColour('inset'), { radius: INSET_RADIUS }),
    ...field(value, box.left + (box.width - width) / 2, box.top + (box.height + value.value.fs) / 2, density, width),
  ];
}

/** The readings of a page in `area`, as many as its height holds. */
function readingsIn(specs: readonly FieldSpec[], order: readonly string[], area: Rect, density: Density): Item[] {
  if (specs.length === 0) return [];
  const prefix = specs[0]!.name.slice(0, specs[0]!.name.length - idOf(specs[0]!).length);
  const kept = readingsThatFit(specs, order, Math.floor(area.height / ROW_HEIGHT));
  return kept.length === 1 ? hero(kept[0]!, area, density, `${prefix}readings.inset`) : list(kept, area, density, `${prefix}readings.inset`);
}

/**
 * The stack of a page in the car's register: its readings in the room the other rows leave, those
 * rows under them each in an inset of its own. Rows that are not readings are kept while the readings
 * can still have one row of room, and dropped from the tail after that, as the house's stack drops them.
 */
function carStack(frame: Rect, rows: readonly StackRow[], density: Density): Item[] {
  const live = rows.filter((row) => row.height > 0);
  const ranks = live.map((row) => READINGS.get(row)).filter((r): r is Readings => r !== undefined);
  const specs = ranks.flatMap((r) => r.specs);
  const order = ranks[0]?.order ?? specs.map(idOf);
  const blocks = live.filter((row) => !READINGS.has(row));
  const room = (kept: readonly StackRow[]): number => frame.height - kept.reduce((sum, row) => sum + row.height + PANEL.gap, 0);
  const kept = [...blocks];
  while (kept.length > 0 && room(kept) < (specs.length > 0 ? ROW_HEIGHT : 0)) kept.pop();
  const readings = rect(frame.left, frame.top, frame.width, specs.length > 0 ? room(kept) : 0);
  const items = readingsIn(specs, order, readings, density);
  let y = specs.length > 0 ? readings.top + readings.height + PANEL.gap : frame.top + (frame.height - (kept.reduce((s, r) => s + r.height + PANEL.gap, 0) - PANEL.gap)) / 2;
  for (const row of kept) {
    const drawn = row.draw(Math.round(y) + row.height);
    const first = drawn[0];
    if (first) items.push(band(`${first.name}.inset`, rect(frame.left, Math.round(y), frame.width, row.height), carColour('inset'), { radius: INSET_RADIUS }));
    items.push(...drawn);
    y += row.height + PANEL.gap;
  }
  return items;
}

function readingsRow(specs: readonly FieldSpec[], ctx: ModuleContext, order: readonly string[]): StackRow {
  // The height is the least the readings take; the stack gives them whatever the other rows leave.
  const row: StackRow = { height: ROW_HEIGHT, draw: () => [] };
  READINGS.set(row, { specs, order, density: ctx.density });
  return row;
}

/** The two cars, one row each for the heading against the gap and for the name against the last lap or the number. */
function opponents(ctx: ModuleContext): Item[] {
  const shape = shapeOf(ctx.frame);
  const order = keepsAt('opponents', archetypeFor('opponents', shape, ctx.frame)) ?? [];
  const column = Math.min(PANEL.column, Math.round(LABEL_SHARE * ctx.frame.width)) - PANEL.labelPad;
  const chars = Math.max(4, Math.floor(column / measureText('BarlowMedium', 'M', PANEL.label)));
  const specs = (['ahead', 'behind'] as const).flatMap((side, i) => {
    const idx = listNeighbour(i === 0 ? -1 : 1, ctx.classOnly);
    const heading = i === 0 ? 'Ahead' : 'Behind';
    const colour = i === 0 ? ds.purpose.delta.faster : ds.purpose.delta.slower;
    const rows: FieldSpec[] = [];
    if (pageKeeps(`${side}.gap`, { ...ctx, page: 'opponents' })) {
      rows.push(fld(ctx, `${side}.gap`, `${heading} · P3`, { sample: i === 0 ? `${MINUS}1.342` : '+0.722', bind: carRelativeGap(idx), chars: CHARS.relativeGap, fs: PANEL.value, color: colour }, {
        labelBind: concat(str(`${heading} · `), positionLabelled(idx)),
        labelWidest: `${heading} · P99`,
      }));
    }
    if (pageKeeps(`${side}.name`, { ...ctx, page: 'opponents' })) {
      const lap = pageKeeps(`${side}.lastLap`, { ...ctx, page: 'opponents' });
      const value = lap
        ? { sample: '1:43.234', bind: carLastLap(idx), chars: CHARS.lapTime, fs: PANEL.value }
        : { sample: '41', bind: carNumber(idx), chars: CHARS.carNumber, fs: PANEL.value, color: ds.color.text.secondary };
      rows.push(fld(ctx, `${side}.name`, 'Liam Byrne', value, { labelBind: nameText(idx, chars, PANEL.label), labelWidest: 'M'.repeat(chars) }));
    }
    return rows;
  });
  return readingsIn(specs, order.length > 0 ? order : specs.map(idOf), ctx.frame, ctx.density);
}

export const porscheModules: ModuleRegister = {
  fieldsRow: readingsRow,
  stack: carStack,
  page: (id, ctx) => (id === 'opponents' ? opponents(ctx) : undefined),
};

/** The inset a table or a drawing page sits on, as large as the module's body. */
export const porscheModuleGround = (page: string, body: Rect, prefix: string): Item[] =>
  INSET_PAGES.has(page) ? [band(`${prefix}inset`, body, carColour('inset'), { radius: INSET_RADIUS })] : [];
