/**
 * The three sectors of the last lap, each with its delta to your own best of that sector, and a
 * three-cell strip in the same colours.
 *
 * The canvas draws twelve mini-sectors. SimHub has no mini-sectors at all, so the strip holds one
 * cell per real sector: a strip of invented subdivisions would be a picture of nothing.
 */
import type { Hex, Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { snapEdges } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { ds } from '../tokens.ts';
import { densityOf, type Density } from './density.ts';
import { field, fieldWidth, type FieldSpec } from './field.ts';
import { CHARS, hasTime, sectorDelta, sectorLast, sectorTime } from './values.ts';

const { iff, and, lt, le, gt, eq, str, num, fmt, concat, timespanToSeconds, isnull } = ncalc;

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

/** "S1 · -0.29": the sector number and its delta, which is the field's label. */
export const sectorLabel = (sector: number): Expr =>
  iff(
    hasTime(sectorLast(sector)),
    concat(str(`S${sector} · `), fmt(sectorDelta(sector), '0.00', true)),
    str(`S${sector}`),
  );

const sectorField = (name: string, sector: number, fs: number): FieldSpec => ({
  name: `${name}.s${sector}`,
  label: `S${sector}`,
  labelBind: sectorLabel(sector),
  labelWidest: `S${sector} · +99.99`,
  value: {
    sample: ['28.41', '41.07', '32.83'][sector - 1] ?? '0.00',
    bind: sectorTime(sectorLast(sector)),
    chars: CHARS.sector,
    fs,
    colorBind: sectorColour(sector),
  },
});

/**
 * The three sector fields laid out across `frame`, bottom-aligned, evenly spaced. The size steps
 * down until a sector time fits its third of the box, so the same code works on an 802 px
 * companion page and a 432 px portrait one.
 */
export function sectorFields(name: string, frame: Rect, density: Density, fs?: number): Item[] {
  const d = densityOf(density);
  const spans = snapEdges(frame.left, frame.width, 3, d.gapX);
  const column = spans[0]?.width ?? frame.width;
  const ladder = fs === undefined ? [d.big, d.mid, d.small, d.tiny] : [fs, d.small, d.tiny];
  const size = ladder.find((candidate) => fieldWidth(sectorField(name, 1, candidate), density) <= column) ?? ladder[ladder.length - 1] ?? d.tiny;
  return SECTORS.flatMap((sector, i) => {
    const span = spans[i];
    if (!span) return [];
    return field(sectorField(name, sector, size), span.left, frame.top + frame.height, density, span.width);
  });
}

/** The colour strip: one cell per sector, in the sector's own colour. */
export function sectorStrip(name: string, frame: Rect, gap = 2): Item[] {
  const spans = snapEdges(frame.left, frame.width, 3, gap);
  return SECTORS.flatMap((sector, i) => {
    const span = spans[i];
    if (!span) return [];
    const cell = band(`${name}.strip${sector}`, rect(span.left, frame.top, span.width, frame.height), ds.color.text.dim as Hex);
    return [{ ...cell, bindings: { BackgroundColor: { mode: 'formula' as const, formula: sectorColour(sector) } } }];
  });
}

/** True when the strip has anything to say: at least one sector of the last lap was timed. */
export const anySector = (): Expr => ncalc.or(...SECTORS.map((s) => hasTime(sectorLast(s))));

/** Kept for callers that want the raw comparison, e.g. a test. */
export const sectorIsSlower = (sector: number): Expr => gt(sectorDelta(sector), num(0));
export const sectorIsZero = (sector: number): Expr => eq(sectorDelta(sector), num(0));
