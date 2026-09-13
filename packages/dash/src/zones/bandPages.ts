/**
 * Band D's eight pages, and the corner blocks that flank them.
 *
 * A band is a wide short box — 1920 by 60 at the reference size — which is a shape nothing else on
 * the face has, and that is why these are the band's own pages rather than the modules under the
 * same names. A module lays itself out in two dimensions and divides its height between rows; hand
 * one a 26 px body and it computes negative rectangles, which is exactly what happened the first
 * time this file reached for `pageBuilder`.
 *
 * So a band page is **one rank of fields**, read left to right, packed and centred, with nothing
 * spread to fill. The field count follows the width: seven at 1920, five at 850.
 *
 * The fields are read off `design/canvas/ZoneCatalogue.dc.html`, page by page.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { measureText } from '../design/advances.ts';
import { cells, monoWidth, textBox } from '../design/metrics.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { densityOf } from '../second/density.ts';
import { dimUnless, rank, type RankMember } from '../second/rank.ts';
import {
  CHARS,
  airTemperature,
  bestLap,
  clock,
  currentLap,
  carRelativeGap,
  fuel as fuelLevel,
  fuelLapsLeft,
  fuelPerLap,
  fuelTimeLeft,
  incidents,
  lastLap,
  localClock,
  roadTemperature,
  simClock,
  windKmh,
} from '../second/values.ts';
import { ds } from '../tokens.ts';

const { fmt, isnull, num, str, iff, eq, game, raw, concat, driver, playerPosition, aheadBehind, timespanToSeconds, toShortTime } = ncalc;

/** One field of a band page: a label above a value, with an optional unit after it. */
export interface BandField {
  id: string;
  label: string;
  sample: string;
  bind: string;
  chars: { digits: number; specials: number };
  /** A small unit or denominator drawn after the value. */
  after?: string;
  color?: `#${string}`;
  colorBind?: string;
  /**
   * True while the game publishes the field. A field without one is always drawn; a field with one
   * is removed when the game has nothing for it and the rank closes over the hole, which is band
   * D's own rule and the reason the strip in the bar is narrower on a car that has fewer settings.
   */
  present?: string;
  /**
   * A word rather than a number, drawn as a proportional label.
   *
   * Rule 19: a value is laid in monospace cells cut for digits, and only what fits a cell may go in
   * one. "MEDIUM" and "GREEN" do not -- M, W and U all overrun -- so a compound or a track state is
   * a label that happens to be bound, not a value. `widest` is what it is measured by.
   */
  widest?: string;
}

const lapTime = (expr: string): string => iff(eq(timespanToSeconds(expr), num(0)), str('--:--.---'), toShortTime(expr, 3));

/** D1 Fuel: what a driver checks on a straight, which is why it is the default. */
const fuel: readonly BandField[] = [
  { id: 'fuel', label: 'Fuel', sample: '15.12', bind: fmt(fuelLevel(), '0.00'), chars: { digits: 5, specials: 1 }, after: 'L', color: ds.purpose.fuel.nominal },
  { id: 'time', label: 'Fuel time', sample: '08:46', bind: clock(fuelTimeLeft()), chars: CHARS.clock },
  { id: 'laps', label: 'Est. laps', sample: '13.1', bind: fmt(fuelLapsLeft(), '0.0'), chars: CHARS.consumption },
  { id: 'refuel', label: 'Refuel', sample: '32.67', bind: fmt(isnull(raw('PitSvFuel'), num(0)), '0.00'), chars: { digits: 5, specials: 1 } },
  { id: 'perLap', label: 'Per lap', sample: '1.432', bind: fmt(fuelPerLap(), '0.000'), chars: { digits: 5, specials: 1 } },
];

/** D2 Energy. Le Mans Ultimate publishes virtual energy; iRacing does not, so this reads `--`. */
const energy: readonly BandField[] = [
  { id: 'energy', label: 'Energy', sample: '68', bind: notAvailable(raw('VirtualEnergy')), chars: CHARS.temperature, after: '%' },
  { id: 'perLap', label: 'Per lap', sample: '5.6', bind: notAvailable(raw('VirtualEnergyPerLap')), chars: CHARS.consumption, after: '%' },
  { id: 'laps', label: 'Est. laps', sample: '12.1', bind: notAvailable(raw('VirtualEnergyLaps')), chars: CHARS.consumption },
  { id: 'refuel', label: 'Refuel', sample: '31', bind: notAvailable(raw('VirtualEnergyRefuel')), chars: CHARS.temperature, after: '%' },
];

