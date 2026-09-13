/**
 * Which field of which page is secondary: the table, transcribed from the catalogue.
 *
 * Rule 17 says a page sheds its secondary ranks before it shrinks its numerals, and XOR-86 built
 * the shape model that decides when. Neither says **what goes first**, and that is not a mechanism
 * to be derived: it is a design decision taken eighty-four times, once per page per shape, on
 * `design/canvas/ZoneCatalogue.dc.html`. Every drawing there is a row of this table.
 *
 * Read it as the answer to one question: *of the fields this page has, which survive at this
 * shape?* The `wide` row is the fullest form and keeps everything the page carries, including the
 * fields the companion artboard gives it and the zone drawing does not. The other three are the
 * catalogue's own answers.
 *
 * Three kinds of page, because three kinds of thing shed:
 *
 * - `fields`, a rank of labelled values, which drops the values the drawing drops.
 * - `columns`, a table, which loses columns as the zone narrows and gains rows as it grows taller.
 *   A narrow zone loses columns before it loses rows; that is the pattern across the catalogue.
 * - `nothing`, a page with nothing to shed: a drawing is cut from its box (rule 18) and a page
 *   that says it has no data is one line of prose.
 *
 * The shedding order is the one thing a page cannot be given later. A page built without it has
 * been laid out as though everything fits, and putting it right afterwards means revisiting the
 * layout rather than adding a line, which is why this arrives with the pages rather than after.
 */
import { type Shape } from '../second/shape.ts';

/** The four shapes the catalogue draws every page at. */
export type Archetype = 'wide' | 'grid' | 'tallNarrow' | 'tall';

/** What a page keeps at each of the four, most complete first. */
export type Keeps = Record<Archetype, readonly string[]>;

export type Shedding =
  | { kind: 'fields'; keeps: Keeps }
  | { kind: 'columns'; keeps: Keeps }
  | { kind: 'nothing'; why: string };

/**
 * The shape a page is drawn at, as one of the four the catalogue draws.
 *
 * A real box is not always one of the four: the pit wall hands a module 607 x 158 and the nano a
 * 269 x 194 zone, and neither is on the canvas. A short box takes the answer of the narrower shape
 * of the same width, because what a short box has is room for one rank -- which is the same
 * constraint from the other side.
 */
export function archetypeOf(shape: Shape): Archetype {
  if (shape.height === 'short') return shape.width === 'wide' ? 'grid' : 'tallNarrow';
  if (shape.width === 'narrow') return 'tallNarrow';
  if (shape.width === 'wide') return 'wide';
  return shape.height === 'tall' ? 'tall' : 'grid';
}

const fields = (keeps: Keeps): Shedding => ({ kind: 'fields', keeps });
const columns = (keeps: Keeps): Shedding => ({ kind: 'columns', keeps });
const nothing = (why: string): Shedding => ({ kind: 'nothing', why });

/**
 * The table. One entry per page of the catalogue, in the catalogue's order.
 *
 * Where a module carries a field the drawing does not name -- the companion artboard gives fuel
 * three per-lap consumptions where the zone drawing gives it one -- the field is kept at `wide`,
 * the fullest form, and follows the drawing everywhere else. Where the drawing names a field the
 * module does not have yet, it is simply not here; the module is what this table is about.
 */
