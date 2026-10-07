/**
 * The modules of zones B and C in the car's register, as the photograph of the 992's Race 1 page on
 * the canvas draws them (RefRace1). There are four kinds of container on that page, and every page of
 * the catalogue is built from them:
 *
 *   - the zone itself, outlined in the panel grey on the black ground (`body.ts` draws the outline);
 *   - its title alone, a full-width grey cell with a border and the title in white (`header`);
 *   - a line: the label in a grey cell with a border at the left, and the value beside it with no
 *     cell at all, right-aligned on the black ground;
 *   - a value alone, on the black ground with no border, centred in the room it has.
 *
 * One label size and one value size for the whole zone, 18 and 28 px in Barlow 500, the largest pair
 * at which every label of the catalogue, in its short form where it has one (`SHORT`), fits its cell
 * beside the widest value of its page in the narrower of the two zones. A label is never set smaller
 * than the rest: where it does not fit it is written short, "Sess best" for "Session best", and the
 * short form is what is drawn and measured.
 *
 * The modules keep their fields, their bindings, their `widest` declarations and their order. A page
 * with more lines than its height holds sheds them in that order, least important first, as the stack
 * does; whatever is not a line, a gauge, a bar or a drawing, goes under the lines on the black ground.
 *
 * Opponents is drawn here in full, because it lays its two cars out itself rather than through
 * `fieldsRow`: `Ahead · P3` against the gap, the driver's name against the last lap or the number,
 * and the same for the car behind, from the same pieces in the same order.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { measureText } from '../../design/advances.ts';
import { rect } from '../../design/geometry.ts';
import { boxSlack, MINUS } from '../../design/metrics.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { fld, pageKeeps, type ModuleContext } from '../../modules/module.ts';
import { archetypeFor, keepsAt } from '../../modules/shedding.ts';
import type { Density } from '../../second/density.ts';
import { field, fieldWidth, scaleFields, type FieldSpec } from '../../second/field.ts';
import { zoneFrameMetrics } from '../../second/header.ts';
import type { StackRow } from '../../second/layout.ts';
import { shapeOf } from '../../second/shape.ts';
import { nameText } from '../../second/table.ts';
import { CHARS, carLastLap, carNumber, carRelativeGap, listNeighbour, positionLabelled } from '../../second/values.ts';
import { ds } from '../../tokens.ts';
import type { ModuleRegister } from '../drawing.ts';
import { carColour, INSET_RADIUS } from './register.ts';

const { concat, str } = ncalc;

/**
 * The zone's one pair of sizes and its cells. A label cell is as tall as its label's line box with
 * four pixels round it, a line is that cell and six pixels under it, the cell's border is 2 px and its
 * radius 5, and the value stands 10 px from the zone's right edge and 8 px clear of the cell.
 */
export const ZONE_TYPE = { label: 16, value: 24 } as const;
const CELL = { pad: 10, border: 2, gap: 6, valuePad: 6, line: 6 };
const CELL_HEIGHT = Math.ceil(1.2 * ZONE_TYPE.label) + 1 + 2 * 4;
const LINE = CELL_HEIGHT + CELL.line;

/**
 * The short forms the car's narrow cells write a long label in, by the label the house writes. Each
 * is what is drawn and what is measured, so `widest` and the fit checks see the string on the screen.
 */
export const SHORT: Readonly<Record<string, string>> = {
  'Session best': 'Sess best',
  'Laps completed': 'Laps done',
  'Delta to your best': 'Delta best',
  'Delta to last lap': 'Delta last',
  'vs session best': 'vs sess best',
  'vs all-time best': 'vs all best',
  'vs last lap': 'vs last',
};

const idOf = (spec: FieldSpec): string => spec.id ?? spec.name;
const labelled = (spec: FieldSpec): boolean => spec.label !== '' || spec.labelBind !== undefined;
const textOf = (spec: FieldSpec): string => SHORT[spec.label] ?? spec.label;

/** A bound label written short wherever it draws one of the long forms, which is what a binding that names its reference draws. */
const shortBind = (bind: string | undefined): string | undefined =>
  bind === undefined ? undefined : Object.entries(SHORT).reduce((expr, [long, short]) => (bind.includes(long) ? ncalc.replace(expr, long, short) : expr), bind);
/**
 * The widest label a field draws in its short forms: its declared widest, or, for a bound label that
 * writes one of the long forms, the widest short form it can write, which a declaration written for the
 * long forms does not name.
 */
function widestLabelOf(spec: FieldSpec): string {
  const own = spec.labelWidest === undefined ? textOf(spec) : (SHORT[spec.labelWidest] ?? spec.labelWidest);
  const written = Object.entries(SHORT).filter(([long]) => spec.labelBind?.includes(long)).map(([, short]) => short);
  return [own, ...written].reduce((a, b) => (measureText('BarlowMedium', b, ZONE_TYPE.label) > measureText('BarlowMedium', a, ZONE_TYPE.label) ? b : a));
}

