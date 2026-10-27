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
import { hasSetting, LEVEL_WIDEST, trackedText, trackedValue } from '../../second/tracked.ts';
import {
  airTemperature, antiRollFront, bestLap, carRelativeGap, estimatedLap, lapTime, lapTimeWidest, lastLap, neighbour, NO_VALUE, oilTemperature, player,
  positionLabelled, RELATIVE_GAP_WIDEST, roadTemperature, waterTemperature,
} from '../../second/values.ts';
import type { FaceContext } from '../drawing.ts';
import { bmwFace, shares } from './geometry.ts';
import { bmwColour, box, caps, centredY, FILLED_SHADOW, fitSize, lineY, readingBox, runWidth, sentence, stackedRuns, type Reading, type Writing } from './register.ts';

const { eq, fmt, iff, isNull, isnull, not, num, str } = ncalc;

/** The tiles in the car's order, each with its colour and what it shows. */
const TILES = (): readonly { id: string; title: string; colour: Hex; reading: Reading; present: Expr }[] => {
  const tracked = (id: string): Reading & { present: Expr } => {
    const value = trackedValue(id);
    return { sample: value.sample, bind: trackedText(value), widest: value.widest, present: hasSetting(value) };
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

/** `count` rooms side by side across `row`, `gap` apart, as a grid of equal columns lays them. */
export function across(row: Rect, count: number, gap: number): Rect[] {
  return shares(row.left, row.width, Array(count).fill(1), gap).map((c) => rect(c.start, row.top, c.size, row.height));
}

/** The room a grid's cells may cast their shadows into: the row, and the gap after it on the face. */
const spill = (row: Rect): Rect => rect(row.left, row.top, row.width + FILLED_SHADOW.x, row.height + FILLED_SHADOW.y);

/**
 * The tiles, 5 px apart: each its colour lit from above, its name in bold over its figure, or beside it
 * on the portrait board, whose tiles are a strip.
 */
function tiles(row: Rect, s: number, portrait: boolean): Item[] {
  const list = TILES();
  const rooms = across(row, list.length, 5);
  const cell = rooms[0]!;
  const name = fitSize(Math.round(15 * s), portrait ? cell.width / 3 : cell.width - 4, list.map((t) => t.title), true);
  const value = Math.min(fitSize(Math.round(40 * s), portrait ? cell.width / 2 : cell.width - 4, list.map((t) => t.reading.widest)), Math.floor((cell.height - (portrait ? 0 : name * 1.1)) / 1.05));
  // A setting the sim does not report keeps its place as an unlit tile, as the foot's pills do, so
  // that the row reads the same in every car rather than closing up round a gap.
  return list.flatMap((tile, i) => {
    const room = rooms[i]!;
    const draw = (id: string, fill: Hex, ink: Hex, reading: Reading, visible: Expr): Item[] => {
      const { items, inner } = box(`chrome.tiles.${id}`, room, { fill, visible, within: spill(row) });
      if (!portrait) {
        return [...items, ...stackedRuns(`chrome.tiles.${id}`, inner, [
          { id: 'name', text: tile.title, size: name, bold: true, lh: 1.1 },
          { id: 'value', text: reading.sample, size: value, lh: 1, bind: reading.bind, widest: reading.widest },
        ], ink, visible)];
      }
      // Beside each other, 14 px apart, the pair centred across the tile.
      const nameWidth = runWidth(tile.title, name, true);
      const valueWidth = runWidth(reading.widest, value);
      const left = Math.round(inner.left + (inner.width - nameWidth - 14 - valueWidth) / 2);
      return [
        ...items,
        label(`chrome.tiles.${id}.name`, tile.title, left, centredY(inner, name), nameWidth, { size: name, weight: 'Bold', color: ink, visibleBind: visible }),
        label(`chrome.tiles.${id}.value`, reading.sample, left + nameWidth + 14, centredY(inner, value, 1), valueWidth, { size: value, color: ink, bind: reading.bind, widest: reading.widest, visibleBind: visible }),
      ];
    };
    return [
      ...draw(`${tile.id}.dim`, bmwColour('pillDim'), bmwColour('pillInk'), { sample: NO_VALUE, widest: tile.reading.widest }, not(tile.present)),
      ...draw(tile.id, tile.colour, bmwColour('tileInk'), tile.reading, tile.present),
    ];
  });
}

/** The temperatures' box: four cells 4 px apart, each a header strip with its name over the value at the right. */
function temps(room: Rect, s: number, portrait: boolean): Item[] {
  const list = TEMPS();
  const { items, inner } = box('chrome.temps', room);
  const grid = rect(inner.left + 4, inner.top + 4, inner.width - 8, inner.height - 8);
  const cells = across(grid, list.length, 4);
  const cell = cells[0]!;
  const nameSize = fitSize(Math.round(16 * s), cell.width - 4, list.map((t) => t.title), true);
  const head = Math.round(nameSize * 1.5);
  const padRight = portrait ? 6 : 2;
  const value = Math.min(fitSize(Math.round(40 * s), cell.width - padRight - 2, list.map((t) => t.reading.widest)), Math.floor((cell.height - head) / 1.2));
  return [
    ...items,
    ...list.flatMap((temp, i) => {
      const c = cells[i]!;
      const lit = box(`chrome.temps.${temp.id}`, c, { fill: bmwColour('pillDim'), within: inner });
      const strip = rect(c.left, c.top, c.width, head);
      const below = rect(c.left, c.top + head, c.width, c.height - head);
      return [
        ...lit.items,
        band(`chrome.temps.${temp.id}.head`, strip, bmwColour('head')),
        label(`chrome.temps.${temp.id}.name`, temp.title, strip.left + 2, lineY(strip.top, nameSize, 1.5), strip.width - 4, { size: nameSize, weight: 'Bold', color: bmwColour('ink'), hAlign: 'center' }),
        label(`chrome.temps.${temp.id}.value`, temp.reading.sample, below.left + 2, centredY(below, value), below.width - 2 - padRight, {
          size: value,
          color: bmwColour('ink'),
          hAlign: 'right',
          bind: temp.reading.bind,
          widest: temp.reading.widest,
        }),
      ];
    }),
  ];
}

/** A column of boxes, flex 1 each and 8 px apart, in one writing. */
function column(name: string, room: Rect, list: readonly { id: string; title: string; reading: Reading }[], writing: Writing): Item[] {
  return shares(room.top, room.height, list.map(() => 1)).map((r, i) => readingBox(`${name}.${list[i]!.id}`, rect(room.left, r.start, room.width, r.size), list[i]!.title, list[i]!.reading, writing));
}

const LAP_WIDEST = lapTimeWidest(2);

const LAPS = (): readonly { id: string; title: string; reading: Reading }[] => [
  { id: 'predicted', title: 'Lap Predict', reading: { sample: '1:37.61', bind: lapTime(estimatedLap(), 2), widest: LAP_WIDEST } },
  { id: 'last', title: 'Last Lap', reading: { sample: '1:37.90', bind: lapTime(lastLap(), 2), widest: LAP_WIDEST } },
  { id: 'best', title: 'Best Lap', reading: { sample: '1:37.42', bind: lapTime(bestLap(), 2), widest: LAP_WIDEST } },
];

const PLACES = (): readonly { id: string; title: string; reading: Reading }[] => [
  { id: 'position', title: 'POS', reading: { sample: 'P4', bind: positionLabelled(player()), widest: 'P444' } },
  { id: 'ahead', title: 'AHEAD', reading: { sample: '+1.24', bind: carRelativeGap(neighbour(-1)), widest: RELATIVE_GAP_WIDEST } },
  { id: 'behind', title: 'BEHIND', reading: { sample: '-0.81', bind: carRelativeGap(neighbour(1)), widest: RELATIVE_GAP_WIDEST } },
];

export function bmwChrome(ctx: FaceContext): Item[] {
  const face = bmwFace(ctx.layout);
  const lab = Math.round(16 * face.s);
  const val = Math.round(40 * face.s);
  return [
    ...(face.tiles ? tiles(face.tiles, face.s, face.portrait) : []),
    ...(face.temps ? temps(face.temps, face.s, face.portrait) : []),
    ...(face.laps ? column('chrome.laps', face.laps, LAPS(), sentence(lab, val, 'center')) : []),
    ...(face.places ? column('chrome.places', face.places, PLACES(), caps(lab, val)) : []),
  ];
}
