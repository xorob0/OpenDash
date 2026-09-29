/**
 * alertBand: the shapes a band on the face is allowed to take, and there are five of them.
 *
 * The canvas states the rule for the whole alert catalogue — "the only shapes are bands, outlined
 * bands and two patterns; nothing else on the face is allowed to compete with them" — and this file
 * is all four of those and one more. A filled bar, a bar outlined in the alert's colour, the chequer,
 * the debris flag's red stripes over its yellow, and the meatball's orange disc on the near-black.
 *
 * The stripes were the one shape left out, on the grounds that the name carries the difference, and
 * #498 reversed that: the name is written only where there is width for it, so on the nano's strip,
 * in the sixteen pixels a settled flag keeps on a face with no corner block and on the LED strip, a
 * debris flag was a yellow flag. They are vertical where the canvas draws them at 135 degrees, which
 * is how the real flag is made and needs nothing SimHub is not known to draw: a diagonal stripe is a
 * rotated rectangle clipped to the band, and docs/research/simhub-dash-format.md establishes neither.
 *
 * The disc is the one shape the canvas's rule does not have, and it is the author's ruling on #498
 * rather than a reading of the canvas: the meatball is a black box with an orange disc in the middle
 * and no text. It is drawn with SimHub's own ellipse, which the round faces' rings already use, so it
 * needs nothing more of the format than the stripes do.
 *
 * Which condition takes which shape is not decided here: `AlertBandSpec` in flags.ts carries it
 * beside the condition, and `flagStrip.ts` joins the two. A shape is therefore a function of a
 * rectangle, a colour and a name, and knows nothing about flags.
 *
 * Every shape is opaque over the whole rectangle. A flag takes band D over precisely so that the
 * page underneath cannot be read, so an outlined band lays `surface.base` under its border rather
 * than leaving the ground transparent, the disc lays it under the disc, and a flashing band
 * alternates two opaque things rather than blinking itself away.
 */
import type { EllipseItem, FontWeight, Hex, Item, Rect, RectangleItem, TextItem } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

/**
 * The band's border, which every artboard draws on every state as `border: 3px solid`, counted
 * inside the box. A filled band draws it in its own fill, where the artboards leave it transparent
 * over that same fill, which is the same three pixels of that colour; an outlined band draws it in
 * the alert's colour, because a dark band on a dark dash needs an edge to read as a band.
 */
export const ALERT_BAND_BORDER = 3;

/** The weight the artboards set the alert's name in, against the 500 of every other label. */
export const ALERT_NAME_WEIGHT: FontWeight = 'Bold';

/** Half period of a flashing band in ms (250 at 2 Hz). */
export const ALERT_FLASH_MS = Math.round(1000 / ds.indicator.flagBand.flashHz / 2);

/** How a band is dressed: whether the alert's name is drawn on it, and how thick an outline is. */
export interface AlertBandStyle {
  /** Draw "YELLOW" and the like centred on the band. */
  labels: boolean;
  /** Border of an outlined band. A filled band always carries the artboards' three pixels. */
  outline: number;
}

export const ALERT_BAND_STYLES = {
  standard: { labels: true, outline: ALERT_BAND_BORDER },
  /** The nano's 12 px strip: colour only, 2 px outline (from the canvas). */
  nano: { labels: false, outline: 2 },
} as const satisfies Record<string, AlertBandStyle>;

const labelY = (frame: Rect): number => frame.top + (frame.height - ds.size.label) / 2;

/**
 * The centred name a band carries, whether it is the alert's own or a longer run standing in for
 * it.
 *
 * Exported because one band writes more than its name: the blue flag can name the car behind, and
 * `flagStrip.ts` draws that as further runs over this same line box rather than as a second box
 * somewhere else on the strip. Keeping the geometry here is what stops the two drifting apart, and
 * this file still knows nothing about which alert asks for it.
 *
 * It is the one label on a face drawn in Bold rather than Medium, which is what every FaceVariants
 * sheet sets it in: the band is read at a glance and from further away than a field label is.
 */
export const alertBandName = (name: string, frame: Rect, text: string, color: Hex, opts: { bind?: Expr; widest?: string; visibleBind?: Expr } = {}): TextItem =>
  label(name, text, frame.left, labelY(frame), frame.width, { color, hAlign: 'center', weight: ALERT_NAME_WEIGHT, ...opts });

/** The centred name, when the style draws one. */
const alertName = (frame: Rect, style: AlertBandStyle, name: string, text: string, color: Hex): Item[] =>
  style.labels ? [alertBandName(name, frame, text, color)] : [];

/**
 * The off phase of a flashing band: the face's own ground, opaque, laid inside the border so that
 * the band keeps its edge through the phase it is dark in.
 *
 * The flash was `blink` on the layer, which serialises as `BlinkEnabled` on the group, so for half
 * of every cycle nothing of the band was drawn and band D's fuel page read through a waved yellow.
 * So the ground and the name stay put and an opaque band flashes over them at the same 2 Hz: the
 * state alternates between two things rather than between a thing and the page.
 */
