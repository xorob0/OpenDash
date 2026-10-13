/**
 * changeNotification: the box that says a car setting has just moved, and which way.
 *
 * A driver who turns a dial cannot look at the strip to see where it landed, which is the whole
 * reason this exists: the value comes to the middle of the face for three seconds, in the setting's
 * own name, with the direction beside it, and then it goes.
 *
 * **Three seconds without a clock of our own.** `changed(ms, value)` is SimHub's own window
 * function: it is true for `ms` after the property last moved and false again afterwards, and ADR
 * 0009's "state that is SimHub's own" is the line that admits it. So the life of the notification is
 * the condition rather than a duration attached to a screen, exactly as the lap-time pop-up reads
 * its three seconds off `CurrentLapTime`. Nothing here keeps state, and a package installed without
 * the plugin evaluates the same window.
 *
 * That also answers the sweep. A rotary turned through four positions moves the property four times
 * within one window, so the window is one window and the box is out once, showing whichever position
 * the dial has reached. `indicator.changeNotification.settleFrames` counts frames for a settle this
 * does not need and is therefore unread; the difference from the canvas is that the value shown
 * during a sweep is the live one rather than the one it settles on, which is the same number a
 * moment later.
 *
 * **One at a time, and under the lap time.** The box is centred on the same rectangle the lap-time
 * pop-up takes, so the two would otherwise draw over one another; the notification therefore waits
 * for that one, the other pop-up that is an event rather than a state. Low fuel and DRS hold for laps
 * and seconds and are drawn in the pit banner's row instead, clear of this box.
 *
 * **A real pop-up, over the whole face** (#1047). It is out for three seconds and then goes, so it is
 * drawn at the sheet's 400 by 96 wherever the body has the room, over the gear and over zones B and C
 * either side for those seconds, rather than shrunk to zone A's width and its reading stepped down to
 * fit: a driver who has just turned a dial reads it at a glance or not at all. What it never covers is
 * what lies outside the body, the rev bar, the bar of settled values and band D, nor the pit banner.
 * The reading steps down beside the setting's name, or goes under it, only in a body too narrow for
 * the sheet's box, which no shipped face has.
 *
 * The one thing the canvas is emphatic about is the trend mark's colour: it wears `text.secondary`
 * and never a purpose colour, because which way a setting moved is information and not a state. The
 * leaderboard's rank mark is the opposite convention, green up and red down, and the two are
 * separate files for that reason.
 */
