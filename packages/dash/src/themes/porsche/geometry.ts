/**
 * The Porsche drawing at every face size, as numbers: the rules of #205's "Other sizes" and of
 * `gen_sizes.py` in its first comment, which drew the size boards on the canvas, written as one
 * function of the face's width and height.
 *
 * The 1280 x 480 face is the reference, the car's own 8:3, and its numbers are the ticket's
 * geometry table. The others follow from it:
 *
 *   - **1280 x 400** keeps every x and scales the strip, the body and the foot as 60 : 226 : 142.
 *   - **1280 x 720** keeps the strip and the foot and gives the body the rest, where the columns
 *     and the modules go deeper rather than larger.
 *   - **1920 x 480** keeps every height and widens zones B and C equally, the settings column, the
 *     gear tile and the tyre box keeping their widths.
 *   - **850 x 480 and 800 x 480** drop the settings column and the badge, start zone B at x 9 and
 *     share the body 315 : 283 : 350, as the squarer Porsche screens do.
 *   - **800 x 286** shows two pictograms in the telltale column and a foot of the tyre box, its
 *     pressures in one row, and the brake bias.
 *   - **600 x 686**, the portrait face, stacks the car's elements: the strip, a full-width gear
 *     tile, zones B and C side by side, then the tyre box beside the brake bias and the TC and ABS
 *     boxes. No Porsche display is portrait; this arrangement is openDash's own.
 *
 * **The one rule the sizes taught.** A font follows the box it is drawn in, never a global scale for
 * the face. The boxes of the strip and the foot are scaled by the face's `scale`, which is the
 * board's figure for that size, and their text with them; the readings in zones B and C stay at the
 * register's 23 and 32 px at every size and the zone sheds lines rather than shrinking them. A
 * column stacks its boxes from the top at a fixed gap and shows as many as fit; it never spreads them.
 *
 * Only rectangles here, because the anatomy reads this file before `ds` exists.
 */
import { rect, type Rect, type Size } from '../../design/geometry.ts';

/** The border every panel, tile and outlined box is drawn with, which a zone sits inside. */
const BORDER = 3;

/** The settings column down the left edge. */
export interface SettingsColumn {
  left: number;
  width: number;
  /** One box's height and the fixed gap between two. */
  box: number;
  gap: number;
  /** The name and the value, and the padding between them and the border. */
  size: number;
  pad: number;
}

/** The telltale column down the right edge: its pictograms stacked from the top at a fixed gap. */
export interface TelltaleColumn {
  left: number;
  width: number;
  /** A pictogram's height, the gap between two, and the inset from the body's ends. */
  icon: number;
  gap: number;
  inset: number;
  /** The most it shows on this face, before what the height holds. */
  most: number;
}

/** One of the strip's boxes: its x, its size and how far below the strip's centre it sits. */
export interface StripBox {
  left: number;
  width: number;
  height: number;
  /** Below the strip's centre, as the canvas sets each box a pixel or two low. */
  drop: number;
}

/** The strip's parts, laid along the `bar` region; each is centred on its height. */
export interface StripParts {
  page: { left: number; size: number };
  headlight: StripBox;
  speed: StripBox & { size: number };
  lap: StripBox & { label: number; value: number; pad: number; title: number };
  track: StripBox & { size: number };
}

/** A coloured setting box of the foot, with the size of its text. */
export interface FootBox {
  rect: Rect;
  size: number;
  pad: number;
}

/** The foot, in the band's own frame: x from the face's left edge, y from the band's top. */
export interface FootParts {
  badge?: Rect;
  tcLa?: FootBox;
  tcLo?: FootBox;
  abs?: FootBox;
  tyre: { rect: Rect; oneRow: boolean };
  bias: { rect: Rect; label: number; value: number; valueWidth: number; pad: number };
}

