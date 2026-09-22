/**
 * lapReview: the debrief of the lap just finished, over the hero, for four seconds at the line.
 *
 * It is the fourth and largest of the face's transient boxes, after the pop-up, the change
 * notification and the pit alerts, and it is the only one a driver has to be given rather than
 * one they are simply shown: `<Face>LapReview` is off until somebody asks for it, because a
 * 1200 by 160 panel takes the gear with it every single lap and the lap-time pop-up already gives
 * the two figures a driver waits for at the line in a third of the room. That is the same reasoning
 * `DEFAULT_FLAG_FORMAT` is `band` for.
 *
 * **Four seconds without a clock of our own**, which is the constraint every box on this face is
 * built under (ADR 0009). `CurrentLapTime` counts from zero at every crossing, so "it is under four
 * seconds" is true for exactly the window the canvas asks for and is arithmetic over a published
 * property; `popUp.ts` reads its own three seconds the same way and the two windows are therefore
 * the same clock seen at two lengths.
 *
 * **How it ranks against what is already on the face.** The panel is centred on the same hero
 * rectangle the pop-ups and the change notification take, and it is larger than both in both
 * directions, so while it is out it covers them rather than arguing with them; it is pushed last in
 * `zones/face.ts`, after the pit alerts, so the limiter banner at the top of zone A is the one thing
 * that is not covered where the two rectangles meet. Band D is outside it altogether, which is
 * deliberate and is the same call the pit alerts made: a lap review and a flag are answers to
 * different questions, and a driver crossing the line under a full-course caution needs both. The
 * rev bar and the bar of settled values are outside it for the same reason they are outside the
 * pop-up.
 *
 * **It is a row of fields and not a drawing.** Every size the canvas quotes for this panel is the
 * companion density -- 116 over a 15 px label, 46 for the deltas and the fuel, 13 for the unit, 24
 * between two fields of a pair -- so the panel is built from `second/field.ts` at that density and
 * from `second/sectors.ts`'s own strip, rather than from placement code that would restate all six
 * numbers. What follows from that is the answer to a narrow face: a field is measured before it is
 * placed, so a panel with less room than its groups need drops a group rather than drawing past its
 * edge, which is what rule 17 asks of every module.
 *
 * The canvas draws twelve mini-sector cells. SimHub publishes no mini-sectors, so the strip is
 * `sectorStrip`'s three, which is the same substitution `second/sectors.ts` records and the same
 * one the sectors module makes.
 */
