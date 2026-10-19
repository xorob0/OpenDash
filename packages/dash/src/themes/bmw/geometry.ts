/**
 * The BMW display's rectangles at every face size (#203), from the `After` component of the design
 * canvas, which lays the Bosch display of the M cars out from the face's width and height alone:
 *
 *   - a row of ten round shift lights across the top, 26 px tall at 480 high;
 *   - the body under it in columns 8 px apart: column B, the gear and speed column A, and column C,
 *     in the canvas's shares of 290, 194 and 280 of what the margins leave; a face 1200 wide or wider
 *     adds a column of lap times at the right, and one 1700 wide or wider a column of places at the left;
 *   - in column B, under zone B's box, the four setting tiles and the four temperatures, which a face
 *     shorter than 360 sheds, zone B then taking the whole column;
 *   - the foot along the bottom, 68 px at 480 high, which is band D.
 *
 * The portrait face is the canvas's own board: the gear column and column C side by side across the
 * top, and zone B's box, the tiles and the temperatures under them, full width.
 *
 * Every share is a CSS flex share of the canvas, `flex: n 1 0` with 8 px gaps, and {@link shares}
 * divides a length the way the browser does, so that the build stands where the canvas does.
 *
 * Only rectangles here, because the registry reads the anatomy, and so this file, before `ds` exists.
 */
import type { Rect } from '../../generator.ts';
import { rect, type Size } from '../../design/geometry.ts';
import { FACE_SIZES } from '../../contract.ts';

export const BMW_SIZES: readonly Size[] = FACE_SIZES.map(({ width, height }) => ({ width, height }));

/** The face's margin at the sides and the foot, and the gap between two boxes or two columns. */
export const MARGIN = 10;
export const GAP = 8;

/** Where the shadow of a box falls, right and down, which every box keeps clear inside its rectangle. */
export const SHADOW = { x: 2, y: 4 } as const;

export interface BmwFace {
  width: number;
  height: number;
  /** The canvas's type scale: 1 at 480 high, 0.74 on a face shorter than 360. */
  s: number;
  short: boolean;
  /** The canvas's `tall`: a body deeper than 480, where zone B's box has the two lap times under it. */
  tall: boolean;
  portrait: boolean;
  /** The row the shift lights stand in, and the lights' diameter and pitch. */
  leds: Rect;
  ledSize: number;
  ledGap: number;
  body: Rect;
  foot: Rect;
  /** Zone B's box, at the top of column B. */
  zoneB: Rect;
  /** The gear and speed column. */
  zoneA: Rect;
  /** Column C. */
  zoneC: Rect;
  /** The row of setting tiles and the row of temperatures under zone B, absent on a short face. */
  tiles?: Rect;
  temps?: Rect;
  /** The column of lap times at the right of a wide face, and the column of places at the left of the widest. */
  laps?: Rect;
  places?: Rect;
}

export interface Span {
  start: number;
  size: number;
}

/**
 * `length` divided as a flex container with `gap` between its children divides it among children of
 * `flex: n 1 0`: each its share of what the gaps leave, the edges rounded where they fall so that the
 * pieces and the gaps add up to the whole.
 */
export function shares(start: number, length: number, flex: readonly number[], gap = GAP): Span[] {
  const total = flex.reduce((a, b) => a + b, 0);
  const free = length - gap * (flex.length - 1);
  let at = 0;
  return flex.map((n, i) => {
    const from = Math.round(start + (free * at) / total + gap * i);
    at += n;
    const to = Math.round(start + (free * at) / total + gap * i);
    return { start: from, size: to - from };
  });
}

/** The portrait board: the gear column 220 wide beside column C over 300 of the body's height, then zone B, the tiles and the temperatures. */
function portrait(width: number, height: number): BmwFace {
  const s = 1;
  const ledH = 26;
  const footH = 60;
  const bodyTop = ledH + 6;
  const body = rect(MARGIN, bodyTop, width - 2 * MARGIN, height - bodyTop - footH - GAP - MARGIN);
  const [top, tyres, tiles, temps] = shares(body.top, body.height, [300, 130, 64, 86]) as [Span, Span, Span, Span];
  const columnA = 220;
  const zoneA = rect(body.left, top.start, columnA, top.size);
  const zoneC = rect(body.left + columnA + GAP, top.start, body.width - columnA - GAP, top.size);
  const zoneB = rect(body.left, tyres.start, body.width, tyres.size);
  const tileRow = rect(body.left, tiles.start, body.width, tiles.size);
  return {
    width,
    height,
    s,
    short: false,
    tall: false,
    portrait: true,
    leds: rect(MARGIN, 0, width - 2 * MARGIN, ledH),
    ledSize: 16,
    ledGap: 18,
    body,
    foot: rect(MARGIN, height - MARGIN - footH, width - 2 * MARGIN, footH),
    zoneB,
    zoneA,
    zoneC,
    tiles: tileRow,
    temps: rect(body.left, temps.start, body.width, temps.size),
  };
}