export const SHEDDING: Record<string, Shedding> = {
  // Six values at `wide`, four at `grid`: the laps and the estimate go, and the delta stays,
  // which is the clearest proof in the catalogue that shedding is not dropping the tail. At
  // `tall narrow` only the two times a driver compares on a lap.
  lapTimes: fields({
    wide: ['last', 'sessionBest', 'yourBest', 'laps', 'estimated', 'delta'],
    grid: ['last', 'sessionBest', 'yourBest', 'delta'],
    tallNarrow: ['last', 'sessionBest'],
    tall: ['last', 'sessionBest', 'yourBest', 'laps', 'estimated', 'delta'],
  }),
  // One value and a bar it is drawn against; there is nothing secondary to lose. The catalogue
  // draws three sector deltas under the bar that this page does not build yet (XOR-171).
  delta: fields({ wide: ['delta'], grid: ['delta'], tallNarrow: ['delta'], tall: ['delta'] }),
  // The three sectors are the page and stay at every shape. Of the three lap times under them the
  // drawings keep two, and not the same two: your own best and the last lap in a narrow zone, the
  // last lap and the session best in a tall one.
  sectors: fields({
    wide: ['yourBest', 'last', 'sessionBest'],
    grid: ['yourBest', 'last', 'sessionBest'],
    tallNarrow: ['yourBest', 'last'],
    tall: ['last', 'sessionBest'],
  }),
  // The redline is a number a driver reads once a car, so it is the first thing the speedo drops.
  speedo: fields({ wide: ['speed', 'rpm', 'redline'], grid: ['speed', 'rpm'], tallNarrow: ['speed', 'rpm'], tall: ['speed', 'rpm'] }),
  // What is left, how long it lasts, and what to add: those three survive everywhere. The three
  // per-lap consumptions are one number three ways, so the narrow shapes keep the average alone.
  fuel: fields({
    wide: ['level', 'time', 'toAdd', 'lastLap', 'thisLap', 'average', 'lapsLeft'],
    grid: ['level', 'time', 'toAdd', 'average'],
    tallNarrow: ['level', 'time', 'toAdd', 'average'],
    tall: ['level', 'time', 'toAdd', 'lastLap', 'thisLap', 'average', 'lapsLeft'],
  }),
  energy: nothing('one line of prose: iRacing publishes no virtual energy'),
  tyres: nothing('four corners cut from the box; rule 18'),
  // The refuel and the pit time stay at every shape; the corner toggles wrap and the tear-off goes
  // with the width, which the toggle block already does for itself.
  pitView: fields({ wide: ['refuel', 'pitTime'], grid: ['refuel', 'pitTime'], tallNarrow: ['refuel', 'pitTime'], tall: ['refuel', 'pitTime'] }),
  // The catalogue keeps seven of its ten cells at `tall narrow` and drops the three drawn last.
  // The same rule here: the car, the two a driver moves every corner, and the brake bias.
  carSettings: fields({
    wide: ['car', 'tc', 'abs', 'bb', 'mix', 'arbFront', 'arbRear'],
    grid: ['car', 'tc', 'abs', 'bb', 'mix', 'arbFront', 'arbRear'],
    tallNarrow: ['car', 'tc', 'abs', 'bb'],
    tall: ['car', 'tc', 'abs', 'bb', 'mix', 'arbFront', 'arbRear'],
  }),
  inputs: nothing('three traces and their bars, cut from the box; rule 18'),
  // Where you are and how long is left survive; the session name and the laps left are what a
  // driver can infer from the rest.
  session: fields({
    wide: ['type', 'position', 'class', 'lap', 'timeLeft', 'lapsLeft'],
    grid: ['position', 'class', 'lap', 'timeLeft'],
    tallNarrow: ['position', 'class', 'lap', 'timeLeft'],
    tall: ['type', 'position', 'class', 'lap', 'timeLeft', 'lapsLeft'],
  }),
  radar: nothing('the cars beside you, cut from the box; rule 18'),
  track: nothing('the map, cut from the box; rule 18'),
  // A list loses columns before it loses rows. The lap times go first, then the chip and the
  // number: a leaderboard without a gap is a list of names, so the gap outlives both.
  leaderboard: columns({
    wide: ['pos', 'num', 'name', 'class', 'gap', 'best', 'last'],
    grid: ['pos', 'num', 'name', 'class', 'gap'],
    tallNarrow: ['pos', 'name', 'gap'],
    tall: ['pos', 'num', 'name', 'class', 'gap'],
  }),
  // The same row with the on-track gap. `tall narrow` is the ticket's own example: position, code
  // and gap, and eight rows rather than six.
  relative: columns({
    wide: ['pos', 'num', 'name', 'class', 'gap'],
    grid: ['pos', 'num', 'name', 'class', 'gap'],
    tallNarrow: ['pos', 'name', 'gap'],
    tall: ['pos', 'num', 'name', 'class', 'gap'],
  }),
  // Two blocks, each a gap with a name, a number, a class chip and a line of detail. A narrow zone
  // keeps the gap and who it belongs to, which is the whole of what the page is for: the catalogue
  // draws the code and the gap there and nothing else.
  opponents: fields({
    wide: ['ahead.gap', 'ahead.name', 'ahead.num', 'ahead.class', 'ahead.detail', 'behind.gap', 'behind.name', 'behind.num', 'behind.class', 'behind.detail'],
    grid: ['ahead.gap', 'ahead.name', 'ahead.num', 'ahead.class', 'ahead.detail', 'behind.gap', 'behind.name', 'behind.num', 'behind.class', 'behind.detail'],
    tallNarrow: ['ahead.gap', 'ahead.name', 'behind.gap', 'behind.name'],
    tall: ['ahead.gap', 'ahead.name', 'ahead.class', 'ahead.detail', 'behind.gap', 'behind.name', 'behind.class', 'behind.detail'],
  }),
  gear: nothing('the gear, cut from the box; rule 18'),
  // The stint is the laps and the stops. The time, the total and the driver are the recap.
  stint: fields({
    wide: ['stintLaps', 'stintTime', 'completed', 'driver', 'stops', 'lastStop'],
    grid: ['stintLaps', 'stops', 'lastStop'],
    tallNarrow: ['stintLaps', 'stops', 'lastStop'],
    tall: ['stintLaps', 'stintTime', 'completed', 'driver', 'stops', 'lastStop'],
  }),
  lapHistory: nothing('three columns and as many rows as fit; there is no fourth to drop'),
  damage: nothing('one line of prose: iRacing publishes no damage'),
  trackRivals: nothing('one line of prose: SimHub times sectors, not segments'),
};

