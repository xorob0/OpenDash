/**
 * The BMW's own pages for zones B and C (#203), added to their catalogue after the house's modules
 * (ADR 0015) and the pages the two zones open on, so that the display reads as the canvas draws it:
 *
 *   - `Tyre pressure`, column B's box: the four corners in green cells lit from above under a
 *     `Tyre Pressure` label, two by two, or four in a row where the box is a strip; on a tall face the
 *     two lap times under it, `Fastest Lap Time` and `Predicted Lap Time`, as the canvas's column has.
 *   - `Race`, column C: the revs beside the brake balance, the fuel and its use per lap, the time
 *     difference and the lap time, which turns into the time in the pit lane, filled magenta, while the
 *     limiter is on, and red while the car is over the limit. The FUEL box fills red under the alarm.
 *     A face shorter than 360, and the portrait one, have no room for the fuel row and shed it.
 *   - `Lap times`, the canvas's qualifying page for column B: the two lap times alone.
 *
 * Every page is drawn in whatever frame the zone gives it, in either zone. The type is the canvas's at
 * the face's scale, found by naming the face from the frame's size (`bmwFaceForZone`), and made smaller
 * where the box it is in cannot hold its widest reading.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import type { Expr } from '../../bind.ts';
import { rect } from '../../design/geometry.ts';
import { label } from '../../elements/label.ts';
import {
  bestLap, brakeBias, CORNERS, currentLapTime, estimatedLap, fuel, fuelPerLap, inPitSeconds, isInPitLane, lapTime, lapTimeWidest, lastPitDuration, pitLimiterOn,
  pitSpeeding, pressureUnit, referenceDelta, referenceDeltaColour, referenceDeltaText, REFERENCE_DELTA_WIDEST, rpm, tankIsLow, tyrePressure, type Corner,
} from '../../second/values.ts';
import type { ThemeBandPage } from '../drawing.ts';
import { bmwFaceForZone, raceRows, shares } from './geometry.ts';
import { bmwColour, box, caps, fitSize, lineY, readingBox, sentence, stackedRuns, type Reading } from './register.ts';

const { concat, div, eq, fmt, iff, max, mod, mul, not, num, real, str, truncate } = ncalc;

/** What a page needs of its face: the type scale and the canvas's switches. */
export interface PageScale {
  s: number;
  short: boolean;
  tall: boolean;
  portrait: boolean;
}

/** The face a zone of this frame is on, or, for a frame no face has, the scale its height suggests. */
export function scaleOf(frame: Rect): PageScale {
  const on = bmwFaceForZone(frame.width, frame.height);
  if (on) return on.face;
  return { s: Math.min(Math.max(frame.height / 362, 0.74), 1.25), short: false, tall: frame.height > 480, portrait: false };
}

/**
 * The part of a zone's frame a page of zone B or C is drawn in: the frame, less the strip at its top
 * that a zone risen into the lights' row gained, which stays as dark as the row, so that the page
 * keeps the canvas's proportions and the settled flag the room it has at the row's end.
 */
export function pageFrame(frame: Rect): Rect {
  const lift = bmwFaceForZone(frame.width, frame.height)?.lift ?? 0;
  return rect(frame.left, frame.top + lift, frame.width, frame.height - lift);
}

/** The canvas's figures at a scale. */
export const typeOf = (s: number) => ({
  lab: Math.round(16 * s),
  val: Math.round(40 * s),
  corner: Math.round(14 * s),
  tyre: Math.round(32 * s),
  lap: Math.round(48 * s),
  rpm: Math.round(46 * s),
  aLab: Math.round(18 * s),
});

/** Two decimals, as the canvas writes every lap time on the display. */
const LAP_DECIMALS = 2;
const LAP_WIDEST = lapTimeWidest(LAP_DECIMALS);
const lap = (ts: Expr): Expr => lapTime(ts, LAP_DECIMALS);

const CORNER_NAMES: Readonly<Record<Corner, string>> = { FrontLeft: 'FL', FrontRight: 'FR', RearLeft: 'RL', RearRight: 'RR' };

/** A pressure as the sim's unit writes it: hundredths of a bar, tenths of a psi or a kilopascal. */
const pressure = (corner: Corner): Expr => iff(eq(pressureUnit(), str('bar')), fmt(tyrePressure(corner), '0.00'), fmt(tyrePressure(corner), '0.0'));
const PRESSURE_WIDEST = '444.4';

