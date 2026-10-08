/**
 * Zone A on the LCD (#204): the gear, bold, over its `8` ghost at the top of the column, and the speed
 * under it with `kph` beside it in the 14-segment face, as the canvas draws them 168 and 54 px tall in
 * a 240 x 284 column. The house's four pages keep their meaning: the first is the gear and the speed,
 * and the revs under them where the column is deep enough to hold a third figure, as it is at
 * 1280 x 720; the second is the gear alone, the third the speed alone, and the fourth the track map,
 * which is a drawing and keeps the house's.
 *
 * Every size is the canvas's scaled to the column's height and then held to its width, so that a
 * figure follows its own box: 168 and 54 px at the reference, smaller in a shallower or a narrower
 * column, never larger than the canvas draws them.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { rpm, speed } from '../../second/values.ts';
import { reading, segment, segmentLine, segmentWidth } from './register.ts';

const { eq, fmt, game, iff, isnull, str } = ncalc;

/** The canvas's column: the gear 168, the speed 54 with its unit 12 and 8 px after it, in 284. */
const COLUMN = { height: 284, gear: 168, speed: 54, unit: 12, unitGap: 8, unitDrop: 4, gap: 16 } as const;

/** The speed unit as the units write it, after `speedUnit`'s reading of the sim's own. */
const unitOf = () => iff(eq(isnull(game('SpeedLocalUnit'), str('KMH')), str('MPH')), str('mph'), str('kph'));

const unitSize = (size: number): number => Math.max(10, Math.round((size * COLUMN.unit) / COLUMN.speed));

/** How wide a figure and its unit are together. */
const figureWidth = (ghost: string, size: number, unit: string): number => segmentWidth('DSEG7Bold', ghost, size) + COLUMN.unitGap + segmentWidth('DSEG14Regular', unit, unitSize(size));

/** The largest size not over `wanted` at which `width(size)` fits `room`. */
function fitting(wanted: number, room: number, width: (size: number) => number): number {
  let size = Math.max(8, Math.floor(wanted));
  while (size > 8 && width(size) > room) size--;
  return size;
}

/** The gear centred across the column with its line box at `top`. */
function gear(prefix: string, frame: Rect, top: number, size: number): Item[] {
  const width = segmentWidth('DSEG7Bold', '8', size);
  const left = Math.round(frame.left + (frame.width - width) / 2);
  return [
    segment(`${prefix}gear.ghost`, 'DSEG7Bold', '8', left, top, width, { size, hAlign: 'center', ghost: true }),
    segment(`${prefix}gear.value`, 'DSEG7Bold', '2', left, top, width, { size, hAlign: 'center', bind: game('Gear'), widest: '8' }),
  ];
}

/** A figure over its ghost with its unit after it, the pair centred across the column. */
function figure(prefix: string, frame: Rect, top: number, size: number, f: { sample: string; ghost: string; bind: string; unit: string; unitBind?: string }): Item[] {
  const unit = unitSize(size);
  const total = figureWidth(f.ghost, size, f.unit);
  const right = Math.round(frame.left + (frame.width - total) / 2) + segmentWidth('DSEG7Bold', f.ghost, size);
  return [
    ...reading(prefix, f.sample, right, top, size, { bind: f.bind, widest: f.ghost }),
    segment(`${prefix}.unit`, 'DSEG14Regular', f.unit, right + COLUMN.unitGap, top + Math.round((COLUMN.unitDrop * size) / COLUMN.speed), segmentWidth('DSEG14Regular', f.unit, unit), {
      size: unit,
      bind: f.unitBind,
      widest: f.unitBind === undefined ? undefined : f.unit,
    }),
  ];
}

const SPEED = { sample: '98', ghost: '888', bind: fmt(speed(), '0'), unit: 'kph', unitBind: unitOf() };
const REVS = { sample: '4320', ghost: '88888', bind: fmt(rpm(), '0'), unit: 'rpm' };

/** The sizes the canvas's column would draw at in this one, before each is held to the width. */
const scaled = (frame: Rect, size: number): number => Math.round((size * Math.min(1, frame.height / COLUMN.height)));

/** A1: the gear at the top, the speed at the foot, and the revs between where the column holds them. */
function gearSpeedRevs(frame: Rect, prefix: string): Item[] {
  const gearSize = fitting(scaled(frame, COLUMN.gear), frame.width - 4, (s) => segmentWidth('DSEG7Bold', '8', s));
  const speedSize = fitting(scaled(frame, COLUMN.speed), frame.width - 4, (s) => figureWidth(SPEED.ghost, s, SPEED.unit));
  const revsSize = fitting(speedSize, frame.width - 4, (s) => figureWidth(REVS.ghost, s, REVS.unit));
  const withRevs = frame.height >= gearSize + speedSize + revsSize + 2 * COLUMN.gap + COLUMN.height - COLUMN.gear - COLUMN.speed;
  // The last figure's box ends on the column's foot, its line box running a fifth of the size under its ink.
  const bottom = frame.top + frame.height;
  const revsTop = bottom - segmentLine(revsSize);
  const speedTop = withRevs ? revsTop - COLUMN.gap - speedSize : bottom - segmentLine(speedSize);
  const items = [...gear(`${prefix}`, frame, frame.top, gearSize), ...figure(`${prefix}speed`, frame, speedTop, speedSize, SPEED)];
  if (withRevs) items.push(...figure(`${prefix}revs`, frame, revsTop, revsSize, REVS));
  return items;
}

/** A2: the gear alone, as large as the column holds it. */
function gearAlone(frame: Rect, prefix: string): Item[] {
  const size = fitting(Math.min((frame.height - 2) / 1.4, COLUMN.gear * 1.4), frame.width - 4, (s) => segmentWidth('DSEG7Bold', '8', s));
  return gear(prefix, frame, Math.min(Math.round(frame.top + (frame.height - size) / 2), frame.top + frame.height - segmentLine(size)), size);
}

/** A3: the speed alone, as large as the column holds it, and the gear under it at the speed's own size. */
function speedAlone(frame: Rect, prefix: string): Item[] {
  const size = fitting(Math.min((frame.height - COLUMN.gap - 2) / 2.4, COLUMN.gear), frame.width - 4, (s) => figureWidth(SPEED.ghost, s, SPEED.unit));
  const top = Math.round(frame.top + (frame.height - 2 * size - COLUMN.gap) / 2);
  return [...figure(`${prefix}speed`, frame, top, size, SPEED), ...gear(prefix, frame, top + size + COLUMN.gap, size)];
}

const PAGES: Record<string, (frame: Rect, prefix: string) => Item[]> = { gearSpeedRevs, gearAlone, speed: speedAlone };

/** One of zone A's pages on the LCD, or nothing for the track map, which the house draws. */
export const aimZoneAPage = (page: string, frame: Rect, prefix: string): Item[] | undefined => PAGES[page]?.(frame, prefix);
