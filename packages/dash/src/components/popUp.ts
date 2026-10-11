/**
 * popUp: the box that takes the hero while one thing is worth more than the speed.
 *
 * The anatomy is the pagesandalerts artboard's: a 560 by 120 box in `purpose.popUp.surface`, a 2 px
 * rule in `purpose.popUp.rule` along its top edge, 32 px of side padding, a 15 px label 4 px above
 * a 64 px value on the left and an optional 46 px secondary at the right end. It is drawn from the
 * band, label and numeral elements rather than from drawing code of its own, so a pop-up is set in
 * the same faces and measured by the same tests as everything else on the face.
 *
 * **One colour, and it reaches the value alone.** The caller passes a colour and nothing else that
 * is a colour; the rule, the label and the secondary take fixed tokens. That is the artboard's rule
 * written into the signature rather than into a convention, so a caller cannot dress the wrong part.
 *
 * **It covers the hero and nothing else.** The box is placed from the hero rectangle each layout
 * gives it rather than at a coordinate, so it is centred on the gear on a face of any size, and it
 * is no wider and no taller than that rectangle, which leaves zones B and C, the rev bar, the bar of
 * settled values and band D uncovered. The artboard's 560 is the width where the hero has it, which
 * only the portrait face does: every landscape zone A is narrower, and a box of the artboard's width
 * centred on one hid the session name and the time left in zone C for as long as low fuel was
 * flagged (#1047), which the sheet's own "it never covers a slot" rules out. Where the box is
 * narrower its runs give ground the way a module's fields do, by `popUpFit`. A flag takes band D
 * over for its first three seconds and keeps the block at each end of it for as long as it is out,
 * and a pop-up must not be the thing that hides either.
 *
 * **One at a time**, by the exclusion chain the flags use: a pop-up is visible when its own
 * condition holds and no higher one's does. An invisible layer's other bindings are never
 * evaluated, so the two that are not showing cost nothing while they are not.
 *
 * Every condition here is a state or is arithmetic over a published property, which is what
 * ADR 0009 leaves a dashboard able to say for itself. None of them is a timer: `indicator.popUp`
 * writes down a 3000 ms life, and nothing in a scene graph can count it. The lap-time pop-up reads
 * that duration off the lap that has started rather than off a clock, which is the honest form of
 * "three seconds at the line"; the other two are conditions that end when the car's state changes.
 */