/** The face of `size`, by the canvas's rules. */
export function bmwFace(size: Size): BmwFace {
  const { width: W, height: H } = size;
  if (H > W) return portrait(W, H);
  const k = Math.min(H / 480, 1.25);
  const short = H < 360;
  const s = short ? 0.74 : k;
  const ledH = Math.round((short ? 22 : 26) * Math.max(k, 0.85));
  const footH = Math.round(68 * (short ? 0.78 : k));
  const bodyTop = ledH + 6;
  const bodyH = H - ledH - footH - 6 - GAP - MARGIN;
  const xC = W >= 1200 ? Math.round(260 * Math.min(W / 1280, 1.15)) : 0;
  const xB = W >= 1700 ? 290 : 0;
  const gaps = 2 + (xC ? 1 : 0) + (xB ? 1 : 0);
  const avail = W - 2 * MARGIN - gaps * GAP - xC - xB;
  const cB = Math.round((avail * 290) / 764);
  const cA = Math.round((avail * 194) / 764);
  const cC = avail - cB - cA;
  const ledSize = Math.round(16 * Math.max(k, 0.85));

  let x = MARGIN;
  const column = (w: number): Rect => {
    const r = rect(x, bodyTop, w, bodyH);
    x += w + GAP;
    return r;
  };
  const places = xB ? column(xB) : undefined;
  const columnB = column(cB);
  const zoneA = column(cA);
  const zoneC = column(cC);
  const laps = xC ? column(xC) : undefined;

  const tall = bodyH > 480;
  let zoneB = columnB;
  let tiles: Rect | undefined;
  let temps: Rect | undefined;
  if (!short) {
    // The canvas's column B: zone B's box 160, on a tall face the two lap times' 160 under it, then
    // the tiles' 84 and the temperatures' 104. Zone B is the box and the lap times together.
    const rows = shares(bodyTop, bodyH, tall ? [160, 160, 84, 104] : [160, 84, 104]);
    const [t, p] = rows.slice(-2) as [Span, Span];
    zoneB = rect(columnB.left, bodyTop, cB, t.start - GAP - bodyTop);
    tiles = rect(columnB.left, t.start, cB, t.size);
    temps = rect(columnB.left, p.start, cB, p.size);
  }

  return {
    width: W,
    height: H,
    s,
    short,
    tall,
    portrait: false,
    leds: rect(MARGIN, 0, W - 2 * MARGIN, ledH),
    ledSize,
    ledGap: Math.round(ledSize * 1.15),
    body: rect(MARGIN, bodyTop, W - 2 * MARGIN, bodyH),
    foot: rect(MARGIN, H - MARGIN - footH, W - 2 * MARGIN, footH),
    zoneB,
    zoneA,
    zoneC,
    tiles,
    temps,
    laps,
    places,
  };
}

/** The ten lights' row, centred in the row they stand in. */
export function ledRow(face: BmwFace): Rect {
  const width = 10 * face.ledSize + 9 * face.ledGap;
  return rect(Math.round(face.leds.left + (face.leds.width - width) / 2), Math.round(face.leds.top + (face.leds.height - face.ledSize) / 2), width, face.ledSize);
}

/** The face a band of this frame is the foot of: the band names its face by its width and height. */
export function bmwFaceForFoot(width: number, height: number): BmwFace {
  const face = BMW_SIZES.map(bmwFace).find((f) => f.foot.width === width && f.foot.height === height);
  if (!face) throw new Error(`bmw: no face has a ${width} x ${height} foot`);
  return face;
}

/**
 * The face a zone of this frame belongs to: a zone dashboard is drawn at its zone's size, so its width
 * and height name the face, the first face whose zone A, B or C is that size. With the rev bar off the
 * zones that start the body grow up into the lights' row (`regionsWithoutRevBar`), and such a zone
 * names its face too, `lift` being what it grew by, which a page leaves empty as the dark row it is.
 */
export function bmwFaceForZone(width: number, height: number): { face: BmwFace; lift: number } | undefined {
  for (const face of BMW_SIZES.map(bmwFace)) {
    const zones = [face.zoneA, face.zoneB, face.zoneC];
    const bodyTop = Math.min(...zones.map((z) => z.top));
    const reclaim = bodyTop - face.leds.top;
    if (zones.some((z) => z.width === width && z.height === height)) return { face, lift: 0 };
    if (zones.some((z) => z.top === bodyTop && z.width === width && z.height + reclaim === height)) return { face, lift: reclaim };
  }
  return undefined;
}

export interface RaceRows {
  top: Rect;
  fuel?: Rect;
  /** The time difference and the lap time together, which a change notification covers. */
  lower: Rect;
  diff: Rect;
  lap: Rect;
}

/**
 * Column C's rows on the race page: the revs and the brake balance, the fuel where the face is tall
 * enough, and the time difference over the lap time, which a change notification covers together.
 */
export function raceRows(frame: Rect, scale: { short: boolean; portrait: boolean }): RaceRows {
  const at = (r: { start: number; size: number }): Rect => rect(frame.left, r.start, frame.width, r.size);
  if (scale.portrait) {
    const [top, diff, lapRow] = shares(frame.top, frame.height, [92, 82, 90]).map(at) as [Rect, Rect, Rect];
    return { top, lower: rect(frame.left, diff.top, frame.width, lapRow.top + lapRow.height - diff.top), diff, lap: lapRow };
  }
  const rows = shares(frame.top, frame.height, scale.short ? [92, 172] : [92, 76, 172]).map(at);
  const lower = rows[rows.length - 1]!;
  const [diff, lapRow] = shares(lower.top, lower.height, [82, 90]).map(at) as [Rect, Rect];
  return { top: rows[0]!, fuel: scale.short ? undefined : rows[1], lower, diff, lap: lapRow };
}
