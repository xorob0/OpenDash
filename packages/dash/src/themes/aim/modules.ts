/**
 * The modules of zones B and C in the LCD's register (#204): rows of a caption and a value, nothing
 * boxed and nothing filled, as the `After` component of the canvas draws `H2OT 78` and `LAST 1:52.31`.
 *
 *   - A row is a caption in the 14-segment face at the left and the reading in the 7-segment face,
 *     bold, right-aligned against the zone's edge over its ghost, the unlit cells of the widest
 *     string its binding can draw.
 *   - One caption size and one value size per zone: at the reference 18 and 40 px on rows of 64 spaced
 *     evenly down 284, which is four rows. A zone that is shallower scales the rows and their type
 *     with it, as 1280 x 400 does; a zone that is deeper keeps them and stacks as many as fit from the
 *     top, as 1280 x 720 does; a zone too shallow for four keeps the rows and holds fewer, as 800 x 286
 *     does. A zone wide enough for two columns, as 1920 x 480's are, lays the rows in two.
 *   - The caption is written as the unit would, in capitals and short (`SHORT`), never set smaller;
 *     a unit or a denominator that follows a value in the house goes into its caption, as `FUEL  L`
 *     does in the foot. A reading whose value is too wide to stand beside its caption takes two rows,
 *     the caption over the value.
 *   - Every figure is in the ink: the LCD has one, so a reading's own colour is not drawn.
 *
 * A page with more readings than the zone has rows sheds them in its own order, least important
 * first. The pages that are rows of cars and the pages that are one drawing keep the house's layout,
 * in the house's faces, in the ink on the ground the overlay gives them, under no title: the LCD
 * has no title cell and no page name, which is how the units draw their pages.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import type { Expr } from '../../bind.ts';
import { rect } from '../../design/geometry.ts';
import { marked, unmarked } from '../../elements/mark.ts';
import type { ModuleContext } from '../../modules/module.ts';
import type { Density } from '../../second/density.ts';
import type { FieldSpec } from '../../second/field.ts';
import { stack, type StackRow } from '../../second/layout.ts';
import type { ModuleRegister } from '../drawing.ts';
import { withHouseLayout } from '../moduleRegister.ts';
import { reading, segment, segmentWidth } from './register.ts';

const { concat, iff, str, ucase } = ncalc;

/** The canvas's row: 64 tall, four of them down 284 with the rest between, an 18 px caption and a 40 px value. */
export const ROW = { height: 64, body: 284, count: 4, caption: 18, value: 40 } as const;

/** Between two rows, which is what is left of 284 after four rows of 64, shared three ways. */
const ROW_GAP = (ROW.body - ROW.count * ROW.height) / (ROW.count - 1);

/** Between a caption and its value, at the least; and between two columns. */
const CAPTION_GAP = 16;
const COLUMN_GAP = 40;

/** A column is at least this wide, which is a five-letter caption beside a lap time at the reference size. */
const COLUMN_MIN = 340;

/** A zone this shallow keeps the reference rows and holds fewer of them rather than scaling them down. */
const SCALES_DOWN_TO = 200;

/**
 * The captions the unit writes for the house's labels, which are what is drawn and what is measured.
 * A label not listed is written in capitals as it is.
 */
export const SHORT: Readonly<Record<string, string>> = {
  'Water temp': 'H2OT',
  'Water': 'H2OT',
  'Oil temp': 'OILT',
  'Oil press': 'OILP',
  'Oil pressure': 'OILP',
  'Fuel pressure': 'FUELP',
  'Voltage': 'VOLT',
  'Battery': 'VOLT',
  'Manifold pressure': 'MAP',
  'Last lap': 'LAST',
  'Last': 'LAST',
  'Best lap': 'BEST',
  'Best': 'BEST',
  'Session best': 'SBEST',
  'Your best': 'PBEST',
  'Delta': 'DELTA',
  'Position': 'POS',
  'Laps completed': 'LAPS',
  'Delta to your best': 'DELTA BEST',
  'Delta to last lap': 'DELTA LAST',
  'vs session best': 'VS SBEST',
  'vs all-time best': 'VS ABEST',
  'vs last lap': 'VS LAST',
};