/** The tyres' box: the label on its line of 1.4, and the four cells in the room under it, 3 px apart. */
function tyreBox(name: string, room: Rect, s: number): Item[] {
  const t = typeOf(s);
  const { items, inner } = box(name, room);
  const lab = fitSize(t.lab, inner.width - 8, ['Tyre Pressure']);
  const grid = rect(inner.left + 4, inner.top + Math.round(lab * 1.4), inner.width - 8, inner.height - Math.round(lab * 1.4) - 4);
  // Four in a row where the box is a strip, as the portrait board lays them; two by two elsewhere.
  const across = grid.width >= 3 * grid.height ? 4 : 2;
  const columns = shares(grid.left, grid.width, Array(across).fill(1), 3);
  const rows = shares(grid.top, grid.height, Array(4 / across).fill(1), 3);
  const cellWidth = Math.min(...columns.map((c) => c.size)) - 4;
  const cellHeight = Math.min(...rows.map((r) => r.size)) - 3;
  const corner = fitSize(t.corner, cellWidth, ['FL', 'FR', 'RL', 'RR'], true);
  const value = Math.min(fitSize(t.tyre, cellWidth, [PRESSURE_WIDEST]), Math.floor((cellHeight - corner) / 1.05));
  return [
    ...items,
    label(`${name}.name`, 'Tyre Pressure', inner.left + 4, lineY(inner.top, lab, 1.4), inner.width - 8, { size: lab, color: bmwColour('ink'), hAlign: 'center' }),
    ...CORNERS.flatMap((c, i) => {
      const col = columns[i % across]!;
      const row = rows[Math.floor(i / across)]!;
      const cell = box(`${name}.${CORNER_NAMES[c]}`, rect(col.start, row.start, col.size, row.size), { fill: bmwColour('lit'), topEdge: { colour: bmwColour('litEdge'), width: 3 }, within: inner });
      return [
        ...cell.items,
        ...stackedRuns(`${name}.${CORNER_NAMES[c]}`, cell.inner, [
          { id: 'corner', text: CORNER_NAMES[c], size: corner, bold: true, lh: 1 },
          { id: 'value', text: c.startsWith('Front') ? '23.5' : '24.5', size: value, lh: 1.05, bind: pressure(c), widest: PRESSURE_WIDEST },
        ], bmwColour('tileInk')),
      ];
    }),
  ];
}

/** The two lap times one over the other, 4 px apart, as the canvas's column B has them on a tall face and on its qualifying page. */
function lapPair(name: string, room: Rect, s: number): Item[] {
  const t = typeOf(s);
  const [a, b] = shares(room.top, room.height, [1, 1], 4) as [{ start: number; size: number }, { start: number; size: number }];
  const write = sentence(t.lab, t.val, 'center', 2);
  return [
    readingBox(`${name}.fastest`, rect(room.left, a.start, room.width, a.size), 'Fastest Lap Time', { sample: '1:37.42', bind: lap(bestLap()), widest: LAP_WIDEST }, write),
    readingBox(`${name}.predicted`, rect(room.left, b.start, room.width, b.size), 'Predicted Lap Time', { sample: '1:37.61', bind: lap(estimatedLap()), widest: LAP_WIDEST }, write),
  ];
}

export const BMW_TYRES: ThemeBandPage = {
  id: 'bmwTyres',
  name: 'Tyre pressure',
  items: (zone, prefix) => {
    const frame = pageFrame(zone);
    const scale = scaleOf(zone);
    if (!scale.tall) return tyreBox(`${prefix}tyres`, frame, scale.s);
    const [top, under] = shares(frame.top, frame.height, [160, 160]) as [{ start: number; size: number }, { start: number; size: number }];
    return [
      ...tyreBox(`${prefix}tyres`, rect(frame.left, top.start, frame.width, top.size), scale.s),
      ...lapPair(`${prefix}laps`, rect(frame.left, under.start, frame.width, under.size), scale.s),
    ];
  },
};

export const BMW_LAPS: ThemeBandPage = {
  id: 'bmwLaps',
  name: 'Lap times',
  items: (zone, prefix) => lapPair(`${prefix}laps`, pageFrame(zone), scaleOf(zone).s),
};