/** The value with its label taken off, at the zone's one size. */
const valueOf = (spec: FieldSpec): FieldSpec => scaleFields([{ ...spec, label: '', labelBind: undefined, labelWidest: undefined, labelBelow: false }], ZONE_TYPE.value / spec.value.fs)[0]!;

const labelWidth = (spec: FieldSpec): number => Math.ceil(measureText('BarlowMedium', widestLabelOf(spec), ZONE_TYPE.label)) + boxSlack(ZONE_TYPE.label);

/** A grey cell with a border and a label in it, which is a line's label and, full width, a title alone. */
function cell(name: string, box: Rect, text: string, opts: { bind?: string; widest?: string; visibleBind?: string; align?: 'left' | 'center' } = {}): Item[] {
  return [
    band(`${name}.cell`, box, carColour('panel'), { border: { color: carColour('edge'), width: CELL.border }, radius: INSET_RADIUS, visibleBind: opts.visibleBind }),
    label(`${name}.label`, text, box.left + CELL.pad, box.top + (box.height - ZONE_TYPE.label) / 2, box.width - 2 * CELL.pad, {
      size: ZONE_TYPE.label,
      color: ds.color.text.primary,
      hAlign: opts.align ?? 'left',
      bind: opts.bind,
      widest: opts.widest,
      visibleBind: opts.visibleBind,
    }),
  ];
}

/** The lines that fit `count` places, shed least important first in the page's order. */
function linesThatFit(specs: readonly FieldSpec[], order: readonly string[], count: number): FieldSpec[] {
  const kept = [...specs];
  const rank = (spec: FieldSpec): number => {
    const i = order.indexOf(idOf(spec));
    return i === -1 ? order.length : i;
  };
  while (kept.length > Math.max(1, count)) kept.splice(kept.indexOf(kept.reduce((a, b) => (rank(b) >= rank(a) ? b : a))), 1);
  return kept;
}

/**
 * A value's items moved left until the rightmost of them ends at `right`. A unit is placed from the
 * value's sample rather than from its cells, so a value with a unit can reach past the width the
 * field measures; right-aligning it is about where it ends, so it is moved by where it really ends.
 */
function withinRight(items: Item[], right: number): Item[] {
  const end = Math.max(...items.map((item) => ('rect' in item ? item.rect.left + item.rect.width : -Infinity)));
  const over = Math.ceil(end - right);
  return over <= 0 ? items : items.map((item) => ('rect' in item ? { ...item, rect: { ...item.rect, left: item.rect.left - over } } : item));
}

/** Lines: the label cells as wide as the widest label of the page, the values right-aligned on the ground. */
function lines(specs: readonly FieldSpec[], area: Rect, density: Density): Item[] {
  const cellWidth = specs.some(labelled) ? Math.max(...specs.filter(labelled).map(labelWidth)) + 2 * CELL.pad : 0;
  const slot = area.height / specs.length;
  return specs.flatMap((spec, i) => {
    const middle = area.top + (i + 0.5) * slot;
    const value = valueOf(spec);
    const width = fieldWidth(value, density);
    const right = area.left + area.width - CELL.valuePad;
    const items: Item[] = labelled(spec)
      ? cell(spec.name, rect(area.left, Math.round(middle - CELL_HEIGHT / 2), cellWidth, CELL_HEIGHT), textOf(spec), {
          bind: shortBind(spec.labelBind),
          widest: spec.labelBind === undefined ? undefined : widestLabelOf(spec),
          visibleBind: spec.visibleBind,
        })
      : [];
    items.push(...withinRight(field(value, Math.max(area.left + cellWidth + CELL.gap, right - width), middle + value.value.fs / 2, density, width), right));
    return items;
  });
}

/** A value alone, centred on the ground, under its title alone where it has a label. */
function alone(spec: FieldSpec, area: Rect, density: Density): Item[] {
  const title = labelled(spec) ? CELL_HEIGHT + CELL.line : 0;
  const value = valueOf(spec);
  const width = fieldWidth(value, density);
  const room = rect(area.left, area.top + title, area.width, area.height - title);
  return [
    ...(title ? cell(spec.name, rect(area.left, area.top, area.width, CELL_HEIGHT), textOf(spec), { bind: shortBind(spec.labelBind), widest: spec.labelBind === undefined ? undefined : widestLabelOf(spec), align: 'center' }) : []),
    ...field(value, room.left + (room.width - width) / 2, room.top + (room.height + value.value.fs) / 2, density, width),
  ];
}

function readingsIn(specs: readonly FieldSpec[], order: readonly string[], area: Rect, density: Density): Item[] {
  if (specs.length === 0) return [];
  const kept = linesThatFit(specs, order, Math.floor(area.height / LINE));
  return kept.length === 1 ? alone(kept[0]!, area, density) : lines(kept, area, density);
}