export interface PorscheFace {
  width: number;
  height: number;
  /** The board's scale for the strip's and the foot's boxes on this face. */
  scale: number;
  /** The row of sixteen dots. */
  dots: Rect;
  strip: Rect;
  /** The outer edges of the three body panels: zone B's grey outline, the gear's tile, zone C's outline. */
  panels: { B: Rect; A: Rect; C: Rect };
  /** The body the limiter takes, from zone B's outline to zone C's. */
  body: Rect;
  foot: Rect;
  settings?: SettingsColumn;
  telltales?: TelltaleColumn;
  parts: StripParts;
  footParts: FootParts;
}

/** Every size the theme draws, each one of `FACE_SIZES`. */
export const PORSCHE_SIZES: readonly Size[] = [
  { width: 1920, height: 480 },
  { width: 1280, height: 480 },
  { width: 1280, height: 400 },
  { width: 850, height: 480 },
  { width: 800, height: 480 },
  { width: 1280, height: 720 },
  { width: 800, height: 286 },
  { width: 600, height: 686 },
];

/**
 * The landscape faces' figures, from `gen_sizes.py`'s table: the scale of the strip's and the foot's
 * boxes, whether the settings column and the badge are drawn, whether zones B and C widen into two
 * columns, and how many pictograms the telltale column may show before its height decides.
 */
const LANDSCAPE: Readonly<Record<string, { scale: number; settings: boolean; wide: boolean; telltales: number }>> = {
  '1920x480': { scale: 1, settings: true, wide: true, telltales: 8 },
  '1280x480': { scale: 1, settings: true, wide: false, telltales: 8 },
  '1280x400': { scale: 0.9, settings: true, wide: false, telltales: 8 },
  '1280x720': { scale: 1, settings: true, wide: false, telltales: 8 },
  '850x480': { scale: 0.92, settings: false, wide: false, telltales: 8 },
  '800x480': { scale: 0.9, settings: false, wide: false, telltales: 8 },
  // The nano's column is two pictograms, which is the ticket's number rather than what its height holds.
  '800x286': { scale: 0.66, settings: false, wide: false, telltales: 2 },
};

const named = (size: Size): string => `${size.width}x${size.height}`;

/** A rect from its four edges, each rounded, so that two neighbours rounded apart never overlap. */
const between = (left: number, top: number, right: number, bottom: number): Rect => {
  const l = Math.round(left);
  const t = Math.round(top);
  return rect(l, t, Math.round(right) - l, Math.round(bottom) - t);
};

/** The dots' row: sixteen dots of `size`, `gap` apart, centred on the face. */
const dotsRow = (width: number, top: number, size: number, gap: number): Rect => {
  const span = 16 * size + 15 * gap;
  return rect(Math.round((width - span) / 2), top, span, size);
};

