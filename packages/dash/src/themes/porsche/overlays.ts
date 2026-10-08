/**
 * The car's three overlay states, drawn over the regions they take while the zones stay built
 * beneath: the limiter over the whole body, green under the pit lane limit and red over it, with
 * the speed, the limit and the gear still in the middle; and in zone C's panel, the blue box of a
 * setting that has just moved and the red alarm of a tank under ten litres.
 *
 * Each is one of the house's states drawn the car's way rather than a state of its own. The limiter
 * body is the house pit family's `limiter` state, shown exactly when that state's banner is, and is
 * drawn over the banner, which therefore never shows in this theme; the four states of the family
 * that are mistakes keep the house's banner at the top of the gear. The blue box is the house's
 * change notification, one per watched setting under the same exclusion chain, moved from the hero
 * to zone C as the car draws it. The alarm is the car's own threshold, `FuelLevel` under ten litres,
 * which iRacing publishes in litres whatever the display unit; it is the one condition here the
 * house does not already draw, and the house's own low-fuel pop-up, which asks about laps rather
 * than litres, still takes the hero when its condition holds.
 *
 * The alarm's warning triangle is the telltale column's hazard pictogram, drawn in the alarm's ink.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings } from '../../bind.ts';
import { changeNotificationVisible } from '../../components/changeNotification.ts';
import { pitAlertVisible } from '../../components/pitAlerts.ts';
import { GEAR_BOX_SLACK, GEAR_CHARS } from '../../components/gear.ts';
import { rect } from '../../design/geometry.ts';
import { gearCells, monoWidth } from '../../design/metrics.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { numeral } from '../../elements/numeral.ts';
import { TRACKED_VALUES } from '../../second/tracked.ts';
import { speed } from '../../second/values.ts';
import { ds, TRANSPARENT } from '../../tokens.ts';
import { gearSizeIn } from '../../zones/zoneAPages.ts';
import { regionRect, zoneRect } from '../anatomy.ts';
import type { FaceContext } from '../drawing.ts';
import { panelOf } from './body.ts';
import { picture } from './pictograms.ts';
import { BORDER, BOX_PAD, centredY, lowFuelAlarm, RADIUS, runWidth } from './register.ts';

const { add, fmt, game, gt, iff, isnull, lt, num, raw, str, concat } = ncalc;

/**
 * The limiter body's own layout, from the ticket: padding 8, 18 and 10, a 300 px column each side,
 * the speed 28 px under its label, and the limit's box padded as a setting box is.
 */
const LIMITER_AT_REFERENCE = { padTop: 8, padX: 18, padBottom: 10, column: 300, label: 24, speed: 96, speedTop: 28, pad: BOX_PAD, limit: { width: 230, height: 50, value: 40 } };

/** The reference body the limiter is drawn for, 957 by 226, and zone C's panel, 350 by 226. */
const BODY = { width: 957, height: 226 };
const PANEL = { width: 350, height: 226 };

/**
 * A layout's numbers at `k`, each rounded: a box smaller than the reference's draws everything in it
 * smaller by the same factor, and one as large or larger draws it as the reference does. This is the
 * ticket's rule that a font follows the box it is drawn in, the box here being the region the state
 * takes over.
 */
function scaled<T>(layout: T, k: number): T {
  if (typeof layout === 'number') return Math.round(layout * k) as T;
  if (layout !== null && typeof layout === 'object') return Object.fromEntries(Object.entries(layout).map(([key, value]) => [key, scaled(value, k)])) as T;
  return layout;
}

/** How much smaller than `reference` a frame is, never larger than one. */
const shrink = (frame: Rect, reference: { width: number; height: number }): number => Math.min(1, frame.width / reference.width, frame.height / reference.height);

/**
 * The pit lane limit in the driver's own unit, which is the unit `SpeedLocal` is in, so the two are
 * compared as they are drawn. An unpublished limit reads as one nothing reaches, so the body stays
 * green rather than going red on a track whose limit the sim does not say.
 */
const pitLimit = (): string => isnull(game('PitLimiterSpeed'), num(999));
/** A limiter holds the car a fraction over its figure, so one unit is let by before the body turns red. */
const overTheLimit = (): string => gt(speed(), add(pitLimit(), num(1)));


/** The change box's name and value, and the alarm's two lines, as the canvas sets them. */
const NOTICE_AT_REFERENCE = { name: 32, value: 116, line: 36 };
const ALARM_AT_REFERENCE = { triangle: { width: 44, height: 40 }, word: 34, reading: 26, gap: 6 };

/**
 * The limiter body over `frame`, the speed and the limit at its left and the gear in its middle. On
 * the portrait face, where the body is the gear's tile over zones B and C, the gear stays on its own
 * tile, `gear`, rather than in the middle of the whole body, where it would stand over zone C.
 */
