/**
 * The three sectors of the last lap, each with its delta to your own best of that sector, and a
 * three-cell strip in the same colours.
 *
 * The canvas draws twelve mini-sectors. SimHub has no mini-sectors at all, so the strip holds one
 * cell per real sector: a strip of invented subdivisions would be a picture of nothing.
 */
import type { Hex, Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { snapEdges } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { ds } from '../tokens.ts';
import { densityOf, type Density } from './density.ts';
import { field, fieldRowFitted, fieldWidth, type FieldSpec } from './field.ts';
import { CHARS, hasTime, sectorDelta, sectorLast, sectorTime } from './values.ts';

const { iff, and, lt, le, gt, eq, str, num, signed, concat, timespanToSeconds, isnull } = ncalc;

export const SECTORS = [1, 2, 3] as const;

/**
 * A sector's colour: purple when it equals the session's best split, green when it matched or beat
 * your own best of that sector, red when it did not, dim when there is no time yet.
 *
 * Zero is green rather than red, and that is the whole of the boundary. A sector the driver has
 * just improved becomes their own best of that sector, so `Sector<n>BestTime` equals
 * `Sector<n>LastLapTime` and the delta is exactly 0.000 on the very lap the improvement happened.
 * A strict `< 0` sends that lap to the slower branch and draws a new personal best in #FF2D46,
 * which is the opposite of what the driver just did.
 */
export function sectorColour(sector: number): Expr {
  const last = sectorLast(sector);
  const delta = sectorDelta(sector);
  const best = ncalc.bestSplitTime(sector);
  const isSessionBest = and(hasTime(best), hasTime(last), lt(ncalc.abs(ncalc.sub(timespanToSeconds(last), timespanToSeconds(isnull(best, num(0))))), num(0.0005)));
  return iff(
    ncalc.not(hasTime(last)),
    str(ds.color.text.dim),
    iff(isSessionBest, str(ds.purpose.lap.sessionBest), iff(le(delta, num(0)), str(ds.purpose.delta.faster), str(ds.purpose.delta.slower))),
  );
}

/** "S1 · −0.29": the sector number and its delta, which is the field's label. */
export const sectorLabel = (sector: number): Expr =>
  iff(
    hasTime(sectorLast(sector)),
    concat(str(`S${sector} · `), signed(sectorDelta(sector), '0.00')),
    str(`S${sector}`),
  );

/**
 * `bare` writes the label as "S1" and nothing else. A zone one column wide is what the catalogue
 * draws that way, and the reason is width rather than taste: "S1 · +99.99" is what a bound label
 * has to be measured by, and it is what makes the 34 px rank the drawing asks for not fit a third
 * of a 274 px zone. The delta is on the page twice over in any case, since the colour says it.
 */
export interface SectorFieldOptions {
  bare?: boolean;
}

const sectorField = (prefix: string, sector: number, fs: number, opts: SectorFieldOptions = {}): FieldSpec => ({
  name: `${prefix}s${sector}`,
  id: `s${sector}`,
  label: `S${sector}`,
  ...(opts.bare ? {} : { labelBind: sectorLabel(sector), labelWidest: `S${sector} · +99.99` }),
  value: {
    sample: ['28.41', '41.07', '32.83'][sector - 1] ?? '0.00',
    bind: sectorTime(sectorLast(sector)),
    chars: CHARS.sector,
    fs,
    colorBind: sectorColour(sector),
  },
});

/**
 * The three sectors as fields, for a page that lays them out itself.
 *
 * A rank that is a row of fields rather than a drawing is one rule 20 can size and rule 17 can
 * shed, which is what the sectors page wants; `sectorFields` below is the absolute placement the
 * pit wall's fixed panel still asks for.
 */
export const sectorSpecs = (prefix: string, fs: number, opts: SectorFieldOptions = {}): FieldSpec[] => SECTORS.map((sector) => sectorField(prefix, sector, fs, opts));

/**
 * A sector's delta as a field of its own: the same colour as the time it belongs to.
 *
 * The delta used to be folded into the time's label, "S1 · −0.29" at 13 px, which is the form a
 * narrow zone still wants. Where there is room the sheet draws it as a value in its own right, so
 * that the number a driver is looking for is a numeral rather than half of a caption, and the pair
 * carries one colour between them: a sector and its delta disagreeing about whether the lap was
 * good would be two readings of one fact.
 */
const sectorDeltaField = (prefix: string, sector: number, fs: number): FieldSpec => ({
  name: `${prefix}delta${sector}`,
  id: `delta${sector}`,
  label: `Δ S${sector}`,
  value: {
    sample: ['−0.29', '+0.11', '−0.04'][sector - 1] ?? '0.00',
    bind: signed(sectorDelta(sector), '0.00'),
    chars: CHARS.delta,
    fs,
    colorBind: sectorColour(sector),
  },
});

/**
 * The sectors laid out across `frame`, bottom-aligned, packed from its left edge.
 *
 * Six fields where the box has room for six, which is what the pit wall's lap delta panel draws:
 * the three times and then the three deltas, in that order rather than interleaved, so that the
 * times read as a set and the deltas as another. A narrower box keeps the three times with their
 * deltas in their labels, which is the older form and the one a zone was always given.
 *
 * Packed rather than spread. Three equal spans across the whole width is what the panel drew
 * before, and six fields in three spans is two per span with no gap between the pair.
 */
export function sectorFields(prefix: string, frame: Rect, density: Density, fs?: number): Item[] {
  const d = densityOf(density);
  const bottom = frame.top + frame.height;
  const ladder = fs === undefined ? [d.big, d.mid, d.small, d.tiny] : [fs, d.small, d.tiny];

  // Three sets, in the order the row gives information up: six fields, then three whose labels
  // carry the delta, then three bare times. Each is tried at every size before the next is reached,
  // so a box loses the shape of the row only after it has lost every size it could have kept it at.
  const sets = [
    (size: number): FieldSpec[] => [...SECTORS.map((s) => sectorField(prefix, s, size, { bare: true })), ...SECTORS.map((s) => sectorDeltaField(prefix, s, size))],
    (size: number): FieldSpec[] => SECTORS.map((s) => sectorField(prefix, s, size)),
    (size: number): FieldSpec[] => SECTORS.map((s) => sectorField(prefix, s, size, { bare: true })),
  ];

  for (const set of sets) {
    for (const size of ladder) {
      const row = fieldRowFitted(set(size), frame.left, bottom, frame.width, density, { gap: d.gapX });
      if (row.fits) return row.items;
    }
  }

  // Narrower than three bare times at the smallest size on the ladder, which no panel the build
  // draws ever is. The row sheds from the tail rather than drawing past the edge.
  return fieldRowFitted(sets[2]!(ladder[ladder.length - 1] ?? d.tiny), frame.left, bottom, frame.width, density, { gap: d.gapX }).items;
}

/** The colour strip: one cell per sector, in the sector's own colour. */
export function sectorStrip(name: string, frame: Rect, gap = 2): Item[] {
  const spans = snapEdges(frame.left, frame.width, 3, gap);
  return SECTORS.flatMap((sector, i) => {
    const span = spans[i];
    if (!span) return [];
    const cell = band(`${name}.strip${sector}`, rect(span.left, frame.top, span.width, frame.height), ds.color.text.dim as Hex);
    return [withMoreBindings(cell, { BackgroundColor: sectorColour(sector) })];
  });
}

/** True when the strip has anything to say: at least one sector of the last lap was timed. */
export const anySector = (): Expr => ncalc.or(...SECTORS.map((s) => hasTime(sectorLast(s))));

/** Kept for callers that want the raw comparison, e.g. a test. */
export const sectorIsSlower = (sector: number): Expr => gt(sectorDelta(sector), num(0));
export const sectorIsZero = (sector: number): Expr => eq(sectorDelta(sector), num(0));