/** What `fieldsRow` returned for each rank, so that the stack can read the fields back out of its rows. */
const READINGS = new WeakMap<StackRow, { specs: readonly FieldSpec[]; order: readonly string[] }>();

/**
 * A page's stack: its lines in the room the other rows leave, those rows under them on the ground.
 * A row that is not lines is kept while the lines still have one line of room, and dropped from the
 * tail after that, as the house's stack drops them.
 */
function carStack(frame: Rect, rows: readonly StackRow[], density: Density): Item[] {
  const live = rows.filter((row) => row.height > 0);
  const ranks = live.map((row) => READINGS.get(row)).filter((r): r is NonNullable<typeof r> => r !== undefined);
  const specs = ranks.flatMap((r) => r.specs);
  const order = ranks[0]?.order ?? specs.map(idOf);
  const blocks = live.filter((row) => !READINGS.has(row));
  const taken = (kept: readonly StackRow[]): number => kept.reduce((sum, row) => sum + row.height + CELL.line, 0);
  const kept = [...blocks];
  while (kept.length > 0 && frame.height - taken(kept) < (specs.length > 0 ? LINE : 0)) kept.pop();
  const room = specs.length > 0 ? frame.height - taken(kept) : 0;
  const items = readingsIn(specs, order, rect(frame.left, frame.top, frame.width, room), density);
  let y = specs.length > 0 ? frame.top + room + CELL.line : frame.top + (frame.height - taken(kept) + CELL.line) / 2;
  for (const row of kept) {
    items.push(...row.draw(Math.round(y) + row.height));
    y += row.height + CELL.line;
  }
  return items;
}

function readingsRow(specs: readonly FieldSpec[], _ctx: ModuleContext, order: readonly string[]): StackRow {
  const row: StackRow = { height: LINE, draw: () => [] };
  READINGS.set(row, { specs, order });
  return row;
}

/** The two cars, a line each for the heading against the gap and for the name against the last lap or the number. */
function opponents(ctx: ModuleContext): Item[] {
  const order = keepsAt('opponents', archetypeFor('opponents', shapeOf(ctx.frame), ctx.frame)) ?? [];
  // The name's cell is as wide as `Behind · P99`, the widest heading, and the name is cut to what it holds.
  const nameRoom = Math.ceil(measureText('BarlowMedium', 'Behind · P99', ZONE_TYPE.label));
  const chars = Math.max(4, Math.floor(nameRoom / measureText('BarlowMedium', 'M', ZONE_TYPE.label)));
  const keeps = (id: string): boolean => pageKeeps(id, { ...ctx, page: 'opponents' });
  const specs = (['ahead', 'behind'] as const).flatMap((side, i) => {
    const idx = listNeighbour(i === 0 ? -1 : 1, ctx.classOnly);
    const heading = i === 0 ? 'Ahead' : 'Behind';
    const rows: FieldSpec[] = [];
    if (keeps(`${side}.gap`)) {
      rows.push(
        fld(ctx, `${side}.gap`, `${heading} · P3`, { sample: i === 0 ? `${MINUS}1.342` : '+0.722', bind: carRelativeGap(idx), chars: CHARS.relativeGap, fs: ZONE_TYPE.value, color: i === 0 ? ds.purpose.delta.faster : ds.purpose.delta.slower }, {
          labelBind: concat(str(`${heading} · `), positionLabelled(idx)),
          labelWidest: 'Behind · P99',
        }),
      );
    }
    if (keeps(`${side}.name`)) {
      const value = keeps(`${side}.lastLap`)
        ? { sample: '1:43.234', bind: carLastLap(idx), chars: CHARS.lapTime, fs: ZONE_TYPE.value }
        : { sample: '41', bind: carNumber(idx), chars: CHARS.carNumber, fs: ZONE_TYPE.value, color: ds.color.text.secondary };
      rows.push(fld(ctx, `${side}.name`, 'Liam Byrne', value, { labelBind: nameText(idx, chars, ZONE_TYPE.label), labelWidest: 'Behind · P99' }));
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

/**
 * A module page's title alone: a full-width grey cell with a border across the zone's header row and
 * the page's name in it at the zone's label size, with room at its right for the counter the face
 * draws there. The house draws the zone letter before the title; the car does not, so neither does this.
 */
export function porscheModuleHeader(page: { id: string; name: string }, frame: Rect, body: Rect): Item[] {
  const metrics = zoneFrameMetrics('zone', 'face');
  const box = rect(frame.left + 2, frame.top + 2, frame.width - 4, body.top - frame.top - 4);
  return [
    band(`${page.id}.zone.cell`, box, carColour('panel'), { border: { color: carColour('edge'), width: CELL.border }, radius: INSET_RADIUS }),
    label(`${page.id}.zone.title`, page.name, box.left + CELL.pad, frame.top + metrics.padTop + (metrics.title - ZONE_TYPE.label) / 2, box.width / 2, {
      size: ZONE_TYPE.label,
      color: ds.color.text.primary,
    }),
  ];
}