const idOf = (spec: FieldSpec): string => spec.id ?? spec.name;

/** The register at a zone's size: its row, its two type sizes, how many rows and columns it holds. */
export interface Register {
  row: number;
  gap: number;
  caption: number;
  value: number;
  rows: number;
  columns: number;
  columnWidth: number;
}

export function registerFor(frame: Rect): Register {
  const columns = frame.width >= 2 * COLUMN_MIN + COLUMN_GAP ? 2 : 1;
  const columnWidth = Math.floor((frame.width - (columns - 1) * COLUMN_GAP) / columns);
  const shallow = frame.height < ROW.body && frame.height >= SCALES_DOWN_TO;
  const k = shallow ? frame.height / ROW.body : 1;
  const row = Math.floor(ROW.height * k);
  const gap = ROW_GAP * k;
  const rows = shallow ? ROW.count : Math.max(1, Math.floor((frame.height + gap) / (row + gap)));
  // The value shrinks with its row, and with a column too narrow for a lap time beside a caption.
  const narrow = Math.floor((columnWidth - CAPTION_GAP - 5 * 0.816 * ROW.caption) / (7 * 0.816));
  return { row, gap, caption: Math.round(ROW.caption * k), value: Math.min(Math.round(ROW.value * k), narrow), rows, columns, columnWidth };
}

const captionText = (label: string): string => (SHORT[label] ?? label).toUpperCase();

/** Longest first, so that `Delta` does not take the start of `Delta to your best` before that is replaced whole. */
const LONGEST_FIRST = Object.entries(SHORT).sort(([a], [b]) => b.length - a.length);

/** A bound text with each long form it can write replaced by its short one. */
const shortened = (bind: Expr): Expr => LONGEST_FIRST.reduce((expr, [long, to]) => (bind.includes(long) ? ncalc.replace(expr, long, to) : expr), bind);

/** The short forms a binding can write, which a declaration written for the long ones does not name. */
const writtenShort = (bind: Expr | undefined): string[] => LONGEST_FIRST.filter(([long]) => bind?.includes(long)).map(([, to]) => to);

const widestOf = (texts: readonly string[]): string => texts.reduce((a, b) => (b.length > a.length ? b : a), '');

/**
 * What a reading's caption says and how it is bound: its label short and in capitals, and the unit or
 * the denominator that follows its value in the house after it, a caption the house writes after the
 * value (the delta's `vs all-time best`) written short as well.
 */
function captionOf(spec: FieldSpec): { text: string; widest: string; bind?: Expr } {
  const follower = spec.value.follower;
  const label = captionText(spec.label);
  const labelWidest = widestOf([spec.labelWidest === undefined ? label : captionText(spec.labelWidest), ...writtenShort(spec.labelBind).map((t) => t.toUpperCase())]);
  const after = follower ? (SHORT[follower.text] ?? follower.text) : '';
  const afterWidest = follower ? widestOf([after, SHORT[follower.widest ?? ''] ?? follower.widest ?? '', ...writtenShort(follower.bind)]) : '';
  const join = (a: string, b: string): string => [a, b].filter((t) => t !== '').join(' ');
  const text = join(label, after);
  const widest = join(labelWidest, afterWidest);
  const labelExpr = spec.labelBind === undefined ? undefined : ucase(shortened(spec.labelBind));
  const followerExpr = follower?.bind === undefined ? undefined : shortened(follower.bind);
  if (labelExpr === undefined && followerExpr === undefined && follower?.visibleBind === undefined) return { text, widest };
  const parts: Expr[] = [];
  if (labelExpr !== undefined || label !== '') parts.push(labelExpr ?? str(label));
  const alone = parts.length === 0 ? str('') : parts[0]!;
  if (follower) parts.push(...(parts.length > 0 ? [str(' ')] : []), followerExpr ?? str(after));
  const bind = parts.length === 1 ? parts[0]! : concat(...parts);
  // A follower the house hides in some state is left off the caption in that state, as the house
  // leaves it off the value: the lap's `/ 30` is the race's length only in a race counted in laps,
  // and written in for every lap it put `LAP / 0` over an open practice (#989).
  return { text, widest, bind: follower?.visibleBind === undefined ? bind : iff(follower.visibleBind, bind, alone) };
}

