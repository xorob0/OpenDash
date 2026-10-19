/**
 * The modules of zones B and C in the BMW's register (#203), as the canvas draws `FUEL` and `LAPS LEFT`
 * in zone B: every reading in a box of its own, an outlined box with its shadow (`register.ts`), its
 * label in bold centred along the top and its value centred under it.
 *
 *   - The boxes stand in a grid that fills the page's frame, as many across as the widest reading
 *     allows and as many down as there are readings for, 8 px apart, each as tall as its share.
 *   - One label size and one value size per page: the reference's 16 and 34 px at face scale, made
 *     smaller in steps, down to six tenths, until every box of the grid holds its reading.
 *   - A page with more readings than the grid holds at the smallest type sheds them in its own order,
 *     least important first, as the house's does.
 *
 * The value is the house's own field with its label taken off, so a unit, a denominator, a mark and a
 * reading's colour are drawn as the house draws them. The pages that are rows of cars, and the pages
 * that are one drawing, keep the house's layout on the glass.
 */
import type { Item, Rect } from '../../generator.ts';
import { rect } from '../../design/geometry.ts';
import { label } from '../../elements/label.ts';
import type { ModuleContext } from '../../modules/module.ts';
import type { Density } from '../../second/density.ts';
import { field, fieldWidth, scaleFields, type FieldSpec } from '../../second/field.ts';
import { stack, type StackRow } from '../../second/layout.ts';
import type { ModuleRegister } from '../drawing.ts';
import { withHouseLayout } from '../moduleRegister.ts';
import { GAP, SHADOW } from './geometry.ts';
import { box, EDGE, inkOf, runWidth } from './register.ts';

/** The reference type: the canvas's zone B box, a 16 px label over a 34 px value. */
const TYPE = { label: 16, value: 34 } as const;

/** The smallest share of the reference a page is set in before it sheds a reading instead. */
const SMALLEST = 0.6;

/** Inside a box: the room either side of the text, and between the label and the value. */
const PAD = 6;
const UNDER = 2;

interface Type {
  label: number;
  value: number;
}

const idOf = (spec: FieldSpec): string => spec.id ?? spec.name;
const labelled = (spec: FieldSpec): boolean => spec.label !== '' || spec.labelBind !== undefined;
const lineOf = (size: number): number => Math.ceil(1.2 * size);

/** The value with its label taken off, at the page's value size. */
const valueOf = (spec: FieldSpec, type: Type): FieldSpec =>
  scaleFields([{ ...spec, label: '', labelBind: undefined, labelWidest: undefined, labelBelow: false }], type.value / spec.value.fs)[0]!;

/** How far a value's items reach: its field's width, or its unit's end where that is further. */
function valueExtent(spec: FieldSpec, type: Type, density: Density): number {
  const value = valueOf(spec, type);
  const width = fieldWidth(value, density);
  const items = field(value, 0, value.value.fs, density, width);
  return Math.ceil(Math.max(width, ...items.map((item) => ('rect' in item ? item.rect.left + item.rect.width : 0))));
}

const labelExtent = (spec: FieldSpec, type: Type): number => (labelled(spec) ? runWidth(spec.labelWidest ?? spec.label, type.label, true) : 0);

/** The narrowest box that holds a reading at `type`, its edges and its shadow included. */
const boxWidth = (spec: FieldSpec, type: Type, density: Density): number =>
  Math.max(labelExtent(spec, type), valueExtent(spec, type, density)) + 2 * PAD + 2 * EDGE + SHADOW.x;

/** The shortest box that holds a reading at `type`. */
const boxHeight = (type: Type): number => lineOf(type.label) + UNDER + type.value + Math.ceil(0.25 * type.value) + 2 * EDGE + SHADOW.y + 4;

interface Grid {
  type: Type;
  columns: number;
  rows: number;
}

/** The grid `specs` stand in within `area` at `type`, or undefined where they do not fit it. */
function gridAt(specs: readonly FieldSpec[], area: Rect, type: Type, density: Density): Grid | undefined {
  const widest = Math.max(...specs.map((spec) => boxWidth(spec, type, density)));
  const columns = Math.min(specs.length, Math.floor((area.width + GAP) / (widest + GAP)));
  if (columns < 1) return undefined;
  const rows = Math.ceil(specs.length / columns);
  if (rows * boxHeight(type) + (rows - 1) * GAP > area.height) return undefined;
  return { type, columns, rows };
}

/** The largest type at which `specs` fit `area`, down to the smallest the register sets. */
function fitted(specs: readonly FieldSpec[], area: Rect, density: Density, scale: number): Grid | undefined {
  for (let k = scale; k >= SMALLEST - 1e-9; k -= 0.05) {
    const grid = gridAt(specs, area, { label: Math.round(TYPE.label * k), value: Math.round(TYPE.value * k) }, density);
    if (grid) return grid;
  }
  return undefined;
}

/** The readings and the grid they stand in, shed least important first until the grid holds them. */
function readingsThatFit(specs: readonly FieldSpec[], order: readonly string[], area: Rect, density: Density, scale: number): { kept: FieldSpec[]; grid: Grid } | undefined {
  const kept = [...specs];
  const rank = (spec: FieldSpec): number => {
    const i = order.indexOf(idOf(spec));
    return i === -1 ? order.length : i;
  };
  for (;;) {
    const grid = fitted(kept, area, density, scale);
    if (grid) return { kept, grid };
    if (kept.length === 1) return undefined;
    kept.splice(kept.indexOf(kept.reduce((a, b) => (rank(b) >= rank(a) ? b : a))), 1);
  }
}

