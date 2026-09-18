/**
 * alertBand: the shapes a band on the face is allowed to take, and there are three of them.
 *
 * The canvas states the rule for the whole alert catalogue — "the only shapes are bands, outlined
 * bands and two patterns; nothing else on the face is allowed to compete with them" — and this file
 * is the first three of those four. A filled bar, a bar outlined in the alert's colour, and the
 * chequer. The second pattern, the debris flag's danger stripes, is not drawn, and
 * docs/design/flag-box.md says so beside the condition.
 *
 * Which condition takes which shape is not decided here: `AlertBandSpec` in flags.ts carries it
 * beside the condition, and `flagStrip.ts` joins the two. A shape is therefore a function of a
 * rectangle, a colour and a name, and knows nothing about flags.
 *
 * Every shape is opaque over the whole rectangle. A flag takes band D over precisely so that the
 * page underneath cannot be read, so an outlined band lays `surface.base` under its border rather
 * than leaving the ground transparent, and a flashing band alternates two opaque things rather than
 * blinking itself away.
 */
import type { FontWeight, Hex, Item, Rect, RectangleItem, TextItem } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { ds } from '../tokens.ts';

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
  /** Draw "YELLOW FLAG" and the like centred on the band. */
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