/**
 * The widest string a value can draw, which is what its ghost shows and what its cell is cut to: the
 * digit positions the binding can reach, so that no dead cell stands before a reading that cannot
 * fill it, and a reading that varies in length shows every position it can reach and right-aligns
 * into them, as the units do. That is the field's `widest` where it declares one; a field that does
 * not has its sample as the short end of the range and its budget as the only statement of how far it
 * reaches, so its sample is filled out to the budget's cells.
 */
function widestValue(spec: FieldSpec): string {
  if (spec.value.widest !== undefined) return spec.value.widest;
  const cellsIn = [...spec.value.sample].filter((ch) => ch !== '.' && ch !== ':').length;
  return '8'.repeat(Math.max(0, spec.value.chars.digits - cellsIn)) + spec.value.sample;
}

const captionWidth = (spec: FieldSpec, size: number): number => {
  const { widest } = captionOf(spec);
  return widest === '' ? 0 : segmentWidth('DSEG14Regular', widest, size);
};

const valueWidth = (spec: FieldSpec, size: number): number => segmentWidth('DSEG7Bold', widestValue(spec), size);

/** How many rows a reading takes: one beside its caption, two where the two do not fit across. */
const slotsOf = (spec: FieldSpec, register: Register): number =>
  captionWidth(spec, register.caption) + CAPTION_GAP + valueWidth(spec, register.value) <= register.columnWidth ? 1 : 2;

/** The readings that fit `slots` rows, shed least important first in the page's order. */
function readingsThatFit(specs: readonly FieldSpec[], order: readonly string[], register: Register, slots: number): FieldSpec[] {
  const kept = [...specs];
  const rank = (spec: FieldSpec): number => {
    const i = order.indexOf(idOf(spec));
    return i === -1 ? order.length : i;
  };
  const taken = (): number => {
    // Per column, a two-row reading cannot start on a column's last row.
    let used = 0;
    let column = 0;
    for (const spec of kept) {
      const need = Math.min(register.rows, slotsOf(spec, register));
      if (used + need > register.rows) {
        column += 1;
        used = 0;
      }
      used += need;
    }
    return column * register.rows + used;
  };
  while (kept.length > 1 && taken() > slots) kept.splice(kept.indexOf(kept.reduce((a, b) => (rank(b) >= rank(a) ? b : a))), 1);
  return kept;
}

/** The value of a reading, right-aligned against `right` with its line box centred in `row`. */
function valueItems(spec: FieldSpec, right: number, row: Rect, size: number): Item[] {
  const y = Math.round(row.top + (row.height - size) / 2);
  const mark = spec.value.mark;
  const visible = unmarked(mark, spec.visibleBind);
  const items = reading(spec.name, spec.value.sample, right, y, size, { bind: spec.value.bind, widest: widestValue(spec), visibleBind: visible });
  if (mark) {
    // The house's mark for a state no figure can say, the `∞` of a session with no clock, in the
    // value's place; the segment faces have no glyph for it, so WPF draws it in a face of its own.
    const width = segmentWidth('DSEG14Regular', mark.text, size);
    items.push(segment(`${spec.name}.mark`, 'DSEG14Regular', mark.text, right - width, y, width, { size, hAlign: 'right', widest: mark.text, visibleBind: marked(mark, spec.visibleBind) }));
  }
  return items;
}

function captionItems(spec: FieldSpec, left: number, row: Rect, size: number): Item[] {
  const caption = captionOf(spec);
  if (caption.widest === '') return [];
  const width = segmentWidth('DSEG14Regular', caption.widest, size);
  return [
    segment(`${spec.name}.caption`, 'DSEG14Regular', caption.text, left, Math.round(row.top + (row.height - size) / 2), width, {
      size,
      bind: caption.bind,
      widest: caption.bind === undefined ? undefined : caption.widest,
      visibleBind: spec.visibleBind,
    }),
  ];
}