/** D3 Stint. The pit window needs a strategy the plugin does not compute; see ADR 0009. */
const stint: readonly BandField[] = [
  { id: 'laps', label: 'Stint laps', sample: '12', bind: fmt(isnull(driver('lapsdonesincelastpitout', playerPosition()), num(0)), '0'), chars: CHARS.position },
  { id: 'time', label: 'Stint time', sample: '0:21:40', bind: clock(timespanToSeconds(isnull(driver('timesincelastpitout', playerPosition()), num(0)))), chars: CHARS.clock },
  { id: 'stops', label: 'Stops', sample: '1', bind: fmt(isnull(driver('pitcount', playerPosition()), num(0)), '0'), chars: CHARS.position },
  { id: 'lastStop', label: 'Last stop', sample: '24.3', bind: fmt(timespanToSeconds(isnull(driver('pitlastduration', playerPosition()), num(0))), '0.0'), chars: CHARS.consumption, after: 's' },
];

/** D4 Tyres: the four carcass temperatures across the band, in car order, and the compound. */
const tyres: readonly BandField[] = [
  { id: 'fl', label: 'FL', sample: '84', bind: fmt(isnull(raw('LFtempCM'), num(0)), '0'), chars: CHARS.temperature, after: '°' },
  { id: 'fr', label: 'FR', sample: '104', bind: fmt(isnull(raw('RFtempCM'), num(0)), '0'), chars: CHARS.temperature, after: '°' },
  { id: 'rl', label: 'RL', sample: '62', bind: fmt(isnull(raw('LRtempCM'), num(0)), '0'), chars: CHARS.temperature, after: '°' },
  { id: 'rr', label: 'RR', sample: '88', bind: fmt(isnull(raw('RRtempCM'), num(0)), '0'), chars: CHARS.temperature, after: '°' },
  { id: 'compound', label: 'Compound', sample: 'MEDIUM', bind: ncalc.ucase(isnull(driver('fronttyrecompound', playerPosition()), str('--'))), chars: { digits: 7, specials: 0 }, widest: 'MEDIUM' },
];

/** D5 Weather. There is no weather module: a companion page of it would be mostly empty, and a
 *  band is exactly the shape for it. */
const weather: readonly BandField[] = [
  { id: 'air', label: 'Air', sample: '21.5', bind: fmt(airTemperature(), '0.0'), chars: CHARS.pressure, after: '°' },
  { id: 'track', label: 'Track', sample: '27.6', bind: fmt(roadTemperature(), '0.0'), chars: CHARS.pressure, after: '°' },
  { id: 'wind', label: 'Wind', sample: '12', bind: fmt(windKmh(), '0'), chars: CHARS.temperature, after: 'KM/H' },
  { id: 'grip', label: 'Grip', sample: 'GREEN', bind: ncalc.ucase(isnull(game('TrackGripStatus'), str('--'))), chars: { digits: 7, specials: 0 }, widest: 'MODERATE' },
];

/** D6 Sectors: the three of the last lap, then the lap itself and the session best. */
const sectors: readonly BandField[] = [
  { id: 's1', label: 'S1', sample: '28.41', bind: fmt(timespanToSeconds(ncalc.driverSector('lastlap', playerPosition(), 1)), '0.00'), chars: CHARS.sector },
  { id: 's2', label: 'S2', sample: '41.07', bind: fmt(timespanToSeconds(ncalc.driverSector('lastlap', playerPosition(), 2)), '0.00'), chars: CHARS.sector },
  { id: 's3', label: 'S3', sample: '32.83', bind: fmt(timespanToSeconds(ncalc.driverSector('lastlap', playerPosition(), 3)), '0.00'), chars: CHARS.sector },
  { id: 'last', label: 'Last', sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime },
  { id: 'best', label: 'Best', sample: '1:41.877', bind: lapTime(bestLap()), chars: CHARS.lapTime, color: ds.purpose.lap.sessionBest },
];

