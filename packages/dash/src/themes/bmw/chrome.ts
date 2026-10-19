/**
 * What the car draws around zones B, A and C (#203): the four setting tiles and the four temperatures
 * under zone B, the column of lap times at the right of a face 1200 wide or wider, and the column of
 * places at the left of one 1700 wide or wider. None of it is a zone, so none of it cycles: the tiles
 * are the M cars' TC, ABS, MAP and ARB in the car's own colours, and the temperatures its TMOT, TOIL,
 * TTRK and TAIR, the engine's water and oil and the track and the air.
 *
 * A tile whose setting the car does not publish is hidden, as the strip's cells are; a temperature the
 * sim does not fill draws `--`.
 */
import type { Hex, Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import type { Expr } from '../../bind.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { hasSetting, LEVEL_WIDEST, trackedValue } from '../../second/tracked.ts';
import {
  airTemperature, antiRollFront, bestLap, carRelativeGap, estimatedLap, lapTime, LAP_TIME_WIDEST, lastLap, neighbour, NO_VALUE, oilTemperature, player,
  positionLabelled, RELATIVE_GAP_WIDEST, roadTemperature, waterTemperature,
} from '../../second/values.ts';
import type { FaceContext } from '../drawing.ts';
import { bmwFace, GAP, SHADOW } from './geometry.ts';
import { bmwColour, box, centredY, stackedBox, stackedTypeFor, type Reading, type StackedType } from './register.ts';

const { eq, fmt, iff, isNull, isnull, not, num, str } = ncalc;

/** The tiles in the car's order, each with its colour and what it shows. */
const TILES = (): readonly { id: string; title: string; colour: Hex; reading: Reading; present: Expr }[] => {
  const tracked = (id: string): Reading & { present: Expr } => {
    const value = trackedValue(id);
    return { sample: value.sample, bind: fmt(value.read, value.pattern), widest: value.widest, present: hasSetting(value) };
  };
  const tc = tracked('tc');
  const abs = tracked('abs');
  const map = tracked('map');
  return [
    { id: 'tc', title: 'TC', colour: bmwColour('tc'), reading: tc, present: tc.present },
    { id: 'abs', title: 'ABS', colour: bmwColour('abs'), reading: abs, present: abs.present },
    { id: 'map', title: 'MAP', colour: bmwColour('map'), reading: map, present: map.present },
    { id: 'arb', title: 'ARB', colour: bmwColour('arb'), reading: { sample: '4', bind: fmt(antiRollFront(), '0'), widest: LEVEL_WIDEST }, present: not(isNull(antiRollFront())) },
  ];
};

/** A whole-degree temperature, `--` where the sim leaves it at null or zero. */
const degrees = (value: Expr): Expr => iff(eq(isnull(value, num(0)), num(0)), str(NO_VALUE), fmt(value, '0'));

const TEMPS = (): readonly { id: string; title: string; reading: Reading }[] => [
  { id: 'water', title: 'TMOT', reading: { sample: '92', bind: degrees(waterTemperature()), widest: '444' } },
  { id: 'oil', title: 'TOIL', reading: { sample: '98', bind: degrees(oilTemperature()), widest: '444' } },
  { id: 'track', title: 'TTRK', reading: { sample: '31', bind: degrees(roadTemperature()), widest: '444' } },
  { id: 'air', title: 'TAIR', reading: { sample: '22', bind: degrees(airTemperature()), widest: '444' } },
];

/** `count` rooms side by side across `row`, `GAP` apart less the shadow each keeps inside its own. */
export function across(row: Rect, count: number, gap = GAP - SHADOW.x): Rect[] {
  const width = (row.width - (count - 1) * gap) / count;
  return Array.from({ length: count }, (_, i) => rect(Math.round(row.left + i * (width + gap)), row.top, Math.round(row.left + (i + 1) * width + i * gap) - Math.round(row.left + i * (width + gap)), row.height));
}

/** `count` rooms one under another down `column`. */
export function down(column: Rect, count: number, gap = GAP - SHADOW.y): Rect[] {
  const height = (column.height - (count - 1) * gap) / count;
  return Array.from({ length: count }, (_, i) => rect(column.left, Math.round(column.top + i * (height + gap)), column.width, Math.round(column.top + (i + 1) * height + i * gap) - Math.round(column.top + i * (height + gap))));
}

function tiles(row: Rect, s: number): Item[] {
  const list = TILES();
  const rooms = across(row, list.length);
  const type = stackedTypeFor(rooms[0]!, list.map((t) => ({ title: t.title, widest: t.reading.widest })), { label: Math.round(15 * s), value: Math.round(40 * s) }, 0.4, true);
  if (!type) return [];
  return list.map((tile, i) => stackedBox(`chrome.tiles.${tile.id}`, rooms[i]!, tile.title, tile.reading, type, { fill: tile.colour, visible: tile.present }, 'center'));
}

/** The temperatures' box: four cells, each a header strip with its name over the value. */
function temps(room: Rect, s: number): Item[] {
  const list = TEMPS();
  const { items, inner } = box('chrome.temps', room);
  const cells = across(rect(inner.left + 4, inner.top + 4, inner.width - 8, inner.height - 8), list.length, 4);
  const labelSize = Math.round(15 * s);
  const head = Math.ceil(labelSize * 1.2) + 4;
  const valueRoom = (cells[0]?.height ?? 0) - head;
  const fit: StackedType | undefined = stackedTypeFor(cells[0]!, list.map((t) => ({ title: t.title, widest: t.reading.widest })), { label: labelSize, value: Math.round(32 * s) }, 0.4, true);
  if (!fit) return [];
  const value = Math.min(fit.value, Math.floor((valueRoom - 2) / 1.2));
  return [
    ...items,
    ...list.flatMap((temp, i) => {
      const cell = cells[i]!;
      const strip = rect(cell.left, cell.top, cell.width, head);
      const below = rect(cell.left, cell.top + head, cell.width, cell.height - head);
      return [
        band(`chrome.temps.${temp.id}.cell`, cell, bmwColour('pillDim')),
        band(`chrome.temps.${temp.id}.head`, strip, bmwColour('head')),
        label(`chrome.temps.${temp.id}.name`, temp.title, strip.left + 2, centredY(strip, fit.label), strip.width - 4, { size: fit.label, weight: 'Bold', color: bmwColour('ink'), hAlign: 'center' }),
        label(`chrome.temps.${temp.id}.value`, temp.reading.sample, below.left + 2, centredY(below, value), below.width - 4, {
          size: value,
          color: bmwColour('ink'),
          hAlign: 'center',
          bind: temp.reading.bind,
          widest: temp.reading.widest,
        }),
      ];
    }),
  ];
}

/** A column of stacked boxes, as many of `list` as its height holds at the canvas's type. */
function column(name: string, room: Rect, list: readonly { id: string; title: string; reading: Reading }[], s: number): Item[] {
  for (let count = list.length; count > 0; count--) {
    const rooms = down(room, count);
    const type = stackedTypeFor(rooms[0]!, list.slice(0, count).map((r) => ({ title: r.title, widest: r.reading.widest })), { label: Math.round(16 * s), value: Math.round(40 * s) }, 0.6);
    if (type) return list.slice(0, count).map((r, i) => stackedBox(`${name}.${r.id}`, rooms[i]!, r.title, r.reading, type));
  }
  return [];
}

const LAPS = (): readonly { id: string; title: string; reading: Reading }[] => [
  { id: 'predicted', title: 'Lap Predict', reading: { sample: '1:38.402', bind: lapTime(estimatedLap()), widest: LAP_TIME_WIDEST } },
  { id: 'last', title: 'Last Lap', reading: { sample: '1:38.917', bind: lapTime(lastLap()), widest: LAP_TIME_WIDEST } },
  { id: 'best', title: 'Best Lap', reading: { sample: '1:37.884', bind: lapTime(bestLap()), widest: LAP_TIME_WIDEST } },
];

const PLACES = (): readonly { id: string; title: string; reading: Reading }[] => [
  { id: 'position', title: 'Position', reading: { sample: 'P4', bind: positionLabelled(player()), widest: 'P444' } },
  { id: 'ahead', title: 'Gap Ahead', reading: { sample: '-1.204', bind: carRelativeGap(neighbour(-1)), widest: RELATIVE_GAP_WIDEST } },
  { id: 'behind', title: 'Gap Behind', reading: { sample: '+0.866', bind: carRelativeGap(neighbour(1)), widest: RELATIVE_GAP_WIDEST } },
];

export function bmwChrome(ctx: FaceContext): Item[] {
  const face = bmwFace(ctx.layout);
  return [
    ...(face.tiles ? tiles(face.tiles, face.s) : []),
    ...(face.temps ? temps(face.temps, face.s) : []),
    ...(face.laps ? column('chrome.laps', face.laps, LAPS(), face.s) : []),
    ...(face.places ? column('chrome.places', face.places, PLACES(), face.s) : []),
  ];
}
