/**
 * popUp: the box that comes out while one thing is worth more than what it covers.
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
 * **Two kinds, by how long they stay** (#1047). The sheet centres every pop-up on the hero, which is
 * the gear, and that is right for a box that is out for the three seconds after the line and wrong
 * for one that is out for as long as a condition holds: low fuel stays up for the last laps of a
 * stint, and a driver who cannot see the gear for two laps has lost the one reading zone A is for.
 *
 * - A **transient** pop-up is an event, out for seconds, and is drawn as a real pop-up: the sheet's
 *   560 by 120, centred on the hero and free to reach over zones B and C, inside the body and clear
 *   of the rev bar, the bar of settled values, band D and the pit banner. The lap time at the line
 *   is the one, and the change notification is drawn the same way.
 * - A **lasting** pop-up is a state, out for as long as its condition holds, and never covers the
 *   gear. It is drawn compact, its label and its value on one line, in the row the face keeps for a
 *   state that holds: the pit banner's, which every theme's anatomy places where it hides nothing a
 *   driver reads by reflex, at the top of zone A above the gear on the house's faces and the
 *   Porsche's and at the top of zone C on the AiM's. The row is cut short where the gear's ink
 *   reaches into it, and the pit family has it first: a lasting pop-up is hidden while any state of
 *   the family is out. Low fuel and DRS are the two.
 *
 * **Under the safety layers.** Every box of the family is drawn after the full-screen flag and the pit
 * family, and none of them is drawn over either while it is out. The lasting pop-ups wait for the
 * whole pit family here, being in its row; the rest of that ranking is the face's, which alone knows
 * what it draws where (`givingWay` in `zones/face.ts`, #1069, #1070).
 *
 * **One at a time of each kind**, by the exclusion chain the flags use: a pop-up is visible when its
 * own condition holds and no higher one of its kind's does. The two kinds draw in different places
 * and are not ranked against each other, so a lap time at the line no longer hides low fuel. An
 * invisible layer's other bindings are never evaluated, so the ones that are not showing cost
 * nothing while they are not.
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
import { measureText, widestFigure } from '../design/advances.ts';
import { overlaps, rect, roundRect } from '../design/geometry.ts';
import { boxSlack, canvasBaseline, canvasYForBaseline, cells, DATA_FACE, monoWidth, textBox, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { flagBox } from '../contract.ts';
import { CHARS, hasTime, LAP_TIME_WIDEST, lapTime, lastLap, REFERENCE_DELTA_WIDEST, referenceDelta, referenceDeltaText, secondsOf, tankIsLow } from '../second/values.ts';
import { FIT_LADDER } from '../second/field.ts';
import { ds } from '../tokens.ts';
import { FLAG_BLINK_MS } from './flagStrip.ts';
import { pitAlertOut } from './pitAlerts.ts';

const { and, computed, concat, eq, fmt, game, isnull, lt, not, num, str } = ncalc;

/**
 * The box the pagesandalerts artboard draws, which is the largest a pop-up is ever drawn.
 *
 * The height is `indicator.popUp.height`; the width is the one figure of this component that
 * tokens.json does not carry, so it is read off the artboard the way every rectangle in `zones/`
 * is, with the sheet as its citation rather than a ratio. A body narrower or a room shorter than
 * either gets a box its own size, `popUpFrame`.
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
 * A lasting pop-up's side padding and the room between its label and its value, which share a line.
 * The banner row it is drawn in is 30 px deep on every face, so the sheet's 32 and its stacked 4 are
 * the anatomy of a box four times taller; these are the next steps down the `space` scale.
 */
export const LASTING_PAD_X = ds.space[3];
export const LASTING_GAP = ds.space[2];

/**
 * The largest a lasting pop-up's value is set: the sheet's secondary, the size the face gives a figure
 * that is read beside another rather than as the hero. The banner row holds about 22.
 */
export const LASTING_VALUE_SIZE = POP_UP_SECONDARY_SIZE;

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