/** Seconds as `m:ss.s`, the canvas's `0:14.6`, the tenths cut rather than rounded so that 59.96 never reads `60.0`. */
const minutesTenths = (seconds: Expr): Expr => {
  const s = max(real(0), seconds);
  return concat(fmt(truncate(div(s, num(60))), '0'), str(':'), fmt(div(truncate(mul(mod(s, num(60)), num(10))), num(10)), '00.0'));
};
const PIT_TIME_WIDEST = '44:44.4';

/** The revs, unboxed: `RPM` over the figure, centred in their half of the row. */
function revs(name: string, room: Rect, s: number): Item[] {
  const t = typeOf(s);
  const across = room.width - 4;
  const aLab = fitSize(t.aLab, across, ['RPM']);
  const value = Math.min(fitSize(t.rpm, across, ['44444']), Math.floor(room.height - aLab * 1.2));
  return [
    label(`${name}.name`, 'RPM', room.left + 2, lineY(room.top, aLab), across, { size: aLab, color: bmwColour('ink'), hAlign: 'center' }),
    label(`${name}.value`, '6820', room.left + 2, lineY(room.top + aLab * 1.2, value, 1), across, { size: value, color: bmwColour('ink'), hAlign: 'center', bind: fmt(rpm(), '0'), widest: '44444' }),
  ];
}

const halves = (row: Rect): [Rect, Rect] => {
  const [a, b] = shares(row.left, row.width, [1, 1]) as [{ start: number; size: number }, { start: number; size: number }];
  return [rect(a.start, row.top, a.size, row.height), rect(b.start, row.top, b.size, row.height)];
};

export const BMW_RACE: ThemeBandPage = {
  id: 'bmwRace',
  name: 'Race',
  items: (zone, prefix) => {
    const frame = pageFrame(zone);
    const scale = scaleOf(zone);
    const t = typeOf(scale.s);
    const rows = raceRows(frame, scale);
    const items: Item[] = [];

    const [rpmRoom, biasRoom] = halves(rows.top);
    items.push(...revs(`${prefix}rpm`, rpmRoom, scale.s));
    items.push(readingBox(`${prefix}bias`, biasRoom, 'BRAKE BAL', { sample: '54.5', bind: fmt(brakeBias(), '0.0'), widest: '88.8' }, caps(t.lab, t.val)));

    if (rows.fuel) {
      const [fuelRoom, perLapRoom] = halves(rows.fuel);
      const fuelReading: Reading = { sample: '46.5', bind: fmt(fuel(), '0.0'), widest: '444.4' };
      const low = tankIsLow();
      items.push(readingBox(`${prefix}fuel`, fuelRoom, 'FUEL', fuelReading, caps(t.lab, t.val), { visible: not(low) }));
      items.push(readingBox(`${prefix}fuelLow`, fuelRoom, 'FUEL', { ...fuelReading, sample: '8.6' }, caps(t.lab, t.val), { fill: bmwColour('alarm'), flat: true, visible: low }));
      items.push(readingBox(`${prefix}perLap`, perLapRoom, 'PER LAP', { sample: '3.42', bind: fmt(fuelPerLap(), '0.00'), widest: '44.44' }, caps(t.lab, t.val)));
    }

    const delta = referenceDelta();
    items.push(readingBox(`${prefix}diff`, rows.diff, 'Time Diff', { sample: '-0.18', bind: referenceDeltaText(delta), widest: REFERENCE_DELTA_WIDEST, colourBind: referenceDeltaColour(delta) }, sentence(t.lab, t.val, 'right')));
    const limiter = pitLimiterOn();
    items.push(readingBox(`${prefix}lap`, rows.lap, 'Lap Time', { sample: '1:38.12', bind: lap(currentLapTime()), widest: LAP_WIDEST }, sentence(t.lab, t.lap, 'center'), { visible: not(limiter) }));
    items.push(
      readingBox(
        `${prefix}pitTime`,
        rows.lap,
        'Pit Time',
        { sample: '0:14.6', bind: minutesTenths(iff(isInPitLane(), inPitSeconds(), lastPitDuration())), widest: PIT_TIME_WIDEST },
        sentence(t.lab, t.lap, 'center'),
        { fill: bmwColour('map'), fillBind: iff(pitSpeeding(), str(bmwColour('alarm')), str(bmwColour('map'))), flat: true, visible: limiter },
      ),
    );
    return items;
  },
};