/** What a page does when it cannot keep everything. Throws for a page the table does not name. */
export function sheddingFor(page: string): Shedding {
  const entry = SHEDDING[page];
  if (!entry) throw new RangeError(`shedding: no page "${page}". Every page declares its shedding order.`);
  return entry;
}

/** The ids a page keeps at a shape, or undefined for a page with nothing to shed. */
export function keepsAt(page: string | undefined, shape: Shape): readonly string[] | undefined {
  if (page === undefined) return undefined;
  const entry = SHEDDING[page];
  if (!entry || entry.kind === 'nothing') return undefined;
  return entry.keeps[archetypeOf(shape)];
}

/**
 * The members of `all` this page keeps at this shape, in the order they were given.
 *
 * A page the table does not name keeps everything, which is what a module built outside the
 * catalogue -- the two zone pages that are not modules -- should do.
 */
export function keptAt<T extends { id?: string }>(all: readonly T[], page: string | undefined, shape: Shape): T[] {
  const keeps = keepsAt(page, shape);
  if (!keeps) return [...all];
  return all.filter((item) => item.id === undefined || keeps.includes(item.id));
}

/**
 * Whether a page keeps one named part at a shape.
 *
 * For the parts of a page that are not fields and not columns: a class chip beside a gap, a line
 * of detail under it. A page with nothing declared keeps everything.
 */
export function keepsPart(id: string, page: string | undefined, shape: Shape): boolean {
  const keeps = keepsAt(page, shape);
  return keeps === undefined || keeps.includes(id);
}

/** The same, for a page whose members are bare ids: the columns of a table. */
export function keptIds<T extends string>(all: readonly T[], page: string | undefined, shape: Shape): T[] {
  const keeps = keepsAt(page, shape);
  if (!keeps) return [...all];
  return all.filter((id) => keeps.includes(id));
}