/**
 * How long a pop-up stays out, which decides where it may be drawn: an event of a few seconds over
 * the whole face, a state that holds somewhere that never covers the gear (#1047).
 */
export type PopUpKind = 'transient' | 'lasting';

export interface PopUpSpec {
  id: string;
  kind: PopUpKind;
  label: PopUpLabel;
  value: PopUpText;
  /** The one colour the caller chooses. It reaches the value and nothing else. */
  colour: Hex;
  /** The dimmer figure at the right end, dropped rather than drawn outside the box. A lasting pop-up draws none. */
  secondary?: PopUpText;
  /** True while it is out. The exclusion chain adds the higher conditions' negations. */
  when: Expr;
  /** Half period of the value's flash, for the one pop-up whose value flashes. */
  flashMs?: number;
}

/** What a label draws: its text, or the widest its binding can write. */
const labelText = (spec: PopUpLabel): string => (spec.bind ? (spec.widest ?? spec.text) : spec.text);

/** The label's box: its measured advances at the label size, and the slack every text box carries. */
const labelWidth = (spec: PopUpLabel): number => Math.ceil(measureText('BarlowMedium', labelText(spec), ds.size.label)) + boxSlack(ds.size.label);

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

/** Whether a text box of this size, set at canvas `y`, is drawn between the rule and the foot of the box. */
function boxInside(frame: Rect, y: number, fs: number): boolean {
  const box = textBox(y, fs);
  return Math.round(box.top) >= frame.top + POP_UP_RULE && Math.round(box.top) + box.height <= frame.top + frame.height;
}