import type { Item, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { zone as zoneSetting, type FaceSize } from '../contract.ts';
import { densityOf } from '../second/density.ts';
import { fieldRowFitted, fieldTail, fieldWidth, rowHeight, type FieldSpec } from '../second/field.ts';
import { sectorStrip } from '../second/sectors.ts';
import {
  CHARS,
  NO_VALUE,
  carNumber,
  carPosition,
  positionLabelled,
  completedLaps,
  deltaColour,
  fuel,
  fuelLastLap,
  fuelLastLapIsSettled,
  fuelUnit,
  hasTime,
  lapTime,
  lastLap,
  player,
  previousLap,
  previousLapDelta,
  sessionType,
} from '../second/values.ts';
import { ds } from '../tokens.ts';

const { and, concat, eq, fmt, game, iff, isnull, lt, num, or, signed, str, sub, timespanToSeconds, ucase } = ncalc;

/** The density the canvas draws this panel at: 116 over a 15 px label, 46, 13, and 24 between a pair. */
const DENSITY = 'companion';

/**
 * The box the pagesandalerts artboard draws. The height is `indicator.lapReview.height`; the width
 * is the figure tokens.json does not carry, as it carries neither the pop-up's nor the change
 * notification's, so it is read off the sheet with the sheet as its citation.
 */
export const LAP_REVIEW_WIDTH = 1200;
export const LAP_REVIEW_HEIGHT = ds.indicator.lapReview.height;

/** The rule along the top edge, counted inside the box, as the other two boxes of this family draw it. */
export const LAP_REVIEW_RULE = 2;

/** Side padding: the artboard's 32, which is `space[6]`. */
export const LAP_REVIEW_PAD_X = ds.space[6];

/**
 * Between two groups of the panel. Forty is off the `space` scale, which goes 24 then 32 then 48,
 * so the literal stays here with the artboard as its citation, the way the change notification's
 * 28 px padding does.
 */
export const LAP_REVIEW_GROUP_GAP = 40;

/** Between the driver line, the sector strip and the deltas under them, off the same sheet. */
export const LAP_REVIEW_STACK_GAP = 10;

/** The strip's own height, which the artboard draws as a 10 px bar and not as the density's gauge. */
export const LAP_REVIEW_STRIP_HEIGHT = 10;

/** The width the artboard gives the strip, which is what it is drawn at where there is room for it. */
export const LAP_REVIEW_STRIP_WIDTH = 300;

/** How long the artboard leaves the panel up, in seconds: `indicator.lapReview.durationMs`. */
export const LAP_REVIEW_SECONDS = ds.indicator.lapReview.durationMs / 1000;

/**
 * Four seconds after the line, read off the lap that has started rather than off a clock, and only
 * once a lap has actually been set: the out lap would otherwise open on a panel of placeholders.
 *
 * The same shape as `popUp.ts`'s own `atTheLine`, deliberately not shared with it: the two windows
 * are different lengths, the pop-up's three seconds against this one's four, and a helper taking a
 * duration would read as though the two were one decision.
 */
export const lapReviewAtTheLine = (): Expr =>
  and(hasTime(lastLap()), lt(timespanToSeconds(isnull(game('CurrentLapTime'), num(0))), num(LAP_REVIEW_SECONDS)));

/**
 * Whether this face wants the review in the session being driven.
 *
 * `Race` is the one session name OpenDash can match with certainty and is the string the pit wall
 * header already writes beside the session, so the comparison goes through `sessionType()` rather
 * than through a second spelling of the same property. `contract.ts` carries the setting and this
 * file carries what it is compared against, because the telemetry cannot be imported there without
 * a cycle.
 */
export const lapReviewWanted = (face: FaceSize): Expr =>
  or(zoneSetting.lapReviewIs(face, 'all'), and(zoneSetting.lapReviewIs(face, 'race'), eq(ucase(sessionType()), str('RACE'))));

/** The panel is wanted and the lap has just landed: the one expression the face binds it to. */
export const lapReviewOut = (face: FaceSize): Expr => and(lapReviewWanted(face), lapReviewAtTheLine());

/** The lap just finished, which is the lap the panel is about. */
const lapNumber = (): Expr => concat(str('LAP '), fmt(completedLaps(), '0'));

/**
 * `YOU · #12 · P4`: who the lap belongs to, the car number and the place it left you in.
 *
 * `YOU` is written rather than bound to the driver's own name, which is what the artboard draws:
 * the panel is over your own gear and the one thing it cannot be about is somebody else.
 */
const driverLine = (): Expr => concat(str('YOU · #'), carNumber(player()), str(' · '), positionLabelled(player()));

/** The widest the driver line can draw: `CHARS.carNumber`'s four digits and a two-digit place. */
export const WIDEST_DRIVER_LINE = 'YOU · #9999 · P99';

/**
 * The lap just finished against the best of the session, which the lap-history plugin publishes per
 * slot rather than being worked out here.
 *
 * `PersistantTrackerPlugin.PreviousLap_00_DeltaToSessionBest` is the delta of the lap in slot zero,
 * and slot zero is the lap just completed -- the same reading `modules/lapHistory.ts` draws its
 * first row from, and the reason `average5` starts at zero rather than at one.
 */
const vsSessionBest = (): Expr => iff(hasTime(lastLap()), signed(isnull(previousLapDelta(num(0)), num(0)), '0.00'), str(NO_VALUE));

/**
 * The lap just finished against the one before it: arithmetic over two published properties, which
 * is what ADR 0009 leaves a dashboard able to say for itself.
 *
 * Slot one and not slot zero. Slot zero is the lap this panel is reporting, so the difference
 * against it would be nought on every lap of every race; the lap before it is slot one, and until
 * there have been two laps there is nothing to compare and the field says so.
 */
const previousLapSeconds = (): Expr => timespanToSeconds(isnull(previousLap(num(1)), num(0)));
const vsPreviousSeconds = (): Expr => sub(timespanToSeconds(isnull(lastLap(), num(0))), previousLapSeconds());
const vsPrevious = (): Expr => iff(and(hasTime(lastLap()), hasTime(previousLap(num(1)))), signed(vsPreviousSeconds(), '0.00'), str(NO_VALUE));

/** A delta field of the panel: 46 px, coloured by the comparison rather than by a second reading of it. */
const deltaField = (prefix: string, id: string, caption: string, value: Expr, colour: Expr, sample: string): FieldSpec => ({
  name: `${prefix}.${id}`,
  id,
  label: caption,
  value: { sample, bind: value, chars: CHARS.delta, fs: densityOf(DENSITY).mid, colorBind: colour },
});

/**
 * A fuel field: 46 px with the sim's own unit after it, which is `L` or `gal` and never both.
 *
 * `guard` is the condition under which the value is a reading at all; without one the field draws
 * the number as it is. The fuel used carries one and the tank does not, because the tank is a
 * reading of its own at every moment whereas a lap's consumption is a figure SimHub publishes as
 * zero for a lap that never happened, and a review of the lap that said the lap cost nothing was
 * contradicting the band and the fuel page it had been brought into agreement with (#382).
 */
const fuelField = (prefix: string, id: string, caption: string, value: Expr, sample: string, chars = CHARS.fuel, guard?: Expr): FieldSpec => ({
  name: `${prefix}.${id}`,
  id,
  label: caption,
  value: {
    sample,
    bind: guard ? iff(guard, fmt(value, '0.0'), str(NO_VALUE)) : fmt(value, '0.0'),
    chars,
    fs: densityOf(DENSITY).mid,
    follower: { text: 'L', widest: 'gal', bind: fuelUnit(), size: densityOf(DENSITY).labelSm },
  },
});

/** The lap time itself: the one field of the panel that is never shed. */
const lapField = (prefix: string, fs: number = densityOf(DENSITY).hero): FieldSpec => ({
  name: `${prefix}.lap`,
  id: 'lap',
  label: 'LAP 12',
  labelBind: lapNumber(),
  labelWidest: 'LAP 999',
  value: { sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime, fs },
});

const deltaFields = (prefix: string): FieldSpec[] => [
  deltaField(prefix, 'vsBest', 'vs session best', vsSessionBest(), deltaColour(isnull(previousLapDelta(num(0)), num(0))), '+1.03'),
  deltaField(prefix, 'vsPrevious', 'vs previous', vsPrevious(), deltaColour(vsPreviousSeconds()), '−0.21'),
];

const fuelFields = (prefix: string): FieldSpec[] => [
  fuelField(prefix, 'fuelUsed', 'Fuel used', fuelLastLap(), '2.84', CHARS.consumption, fuelLastLapIsSettled()),
  fuelField(prefix, 'fuelLeft', 'Fuel left', fuel(), '38.4'),
];

/**
 * The panel, centred on the rectangle the face calls its hero, clamped to the face it is drawn on.
 *
 * Centred rather than placed, so that the same component sits over the gear on the 1920 reference
 * face and on the 600 portrait one without either of them stating a coordinate; clamped in width,
 * because the artboard's 1200 is wider than five of the eight faces and a panel is a function of
 * the rectangle it is given rather than a fixed picture. Everything inside it is measured against
 * whatever width that leaves.
 */
export function lapReviewFrame(hero: Rect, faceWidth: number): Rect {
  const width = Math.min(LAP_REVIEW_WIDTH, faceWidth);
  const left = Math.max(0, Math.min(faceWidth - width, hero.left + (hero.width - width) / 2));
  return roundRect({ left, top: hero.top + (hero.height - LAP_REVIEW_HEIGHT) / 2, width, height: LAP_REVIEW_HEIGHT });
}

/** How wide a row of fields is at this density, gaps included. */
const rowWidth = (specs: readonly FieldSpec[], gap: number): number =>
  specs.reduce((sum, spec) => sum + fieldWidth(spec, DENSITY), 0) + gap * Math.max(0, specs.length - 1);

/**
 * What the panel keeps in the room it has, and at what size it sets the lap time.
 *
 * Two rules, in this order, and they are rule 17 in the small exactly as `popUpFit` states it.
 *
 * **Shed before shrinking.** The order is the canvas's own reading of the row: the lap time is what
 * the panel is for, the two deltas are what it is worth, the driver line and its sector strip say
 * how the lap was made, and the fuel pair is the only part a driver could read on the next straight
 * instead. So the fuel goes first, then the line and the strip, and the lap time never goes at all.
 *
 * **The deltas are the floor.** The panel covers the lap-time pop-up while it is out, and that
 * pop-up already carries the lap and one delta in 560 px; a review that had shed its way down to a
 * lap time alone would be four seconds of the gear spent saying less than the box it replaced. So
 * where the deltas will not fit beside a 116 px lap time, the lap time steps down the density's own
 * ramp to 64 rather than the deltas being dropped, which is what the portrait face at 600 px asks
 * for. The last line below is a guard and not a case: no face that ships is narrower than a 64 px
 * lap time and two deltas.
 */
export interface LapReviewFit {
  /** The size the lap time is set at: the density's `hero`, or its `big` on a narrow panel. */
  lapFs: number;
  /** The driver line and the sector strip above the deltas. */
  strip: boolean;
  deltas: boolean;
  fuel: boolean;
}

export function lapReviewFit(frame: Rect): LapReviewFit {
  const prefix = 'fit';
  const d = densityOf(DENSITY);
  const room = frame.width - 2 * LAP_REVIEW_PAD_X;
  const deltas = rowWidth(deltaFields(prefix), d.gapX);
  const group = Math.max(deltas, LAP_REVIEW_STRIP_WIDTH);
  const fuel = rowWidth(fuelFields(prefix), d.gapX);
  const fits = (parts: readonly number[]): boolean => parts.reduce((sum, w) => sum + w, 0) + LAP_REVIEW_GROUP_GAP * (parts.length - 1) <= room;
  for (const lapFs of [d.hero, d.big]) {
    const lap = fieldWidth(lapField(prefix, lapFs), DENSITY);
    if (fits([lap, group, fuel])) return { lapFs, strip: true, deltas: true, fuel: true };
    if (fits([lap, group])) return { lapFs, strip: true, deltas: true, fuel: false };
    if (fits([lap, deltas])) return { lapFs, strip: false, deltas: true, fuel: false };
  }
  return { lapFs: d.big, strip: false, deltas: false, fuel: false };
}

/**
 * The driver group: the line, the sector strip under it, and the two deltas under that.
 *
 * Three rows rather than a rank, because they are three different kinds of thing stacked rather
 * than a row of fields: `stack` would have to be told each one's height anyway and the strip is not
 * a field at all.
 */
function driverGroup(prefix: string, left: number, bottom: number, width: number, fit: LapReviewFit): Item[] {
  const d = densityOf(DENSITY);
  const deltas = deltaFields(prefix);
  const items: Item[] = fit.deltas ? fieldRowFitted(deltas, left, bottom, width, DENSITY, { gap: d.gapX }).items : [];
  if (!fit.strip) return items;
  const stripBottom = (fit.deltas ? bottom - rowHeight(deltas, DENSITY) : bottom) - LAP_REVIEW_STACK_GAP;
  const strip = rect(left, stripBottom - LAP_REVIEW_STRIP_HEIGHT, Math.min(width, LAP_REVIEW_STRIP_WIDTH), LAP_REVIEW_STRIP_HEIGHT);
  return [
    label(`${prefix}.driver`, 'YOU · #12 · P4', left, strip.top - LAP_REVIEW_STACK_GAP - d.label, width, {
      bind: driverLine(),
      widest: WIDEST_DRIVER_LINE,
    }),
    ...sectorStrip(`${prefix}.sectors`, strip),
    ...items,
  ];
}

/**
 * The panel's items, behind nothing: the caller decides when it is out.
 *
 * The three groups sit on one baseline, which is the rule a row mixing a 116 px lap time with a
 * 46 px fuel reading is laid out by everywhere else (`second/field.ts`), and the fuel pair is
 * pushed to the right edge, which is the artboard's `margin-left: auto`.
 */
export function lapReviewItems(frame: Rect, prefix = 'lapReview'): Item[] {
  const d = densityOf(DENSITY);
  const fit = lapReviewFit(frame);
  const lap = lapField(prefix, fit.lapFs);
  const inner = frame.top + LAP_REVIEW_RULE;
  const height = frame.height - LAP_REVIEW_RULE;
  // The tallest group decides the baseline, and the lap time is always the tallest: the block is
  // centred in what the rule leaves, and then pushed up by however far the WPF box of a 116 px run
  // hangs below the line it is placed on. Without that last term the box of the one run the panel
  // exists for ends eight pixels past the panel's own bottom edge, which is a box drawn outside its
  // frame even though the glyphs inside it are not.
  const bottom = Math.min(inner + (height + rowHeight([lap], DENSITY)) / 2, frame.top + frame.height - fieldTail(lap, DENSITY));
  const left = frame.left + LAP_REVIEW_PAD_X;
  const right = frame.left + frame.width - LAP_REVIEW_PAD_X;
  const lapWidth = fieldWidth(lap, DENSITY);
  const items: Item[] = [
    band(`${prefix}.box`, frame, ds.purpose.popUp.surface),
    band(`${prefix}.rule`, rect(frame.left, frame.top, frame.width, LAP_REVIEW_RULE), ds.purpose.popUp.rule),
    ...fieldRowFitted([lap], left, bottom, lapWidth, DENSITY).items,
  ];
  const fuel = fuelFields(prefix);
  const fuelWidth = fit.fuel ? rowWidth(fuel, d.gapX) : 0;
  if (fit.fuel) items.push(...fieldRowFitted(fuel, right - fuelWidth, bottom, fuelWidth, DENSITY, { gap: d.gapX }).items);
  if (fit.deltas) {
    const driverLeft = left + lapWidth + LAP_REVIEW_GROUP_GAP;
    const driverRight = fit.fuel ? right - fuelWidth - LAP_REVIEW_GROUP_GAP : right;
    items.push(...driverGroup(prefix, driverLeft, bottom, Math.max(0, driverRight - driverLeft), fit));
  }
  return items;
}

/** The panel behind the expression that decides when it is out. */
export function lapReview(frame: Rect, when: Expr, prefix = 'lapReview'): LayerItem {
  return withMoreBindings({ kind: 'layer', name: prefix, children: lapReviewItems(frame, prefix) }, { Visible: when });
}