/** The readings laid in rows down the columns of `area`, from the top. */
function rowsIn(specs: readonly FieldSpec[], area: Rect, register: Register): Item[] {
  const items: Item[] = [];
  const rowAt = (column: number, index: number): Rect =>
    rect(area.left + column * (register.columnWidth + COLUMN_GAP), Math.round(area.top + index * (register.row + register.gap)), register.columnWidth, register.row);
  let column = 0;
  let index = 0;
  for (const spec of specs) {
    // A zone one row deep sets a reading too wide to stand beside its caption on that row all the same.
    const need = Math.min(register.rows, slotsOf(spec, register));
    if (index + need > register.rows) {
      column += 1;
      index = 0;
    }
    const first = rowAt(column, index);
    const right = first.left + first.width;
    items.push(...captionItems(spec, first.left, first, register.caption));
    items.push(...valueItems(spec, right, need === 1 ? first : rowAt(column, index + 1), register.value));
    index += need;
  }
  return items;
}

/** What `fieldsRow` returned for each rank, so that the stack can read the fields back out of its rows. */
const READINGS = new WeakMap<StackRow, { specs: readonly FieldSpec[]; order: readonly string[] }>();

function readingsRow(specs: readonly FieldSpec[], _ctx: ModuleContext, order: readonly string[]): StackRow {
  const row: StackRow = { height: ROW.height, draw: () => [] };
  READINGS.set(row, { specs, order });
  return row;
}

/**
 * A page's stack: its readings in rows from the top, and the rows that are not readings (a gauge, a
 * bar, a drawing) under them in the room the readings leave, dropped from the tail where there is
 * none, as the house's stack drops them. A page with no readings is a drawing, and the house's stack
 * is what fits a drawing to its box.
 */
function lcdStack(frame: Rect, rows: readonly StackRow[], density: Density): Item[] {
  const live = rows.filter((row) => row.height > 0);
  const ranks = live.map((row) => READINGS.get(row)).filter((r): r is NonNullable<typeof r> => r !== undefined);
  const specs = ranks.flatMap((r) => r.specs);
  if (specs.length === 0) return withHouseLayout(() => stack(frame, rows, density));
  const order = ranks[0]?.order ?? specs.map(idOf);
  const register = registerFor(frame);
  const others = live.filter((row) => !READINGS.has(row));
  const othersHeight = (kept: readonly StackRow[]): number => kept.reduce((sum, row) => sum + row.height + register.gap, 0);
  const kept = [...others];
  // The readings keep the rows their first needs, two where its caption stands over its value; the
  // other rows go from the tail until they do.
  const first = specs.find((spec) => idOf(spec) === order[0]) ?? specs[0]!;
  const least = Math.min(register.rows, slotsOf(first, register));
  while (kept.length > 0 && frame.height - othersHeight(kept) < least * register.row + (least - 1) * register.gap) kept.pop();
  const room = frame.height - othersHeight(kept);
  const rowsHere = Math.max(1, Math.min(register.rows, Math.floor((room + register.gap) / (register.row + register.gap))));
  const here = { ...register, rows: rowsHere };
  const shown = readingsThatFit(specs, order, here, here.rows * here.columns);
  const items = rowsIn(shown, rect(frame.left, frame.top, frame.width, room), here);
  let y = frame.top + rowsHere * (register.row + register.gap);
  for (const row of kept) {
    items.push(...row.draw(Math.round(y) + row.height));
    y += row.height + register.gap;
  }
  return items;
}

/**
 * The pages that list other cars in rows, and the pages that are one drawing, which keep the house's
 * layout: they have no readings to set as a caption and a value.
 */
const HOUSE_PAGES: ReadonlySet<string> = new Set(['leaderboard', 'relative', 'opponents', 'trackRivals', 'lapHistory', 'inputs', 'radar', 'track', 'tyres', 'damage']);

export const aimModules: ModuleRegister = { fieldsRow: readingsRow, stack: lcdStack, houseLayout: HOUSE_PAGES };

/**
 * A module page's frame: nothing drawn, since the LCD writes no page name, and the whole zone for the
 * page to draw in. The house's lists keep a few pixels at their ends, where their rows' line boxes
 * would otherwise touch the zone's edge.
 */
export function aimModuleFrame(page: { id: string; name: string }, frame: Rect): { items: Item[]; body: Rect } {
  return { items: [], body: HOUSE_PAGES.has(page.id) ? rect(frame.left, frame.top + 4, frame.width, frame.height - 8) : frame };
}
