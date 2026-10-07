/**
 * The modules of zones B and C in the car's register, measured off the Race 1 render of the 992's
 * display (RefRace1 on the canvas), which is iRacing's and is not in the repository. Every page of
 * the catalogue is built from the kinds of container that render shows:
 *
 *   - the zone, a container with a grey border and nothing else, on the black ground (`body.ts`);
 *   - a title cell: grey, rounded 5, no border of its own, as tall as its row with no padding, the
 *     title in white; full width it is the zone's header and a lone value's title, half width it is
 *     one of a pair's two titles;
 *   - a line: a title cell at the left, as tall as its line, and the value bare on the black ground
 *     at its right, right-aligned. The lines fill the container's height, so their cells stack into
 *     one grey column down the left, as the render's `Oil temp` box does;
 *   - a value alone, bare on the black ground, under its title cell.
 *
 * One label size and one value size in every zone, the ticket's register: labels 23 px and values
 * 32 px, Barlow 500, which is what the render measures at face scale. A label that does not fit its
 * cell is written short (`SHORT`), never set smaller. A page whose lines are too wide for its box at
 * those sizes draws each line as a title cell over its value instead, and a page with more lines than
 * its height holds sheds them in its own order, least important first.
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
import { zoneCounterX, zoneFrameMetrics } from '../../second/header.ts';
import type { StackRow } from '../../second/layout.ts';
import { shapeOf } from '../../second/shape.ts';
import { nameText } from '../../second/table.ts';
import { CHARS, carLastLap, carNumber, carRelativeGap, listNeighbour, positionLabelled } from '../../second/values.ts';
import { ds } from '../../tokens.ts';
import type { ModuleRegister } from '../drawing.ts';
import { carColour, INSET_RADIUS } from './register.ts';

const { concat, str } = ncalc;

/** The ticket's register: labels 23 px and values 32 px, the same in every zone. */
export const ZONE_TYPE = { label: 23, value: 32 } as const;

/**
 * A line is the value's line box and two pixels; a title row is the label's. Between two cells of a
 * column there are two pixels, the render's notch; the value stands 6 px from the zone's edge and at
 * least 6 px clear of its cell, and a cell keeps 8 px either side of its label.
 */
const LINE = Math.ceil(1.2 * ZONE_TYPE.value) + 2;
const TITLE = Math.ceil(1.2 * ZONE_TYPE.label) + 2;
const CELL = { pad: 8, notch: 2, gap: 6, valuePad: 6 };

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

/** A title cell: grey, rounded 5, no border, the label in white, centred unless it says otherwise. */
function cell(name: string, box: Rect, text: string, opts: { bind?: string; widest?: string; visibleBind?: string; align?: 'left' | 'center' } = {}): Item[] {
  const inset = opts.align === 'left' ? CELL.pad : 0;
  return [
    band(`${name}.cell`, box, carColour('panel'), { radius: INSET_RADIUS, visibleBind: opts.visibleBind }),
    label(`${name}.label`, text, box.left + inset, box.top + (box.height - ZONE_TYPE.label) / 2, box.width - 2 * inset, {
      size: ZONE_TYPE.label,
      color: ds.color.text.primary,
      hAlign: opts.align ?? 'center',
      bind: opts.bind,
      widest: opts.widest,
      visibleBind: opts.visibleBind,
    }),
  ];
}

const titleOf = (spec: FieldSpec, box: Rect): Item[] =>
  cell(spec.name, box, textOf(spec), { bind: shortBind(spec.labelBind), widest: spec.labelBind === undefined ? undefined : widestLabelOf(spec), visibleBind: spec.visibleBind });

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

/** How far a value's items reach at the zone's size: its field width, or its unit's end where that is further. */
function valueExtent(spec: FieldSpec, density: Density): number {
  const value = valueOf(spec);
  const items = field(value, 0, value.value.fs, density, fieldWidth(value, density));
  return Math.max(fieldWidth(value, density), ...items.map((item) => ('rect' in item ? item.rect.left + item.rect.width : 0)));
}

/** The value bare on the ground, centred in `box`, or right-aligned in it. */
function bareValue(spec: FieldSpec, box: Rect, density: Density, align: 'right' | 'center'): Item[] {
  const value = valueOf(spec);
  const width = fieldWidth(value, density);
  const bottom = box.top + (box.height + value.value.fs) / 2;
  if (align === 'center') return withinRight(field(value, box.left + (box.width - width) / 2, bottom, density, width), box.left + box.width);
  const items = withinRight(field(value, box.left + box.width - CELL.valuePad - width, bottom, density, width), box.left + box.width - CELL.valuePad);
  // A value drawn in cells cut for its widest reading starts at the left of them, so a short reading
  // would stand away from the edge the car aligns its values on. Where nothing follows it, the value is
  // set against the right of its box instead; a unit is placed from the value's left, so it keeps it.
  if (spec.value.follower || spec.value.mark) return items;
  return items.map((item) => (item.kind === 'text' && item.name === `${spec.name}.value` ? { ...item, hAlign: 'right' as const } : item));
}

/** The width of a page's column of title cells: its widest label, short, with 8 px either side. */
const columnOf = (specs: readonly FieldSpec[]): number => (specs.some(labelled) ? Math.max(...specs.filter(labelled).map(labelWidth)) + 2 * CELL.pad : 0);

/** Whether a page's lines fit side by side: the column, the gap, and its widest value. */
const sideBySide = (specs: readonly FieldSpec[], width: number, density: Density): boolean =>
  columnOf(specs) + CELL.gap + Math.max(...specs.map((spec) => valueExtent(spec, density))) + CELL.valuePad <= width;

