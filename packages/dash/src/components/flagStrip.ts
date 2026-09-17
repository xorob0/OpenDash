/**
 * flagStrip: band D's flag, one Layer per condition of `FLAG_CATALOGUE`, sharing the band anatomy
 * and differing in shape, colour, name and behaviour. One shows at a time and nothing is drawn when
 * nothing is raised.
 *
 * It reads the catalogue's bits through `conditionVisible` rather than SimHub's six normalised
 * `Flag_*` properties, which is the whole of what this file is for. Those six are a lossy summary:
 * `Flag_Yellow` folds the standing yellow, the waved yellow and both cautions into one band, and
 * `Flag_Black` is only the `black` bit, so a red flag, a disqualification, a furled black, a
 * meatball, a full-course caution, the debris flag and the start gantry were invisible on the face
 * and visible on the 8x8 box. The face, the box and the pit wall header now rank one list, so the
 * three cannot disagree about which of two live conditions wins.
 *
 * The price is that band D is iRacing's, as the box already was: `SessionFlagsDetails` is a raw
 * iRacing field, so in another sim the band stays dark rather than drawing an approximation of a
 * flag nobody published. `safeBitSet` is what keeps it dark, since a bare read of an absent
 * property is not a boolean.
 *
 * The nano's 12 px strip is too thin for a name, so its style drops the names and thins the
 * outline to 2 px; at that size a debris flag and a yellow are one band, which flags.ts records.
 */
import type { Item, LayerItem, Rect } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { ncalc } from '../generator.ts';
import { ALERT_BAND_STYLES, chequerBand, filledBand, outlinedBand, type AlertBandStyle } from './alertBand.ts';
import { bandRaised, conditionVisible, FACE_FLAG_PRIORITY, FLAG_CATALOGUE, type AlertBandSpec, type FaceFlag, type FlagCondition } from '../flags.ts';

export { ALERT_BAND_BORDER as BLACK_FLAG_BORDER, ALERT_FLASH_MS as FLAG_BLINK_MS, ALERT_NAME_WEIGHT as FLAG_NAME_WEIGHT, ALERT_BAND_STYLES as FLAG_STRIP_STYLES } from './alertBand.ts';
export type { AlertBandStyle as FlagStripStyle } from './alertBand.ts';

const { game, eq, and, num } = ncalc;

/**
 * SimHub flag properties in priority order, taken from `FLAG_CATALOGUE` rather than restated.
 *
 * Band D no longer reads them. What is left on them is the round face's ring, which has one colour
 * and no room for a name, and the pit wall header, which writes the flag's name beside the session:
 * both draw the six SimHub normalises and both rank them in the catalogue's order through this.
 */
export const FLAG_PRIORITY: readonly FaceFlag[] = FACE_FLAG_PRIORITY;
export type FlagProperty = FaceFlag;

/** `[Flag_X] = 1` and every higher-priority flag `= 0`. */
export function flagVisible(flag: FlagProperty): Expr {
  const index = FLAG_PRIORITY.indexOf(flag);
  const higher = FLAG_PRIORITY.slice(0, index).map((f) => eq(game(f), num(0)));
  return and(...higher, eq(game(flag), num(1)));
}

/** The shape the condition asks for, drawn over `frame`. */
const bandParts = (name: string, frame: Rect, style: AlertBandStyle, spec: AlertBandSpec): Item[] => {
  switch (spec.shape) {
    case 'filled':
      return filledBand(name, frame, style, spec.colour, spec.label, spec.flash ?? false);
    case 'outlined':
      return outlinedBand(name, frame, style, spec.colour, spec.label);
    case 'chequer':
      return chequerBand(name, frame);
  }
};

/**
 * One condition's layer, ranked by `bandRaised` rather than by the box's own reading: null-safe, so
 * that a sim publishing no `SessionFlagsDetails` leaves the band dark rather than lighting it, and
 * limited where a bit is held longer than the flag it announces.
 *
 * The band passes `false` for the critical-flags switch and so draws the whole list. That switch is
 * the box's, where sixty-four pixels are the only thing a driver has; a driver who wants band D
 * quieter turns the flag format off instead.
 */
const conditionLayer = (frame: Rect, style: AlertBandStyle, prefix: string, condition: FlagCondition): LayerItem => ({
  kind: 'layer',
  name: `${prefix}.${condition.id}`,
  children: bandParts(`${prefix}.${condition.id}`, frame, style, condition.band),
  ...withBindings({ Visible: conditionVisible(condition, false, FLAG_CATALOGUE, bandRaised) }),
});

export function flagStrip(frame: Rect, style: AlertBandStyle = ALERT_BAND_STYLES.standard, prefix = 'flag'): Item[] {
  return FLAG_CATALOGUE.map((condition) => conditionLayer(frame, style, prefix, condition));
}