/** Whether a value of this size, under its label, is drawn inside the box from top to bottom. */
function stackFits(frame: Rect, valueFs: number): boolean {
  const { labelY, valueY } = popUpStack(frame, valueFs);
  return boxInside(frame, labelY, ds.size.label) && boxInside(frame, valueY, valueFs);
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
 * The size a transient pop-up's value is drawn at in a box, and whether the secondary survives.
 *
 * Rule 17 in the small, and the same order `fitFields` takes: the least important run is shed
 * before the most important one is shrunk, so the secondary goes first and only a value that still
 * does not fit the box on its own is scaled down, by the ladder's steps and then, in a box narrower
 * than the ladder reaches, to the size that fills it. A bound run draws its binding rather than its
 * sample, so all of this is measured from `widest`. The box is the sheet's wherever the body has
 * room for it, so this is answered by a narrow theme body or a short room under the pit banner.
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
 * A box of the sheet's size, centred on `room` and kept inside `body`.
 *
 * Placed rather than fixed, so the same component sits over the gear on the 1920 reference face and
 * on the 600 portrait one without either of them stating a coordinate. The box is as wide as the
 * sheet draws it wherever the body has the width, which on a landscape face reaches over zones B and
 * C for the seconds it is out, and no taller than the room, which is what keeps it off the pit banner.
 * Where a theme puts the hero at an edge, the box is moved along rather than drawn off the body.
 */
export function sheetFrame(room: Rect, body: Rect, width: number, height: number): Rect {
  const w = Math.min(width, body.width);
  const h = Math.min(height, room.height, body.height);
  const within = (start: number, span: number, size: number, wanted: number): number => Math.max(start, Math.min(start + span - size, wanted));
  return roundRect({
    left: within(body.left, body.width, w, room.left + (room.width - w) / 2),
    top: within(body.top, body.height, h, room.top + (room.height - h) / 2),
    width: w,
    height: h,
  });
}

/** The transient pop-up's box: the sheet's 560 by 120, centred on the room and inside the body. */
export const popUpFrame = (room: Rect, body: Rect = room): Rect => sheetFrame(room, body, POP_UP_WIDTH, POP_UP_HEIGHT);

/**
 * The rectangle the transient boxes are centred on: the hero, or the part of it the pit banner leaves
 * where a box centred on the whole hero would cover the banner.
 *
 * The banner is the one thing inside the hero with a state of its own. On the house face it sits at
 * the top of zone A and a box centred on the zone clears it on every face, so the hero is the room
 * there and nothing moves. A theme can put it where a short hero brings the box up over it: the
 * Porsche draws it inside the gear's tile, and on its 1280 x 400 and 600 x 686 faces a pop-up
 * centred on the tile covered the banner's bottom, on the portrait face the lower half of
 * `Engage limiter` with it (#1052). Where that happens the room is the hero below the banner, or
 * above it where that is the taller, and the box is centred there and no taller than it. The pop-up's
 * frame decides for the change notification too, so the two are centred on one rectangle and the
 * notification stays inside the pop-up's frame.
 */
export function popUpRoom(hero: Rect, banner: Rect, body: Rect = hero): Rect {
  if (!overlaps(popUpFrame(hero, body), banner)) return hero;
  const heroBottom = hero.top + hero.height;
  const bannerBottom = banner.top + banner.height;
  const below = rect(hero.left, bannerBottom, hero.width, Math.max(0, heroBottom - bannerBottom));
  const above = rect(hero.left, hero.top, hero.width, Math.max(0, banner.top - hero.top));
  return below.height >= above.height ? below : above;
}

/** The shortest row a lasting pop-up is drawn in: the rule and a label's line box under it. */
export const LASTING_LEAST_HEIGHT = POP_UP_RULE + textBox(0, ds.size.label).height;

/**
 * The row a lasting pop-up is drawn in: the pit banner's rectangle, cut short above the gear.
 *
 * The banner's row is the face's place for a state that holds. Every theme's anatomy puts it where
 * a state may sit for as long as the limiter is on in the lane without hiding what a driver reads by
 * reflex: on the house's faces at the top of zone A, in the leading the gear's line box leaves above
 * its figures; on the Porsche's at the top of the gear's tile; on the AiM's at the top of zone C. A
 * lasting pop-up takes the same row, and the pit family has it first (`popUpVisible`).
 *
 * `gear` is the ink of the gear on every one of zone A's pages (`zoneAGearInk`). No shipped face's
 * gear reaches into the row any more, since zone A draws the gear below the banner (#1182). Where a
 * page's ink did reach it, the row would end where the ink begins, so that the box is never over the
 * gear whichever page is showing. A row the gear leaves too short for a label's line box is a layout
 * this cannot draw, and says so rather than covering the gear: that is a theme's anatomy to answer.
 */
export function lastingPopUpFrame(banner: Rect, gear: readonly Rect[]): Rect {
  const reaching = gear.filter((ink) => overlaps(ink, banner));
  if (reaching.some((ink) => ink.top <= banner.top)) {
    throw new Error(`popUp: the gear's ink starts above the pit banner's row at ${JSON.stringify(banner)}, so a lasting pop-up there would cover it`);
  }
  const bottom = Math.min(banner.top + banner.height, ...reaching.map((ink) => Math.floor(ink.top)));
  const frame = rect(banner.left, banner.top, banner.width, bottom - banner.top);
  if (frame.height < LASTING_LEAST_HEIGHT) {
    throw new Error(`popUp: the gear leaves ${frame.height} px of the pit banner's row at ${JSON.stringify(banner)}, and a lasting pop-up needs ${LASTING_LEAST_HEIGHT}`);
  }
  return frame;
}

/** Where a lasting pop-up's two runs are set in its row, at a value size. */
function lastingLine(spec: PopUpSpec, frame: Rect, valueFs: number): { left: number; labelY: number; valueY: number; valueWidth: number } {
  const valueWidth = runWidth(spec.value, valueFs) + boxSlack(valueFs);
  const group = labelWidth(spec.label) + LASTING_GAP + valueWidth;
  const left = Math.round(frame.left + Math.max(LASTING_PAD_X, (frame.width - group) / 2));
  // The value's line box centred in what the rule leaves, and the label on the value's baseline.
  const inner = frame.height - POP_UP_RULE;
  const valueY = frame.top + POP_UP_RULE + (inner - textBox(0, valueFs).height) / 2 - textBox(0, valueFs).top;
  const labelY = canvasYForBaseline(canvasBaseline(valueY, valueFs), ds.size.label);
  return { left, labelY, valueY, valueWidth };
}

/**
 * The size a lasting pop-up's value is set at in its row: the largest, up to the sheet's secondary,
 * at which the label and the value share the line inside the side padding and both line boxes are
 * drawn between the rule and the foot of the row. There is nothing to shed; a row narrower than the
 * label and a 12 px value would be drawn at 12 and fail the face's fit tests, which is where it
 * should be noticed.
 */
export function lastingFit(spec: PopUpSpec, frame: Rect): { valueFs: number } {
  for (let fs = LASTING_VALUE_SIZE; fs > LEAST_VALUE_SIZE; fs--) {
    const { labelY, valueY, valueWidth } = lastingLine(spec, frame, fs);
    const wide = labelWidth(spec.label) + LASTING_GAP + valueWidth <= frame.width - 2 * LASTING_PAD_X;
    if (wide && boxInside(frame, valueY, fs) && boxInside(frame, labelY, ds.size.label)) return { valueFs: fs };
  }
  return { valueFs: LEAST_VALUE_SIZE };
}

/** The items of one pop-up, behind the Visible expression that ranks it against the others of its kind. */
export function popUp(frame: Rect, spec: PopUpSpec, prefix = 'popUp'): LayerItem {
  const name = `${prefix}.${spec.id}`;
  const ground: Item[] = [
    band(`${name}.box`, frame, ds.purpose.popUp.surface),
    band(`${name}.rule`, rect(frame.left, frame.top, frame.width, POP_UP_RULE), ds.purpose.popUp.rule),
  ];
  const labelOpts = { bind: spec.label.bind, ...(spec.label.widest ? { widest: spec.label.widest } : {}) };
  const children: Item[] = [...ground];
  if (spec.kind === 'lasting') {
    const { valueFs } = lastingFit(spec, frame);
    const { left, labelY, valueY } = lastingLine(spec, frame, valueFs);
    const width = labelWidth(spec.label);
    children.push(
      label(`${name}.label`, spec.label.text, left, labelY, width, labelOpts),
      popUpRun(`${name}.value`, spec.value, left + width + LASTING_GAP, valueY, valueFs, spec.colour, spec.flashMs),
    );
  } else {
    const fit = popUpFit(spec, frame);
    const x = frame.left + POP_UP_PAD_X;
    const { labelY, valueY } = popUpStack(frame, fit.valueFs);
    children.push(
      label(`${name}.label`, spec.label.text, x, labelY, labelWidth(spec.label), labelOpts),
      popUpRun(`${name}.value`, spec.value, x, valueY, fit.valueFs, spec.colour, spec.flashMs),
    );
    if (fit.secondary && spec.secondary) {
      const fs = POP_UP_SECONDARY_SIZE;
      const width = runWidth(spec.secondary, fs) + boxSlack(fs);
      const right = frame.left + frame.width - POP_UP_PAD_X - width;
      // On the value's baseline rather than on its line box, which is what puts a smaller run beside
      // a larger one everywhere else on the face.
      children.push(popUpRun(`${name}.secondary`, spec.secondary, right, canvasYForBaseline(canvasBaseline(valueY, fit.valueFs), fs), fs, ds.color.text.secondary, undefined, width));
    }
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
const atTheLine = (): Expr => and(hasTime(lastLap()), lt(secondsOf(game('CurrentLapTime')), num(POP_UP_SECONDS)));

/** A SimHub status property, read the null-safe way the LED catalogue reads the same ones. */
const on = (name: string): Expr => eq(isnull(game(name), num(0)), num(1));

/**
 * Lap time at the line: the previous lap, and the delta against the reference the driver chose.
 *
 * Transient: it is out for the three seconds after the line and then goes whatever the car does, so
 * it is drawn as the sheet draws it, over the gear and the zones either side, for those three seconds.
 */
export const LAP_POP_UP: PopUpSpec = {
  id: 'lap',
  kind: 'transient',
  label: { text: 'Lap' },
  value: { sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime, widest: LAP_TIME_WIDEST },
  colour: ds.color.text.primary,
  secondary: { sample: '−0.21', bind: referenceDeltaText(referenceDelta()), widest: REFERENCE_DELTA_WIDEST, chars: CHARS.referenceDelta },
  when: atTheLine(),
};

/**
 * Low fuel, flashing at the rate the flag band defines, so one rate is written once.
 *
 * Lasting: the tank is low from the threshold to the stop, which is laps, so it never covers the gear.
 *
 * The laps carry their unit inside the run, which is what the artboard draws and what stops "FUEL /
 * 0.8" being read as litres. That costs the run its cells: a word cannot sit in a digit cell, so
 * this one is proportional and is measured from the widest reading the threshold allows: two digits
 * and a tenth under the 99 laps the panel's number box will accept, each the widest figure of the
 * face. That is not `99.9 laps`: in Barlow Condensed SemiBold the 4 and the 0 are wider than the 9,
 * and `44.4 laps` is the reading that lost the `s` of its unit to a box measured from the nines (#994).
 */
export const FUEL_POP_UP: PopUpSpec = {
  id: 'fuel',
  kind: 'lasting',
  label: { text: 'Fuel' },
  value: {
    sample: '0.8 laps',
    bind: concat(fmt(isnull(computed('Fuel_RemainingLaps'), num(0)), '0.0'), str(' laps')),
    widest: `${widestFigure(DATA_FACE.SemiBold, '99.9')} laps`,
  },
  colour: ds.purpose.fuel.low,
  when: tankIsLow(),
  flashMs: FLAG_BLINK_MS,
};

/**
 * DRS available.
 *
 * Lasting: the flap is free for the length of a zone, and a zone is a straight, which is where a
 * driver changes up through the gears and has to read them.
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
  kind: 'lasting',
  label: { text: 'DRS' },
  value: { sample: 'Available' },
  colour: ds.color.good.primary,
  when: on('DRSAvailable'),
};

/**
 * The pop-ups in priority order within each kind, highest first.
 *
 * The lap time is the one transient pop-up. Of the two lasting ones danger outranks an aid, as it
 * does everywhere else on this face.
 */
export const POP_UPS: readonly PopUpSpec[] = [LAP_POP_UP, FUEL_POP_UP, DRS_POP_UP];

/** The pop-ups of one kind, in their order. */
export const popUpsOfKind = (kind: PopUpKind): readonly PopUpSpec[] => POP_UPS.filter((spec) => spec.kind === kind);

/**
 * `spec.when` and no higher pop-up of its kind's condition: the chain the flag band ranks itself with.
 * A lasting pop-up also waits for the pit family, whose banner row it shares.
 */
export function popUpVisible(id: string): Expr {
  const spec = POP_UPS.find((p) => p.id === id);
  if (!spec) throw new Error(`popUp: no pop-up ${id}`);
  const kind = popUpsOfKind(spec.kind);
  const higher = kind.slice(0, kind.indexOf(spec)).map((p) => not(p.when));
  return spec.kind === 'lasting' ? and(not(pitAlertOut()), ...higher, spec.when) : and(...higher, spec.when);
}

/** The transient pop-ups, each in the sheet's box centred on `room` and inside `body`. */
export const transientPopUps = (room: Rect, body: Rect = room, prefix = 'popUp'): Item[] =>
  popUpsOfKind('transient').map((spec) => popUp(popUpFrame(room, body), spec, prefix));

/** The lasting pop-ups, each in `row`, which is `lastingPopUpFrame`'s. */
export const lastingPopUps = (row: Rect, prefix = 'popUp'): Item[] => popUpsOfKind('lasting').map((spec) => popUp(row, spec, prefix));
