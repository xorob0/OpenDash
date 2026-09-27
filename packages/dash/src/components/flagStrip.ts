/**
 * flagStrip: band D's flag, one Layer per condition of `FLAG_CATALOGUE`, sharing the band anatomy
 * and differing in shape, colour, name and behaviour. One shows at a time and nothing is drawn when
 * nothing is raised.
 *
 * It draws in two phases, #380. `flagStrip` is the takeover, the whole band for the few seconds a
 * flag has just come out or just changed; `flagCorners` is what it settles into, the same fifteen
 * conditions in the block at each end of the band, so the page a driver was reading comes back while
 * the flag stays out. `flagTakingBand` is the one window that decides between them, and `zones/face.ts`
 * is where the two groups are gated against each other.
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
import { ALERT_BAND_BORDER, ALERT_BAND_STYLES, alertBandName, chequerBand, filledBand, outlinedBand, type AlertBandStyle } from './alertBand.ts';
import { bandRaised, conditionVisible, FACE_FLAG_PRIORITY, FLAG_CATALOGUE, raisedRank, type AlertBandSpec, type FaceFlag, type FlagCondition } from '../flags.ts';
import { BLUE_FLAG_DETAILS, setting, type BlueFlagDetail } from '../contract.ts';
import { measureText } from '../design/advances.ts';
import { CHIP_WIDEST } from '../second/chip.ts';
import { carBehindClass, carBehindPositionClass, WIDEST_BEHIND_POSITION_CLASS } from '../second/values.ts';
import { ds } from '../tokens.ts';

export { ALERT_BAND_BORDER as BLACK_FLAG_BORDER, ALERT_FLASH_MS as FLAG_BLINK_MS, ALERT_NAME_WEIGHT as FLAG_NAME_WEIGHT, ALERT_BAND_STYLES as FLAG_STRIP_STYLES } from './alertBand.ts';
export type { AlertBandStyle as FlagStripStyle } from './alertBand.ts';

const { game, eq, and, changed, num, concat, iff, str } = ncalc;

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
const conditionLayer = (condition: FlagCondition, name: string, children: Item[]): LayerItem =>
  withMoreBindings({ kind: 'layer', name, children }, { Visible: conditionVisible(condition, false, FLAG_CATALOGUE, bandRaised) });

export function flagStrip(frame: Rect, style: AlertBandStyle = ALERT_BAND_STYLES.standard, prefix = 'flag'): Item[] {
  return FLAG_CATALOGUE.map((condition) => {
    const name = `${prefix}.${condition.id}`;
    const parts = condition.id === BLUE_FLAG_ID ? blueFlagParts : bandParts;
    return conditionLayer(condition, name, parts(name, frame, style, condition.band));
  });
}

// --- The settled form: the flag after it has had the band for its few seconds ------------------

/**
 * How long a flag keeps the whole band before it settles into the blocks at its ends.
 *
 * `indicator.alert.durationMs` is the canvas's own figure for how long the highest-priority alert
 * stands, and it is three seconds, which is the period #380 asks for. It is the same three seconds
 * the lap-time pop-up and the change notification are out for, which is the point: the face has one
 * answer to "how long is a driver shown a thing he did not ask for".
 */
export const FLAG_TAKEOVER_MS = ds.indicator.alert.durationMs;

/**
 * The flag has just come out, or has just changed: the band is its for these few seconds.
 *
 * `changed(ms, value)` is SimHub's own window and not a clock of ours, which is what ADR 0009
 * admits, and the value it watches is `raisedRank`, the position of the *winning* condition rather
 * than any one condition's bits. That is what makes a caution clearing to the yellow under it, or a
 * yellow going green, take the band again: the winner moved even where no bit did.
 *
 * Two facts about SimHub's implementation matter here, both read off the decompiled
 * `NCalcEngineBase.Function_Changed` in 9.12.6. The window lives in the engine's own `ChangeState`
 * keyed by **the text of the value expression**, not by the item asking, and `EditorModel` builds
 * one engine per dashboard, so the takeover group and the settled group share one window and cannot
 * disagree about which phase the band is in -- which is the whole reason this is one expression used
 * twice rather than a duration attached to each of two items. And the first evaluation of a key
 * records the value and returns false, so a face that starts up under a flag opens in the settled
 * form rather than taking the band for a flag that was already out before the dash was.
 */
export const flagTakingBand = (): Expr => changed(num(FLAG_TAKEOVER_MS), raisedRank(bandRaised));

/** The two rectangles a settled flag keeps, which `zones/bandPages.ts` measures off the band. */
export interface FlagCornerBlocks {
  left: Rect;
  right: Rect;
}

/**
 * The flag's name fits the block it would be centred on, with the band's own border cleared at each
 * end.
 *
 * Strictly, and measured in Bold, which is the face the name is drawn in: SimHub hands the box to
 * WPF as `MaxTextWidth` and a run measured in Medium and drawn in Bold loses its last glyph. A name
 * that does not fit is not shrunk and not clipped; it is simply not written, and the block is colour
 * alone, which is what the nano's twelve-pixel strip already is.
 *
 * All fourteen names fit all four corner-block sizes and none fits the sixteen pixels of side padding,
 * so the answer comes out per face rather than per condition: on the four faces with no corner block a
 * settled flag is a colour, and a colour is a family rather than a member -- the three blacks are one
 * outlined sliver and the debris flag is a yellow. zones.md §6 weighs that against holding the whole
 * band for the length of a caution, and §10 records what the canvas still owes those four faces.
 */
const cornerNameFits = (block: Rect, text: string): boolean =>
  measureText('BarlowBold', text, ds.size.label) + 2 * ALERT_BAND_BORDER < block.width;

/** One end of the settled flag: the condition's own shape, with its name where the block has room. */
const cornerParts = (name: string, block: Rect, style: AlertBandStyle, spec: AlertBandSpec): Item[] => {
  const labels = style.labels && spec.shape !== 'chequer' && cornerNameFits(block, spec.label);
  return bandParts(name, block, { ...style, labels }, spec);
};

/**
 * The settled flag: the same fifteen conditions, ranked the same way, drawn in the block at each end
 * of the band instead of across the whole of it.
 *
 * The page underneath is back, which is the point of #380, and the flag is still out until its bits
 * clear. A blinking flag keeps blinking, because the flash is part of what a waved yellow means and
 * `filledBand` puts it inside the rectangle it is given, whatever that rectangle is.
 *
 * One thing the takeover has that this does not: the blue flag's detail. Naming the class of the car
 * behind takes a whole band -- "BLUE FLAG · P4 GT3" is wider than any corner block at any size --
 * and a block that wrote it on the widest face and not on the others would be a different drawing
 * per face. The blue block writes BLUE FLAG where that fits, and the detail belongs to the seconds
 * the flag has the band.
 */
export function flagCorners(blocks: FlagCornerBlocks, style: AlertBandStyle = ALERT_BAND_STYLES.standard, prefix = 'flagCorner'): Item[] {
  return FLAG_CATALOGUE.map((condition) => {
    const name = `${prefix}.${condition.id}`;
    return conditionLayer(condition, name, [
      ...cornerParts(`${name}.left`, blocks.left, style, condition.band),
      ...cornerParts(`${name}.right`, blocks.right, style, condition.band),
    ]);
  });
}