/** Lines filling `area`: a column of flush title cells at the left, the values bare at the right. */
function lines(specs: readonly FieldSpec[], area: Rect, density: Density): Item[] {
  const column = columnOf(specs);
  const row = area.height / specs.length;
  return specs.flatMap((spec, i) => {
    const top = Math.round(area.top + i * row);
    const bottom = Math.round(area.top + (i + 1) * row);
    const height = bottom - top - (i < specs.length - 1 ? CELL.notch : 0);
    return [
      ...(labelled(spec) ? titleOf(spec, rect(area.left, top, column, height)) : []),
      ...bareValue(spec, rect(area.left + column + CELL.gap, top, area.width - column - CELL.gap, height), density, 'right'),
    ];
  });
}

/** Each reading as a title cell over its value, for a page whose lines are too wide to stand side by side. */
function stacked(specs: readonly FieldSpec[], area: Rect, density: Density): Item[] {
  const block = area.height / specs.length;
  return specs.flatMap((spec, i) => {
    const top = Math.round(area.top + i * block);
    return [...(labelled(spec) ? titleOf(spec, rect(area.left, top, area.width, TITLE)) : []), ...bareValue(spec, rect(area.left, top + TITLE + CELL.notch, area.width, Math.round(block) - TITLE - CELL.notch), density, 'center')];
  });
}

/** Two readings as the render draws `Time Diff` and `Pred. Time`: two half-width title cells over two bare values. */
function pair(specs: readonly FieldSpec[], area: Rect, density: Density): Item[] {
  const half = (area.width - CELL.gap) / 2;
  return specs.flatMap((spec, i) => {
    const box = rect(Math.round(area.left + i * (half + CELL.gap)), area.top, Math.floor(half), area.height);
    return [...(labelled(spec) ? titleOf(spec, rect(box.left, box.top, box.width, TITLE)) : []), ...bareValue(spec, rect(box.left, box.top + TITLE + CELL.notch, box.width, box.height - TITLE - CELL.notch), density, 'center')];
  });
}

/** A value alone, bare and centred under its title cell, as the render draws `Laptime`. */
const alone = (spec: FieldSpec, area: Rect, density: Density): Item[] => (labelled(spec) ? stacked([spec], area, density) : bareValue(spec, area, density, 'center'));

/**
 * A page's readings in `area`: lines side by side where they fit across, a pair where two readings
 * do not but stand side by side as halves, and otherwise each a title cell over its value. In each,
 * as many as the height holds, shed in the page's order.
 */
function readingsIn(specs: readonly FieldSpec[], order: readonly string[], area: Rect, density: Density): Item[] {
  if (specs.length === 0) return [];
  const halves = (two: readonly FieldSpec[]): boolean => two.every((spec) => (!labelled(spec) || labelWidth(spec) <= (area.width - CELL.gap) / 2) && valueExtent(spec, density) <= (area.width - CELL.gap) / 2);
  const across = linesThatFit(specs, order, Math.floor(area.height / LINE));
  if (across.length === 1) return alone(across[0]!, area, density);
  if (sideBySide(across, area.width, density)) return lines(across, area, density);
  const two = linesThatFit(specs, order, 2);
  if (two.length === 2 && halves(two) && area.height >= TITLE + LINE) return pair(two, area, density);
  const down = linesThatFit(specs, order, Math.floor(area.height / (TITLE + LINE)));
  return down.length === 1 ? alone(down[0]!, area, density) : stacked(down, area, density);
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
  const taken = (kept: readonly StackRow[]): number => kept.reduce((sum, row) => sum + row.height + CELL.gap, 0);
  const kept = [...blocks];
  while (kept.length > 0 && frame.height - taken(kept) < (specs.length > 0 ? LINE : 0)) kept.pop();
  const room = specs.length > 0 ? frame.height - taken(kept) : 0;
  const items = readingsIn(specs, order, rect(frame.left, frame.top, frame.width, room), density);
  let y = specs.length > 0 ? frame.top + room + CELL.gap : frame.top + (frame.height - taken(kept) + CELL.gap) / 2;
  for (const row of kept) {
    items.push(...row.draw(Math.round(y) + row.height));
    y += row.height + CELL.gap;
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
 * A module page's header: a title cell across the zone's header row, grey and rounded with no border
 * of its own, as tall as the row, with the page's name centred in it at the zone's label size. The
 * face draws the counter at its right, inside the cell, so the name is centred between two margins
 * as wide as the counter's room. The house draws the zone letter before the title; the car does not.
 */
export function porscheModuleHeader(page: { id: string; name: string }, frame: Rect, body: Rect): Item[] {
  const metrics = { ...zoneFrameMetrics('zone', 'face'), size: ZONE_TYPE.label };
  const box = rect(frame.left + 2, frame.top + 2, frame.width - 4, body.top - frame.top - 4);
  const counter = frame.left + frame.width - zoneCounterX(frame, { kind: 'reserved', widest: '21 / 21' }, metrics);
  return [
    band(`${page.id}.zone.cell`, box, carColour('panel'), { radius: INSET_RADIUS }),
    label(`${page.id}.zone.title`, page.name, box.left + counter, frame.top + metrics.padTop + (metrics.title - ZONE_TYPE.label) / 2, box.width - 2 * counter, {
      size: ZONE_TYPE.label,
      color: ds.color.text.primary,
      hAlign: 'center',
    }),
  ];
}