const flashBand = (name: string, frame: Rect): RectangleItem => {
  const inset = ALERT_BAND_BORDER;
  return {
    ...band(name, rect(frame.left + inset, frame.top + inset, frame.width - 2 * inset, frame.height - 2 * inset), ds.color.surface.base),
    blink: { enabled: true, delayMs: ALERT_FLASH_MS },
  };
};

/** A band filled with the alert's colour, named in `purpose.flag.onFlag`, flashing where it flashes. */
export function filledBand(name: string, frame: Rect, style: AlertBandStyle, colour: Hex, text: string, flash = false): Item[] {
  return [
    band(`${name}.band`, frame, colour, { border: { color: colour, width: ALERT_BAND_BORDER } }),
    ...alertName(frame, style, `${name}.label`, text, ds.purpose.flag.onFlag),
    ...(flash ? [flashBand(`${name}.flash`, frame)] : []),
  ];
}

/**
 * A band outlined and named in the alert's colour over an opaque ground.
 *
 * It is the generalisation of what the black flag alone used to be, and the reason it exists is
 * that `purpose.flag.black` is `#F5F7FA`, which is the ink rather than the ground: a band filled
 * with it would be indistinguishable from the white flag at `#FFFFFF`, and two states a driver
 * cannot tell apart is a bug whoever chose the colours. The black family is therefore light on dark
 * where the others are dark on light, which is also what a black flag looks like, and the start
 * gantry's green takes the same form so that a gantry light is not read as a green flag.
 *
 * The ground is `surface.base` and never transparent: an outline with nothing behind it leaves band
 * D's page fully readable underneath the most serious thing the band can say.
 */
export function outlinedBand(name: string, frame: Rect, style: AlertBandStyle, colour: Hex, text: string): Item[] {
  return [
    band(`${name}.band`, frame, ds.color.surface.base, { border: { color: colour, width: style.outline } }),
    ...alertName(frame, style, `${name}.label`, text, colour),
  ];
}

/**
 * The chequer, in the phase the canvas draws. `repeating-conic-gradient(#F5F7FA 0 25%, #0A0B0D 0
 * 50%)` sweeps clockwise from twelve o'clock, so the light quadrant of every tile is its top right
 * and the band opens on the ground: row 0 starts one square in, not at the band's left edge.
 *
 * The check is half the band high rather than the canvas's 40 px, which is the one rule that makes
 * the same board correct on a 40 px alert band, on band D's sixty and on the nano's twelve.
 */
export function chequerBand(name: string, frame: Rect): Item[] {
  const check = frame.height / 2;
  const columns = Math.ceil(frame.width / check);
  const children: Item[] = [band(`${name}.band`, frame, ds.color.surface.base)];
  for (let row = 0; row < 2; row++) {
    for (let col = (row + 1) % 2; col < columns; col += 2) {
      const width = Math.min(check, frame.left + frame.width - (frame.left + col * check));
      children.push(band(`${name}.r${row}c${String(col).padStart(2, '0')}`, rect(frame.left + col * check, frame.top + row * check, width, check), ds.purpose.flag.chequer));
    }
  }
  return children;
}

/** The odd count nearest to `n`, and never less than one. */
export const nearestOdd = (n: number): number => Math.max(1, 2 * Math.round((n - 1) / 2) + 1);

/**
 * The debris flag's stripes, as the rectangles laid over its ground: every other one of an odd count
 * of equal vertical stripes, each as near `width` as the frame divides into.
 *
 * Odd, so that the flag opens and closes on its ground and is the same at both ends; and never fewer
 * than three, so that the narrowest rectangle a band is drawn in, the twelve or sixteen pixels a
 * settled flag keeps on a face with no corner block, still carries one stripe between two runs of
 * the ground rather than being the yellow flag. The edges are rounded and the stripes cut between
 * them, as the full-screen chequer cuts its squares, so that no stripe is a pixel wider than another
 * for want of a fraction and the last one ends where the frame does.
 *
 * Exported because the full-screen block draws the same flag at its own scale, and the rule that
 * makes it the same flag is this one rather than the width either passes.
 */
export function stripes(name: string, frame: Rect, width: number, colour: Hex): RectangleItem[] {
  const count = Math.max(3, nearestOdd(frame.width / width));
  const edge = (k: number): number => Math.round(frame.left + (k * frame.width) / count);
  const drawn: RectangleItem[] = [];
  for (let k = 1; k < count; k += 2) drawn.push(band(`${name}.s${String(k).padStart(2, '0')}`, rect(edge(k), frame.top, edge(k + 1) - edge(k), frame.height), colour));
  return drawn;
}