import type { Item, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { assetBox, imageOf, TREND_DOWN, TREND_UP } from '../design/assets.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { boxSlack, cells, monoWidth, textBox, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { FIT_LADDER } from '../second/field.ts';
import { TRACKED_VALUES, hasSetting, type TrackedValue } from '../second/tracked.ts';
import { inTheCar } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { LAP_POP_UP, sheetFrame } from './popUp.ts';

const { and, changed, fmt, isdecreasing, isincreasing, not, num } = ncalc;

/**
 * The box the pagesandalerts artboard draws.
 *
 * The height is `indicator.changeNotification.height`; the width is the figure tokens.json does not
 * carry, as it does not carry the pop-up's, so it is read off the sheet with the sheet as its
 * citation.
 */
export const CHANGE_NOTIFICATION_WIDTH = 400;
export const CHANGE_NOTIFICATION_HEIGHT = ds.indicator.changeNotification.height;

/** The rule along the top edge, counted inside the box, as the pop-up's is. */
export const CHANGE_NOTIFICATION_RULE = 2;

/** Side padding. Off the sheet: it is neither of the two spacing steps either side of it. */
export const CHANGE_NOTIFICATION_PAD_X = 28;

/** The 64 px the sheet sets the new value in, which is the pop-up's value size. */
export const CHANGE_NOTIFICATION_VALUE_SIZE = ds.size.lapTime;

/** The trend mark's square, and the room between it and the value. */
export const TREND_SIZE = 14;
export const TREND_GAP = ds.space[3];

/** How long the window stays open after the setting moves: `indicator.changeNotification.durationMs`. */
export const CHANGE_NOTIFICATION_MS = ds.indicator.changeNotification.durationMs;

/**
 * The value has moved within the window, and is worth showing.
 *
 * The canvas asks for a value that is zero or empty at the start of a session to be ignored, which
 * needs the start value remembered and nothing here remembers anything. It is approximated as "the
 * car has this setting at all": a car that does not publish it raises nothing, which is the case the
 * clause is really about, while a setting that genuinely moves to zero still shows. The test is the
 * strip's own `hasSetting`, because for three of the seven the reading is defaulted and so is never
 * null itself.
 */
const moved = (value: TrackedValue): Expr => and(inTheCar(), hasSetting(value), changed(num(CHANGE_NOTIFICATION_MS), value.read));

/**
 * The one pop-up that outranks this box.
 *
 * Only the lap time, and the reason is the difference between the three. A lap time at the line is
 * out for three seconds and is the thing a driver is waiting for, so a dial turned at the same
 * moment waits its turn. Low fuel and DRS are not events: the tank is low for the rest of a stint
 * and the flap is free on every straight, so a notification that waited for them would be silent
 * for exactly the part of a race where a driver moves the most settings. They are drawn in the pit
 * banner's row rather than over the hero (#1047), so the two are out together and neither covers the
 * other.
 */
const lapPopUpOut = (): Expr => LAP_POP_UP.when;

/**
 * This setting moved and no higher one did: the exclusion chain band D and the pop-ups both rank
 * with, so that two notifications can never be out at once over the same rectangle.
 */
export function changeNotificationVisible(id: string): Expr {
  const index = TRACKED_VALUES.findIndex((value) => value.id === id);
  const value = TRACKED_VALUES[index];
  if (!value) throw new RangeError(`changeNotification: no tracked value ${id}`);
  return and(not(lapPopUpOut()), ...TRACKED_VALUES.slice(0, index).map((higher) => not(moved(higher))), moved(value));
}

/** What the reading takes in cells: a number with one decimal, or a bare one. */
const valueChars = (value: TrackedValue): Chars => ({
  digits: value.sample.replace('.', '').length,
  specials: value.sample.includes('.') ? 1 : 0,
});

/**
 * The box: the sheet's 400 by 96, centred on the room the lap-time pop-up is centred on and kept
 * inside the body, which is `popUpFrame`'s rule at the notification's size.
 *
 * Centred rather than placed, so that the same component sits over the gear on the reference face
 * and on the portrait one without either of them stating a coordinate. The sheet's 400 by 96 is the
 * most it takes, as the pop-up's 560 by 120 is the most that one does, so the notification stays
 * inside the pop-up's frame on every face and covers no more than a pop-up would.
 */
export const changeNotificationFrame = (room: Rect, body: Rect = room): Rect => sheetFrame(room, body, CHANGE_NOTIFICATION_WIDTH, CHANGE_NOTIFICATION_HEIGHT);

/** The name's box: its measured advances at the label size, and the slack every text box carries. */
const nameWidth = (value: TrackedValue): number => Math.ceil(measureText('BarlowMedium', value.notice, ds.size.label)) + boxSlack(ds.size.label);

/** The reading's box at a size: its cells, and the slack. */
const readingWidth = (value: TrackedValue, fs: number): number => monoWidth(cells('SemiBold', fs), valueChars(value)) + boxSlack(fs);

/** How a notification is set in the box it is given. */
export interface ChangeNotificationFit {
  /** The reading's size: the sheet's 64, or a step down from it. */
  valueFs: number;
  /**
   * The name over the reading rather than beside it, for a box too narrow to hold the two side by
   * side at the ladder's last step.
   */
  stacked: boolean;
}

/** The smallest a reading is set, the floor every fitted run on the face shares. */
const LEAST_VALUE_SIZE = 12;

/** The pop-up's 4 px between a label's line box and the value's, which is the anatomy a stacked box borrows. */
const STACK_GAP = ds.space[1];

/** Where the name and the reading are set, given the box and the fit. */
function rows(frame: Rect, fit: ChangeNotificationFit): { labelY: number; valueY: number } {
  const inner = frame.top + CHANGE_NOTIFICATION_RULE;
  const height = frame.height - CHANGE_NOTIFICATION_RULE;
  if (!fit.stacked) return { labelY: inner + (height - ds.size.label) / 2, valueY: inner + (height - fit.valueFs) / 2 };
  const labelY = inner + (height - (ds.size.label + STACK_GAP + fit.valueFs)) / 2;
  return { labelY, valueY: labelY + ds.size.label + STACK_GAP };
}

/** Whether both lines' boxes are drawn between the rule and the foot of the box. */
function tallEnough(frame: Rect, fit: ChangeNotificationFit): boolean {
  const { labelY, valueY } = rows(frame, fit);
  const top = frame.top + CHANGE_NOTIFICATION_RULE;
  const bottom = frame.top + frame.height;
  const label = textBox(labelY, ds.size.label);
  const value = textBox(valueY, fit.valueFs);
  return Math.round(label.top) >= top && Math.round(value.top) >= top && Math.round(label.top) + label.height <= bottom && Math.round(value.top) + value.height <= bottom;
}

/**
 * How the name and the reading share a box: the sheet's 64 beside the name where the name, the
 * reading and the mark fit in a row with the sheet's 12 between each, and otherwise the first step
 * of the ladder `popUpFit` takes that does.
 *
 * A box too narrow for even the ladder's last step beside the name sets the name over the reading,
 * 4 px above it as a pop-up's label is, and gives the reading the whole width. The name is never the
 * part that gives: which setting moved is what the box is for, and nothing is shed, because there is
 * nothing in this box a driver could do without.
 */
export function changeNotificationFit(frame: Rect, value: TrackedValue): ChangeNotificationFit {
  const mark = TREND_GAP + TREND_SIZE;
  const inner = frame.width - 2 * CHANGE_NOTIFICATION_PAD_X - mark;
  const ladder = FIT_LADDER.map((factor) => Math.round(CHANGE_NOTIFICATION_VALUE_SIZE * factor));
  for (const valueFs of ladder) {
    const fit = { valueFs, stacked: false };
    if (nameWidth(value) + TREND_GAP + readingWidth(value, valueFs) <= inner && tallEnough(frame, fit)) return fit;
  }
  for (let valueFs = CHANGE_NOTIFICATION_VALUE_SIZE; valueFs > LEAST_VALUE_SIZE; valueFs--) {
    const fit = { valueFs, stacked: true };
    if (readingWidth(value, valueFs) <= inner && nameWidth(value) <= frame.width - 2 * CHANGE_NOTIFICATION_PAD_X && tallEnough(frame, fit)) return fit;
  }
  return { valueFs: LEAST_VALUE_SIZE, stacked: true };
}

/** One notification: the name on the left, and the reading with its trend mark against the right edge. */
export function changeNotification(frame: Rect, value: TrackedValue, prefix = 'notice'): LayerItem {
  const name = `${prefix}.${value.id}`;
  const fit = changeNotificationFit(frame, value);
  const size = fit.valueFs;
  const { labelY, valueY } = rows(frame, fit);
  const right = frame.left + frame.width - CHANGE_NOTIFICATION_PAD_X;
  // The mark is centred on the reading's line, which is the middle of the box when the two share it.
  const trend = rect(right - TREND_SIZE, valueY + (size - TREND_SIZE) / 2, TREND_SIZE, TREND_SIZE);
  const reading = readingWidth(value, size);
  const labelWidth = nameWidth(value);
  const children: Item[] = [
    band(`${name}.box`, frame, ds.purpose.popUp.surface),
    band(`${name}.rule`, rect(frame.left, frame.top, frame.width, CHANGE_NOTIFICATION_RULE), ds.purpose.popUp.rule),
    label(`${name}.label`, value.notice, frame.left + CHANGE_NOTIFICATION_PAD_X, labelY, labelWidth),
    numeral(`${name}.value`, value.sample, trend.left - TREND_GAP - reading, valueY, size, valueChars(value), {
      width: reading,
      hAlign: 'right',
      bind: fmt(value.read, value.pattern),
    }),
    // One file per direction, because an image carries neither colour nor rotation, and both are
    // hidden while the setting holds still: a mark with no direction to report is no mark.
    ...([
      [TREND_UP, isincreasing(num(CHANGE_NOTIFICATION_MS), value.read), 'up'],
      [TREND_DOWN, isdecreasing(num(CHANGE_NOTIFICATION_MS), value.read), 'down'],
    ] as const).map(([asset, visible, id]) => withMoreBindings({
      kind: 'image' as const,
      name: `${name}.trend.${id}`,
      image: asset.name,
      rect: assetBox(trend, imageOf(asset)),
    }, { Visible: visible })),
  ];
  return withMoreBindings({ kind: 'layer', name, children }, { Visible: changeNotificationVisible(value.id) });
}

/** Every watched setting's notification, centred on the room the lap-time pop-up takes and inside the body. */
export const changeNotifications = (room: Rect, body: Rect = room, prefix = 'notice'): Item[] =>
  TRACKED_VALUES.map((value) => changeNotification(changeNotificationFrame(room, body), value, prefix));