function limiterBody(frame: Rect, gear: Rect): Item {
  const ink = ds.color.surface.base;
  const LIMITER = scaled(LIMITER_AT_REFERENCE, shrink(frame, BODY));
  const fill = iff(overTheLimit(), str(ds.color.danger.primary), str(ds.color.good.primary));
  const left = frame.left + LIMITER.padX;
  const top = frame.top + LIMITER.padTop;
  const limit = rect(left, frame.top + frame.height - LIMITER.padBottom - LIMITER.limit.height, LIMITER.limit.width, LIMITER.limit.height);
  const limitValueWidth = runWidth('888', LIMITER.limit.value);
  const portrait = frame.height > frame.width / 2;
  const middle = portrait
    ? rect(left + LIMITER.column, top, frame.width - 2 * (LIMITER.padX + LIMITER.column), gear.top + gear.height - top)
    : rect(left + LIMITER.column, top, frame.width - 2 * (LIMITER.padX + LIMITER.column), frame.height - LIMITER.padTop - LIMITER.padBottom);
  const size = gearSizeIn(middle);
  const cells = gearCells(size);
  const gearWidth = monoWidth(cells, GEAR_CHARS);
  const children: Item[] = [
    withMoreBindings(band('limiter.body', frame, ds.color.good.primary, { radius: RADIUS }), { BackgroundColor: fill }),
    label('limiter.speed.label', 'Speed', left, top, runWidth('Speed', LIMITER.label), { size: LIMITER.label, color: ink }),
    label('limiter.speed.value', '58', left, top + LIMITER.speedTop, LIMITER.column, { size: LIMITER.speed, color: ink, hAlign: 'center', bind: fmt(speed(), '0'), widest: '888' }),
    band('limiter.limit.box', limit, TRANSPARENT, { border: { color: ink, width: BORDER }, radius: RADIUS }),
    label('limiter.limit.label', 'Limit', limit.left + LIMITER.pad, centredY(limit, LIMITER.label), runWidth('Limit', LIMITER.label), { size: LIMITER.label, color: ink }),
    label('limiter.limit.value', '60', limit.left + limit.width - LIMITER.pad - limitValueWidth, centredY(limit, LIMITER.limit.value), limitValueWidth, {
      size: LIMITER.limit.value,
      color: ink,
      hAlign: 'right',
      bind: iff(lt(pitLimit(), num(999)), fmt(pitLimit(), '0'), str('--')),
      widest: '888',
    }),
    // The gear stays in the middle, in the face and weight zone A draws it, so that it reads as the
    // same gear with the body coloured round it.
    numeral('limiter.gear', '2', middle.left + (middle.width - gearWidth) / 2, centredY(middle, size), size, GEAR_CHARS, {
      weight: 'Bold',
      mono: cells,
      color: ink,
      maxWidth: gearWidth + GEAR_BOX_SLACK,
      bind: game('Gear'),
    }),
  ];
  return withMoreBindings({ kind: 'layer', name: 'limiter', children }, { Visible: pitAlertVisible('limiter') });
}

function lowFuel(frame: Rect): Item {
  const ink = ds.color.surface.base;
  const ALARM = scaled(ALARM_AT_REFERENCE, shrink(frame, PANEL));
  const fuel = raw('FuelLevel');
  const top = frame.top + (frame.height - (ALARM.triangle.height + ALARM.gap + ALARM.word + ALARM.gap + ALARM.reading)) / 2;
  const triangle = rect(frame.left + (frame.width - ALARM.triangle.width) / 2, top, ALARM.triangle.width, ALARM.triangle.height);
  const words = top + ALARM.triangle.height + ALARM.gap;
  const children: Item[] = [
    band('lowFuel.box', frame, ds.color.danger.primary, { radius: RADIUS }),
    // The alarm's own triangle is drawn in the ground's ink on the red, which is the hazard's third file.
    picture('lowFuel.triangle', 'hazard', 'ink', triangle),
    label('lowFuel.word', 'ALARM', frame.left, words, frame.width, { size: ALARM.word, color: ink, hAlign: 'center' }),
    label('lowFuel.reading', 'Fuel level 8.6', frame.left, words + ALARM.word + ALARM.gap, frame.width, {
      size: ALARM.reading,
      color: ink,
      hAlign: 'center',
      bind: concat(str('Fuel level '), fmt(fuel, '0.0')),
      widest: 'Fuel level 8.8',
    }),
  ];
  return withMoreBindings({ kind: 'layer', name: 'lowFuel', children }, { Visible: lowFuelAlarm() });
}

export const porscheTakeovers = (ctx: FaceContext): Item[] => [limiterBody(regionRect(ctx.regions, 'flagBody'), zoneRect(ctx.regions, 'A')), lowFuel(panelOf(ctx, 'C'))];

/** The house's change notifications, each filling zone C's panel in blue with its name over its value. */
export function porscheChangeNotifications(ctx: FaceContext): Item[] {
  const frame = panelOf(ctx, 'C');
  const ink = ds.color.surface.base;
  const NOTICE = scaled(NOTICE_AT_REFERENCE, shrink(frame, PANEL));
  const top = frame.top + (frame.height - (NOTICE.line + NOTICE.value)) / 2;
  return TRACKED_VALUES.map((value) => {
    const name = `notice.${value.id}`;
    const children: Item[] = [
      band(`${name}.box`, frame, ds.color.info.primary, { radius: RADIUS }),
      label(`${name}.label`, value.notice, frame.left, top, frame.width, { size: NOTICE.name, color: ink, hAlign: 'center' }),
      label(`${name}.value`, value.sample, frame.left, top + NOTICE.line, frame.width, {
        size: NOTICE.value,
        color: ink,
        hAlign: 'center',
        bind: fmt(value.read, value.pattern),
        widest: value.pattern === '0' ? '88' : '88.8',
      }),
    ];
    return withMoreBindings({ kind: 'layer', name, children }, { Visible: changeNotificationVisible(value.id) });
  });
}