/** D7 Relative: the car ahead, the driver, and the car behind. Three gaps, nothing else. */
const relative: readonly BandField[] = [
  { id: 'ahead', label: 'Ahead', sample: '-1.342', bind: fmt(carRelativeGap(aheadBehind(num(-1))), '0.000'), chars: CHARS.relativeGap, color: ds.color.text.secondary },
  { id: 'you', label: 'You', sample: '0.000', bind: str('0.000'), chars: CHARS.relativeGap },
  { id: 'behind', label: 'Behind', sample: '+0.722', bind: fmt(carRelativeGap(aheadBehind(num(1))), '+0.000;-0.000;0.000'), chars: CHARS.relativeGap, color: ds.color.text.secondary },
];

/**
 * D8 Car: the telltale row belongs here, and the pictograms need the generator's image item
 * (XOR-115) before XOR-97 can draw them. Until then this is the readings the lamps would sit
 * beside, so the page is worth cycling to rather than an empty step in the ring.
 */
const car: readonly BandField[] = [
  { id: 'water', label: 'Water', sample: '92', bind: fmt(isnull(game('WaterTemperature'), num(0)), '0'), chars: CHARS.temperature, after: '°' },
  { id: 'oil', label: 'Oil', sample: '104', bind: fmt(isnull(game('OilTemperature'), num(0)), '0'), chars: CHARS.temperature, after: '°' },
  // The three gauges below the temperatures are the ones a sim either wires or does not. They are
  // removed rather than zeroed: 0.0 bar of oil pressure is a reading, and a wrong one.
  { id: 'oilPressure', label: 'Oil pressure', sample: '4.2', bind: fmt(game('OilPressure'), '0.0'), chars: CHARS.consumption, present: present(game('OilPressure')) },
  { id: 'fuelPressure', label: 'Fuel pressure', sample: '3.8', bind: fmt(raw('FuelPress'), '0.0'), chars: CHARS.consumption, present: present(raw('FuelPress')) },
  { id: 'voltage', label: 'Voltage', sample: '13.8', bind: fmt(raw('Voltage'), '0.0'), chars: CHARS.consumption, present: present(raw('Voltage')) },
];

/** True while the game publishes the property: what a removable field is guarded by. */
function present(expr: string): string {
  return ncalc.not(ncalc.isNull(expr));
}

/** `--` when the sim publishes nothing, rather than a zero that reads as a reading. */
function notAvailable(expr: string): string {
  return iff(ncalc.isNull(expr), str('--'), fmt(expr, '0.0'));
}

/** The eight pages, by the id the contract gives them. */
export const BAND_PAGES: Record<string, readonly BandField[]> = {
  fuel,
  energy,
  stint,
  tyres,
  weather,
  sectors,
  relative,
  car,
};

/**
 * The largest value size whose line box fits the band, given the label above it.
 *
 * The value takes the height the band gives it rather than the height the density prefers. A band
 * is 60 px at 1920, 58 on the nano and 54 at 1280 by 400, and a value sized for the tallest hangs
 * out of the shortest -- where WPF clips it and the row reads as a rendering fault. A rank of one
 * row has nothing to shed, so this is one of the two places a value shrinks instead.
 *
 * Solved by trying rather than by algebra, because the thing that has to fit is a WPF line box:
 * it starts a tenth of the size above the line it is given and runs about a fifth below the
 * baseline, and rounding at both ends is what the last pixel turns on.
 */
function valueSizeFor(height: number, preferred: number, labelFs: number, fieldGap: number): number {
  for (let fs = preferred; fs > 8; fs--) {
    const blockHeight = labelFs + fieldGap + fs;
    const top = Math.max(0, (height - blockHeight) / 2);
    const box = textBox(top + labelFs + fieldGap, fs);
    if (box.top >= 0 && box.top + box.height <= height) return fs;
  }
  return 8;
}

/** Width the unit after a value takes. Measured, not the remainder of the field: a field whose
 *  value fills its width left the unit a box narrower than its own glyph, and WPF clipped it. */
const unitWidth = (field: BandField, labelFs: number): number => (field.after ? Math.ceil(measureText('BarlowMedium', field.after, labelFs)) + 2 : 0);