import type { Hex, Item, LayerItem, Rect, TextItem } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { boxSlack, canvasBaseline, canvasYForBaseline, cells, DATA_FACE, monoWidth, textBox, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { flagBox } from '../contract.ts';
import { CHARS, hasTime, lapTime, lastLap, REFERENCE_DELTA_WIDEST, referenceDelta, referenceDeltaText, tankIsLow } from '../second/values.ts';
import { FIT_LADDER } from '../second/field.ts';
import { ds } from '../tokens.ts';
import { FLAG_BLINK_MS } from './flagStrip.ts';

const { and, computed, concat, eq, fmt, game, isnull, lt, not, num, str, timespanToSeconds } = ncalc;

/**
 * The box the pagesandalerts artboard draws, which is the largest a pop-up is ever drawn.
 *
 * The height is `indicator.popUp.height`; the width is the one figure of this component that
 * tokens.json does not carry, so it is read off the artboard the way every rectangle in `zones/`
 * is, with the sheet as its citation rather than a ratio. A hero smaller than either gets a box its
 * own size, `popUpFrame`.
 */
export const POP_UP_WIDTH = 560;
export const POP_UP_HEIGHT = ds.indicator.popUp.height;

/** The rule along the top edge, counted inside the box. */
export const POP_UP_RULE = 2;

/** Side padding, and the least room allowed between the value and the secondary after it. */
export const POP_UP_PAD_X = ds.space[6];

/** Between the label's line box and the value's, which is the artboard's 4 and not the readout's 5. */
export const POP_UP_GAP = ds.space[1];

/** The 64 px the artboard sets a pop-up value in: `font.size.lapTime`, the step a rung L value uses. */
export const POP_UP_VALUE_SIZE = ds.size.lapTime;

/** The 46 px of the figure at the right end. */
export const POP_UP_SECONDARY_SIZE = ds.size.value;

/**
 * How long the artboard gives a pop-up, in seconds: `indicator.popUp.durationMs`.
 *
 * It is a length of time and a scene graph has no clock, so it is only ever read as part of a
 * condition that is true for that long on its own. {@link LAP_POP_UP} is the one that can.
 */
export const POP_UP_SECONDS = ds.indicator.popUp.durationMs / 1000;

/** One run of a pop-up: the value, or the figure after it. */
export interface PopUpText {
  /** What DashStudio draws, and what an unbound run draws for good. */
  sample: string;
  bind?: Expr;
  /** The widest string `bind` can draw. Every box here is measured from this, never from the sample. */
  widest?: string;
  /**
   * Cell budget, for a run that is a number and would jitter drawn proportionally. A run that is
   * or carries a word has none and is set in the face's own advances.
   */
  chars?: Chars;
}

/** The word over the value. Bound where the car decides which word it is. */
export interface PopUpLabel {
  text: string;
  bind?: Expr;
  /** The widest text `bind` can draw, in the case it will be drawn in. */
  widest?: string;
}

export interface PopUpSpec {
  id: string;
  label: PopUpLabel;
  value: PopUpText;
  /** The one colour the caller chooses. It reaches the value and nothing else. */
  colour: Hex;
  /** The dimmer figure at the right end, dropped rather than drawn outside the box. */
  secondary?: PopUpText;
  /** True while it is out. The exclusion chain adds the higher conditions' negations. */
  when: Expr;
  /** Half period of the value's flash, for the one pop-up whose value flashes. */
  flashMs?: number;
}

/** What a label draws: its text, or the widest its binding can write. */
const labelText = (spec: PopUpLabel): string => (spec.bind ? (spec.widest ?? spec.text) : spec.text);

/** Width of a run at a size: its cells when it has a budget, its measured advances otherwise. */
const runWidth = (text: PopUpText, fs: number): number =>
  text.chars ? monoWidth(cells('SemiBold', fs), text.chars) : Math.ceil(measureText(DATA_FACE.SemiBold, text.widest ?? text.sample, fs));

/** The room the two runs share in a box of this width. */
const innerWidth = (width: number): number => width - 2 * POP_UP_PAD_X;

/**
 * Where the label and the value are set in a box: the two line boxes stacked 4 px apart and the
 * stack centred in what the rule leaves, which is the artboard's `align-items: center`.
 */
function popUpStack(frame: Rect, valueFs: number): { labelY: number; valueY: number } {
  const labelY = frame.top + POP_UP_RULE + (frame.height - POP_UP_RULE - (ds.size.label + POP_UP_GAP + valueFs)) / 2;
  return { labelY, valueY: labelY + ds.size.label + POP_UP_GAP };
}

/** Whether a value of this size, under its label, is drawn inside the box from top to bottom. */
function stackFits(frame: Rect, valueFs: number): boolean {
  const { labelY, valueY } = popUpStack(frame, valueFs);
  const label = textBox(labelY, ds.size.label);
  const value = textBox(valueY, valueFs);
  return Math.round(label.top) >= frame.top + POP_UP_RULE && Math.round(value.top) + value.height <= frame.top + frame.height;
}

/** How a pop-up whose runs are wider than its box gives ground. */
export interface PopUpFit {
  valueFs: number;
  /** False where the secondary was shed to keep the value at size. */
  secondary: boolean;
}

/** The smallest a value is ever set, which is the floor every fitted run on the face shares. */
const LEAST_VALUE_SIZE = 12;

/**
 * The size the value is drawn at in a box, and whether the secondary survives.
 *
 * Rule 17 in the small, and the same order `fitFields` takes: the least important run is shed
 * before the most important one is shrunk, so the secondary goes first and only a value that still
 * does not fit the box on its own is scaled down, by the ladder's steps and then, in a box narrower
 * than the ladder reaches, to the size that fills it. A bound run draws its binding rather than its
 * sample, so all of this is measured from `widest`. The box is the one `popUpFrame` gives the face's
 * hero, so a narrow hero is answered here rather than by a box wider than the hero.
 */
export function popUpFit(spec: PopUpSpec, frame: Rect = rect(0, 0, POP_UP_WIDTH, POP_UP_HEIGHT)): PopUpFit {
  const room = innerWidth(frame.width);
  const fits = (fs: number): boolean => runWidth(spec.value, fs) <= room && stackFits(frame, fs);
  const after = spec.secondary ? runWidth(spec.secondary, POP_UP_SECONDARY_SIZE) + POP_UP_PAD_X : 0;
  if (runWidth(spec.value, POP_UP_VALUE_SIZE) + after <= room && stackFits(frame, POP_UP_VALUE_SIZE)) {
    return { valueFs: POP_UP_VALUE_SIZE, secondary: spec.secondary !== undefined };
  }
  for (const factor of FIT_LADDER) {
    const fs = Math.max(LEAST_VALUE_SIZE, Math.round(POP_UP_VALUE_SIZE * factor));
    if (fits(fs)) return { valueFs: fs, secondary: false };
  }
  for (let fs = Math.round(POP_UP_VALUE_SIZE * FIT_LADDER[FIT_LADDER.length - 1]!) - 1; fs > LEAST_VALUE_SIZE; fs--) {
    if (fits(fs)) return { valueFs: fs, secondary: false };
  }
  return { valueFs: LEAST_VALUE_SIZE, secondary: false };
}

/**
 * The box, centred on the rectangle the face calls its hero, and no larger than it.
 *
 * Placed rather than fixed, so the same component sits over the gear on the 1920 reference face and
 * on the 600 portrait one without either of them stating a coordinate. The artboard's 560 by 120 is
 * the most it takes: a hero narrower or shorter than that gets a box of its own width or height, so
 * that the pop-up hides the gear and not the zones either side of it.
 */
export function popUpFrame(hero: Rect): Rect {
  const width = Math.min(POP_UP_WIDTH, hero.width);
  const height = Math.min(POP_UP_HEIGHT, hero.height);
  return roundRect({ left: hero.left + (hero.width - width) / 2, top: hero.top + (hero.height - height) / 2, width, height });
}

/** The items of one pop-up, behind the Visible expression that ranks it against the others. */
export function popUp(frame: Rect, spec: PopUpSpec, prefix = 'popUp'): LayerItem {
  const name = `${prefix}.${spec.id}`;
  const fit = popUpFit(spec, frame);
  const x = frame.left + POP_UP_PAD_X;
  const { labelY, valueY } = popUpStack(frame, fit.valueFs);
  const labelWidth = Math.ceil(measureText('BarlowMedium', labelText(spec.label), ds.size.label)) + boxSlack(ds.size.label);
  const children: Item[] = [
    band(`${name}.box`, frame, ds.purpose.popUp.surface),
    band(`${name}.rule`, rect(frame.left, frame.top, frame.width, POP_UP_RULE), ds.purpose.popUp.rule),
    label(`${name}.label`, spec.label.text, x, labelY, labelWidth, { bind: spec.label.bind, ...(spec.label.widest ? { widest: spec.label.widest } : {}) }),
    popUpRun(`${name}.value`, spec.value, x, valueY, fit.valueFs, spec.colour, spec.flashMs),
  ];
  if (fit.secondary && spec.secondary) {
    const fs = POP_UP_SECONDARY_SIZE;
    const width = runWidth(spec.secondary, fs) + boxSlack(fs);
    const right = frame.left + frame.width - POP_UP_PAD_X - width;
    // On the value's baseline rather than on its line box, which is what puts a smaller run beside
    // a larger one everywhere else on the face.
    children.push(popUpRun(`${name}.secondary`, spec.secondary, right, canvasYForBaseline(canvasBaseline(valueY, fit.valueFs), fs), fs, ds.color.text.secondary, undefined, width));
  }
  return withMoreBindings({ kind: 'layer', name, children }, { Visible: popUpVisible(spec.id) });
}

/**
 * One run of a pop-up, monospaced where it has a cell budget.
 *
 * The flash is on the run and not on the layer. A blink on the group is what once left band D's
 * page readable through half of every yellow flag: for half the cycle nothing of the box is drawn
 * and the hero reads through the pop-up that is covering it.
 */
function popUpRun(name: string, text: PopUpText, x: number, y: number, fs: number, color: Hex, flashMs?: number, width?: number): TextItem {
  const item = numeral(name, text.sample, x, y, fs, text.chars ?? { digits: 0, specials: 0 }, {
    color,
    bind: text.bind,
    ...(text.chars ? {} : { proportional: true }),
    ...(text.widest ? { widest: text.widest } : {}),
    ...(width === undefined ? {} : { width, hAlign: 'right' as const }),
  });
  return flashMs === undefined ? item : { ...item, blink: { enabled: true, delayMs: flashMs } };
}

// --- the three that can be built ---------------------------------------------------------------

/**
 * Three seconds after the line, read off the lap that has started rather than off a clock.
 *
 * `CurrentLapTime` counts from zero at every crossing, so "it is under three seconds" is true for
 * exactly the three seconds the artboard asks for and is arithmetic over a published property in
 * the sense ADR 0009 allows. The last lap has to be a real time as well, or the out lap would open
 * with a pop-up whose value is the no-data glyph.
 */
const atTheLine = (): Expr => and(hasTime(lastLap()), lt(timespanToSeconds(isnull(game('CurrentLapTime'), num(0))), num(POP_UP_SECONDS)));

/** A SimHub status property, read the null-safe way the LED catalogue reads the same ones. */
const on = (name: string): Expr => eq(isnull(game(name), num(0)), num(1));

/** Lap time at the line: the previous lap, and the delta against the reference the driver chose. */
export const LAP_POP_UP: PopUpSpec = {
  id: 'lap',
  label: { text: 'Lap' },
  value: { sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime },
  colour: ds.color.text.primary,
  secondary: { sample: '−0.21', bind: referenceDeltaText(referenceDelta()), widest: REFERENCE_DELTA_WIDEST, chars: CHARS.referenceDelta },
  when: atTheLine(),
};

/**
 * Low fuel, flashing at the rate the flag band defines, so one rate is written once.
 *
 * The laps carry their unit inside the run, which is what the artboard draws and what stops "FUEL /
 * 0.8" being read as litres. That costs the run its cells: a word cannot sit in a digit cell, so
 * this one is proportional and is measured from the widest reading the threshold allows, which is
 * the 99 laps the panel's number box will accept.
 */
export const FUEL_POP_UP: PopUpSpec = {
  id: 'fuel',
  label: { text: 'Fuel' },
  value: { sample: '0.8 laps', bind: concat(fmt(isnull(computed('Fuel_RemainingLaps'), num(0)), '0.0'), str(' laps')), widest: '99.9 laps' },
  colour: ds.purpose.fuel.low,
  when: tankIsLow(),
  flashMs: FLAG_BLINK_MS,
};

/**
 * DRS available.
 *
 * The strip's `drs` effect lights on `DRSAvailable` **or** `DRSEnabled`, the flap free and the flap
 * open, and tells the two apart by flashing. A pop-up whose value is the word "Available" cannot
 * say both, so it takes the first half of that condition: it is out while the flap may be opened
 * and goes the moment it is, and `popUp.test.ts` holds it to being a part of what lights the strip.
 *
 * Push to pass is the other half of this pop-up and is not here yet. It reads
 * `GameRawData.Telemetry.PlayerP2P_Count`, which no committed trace carries, and a trace that is
 * telemetry is re-recorded on the VM rather than written by hand; traces/README.md is the rule. The
 * label becomes a binding between the two words when that recording happens.
 */
export const DRS_POP_UP: PopUpSpec = {
  id: 'drs',
  label: { text: 'DRS' },
  value: { sample: 'Available' },
  colour: ds.color.good.primary,
  when: on('DRSAvailable'),
};

/**
 * The pop-ups in priority order, highest first.
 *
 * The lap time leads because it is the only one of the three whose condition lasts seconds: low
 * fuel is true for the rest of a stint and an aid for the length of a zone, so either of them
 * placed above it would swallow every lap time a driver is waiting for at the line. Below it,
 * danger outranks an aid, as it does everywhere else on this face.
 */
export const POP_UPS: readonly PopUpSpec[] = [LAP_POP_UP, FUEL_POP_UP, DRS_POP_UP];

/** `spec.when` and no higher pop-up's condition: the chain the flag band ranks itself with. */
export function popUpVisible(id: string): Expr {
  const index = POP_UPS.findIndex((p) => p.id === id);
  const spec = POP_UPS[index];
  if (!spec) throw new Error(`popUp: no pop-up ${id}`);
  return and(...POP_UPS.slice(0, index).map((p) => not(p.when)), spec.when);
}

/** Every pop-up, over the hero of a face. */
export const popUps = (hero: Rect, prefix = 'popUp'): Item[] => POP_UPS.map((spec) => popUp(popUpFrame(hero), spec, prefix));
