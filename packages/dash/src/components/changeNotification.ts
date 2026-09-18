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
 * **One at a time, and under the lap time.** The box is centred on the same hero rectangle a pop-up
 * takes, so the two would otherwise draw over one another; the notification therefore waits for the
 * one pop-up that is an event rather than a state, and covers the other two for its three seconds.
 *
 * The one thing the canvas is emphatic about is the trend mark's colour: it wears `text.secondary`
 * and never a purpose colour, because which way a setting moved is information and not a state. The
 * leaderboard's rank mark is the opposite convention, green up and red down, and the two are
 * separate files for that reason.
 */
import type { Item, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { assetBox, imageOf, TREND_DOWN, TREND_UP } from '../design/assets.ts';
import { measureText } from '../design/advances.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { boxSlack, cells, monoWidth, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { TRACKED_VALUES, type TrackedValue } from '../second/tracked.ts';
import { ds } from '../tokens.ts';
import { LAP_POP_UP } from './popUp.ts';

const { and, changed, fmt, isdecreasing, isincreasing, isNull, isnull, not, num, raw } = ncalc;

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
 * The driver is in the car: iRacing's `IsOnTrack`, which is "car on track physics running with
 * player in car" and is exactly the canvas's condition.
 *
 * Read as a boolean and not compared with a number. A raw telemetry boolean reaches a binding as
 * `true` or `false`, where `GameData`'s booleans arrive as 1 and 0 -- the committed traces show
 * both forms side by side -- so `= 1` would never hold and the gate would silence every
 * notification. It is the same reading `carAvailable` uses for the leaderboard's rows.
 *
 * The default is `true`, so a sim that publishes nothing leaves the notifications showing rather
 * than suppressing all of them.
 */
const inTheCar = (): Expr => isnull(raw('IsOnTrack'), 'true');

/**
 * The value has moved within the window, and is worth showing.
 *
 * The canvas asks for a value that is zero or empty at the start of a session to be ignored, which
 * needs the start value remembered and nothing here remembers anything. It is approximated as "the
 * car has this setting at all": a car that does not publish it raises nothing, which is the case the
 * clause is really about, while a setting that genuinely moves to zero still shows. The test is the
 * strip's own `present`, because for three of the seven the reading is defaulted and so is never
 * null itself.
 */
const moved = (value: TrackedValue): Expr =>
  and(inTheCar(), not(isNull(value.present ?? value.read)), changed(num(CHANGE_NOTIFICATION_MS), value.read));

/**
 * The one pop-up that outranks this box.
 *
 * Only the lap time, and the reason is the difference between the three. A lap time at the line is
 * out for three seconds and is the thing a driver is waiting for, so a dial turned at the same
 * moment waits its turn. Low fuel and DRS are not events: the tank is low for the rest of a stint
 * and the flap is free on every straight, so a notification that waited for them would be silent
 * for exactly the part of a race where a driver moves the most settings. Those two are covered for
 * the three seconds and come back, which is what a condition that is still true does.
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
 * The box, centred on the rectangle the face calls its hero, which is where a pop-up goes.
 *
 * Centred rather than placed, so that the same component sits over the gear on the reference face
 * and on the portrait one without either of them stating a coordinate.
 */
export const changeNotificationFrame = (hero: Rect): Rect =>
  roundRect({
    left: hero.left + (hero.width - CHANGE_NOTIFICATION_WIDTH) / 2,
    top: hero.top + (hero.height - CHANGE_NOTIFICATION_HEIGHT) / 2,
    width: CHANGE_NOTIFICATION_WIDTH,
    height: CHANGE_NOTIFICATION_HEIGHT,
  });

/** One notification: the name on the left, and the reading with its trend mark against the right edge. */
export function changeNotification(frame: Rect, value: TrackedValue, prefix = 'notice'): LayerItem {
  const name = `${prefix}.${value.id}`;
  const size = CHANGE_NOTIFICATION_VALUE_SIZE;
  const inner = frame.top + CHANGE_NOTIFICATION_RULE;
  const height = frame.height - CHANGE_NOTIFICATION_RULE;
  const right = frame.left + frame.width - CHANGE_NOTIFICATION_PAD_X;
  const trend = rect(right - TREND_SIZE, inner + (height - TREND_SIZE) / 2, TREND_SIZE, TREND_SIZE);
  const reading = monoWidth(cells('SemiBold', size), valueChars(value)) + boxSlack(size);
  const labelWidth = Math.ceil(measureText('BarlowMedium', value.notice.toUpperCase(), ds.size.label)) + boxSlack(ds.size.label);
  const children: Item[] = [
    band(`${name}.box`, frame, ds.purpose.popUp.surface),
    band(`${name}.rule`, rect(frame.left, frame.top, frame.width, CHANGE_NOTIFICATION_RULE), ds.purpose.popUp.rule),
    label(`${name}.label`, value.notice, frame.left + CHANGE_NOTIFICATION_PAD_X, inner + (height - ds.size.label) / 2, labelWidth),
    numeral(`${name}.value`, value.sample, trend.left - TREND_GAP - reading, inner + (height - size) / 2, size, valueChars(value), {
      width: reading,
      hAlign: 'right',
      bind: fmt(value.read, value.pattern),
    }),
    // One file per direction, because an image carries neither colour nor rotation, and both are
    // hidden while the setting holds still: a mark with no direction to report is no mark.
    ...([
      [TREND_UP, isincreasing(num(CHANGE_NOTIFICATION_MS), value.read), 'up'],
      [TREND_DOWN, isdecreasing(num(CHANGE_NOTIFICATION_MS), value.read), 'down'],
    ] as const).map(([asset, visible, id]) => ({
      kind: 'image' as const,
      name: `${name}.trend.${id}`,
      image: asset.name,
      rect: assetBox(trend, imageOf(asset)),
      ...withBindings({ Visible: visible }),
    })),
  ];
  return { kind: 'layer', name, children, ...withBindings({ Visible: changeNotificationVisible(value.id) }) };
}

/** Every watched setting's notification, over the hero of a face. */
export const changeNotifications = (hero: Rect, prefix = 'notice'): Item[] =>
  TRACKED_VALUES.map((value) => changeNotification(changeNotificationFrame(hero), value, prefix));
