/**
 * Bar gauges. SimHub's LinearGaugeItem paints its fill over its own background, so a level gauge
 * is one item: the track is the item's background colour and the fill is the gauge colour.
 *
 * There is no centre-zero mode in SimHub, so a delta bar is two gauges meeting in the middle: the
 * left one is anchored to its right edge and runs from 0 down to minus the range, the right one is
 * anchored to its left edge and runs from 0 up. Both read the same delta; each clamps the half it
 * does not own to nothing.
 */
import type { Hex, Item, LinearGaugeItem, Rect } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { ds } from '../tokens.ts';

export interface LevelGaugeOptions {
  /** Track colour; surface.raised by default. */
  track?: Hex;
  /** Fill colour; text.primary by default. */
  fill?: Hex;
  /** Live fill colour, e.g. fuel.low under a lap of fuel. */
  fillBind?: Expr;
  min?: number;
  max?: number;
  /** Design-time fill, as a value between min and max. */
  value?: number;
  visibleBind?: Expr;
}

/** A horizontal level gauge filling `frame` from the left. */
export function levelGauge(name: string, frame: Rect, valueBind: Expr, opts: LevelGaugeOptions = {}): LinearGaugeItem {
  return {
    kind: 'linearGauge',
    name,
    rect: roundRect(frame),
    orientation: 'horizontal',
    alignment: 'start',
    gaugeColor: opts.fill ?? ds.color.text.primary,
    backgroundColor: opts.track ?? ds.color.surface.raised,
    minimum: opts.min ?? 0,
    maximum: opts.max ?? 100,
    value: opts.value ?? 0,
    ...withBindings({ Value: valueBind, GaugeColor: opts.fillBind, Visible: opts.visibleBind }),
  };
}

/** A vertical gauge filling `frame` from the bottom: the input bars and the tyre wear bars. */
export function barGauge(name: string, frame: Rect, valueBind: Expr, opts: LevelGaugeOptions = {}): LinearGaugeItem {
  return { ...levelGauge(name, frame, valueBind, opts), orientation: 'vertical', alignment: 'start' };
}

export interface CentreZeroOptions {
  /** Seconds either side of zero the bar covers. */
  range: number;
  /** Colour of the left half, which is the faster side. */
  faster?: Hex;
  /** Colour of the right half. */
  slower?: Hex;
  /** Track colour. */
  track?: Hex;
  /** Height of the graduation ticks and of the centre marker, above and below the track. */
  tickOverhang?: number;
}

/**
 * A delta bar: a track with graduations at the quarters, a centre marker, and two gauges that
 * grow outwards from the middle. `deltaBind` is seconds, negative when faster.
 */
export function centreZeroGauge(name: string, frame: Rect, deltaBind: Expr, opts: CentreZeroOptions): Item[] {
  const overhang = opts.tickOverhang ?? 4;
  const half = Math.floor(frame.width / 2);
  const track = opts.track ?? ds.color.surface.raised;
  const items: Item[] = [band(`${name}.track`, roundRect(frame), track)];
  // The negative half is anchored to its right edge and counts down to -range, so a faster delta
  // grows leftwards from the centre; the positive half mirrors it.
  items.push({
    ...levelGauge(`${name}.faster`, rect(frame.left, frame.top, half, frame.height), deltaBind, {
      track,
      fill: opts.faster ?? ds.purpose.delta.faster,
      min: 0,
      max: -opts.range,
    }),
    alignment: 'end',
  });
  items.push(
    levelGauge(`${name}.slower`, rect(frame.left + half, frame.top, frame.width - half, frame.height), deltaBind, {
      track,
      fill: opts.slower ?? ds.purpose.delta.slower,
      min: 0,
      max: opts.range,
    }),
  );
  for (const [i, fraction] of [0, 0.25, 0.75, 1].entries()) {
    const x = Math.round(frame.left + fraction * (frame.width - 1));
    items.push(band(`${name}.tick${i}`, rect(x, frame.top - overhang, 1, frame.height + 2 * overhang), ds.color.text.dim));
  }
  items.push(band(`${name}.centre`, rect(frame.left + half - 1, frame.top - overhang - 2, 2, frame.height + 2 * overhang + 4), ds.color.text.primary));
  return items;
}
