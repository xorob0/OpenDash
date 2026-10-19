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

/** The portrait board: the gear column 220 wide beside column C over 300 of the body's height, then zone B, the tiles and the temperatures. */
function portrait(width: number, height: number): BmwFace {
  const s = 1;
  const ledH = 26;
  const footH = 68;
  const bodyTop = ledH + 6;
  const body = rect(MARGIN, bodyTop, width - 2 * MARGIN, height - bodyTop - footH - GAP - MARGIN);
  const share = (body.height - 3 * GAP) / (300 + 130 + 64 + 104);
  const top = Math.round(300 * share);
  const tyres = Math.round(130 * share);
  const tiles = Math.round(64 * share);
  const temps = body.height - 3 * GAP - top - tyres - tiles;
  const columnA = 220;
  let y = body.top;
  const zoneA = rect(body.left, y, columnA, top);
  const zoneC = rect(body.left + columnA + GAP, y, body.width - columnA - GAP, top);
  y += top + GAP;
  const zoneB = rect(body.left, y, body.width, tyres);
  y += tyres + GAP;
  const tileRow = rect(body.left, y, body.width, tiles);
  y += tiles + GAP;
  return {
    width,
    height,
    s,
    short: false,
    leds: rect(MARGIN, 0, width - 2 * MARGIN, ledH),
    ledSize: 16,
    ledGap: 18,
    body,
    foot: rect(MARGIN, height - MARGIN - footH, width - 2 * MARGIN, footH),
    zoneB,
    zoneA,
    zoneC,
    tiles: tileRow,
    temps: rect(body.left, y, body.width, temps),
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

  let zoneB = columnB;
  let tiles: Rect | undefined;
  let temps: Rect | undefined;
  if (!short) {
    // The canvas's shares, 160, 84 and 104; a deep column gives its extra height to zone B and keeps
    // the tiles and the temperatures at the reference's.
    const share = Math.min((bodyH - 2 * GAP) / 348, k);
    const tilesH = Math.round(84 * share);
    const tempsH = Math.round(104 * share);
    const zoneH = bodyH - 2 * GAP - tilesH - tempsH;
    zoneB = rect(columnB.left, bodyTop, cB, zoneH);
    tiles = rect(columnB.left, bodyTop + zoneH + GAP, cB, tilesH);
    temps = rect(columnB.left, bodyTop + zoneH + GAP + tilesH + GAP, cB, tempsH);
  }

  return {
    width: W,
    height: H,
    s,
    short,
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

/** A box's own rectangle in the room it is given, its shadow kept inside the room. */
export const boxIn = (room: Rect): Rect => rect(room.left, room.top, room.width - SHADOW.x, room.height - SHADOW.y);