/** The landscape faces, by the rules in the file comment. */
function landscape(size: Size): PorscheFace {
  const { width: W, height: H } = size;
  const figures = LANDSCAPE[named(size)];
  if (!figures) throw new Error(`porsche: no drawing at ${named(size)}`);
  const { scale: fs, settings, wide } = figures;
  const s = (n: number): number => Math.round(n * fs);

  // Vertical: the dots, then the strip, the body and the foot with a fixed gap between them.
  const small = H <= 286;
  const dotSize = small ? 13 : 15;
  const dotTop = small ? 3 : 4;
  const stripTop = dotTop + dotSize + (small ? 8 : 9);
  const gap = small ? 6 : 8;
  let stripH: number;
  let bodyH: number;
  let footH: number;
  if (small) {
    stripH = 42;
    footH = 60;
    bodyH = H - stripTop - stripH - 3 * gap - footH;
  } else if (H < 480) {
    const k = (H - stripTop - 3 * gap) / 428;
    stripH = Math.round(60 * k);
    bodyH = Math.round(226 * k);
    footH = H - stripTop - stripH - bodyH - 3 * gap;
  } else {
    stripH = 60;
    footH = 142;
    bodyH = H - stripTop - stripH - 3 * gap - footH;
  }
  const bodyTop = stripTop + stripH + gap;
  const footTop = bodyTop + bodyH + gap;

  // Horizontal: the settings column, zones B, A and C, and the telltale column. Every 1280 face keeps
  // the reference's x, so the column is its 46 px there whatever the face's scale.
  const tellW = W >= 1280 ? 46 : s(46);
  const tellX = W - (W >= 1280 ? 87 : 40) - tellW;
  const bodyLeft = 9 + (settings ? 146 + 8 : 0);
  const bodyRight = tellX - (W >= 1280 ? 27 : 16);
  const bw = bodyRight - bodyLeft - 9;
  const [B, A, C] = wide ? [315 + (bw - 948) / 2, 283, 350 + (bw - 948) / 2] : [(bw * 315) / 948, (bw * 283) / 948, (bw * 350) / 948];
  const bEnd = bodyLeft + B;
  const aLeft = bEnd + 5;
  const aEnd = aLeft + A;
  const cLeft = aEnd + 4;
  const panels = {
    B: between(bodyLeft, bodyTop, bEnd, bodyTop + bodyH),
    A: between(aLeft, bodyTop, aEnd, bodyTop + bodyH),
    C: between(cLeft, bodyTop, cLeft + C, bodyTop + bodyH),
  };

  const strip = rect(0, stripTop, W, stripH);
  const lapWidth = s(178);
  const trackWidth = s(119);
  const parts: StripParts = {
    page: { left: s(41), size: s(24) },
    headlight: { left: s(174), width: s(50), height: s(32), drop: 0 },
    speed: { left: panels.A.left, width: panels.A.width, height: s(58), drop: fs, size: s(40) },
    lap: { left: panels.C.left + s(34), width: lapWidth, height: s(46), drop: fs, label: s(23), value: s(26), pad: s(14), title: s(64) },
    track: { left: W - 39 - trackWidth, width: trackWidth, height: s(56), drop: 2 * fs, size: s(27) },
  };

  return {
    width: W,
    height: H,
    scale: fs,
    dots: dotsRow(W, dotTop, dotSize, s(22)),
    strip,
    panels,
    body: rect(panels.B.left, bodyTop, panels.C.left + panels.C.width - panels.B.left, bodyH),
    foot: rect(0, footTop, W, footH),
    settings: settings ? { left: 9, width: 146, box: Math.round(48 * Math.min(1, bodyH / 226)), gap: s(10), size: s(23), pad: s(14) } : undefined,
    telltales: { left: tellX, width: tellW, icon: s(32), gap: s(26), inset: 8, most: figures.telltales },
    parts,
    footParts: small ? compactFoot(panels, footH, fs) : fullFoot(panels, footH, fs, settings),
  };
}

/**
 * The foot of a landscape face: the badge where the settings column is, the TC pair over ABS under
 * zone B, the tyre box under the gear and the brake bias under zone C. The reference's ABS box is
 * 236 and 153 wide under a pair of 151, which is kept as a proportion of the pair's width.
 */
function fullFoot(panels: PorscheFace['panels'], footH: number, fs: number, badge: boolean): FootParts {
  const s = (n: number): number => Math.round(n * fs);
  const v = footH / 142;
  const boxH = Math.round(56 * v);
  const tw = Math.min(s(151), Math.floor((panels.B.width - 7) / 2));
  const tcLeft = panels.B.left - BORDER;
  const box = (left: number, top: number, width: number): FootBox => ({ rect: rect(left, top, width, boxH), size: s(23), pad: s(14) });
  const badgeW = s(54);
  const badgeH = s(62);
  const biasH = s(50);
  return {
    // At the foot of the band, where `foot.ts` draws it on the reference.
    badge: badge ? rect(14, footH - badgeH, badgeW, badgeH) : undefined,
    tcLa: box(tcLeft, 0, tw),
    tcLo: box(tcLeft + tw + 7, 0, tw),
    abs: box(tcLeft + Math.round((76 * tw) / 151), Math.round(66 * v), Math.round((153 * tw) / 151)),
    tyre: { rect: rect(panels.A.left - 8, 0, panels.A.width + 8, footH), oneRow: false },
    bias: { rect: rect(panels.C.left + 18, Math.round(54 * v), Math.min(s(309), panels.C.width - 36), biasH), label: s(23), value: s(30), valueWidth: s(120), pad: s(14) },
  };
}