/** Width a band field takes: its value with its unit, or its label, whichever is wider. */
function fieldWidth(field: BandField, valueFs: number, labelFs: number): number {
  const value = field.widest
    ? Math.ceil(measureText('BarlowMedium', field.widest, valueFs)) + 2
    : monoWidth(cells('SemiBold', valueFs), field.chars);
  const after = field.after ? ds.space[2] + unitWidth(field, labelFs) : 0;
  const text = Math.ceil(measureText('BarlowMedium', field.label.toUpperCase(), labelFs)) + 2;
  return Math.ceil(Math.max(value + after, text));
}

/**
 * One field of a band page as a member of its rank: how wide it is, whether the game publishes it,
 * and how it draws itself wherever the rank puts it.
 */
function bandMember(field: BandField, prefix: string, geometry: { valueFs: number; labelFs: number; top: number; valueTop: number }): RankMember {
  const { valueFs, labelFs, top, valueTop } = geometry;
  const w = fieldWidth(field, valueFs, labelFs);
  const valueWidth = field.widest ? w : monoWidth(cells('SemiBold', valueFs), field.chars);
  return {
    id: field.id,
    width: w,
    present: field.present,
    draw: (at) => {
      const items: Item[] = [
        label(`${prefix}${field.id}.label`, field.label.toUpperCase(), at.x, top, w, { size: labelFs, leftBind: at.leftAt(), visibleBind: at.visibleBind }),
        field.widest
          ? label(`${prefix}${field.id}.value`, field.sample, at.x, valueTop, w, {
              size: valueFs,
              color: field.color ?? ds.color.text.primary,
              bind: field.bind,
              widest: field.widest,
              leftBind: at.leftAt(),
              visibleBind: at.visibleBind,
            })
          : numeral(`${prefix}${field.id}.value`, field.sample, at.x, valueTop, valueFs, field.chars, {
              bind: field.bind,
              color: field.color,
              colorBind: field.colorBind,
              maxWidth: valueWidth + 4,
              leftBind: at.leftAt(),
              visibleBind: at.visibleBind,
            }),
      ];
      if (field.after) {
        items.push(
          unit(`${prefix}${field.id}.unit`, field.after, at.x + valueWidth + ds.space[2], valueTop + (valueFs - labelFs), unitWidth(field, labelFs), {
            size: labelFs,
            leftBind: at.leftAt(valueWidth + ds.space[2]),
            visibleBind: at.visibleBind,
          }),
        );
      }
      return items;
    },
  };
}

/**
 * One band page drawn in `frame`: its fields as one rank, packed and centred.
 *
 * The rank sheds from the tail when the width is not there, because the fields are listed in
 * importance order, and it closes over any field the game does not publish. Nothing is spread to
 * fill: a band with three fields in it is three fields in the middle, not three fields stretched
 * across 1920 px.
 */
export function bandPageItems(id: string, frame: Rect, prefix: string): Item[] {
  const fields = BAND_PAGES[id];
  if (!fields) throw new RangeError(`band D has no page "${id}"`);
  const d = densityOf('zone');
  const labelFs = d.labelSm;
  const valueFs = valueSizeFor(frame.height, d.mid, labelFs, d.fieldGap);
  const blockHeight = labelFs + d.fieldGap + valueFs;
  const top = frame.top + Math.max(0, (frame.height - blockHeight) / 2);
  const geometry = { valueFs, labelFs, top, valueTop: top + labelFs + d.fieldGap };

  return rank(
    fields.map((field) => bandMember(field, prefix, geometry)),
    { left: frame.left, width: frame.width, gap: d.gapX, when: 'close' },
  ).items;
}

/**
 * The corner blocks at each end of the band: incidents against their limit and the track state on
 * the left; DRS, push to pass, the spotter lamps and both clocks on the right.
 *
 * Drawn at 1920x480, 1280x480, 1280x400 and 1280x720 and absent at 850x480, 800x286 and 600x686.
 * The threshold is those drawings rather than a round number, which is why the layout carries the
 * answer rather than this file computing one.
 */
