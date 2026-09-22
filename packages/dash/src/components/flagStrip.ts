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
 *
 * One condition says more than its own name. A blue flag is thrown for a car that is about to
 * arrive, and which car that is decides whether a driver lifts or holds the line, so
 * `OpenDash.BlueFlagDetail` lets the band name the class of the car behind or its position and
 * class. It is drawn as three runs over one centred line box rather than as a name with a detail
 * beside it, because the canvas writes one sentence -- "Blue flag · GT3 behind" -- and two boxes
 * competing for the same middle would be a different drawing at every value.
 */
import type { Item, LayerItem, Rect } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { ncalc } from '../generator.ts';
import { ALERT_BAND_STYLES, alertBandName, chequerBand, filledBand, outlinedBand, type AlertBandStyle } from './alertBand.ts';
import { bandRaised, conditionVisible, FACE_FLAG_PRIORITY, FLAG_CATALOGUE, type AlertBandSpec, type FaceFlag, type FlagCondition } from '../flags.ts';
import { BLUE_FLAG_DETAILS, setting, type BlueFlagDetail } from '../contract.ts';
import { CHIP_WIDEST } from '../second/chip.ts';
import { carBehindClass, carBehindPositionClass, WIDEST_BEHIND_POSITION_CLASS } from '../second/values.ts';
import { ds } from '../tokens.ts';

export { ALERT_BAND_BORDER as BLACK_FLAG_BORDER, ALERT_FLASH_MS as FLAG_BLINK_MS, ALERT_NAME_WEIGHT as FLAG_NAME_WEIGHT, ALERT_BAND_STYLES as FLAG_STRIP_STYLES } from './alertBand.ts';
export type { AlertBandStyle as FlagStripStyle } from './alertBand.ts';

const { game, eq, and, num, concat, iff, str } = ncalc;

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

/** The condition whose band can say more than its own name, and the only one. */
export const BLUE_FLAG_ID = 'blue';

/**
 * The three runs the blue band can carry, one per value of `OpenDash.BlueFlagDetail`, of which
 * exactly one is visible.
 *
 * Three runs rather than one bound run, and three rather than a name with a second run beside it.
 * The canvas draws the detail as part of the sentence -- "Blue flag · GT3 behind" -- so what the
 * band writes is one centred string in all three cases, and a name pinned to the centre with a
 * detail hung off its end would be two boxes fighting over the same middle. Each run declares the
 * widest string it can draw, which is what `textFit.test.ts` measures against the band: the
 * longest is the label, a two-digit place and the widest chip, and a run that stopped fitting some
 * face's band would fail there rather than be clipped by WPF.
 *
 * The separator goes with the detail rather than before it, so a lap with nothing behind reads
 * `BLUE FLAG` and not `BLUE FLAG · `.
 *
 * It lives here and not in `FLAG_CATALOGUE` because the catalogue is also the 8x8 box's list, the
 * LED strips' and the pit wall header's, and none of those three can draw a second run: a picture
 * on a matrix, a lamp and a name beside the session. A detail is band D's alone.
 */
interface BlueFlagRun {
  detail: BlueFlagDetail;
  /** What DashStudio draws, which is the canvas's own sample. */
  sample: string;
  /** The longest string the run can draw, and what its box is measured from. */
  widest: string;
  /** Absent on `none`, which is the label as it has always been drawn. */
  bind?: Expr;
}

const blueFlagRuns = (label: string): readonly BlueFlagRun[] => {
  // The detail, with its separator, or nothing at all where there is no car behind to name.
  const withLabel = (detail: Expr): Expr => iff(eq(detail, str('')), str(label), concat(str(`${label} · `), detail));
  const runs: readonly BlueFlagRun[] = [
    { detail: 'none', sample: label, widest: label },
    { detail: 'class', sample: `${label} · GT3`, widest: `${label} · ${CHIP_WIDEST}`, bind: withLabel(carBehindClass()) },
    {
      detail: 'positionClass',
      sample: `${label} · P4 GT3`,
      widest: `${label} · ${WIDEST_BEHIND_POSITION_CLASS}`,
      bind: withLabel(carBehindPositionClass()),
    },
  ];
  // A value the setting offers and the band cannot draw is a band that goes blank when somebody
  // picks it, so the two lists are held together here rather than by authorship.
  const missing = BLUE_FLAG_DETAILS.filter((detail) => !runs.some((run) => run.detail === detail));
  if (missing.length > 0) throw new RangeError(`the blue band draws no run for ${missing.join(', ')}`);
  return runs;
};

/**
 * The blue band: the filled bar with no name of its own, and the three runs that stand in for one.
 *
 * A style that writes no name writes no detail either, which is what the nano's twelve pixel strip
 * and the companion's band need: there is no room for a word there, so there is none for a word
 * and a class. The plain filled band is what those two keep.
 */
const blueFlagParts = (name: string, frame: Rect, style: AlertBandStyle, spec: AlertBandSpec): Item[] => {
  if (!style.labels || spec.shape !== 'filled') return bandParts(name, frame, style, spec);
  return [
    ...bandParts(name, frame, { ...style, labels: false }, spec),
    ...blueFlagRuns(spec.label).map((run) =>
      alertBandName(`${name}.label${run.detail === 'none' ? '' : `.${run.detail}`}`, frame, run.sample, ds.purpose.flag.onFlag, {
        widest: run.widest,
        visibleBind: setting.blueFlagDetailIs(run.detail),
        ...(run.bind ? { bind: run.bind } : {}),
      }),
    ),
  ];
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
const conditionLayer = (frame: Rect, style: AlertBandStyle, prefix: string, condition: FlagCondition): LayerItem => {
  const name = `${prefix}.${condition.id}`;
  const parts = condition.id === BLUE_FLAG_ID ? blueFlagParts : bandParts;
  return withMoreBindings({
    kind: 'layer',
    name,
    children: parts(name, frame, style, condition.band),
  }, { Visible: conditionVisible(condition, false, FLAG_CATALOGUE, bandRaised) });
};

export function flagStrip(frame: Rect, style: AlertBandStyle = ALERT_BAND_STYLES.standard, prefix = 'flag'): Item[] {
  return FLAG_CATALOGUE.map((condition) => conditionLayer(frame, style, prefix, condition));
}