/** The nano's foot: the tyre box with its pressures in one row, and the brake bias beside it. */
function compactFoot(panels: PorscheFace['panels'], footH: number, fs: number): FootParts {
  const s = (n: number): number => Math.round(n * fs);
  const biasH = s(44);
  return {
    tyre: { rect: rect(panels.A.left - 8, 0, panels.A.width + 8, footH), oneRow: true },
    bias: { rect: rect(panels.C.left + 10, Math.round((footH - biasH) / 2), Math.min(s(300), panels.C.width - 20), biasH), label: s(23), value: s(30), valueWidth: s(120), pad: s(14) },
  };
}

/**
 * The portrait face, read off its board: the strip, the gear's tile across the width, zones B and
 * C side by side under it, and the foot of the tyre box beside the bias over the TC pair and ABS.
 * No settings column, no telltale column and no badge.
 */
function portrait(): PorscheFace {
  const W = 600;
  const footTop = 528;
  const fs = 0.8;
  const box = (left: number, top: number, width: number, height: number): FootBox => ({ rect: rect(left, top, width, height), size: 18, pad: 11 });
  return {
    width: W,
    height: 686,
    scale: fs,
    dots: dotsRow(W, 4, 13, 14),
    strip: rect(0, 28, W, 56),
    panels: { A: rect(9, 92, 582, 170), B: rect(9, 270, 287, 250), C: rect(304, 270, 287, 250) },
    body: rect(9, 92, 582, 428),
    foot: rect(0, footTop, W, 150),
    parts: {
      page: { left: 16, size: 20 },
      headlight: { left: 100, width: 40, height: 26, drop: 1 },
      speed: { left: 150, width: 180, height: 50, drop: 0, size: 34 },
      lap: { left: 340, width: 130, height: 42, drop: 0, label: 20, value: 22, pad: 12, title: 48 },
      track: { left: 500, width: 91, height: 48, drop: 0, size: 22 },
    },
    footParts: {
      tcLa: box(308, 54, 138, 46),
      tcLo: box(453, 54, 138, 46),
      abs: box(380, 106, 138, 44),
      tyre: { rect: rect(9, 0, 291, 150), oneRow: false },
      bias: { rect: rect(308, 0, 283, 46), label: 18, value: 24, valueWidth: 96, pad: 11 },
    },
  };
}

const FACES = new Map<string, PorscheFace>();

/** The Porsche drawing at one of {@link PORSCHE_SIZES}, refusing any other. */
export function porscheFace(size: Size): PorscheFace {
  const key = named(size);
  const known = FACES.get(key);
  if (known) return known;
  if (!PORSCHE_SIZES.some((s) => named(s) === key)) throw new Error(`porsche: no drawing at ${key}; it draws ${PORSCHE_SIZES.map(named).join(', ')}`);
  const face = size.width === 600 && size.height === 686 ? portrait() : landscape(size);
  FACES.set(key, face);
  return face;
}

/**
 * The face whose foot is a band of this size, which is all a page of band D is handed. The two
 * 1280 faces of 480 and 720 share one foot, which is the point of keeping the foot's height.
 */
export function porscheFaceForFoot(width: number, height: number): PorscheFace {
  const face = PORSCHE_SIZES.map(porscheFace).find((f) => f.foot.width === width && f.foot.height === height);
  if (!face) throw new Error(`porsche: no face has a ${width} x ${height} foot`);
  return face;
}

/** A zone fills the inside of its panel's border, so that its cells stand flush against it. */
export const insideBorder = (panel: Rect): Rect => rect(panel.left + BORDER, panel.top + BORDER, panel.width - 2 * BORDER, panel.height - 2 * BORDER);