/** A value's items moved left until the rightmost of them ends at `right`. */
function withinRight(items: Item[], right: number): Item[] {
  const end = Math.max(...items.map((item) => ('rect' in item ? item.rect.left + item.rect.width : -Infinity)));
  const over = Math.ceil(end - right);
  return over <= 0 ? items : items.map((item) => ('rect' in item ? { ...item, rect: { ...item.rect, left: item.rect.left - over } } : item));
}

/** One reading in its box: the label bold along the top, the value centred under it. */
function readingBox(spec: FieldSpec, room: Rect, type: Type, density: Density): Item {
  const { items, inner } = box(`${spec.name}.frame`, room, { visible: spec.visibleBind });
  const labelH = labelled(spec) ? lineOf(type.label) + UNDER : 0;
  const free = inner.height - labelH - lineOf(type.value);
  const top = inner.top + Math.max(0, Math.floor(free / 3));
  const value = valueOf(spec, type);
  const width = fieldWidth(value, density);
  const left = inner.left + PAD;
  const across = inner.width - 2 * PAD;
  const valueTop = top + labelH + Math.max(0, Math.floor(free / 3));
  const bottom = valueTop + type.value;
  const children: Item[] = [
    ...items,
    ...(labelled(spec)
      ? [
          label(`${spec.name}.label`, spec.label, left, top, across, {
            size: type.label,
            weight: 'Bold',
            color: inkOf({}),
            hAlign: 'center',
            bind: spec.labelBind,
            widest: spec.labelWidest,
            visibleBind: spec.visibleBind,
          }),
        ]
      : []),
    ...withinRight(field(value, Math.round(left + (across - width) / 2), bottom, density, width), left + across),
  ];
  return { kind: 'layer', name: `${spec.name}.cell`, children };
}

function boxesIn(specs: readonly FieldSpec[], area: Rect, grid: Grid, density: Density): Item[] {
  const width = (area.width - (grid.columns - 1) * GAP) / grid.columns;
  const height = (area.height - (grid.rows - 1) * GAP) / grid.rows;
  return specs.map((spec, i) => {
    const column = i % grid.columns;
    const row = Math.floor(i / grid.columns);
    const left = Math.round(area.left + column * (width + GAP));
    const top = Math.round(area.top + row * (height + GAP));
    const room = rect(left, top, Math.round(area.left + column * (width + GAP) + width) - left, Math.round(area.top + row * (height + GAP) + height) - top);
    return readingBox(spec, room, grid.type, density);
  });
}

/** What `fieldsRow` returned for each rank, so that the stack can read the fields back out of its rows. */
const READINGS = new WeakMap<StackRow, { specs: readonly FieldSpec[]; order: readonly string[] }>();

function readingsRow(specs: readonly FieldSpec[], _ctx: ModuleContext, order: readonly string[]): StackRow {
  const row: StackRow = { height: boxHeight(TYPE), draw: () => [] };
  READINGS.set(row, { specs, order });
  return row;
}

/**
 * A page's stack: its readings in boxes from the top, and the rows that are not readings (a gauge, a
 * bar, a drawing) under them in the room the boxes leave, dropped from the tail where there is none,
 * as the house's stack drops them. A page with no readings is a drawing, and the house's stack is
 * what fits a drawing to its box.
 */
function bmwStack(frame: Rect, rows: readonly StackRow[], density: Density): Item[] {
  const live = rows.filter((row) => row.height > 0);
  const ranks = live.map((row) => READINGS.get(row)).filter((r): r is NonNullable<typeof r> => r !== undefined);
  const specs = ranks.flatMap((r) => r.specs);
  if (specs.length === 0) return withHouseLayout(() => stack(frame, rows, density));
  const order = ranks[0]?.order ?? specs.map(idOf);
  const others = live.filter((row) => !READINGS.has(row));
  const kept = [...others];
  const othersHeight = (): number => kept.reduce((sum, row) => sum + row.height + GAP, 0);
  const least = boxHeight({ label: Math.round(TYPE.label * SMALLEST), value: Math.round(TYPE.value * SMALLEST) });
  while (kept.length > 0 && frame.height - othersHeight() < least) kept.pop();
  const area = rect(frame.left, frame.top, frame.width, frame.height - othersHeight());
  const fit = readingsThatFit(specs, order, area, density, 1);
  if (!fit) return [];
  const items = boxesIn(fit.kept, area, fit.grid, density);
  let y = area.top + area.height + GAP;
  for (const row of kept) {
    items.push(...row.draw(Math.round(y) + row.height));
    y += row.height + GAP;
  }
  return items;
}

/** The pages that list other cars in rows, and the pages that are one drawing, which keep the house's layout. */
const HOUSE_PAGES: ReadonlySet<string> = new Set(['leaderboard', 'relative', 'opponents', 'trackRivals', 'lapHistory', 'inputs', 'radar', 'track', 'tyres', 'damage']);

export const bmwModules: ModuleRegister = { fieldsRow: readingsRow, stack: bmwStack, houseLayout: HOUSE_PAGES };