/**
 * The ground a name is written on over the stripes: the flag's own colour, centred on the frame,
 * covering the name's measured width and its line box with `pad` round both, and never outside the
 * frame.
 *
 * Dark ink reads on the red as well as on the yellow, but a word cut by the edge between two colours
 * is read as two shapes before it is read as a word, and a stripe half the band high is narrower than
 * DEBRIS, so the word would always straddle one. The canvas writes the name straight over its
 * stripes, so the plate is an addition to its drawing rather than a reading of it.
 */
export function namePlate(name: string, frame: Rect, textWidth: number, lineTop: number, size: number, pad: number, colour: Hex): RectangleItem {
  const width = Math.min(frame.width, Math.ceil(textWidth) + 2 * pad);
  const top = Math.max(frame.top, Math.floor(lineTop - pad));
  const bottom = Math.min(frame.top + frame.height, Math.ceil(lineTop + size + pad));
  return band(name, rect(frame.left + Math.floor((frame.width - width) / 2), top, width, bottom - top), colour);
}

/**
 * The debris flag: its yellow over the whole rectangle, red stripes across it, and its name, where
 * the style writes one, on a plate of the yellow.
 *
 * A stripe is as wide as the chequer's check on the same band, half the band high, so that the
 * canvas's two patterns are drawn at one scale. It carries no border, as the chequer carries none:
 * the pattern is the flag, and three pixels of yellow over the ends of every stripe would frame it as
 * a yellow band with red marks inside. Nothing here flashes, the waved yellow being the one band
 * that does; it is the lamp that alternates the two colours, `leds/effects.ts`.
 */
export function stripedBand(name: string, frame: Rect, style: AlertBandStyle, colour: Hex, stripe: Hex, text: string): Item[] {
  const named = style.labels
    ? [
        namePlate(`${name}.plate`, frame, measureText('BarlowBold', text, ds.size.label), labelY(frame), ds.size.label, ds.space[2], colour),
        alertBandName(`${name}.label`, frame, text, ds.purpose.flag.onFlag),
      ]
    : [];
  return [band(`${name}.band`, frame, colour), ...stripes(name, frame, frame.height / 2, stripe), ...named];
}

/**
 * The meatball's disc as a fraction of the shorter side of the rectangle it is drawn in: two thirds,
 * which is the flag's own proportion, FIA Appendix H drawing it as a disc 40 cm across on a flag 60 cm
 * by 80.
 *
 * The shorter side rather than the height, because not every rectangle a flag is drawn in is wider
 * than it is high. A settled block on a face with no corner block is sixteen pixels wide and sixty
 * high, where two thirds of the height is a disc of forty, and a full-screen block can be taller than
 * it is wide. On every band the shorter side is the height, so the rule is the flag's proportion
 * there and the box's own limit elsewhere. The 8x8 box fills its panel with the disc instead, since
 * sixty-four pixels have none to spend on the black around it.
 */
export const ALERT_DISC_RATIO = 2 / 3;

/**
 * The disc, centred on the frame and as near two thirds of its shorter side as whole pixels allow.
 *
 * The margin is rounded rather than the diameter, so that the disc sits the same number of pixels
 * from both of the frame's nearer edges rather than half a pixel nearer one of them: sixty pixels of
 * band give a disc of forty with ten above and ten below, and the nano's twelve a disc of eight with
 * two either side. It has no stroke, and its `BackgroundColor` is transparent, since on an ellipse
 * that is the square behind it rather than its fill.
 */
export function alertDisc(name: string, frame: Rect, colour: Hex): EllipseItem {
  const side = Math.min(frame.width, frame.height);
  const diameter = side - 2 * Math.round(((1 - ALERT_DISC_RATIO) * side) / 2);
  return {
    kind: 'ellipse',
    name,
    rect: rect(Math.floor(frame.left + (frame.width - diameter) / 2), Math.floor(frame.top + (frame.height - diameter) / 2), diameter, diameter),
    fillColor: colour,
    strokeColor: TRANSPARENT,
    strokeThickness: 0,
    backgroundColor: TRANSPARENT,
  };
}

/**
 * The meatball: the face's own near-black over the whole rectangle, its orange disc in the middle,
 * and nothing else, which is the black box with an orange disc in it and no text that the author
 * ruled on #498.
 *
 * The ground is `surface.base` and opaque for the reason an outlined band's is, that the page under a
 * flag is not to be read. It carries no border and no name: the flag has neither, and nor does the
 * chequer, the other shape that is its own flag. The same drawing serves every rectangle, the
 * band, a corner block, the nano's strip and the full-screen block, because the disc is sized from
 * the rectangle and there is no name whose size would have to be measured against it.
 */
export function discBand(name: string, frame: Rect, colour: Hex): Item[] {
  return [band(`${name}.band`, frame, ds.color.surface.base), alertDisc(`${name}.disc`, frame, colour)];
}