export function bandCorners(frame: Rect, prefix: string): Item[] {
  const d = densityOf('zone');
  const labelFs = d.labelSm;
  const valueFs = valueSizeFor(frame.height, d.small, labelFs, d.fieldGap);
  const padX = 20;
  const blockHeight = labelFs + d.fieldGap + valueFs;
  const top = frame.top + Math.max(0, (frame.height - blockHeight) / 2);
  const valueTop = top + labelFs + d.fieldGap;
  const items: Item[] = [];

  const left: BandField[] = [
    { id: 'incidents', label: 'Incidents', sample: '3x', bind: concat(fmt(isnull(incidents(), num(0)), '0'), str('x')), chars: CHARS.count },
    { id: 'trackState', label: 'Track', sample: 'DRY', bind: ncalc.ucase(isnull(game('TrackGripStatus'), str('--'))), chars: { digits: 5, specials: 0 }, widest: 'MODERATE' },
  ];
  let x = frame.left + padX;
  for (const field of left) {
    const w = fieldWidth(field, valueFs, labelFs);
    items.push(label(`${prefix}${field.id}.label`, field.label.toUpperCase(), x, top, w, { size: labelFs }));
    items.push(
      field.widest
        ? label(`${prefix}${field.id}.value`, field.sample, x, valueTop, w, { size: valueFs, color: ds.color.text.primary, bind: field.bind, widest: field.widest })
        : numeral(`${prefix}${field.id}.value`, field.sample, x, valueTop, valueFs, field.chars, { bind: field.bind, maxWidth: w + 4 }),
    );
    x += w + d.gapX;
  }

  // The right corner: three lamps, then the two clocks. A lamp is drawn dim rather than removed --
  // `when: 'dim'`, the other half of the rank's contract -- because a lamp that vanished would move
  // the two beside it at the moment they matter.
  const lamps: { id: string; text: string; on: string; colour: `#${string}` }[] = [
    { id: 'drs', text: 'DRS', on: eq(isnull(game('DRSAvailable'), num(0)), num(1)), colour: ds.purpose.flag.green },
    { id: 'p2p', text: 'P2P', on: eq(isnull(raw('PushToPass'), num(0)), num(1)), colour: ds.purpose.flag.blue },
    { id: 'spt', text: 'SPT', on: ncalc.ne(isnull(game('CarLeftRight'), num(0)), num(1)), colour: ds.purpose.flag.yellow },
  ];
  const clocks: BandField[] = [
    { id: 'clock', label: 'Clock', sample: '13:11', bind: localClock(), chars: CHARS.clock },
    { id: 'sim', label: 'Sim', sample: '19:26', bind: simClock(), chars: CHARS.clock },
  ];
  const clockWidth = clocks.reduce((sum, f) => sum + fieldWidth(f, valueFs, labelFs), 0) + d.gapX;
  const lampWidth = Math.ceil(measureText('BarlowMedium', 'DRS', labelFs)) + 8;
  const rightWidth = lamps.length * (lampWidth + d.gapX / 2) + clockWidth;

  x = frame.left + frame.width - padX - rightWidth;
  const lampGap = d.gapX / 2;
  items.push(
    ...rank(
      lamps.map((lamp) => ({
        id: lamp.id,
        width: lampWidth,
        present: lamp.on,
        draw: (at) => [
          label(`${prefix}${lamp.id}`, lamp.text, at.x, top + (blockHeight - labelFs) / 2, lampWidth, {
            size: labelFs,
            hAlign: 'center',
            color: ds.color.text.dim,
            colorBind: dimUnless(at.litBind, lamp.colour),
          }),
        ],
      })),
      { left: x, width: lamps.length * (lampWidth + lampGap), gap: lampGap, when: 'dim', align: 'left' },
    ).items,
  );
  x += lamps.length * (lampWidth + lampGap);
  for (const field of clocks) {
    const w = fieldWidth(field, valueFs, labelFs);
    items.push(label(`${prefix}${field.id}.label`, field.label.toUpperCase(), x, top, w, { size: labelFs }));
    items.push(numeral(`${prefix}${field.id}.value`, field.sample, x, valueTop, valueFs, field.chars, { bind: field.bind, maxWidth: w + 4 }));
    x += w + d.gapX;
  }
  return items;
}

/** Every page id band D can show, for a test that wants to walk them. */
export const BAND_PAGE_IDS: readonly string[] = Object.keys(BAND_PAGES);
