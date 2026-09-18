/**
 * The pit wall's telemetry column, measured against its artboard.
 *
 * The plot heights the canvas declares, 180 / 130 / 90 / 180 / 110, used to be shares of whatever
 * the column had left, so a fifth panel would have quietly taken height off the other four and the
 * page would still have looked plausible. They are declarations now, and the assertions below
 * measure the built plots rather than the numbers the spec carries: the frame a panel is given is
 * derived from `panel`'s padding and from the legend row `trace` takes off the body, and neither
 * module exports those, so a change to either has to fail here rather than by WPF clipping a row.
 *
 * The copy is pinned for the same reason. A unit in a title and the three words under the axis are
 * the sort of thing that drifts from the canvas without anybody noticing, because nothing else
 * reads them.
 */
import { describe, expect, test } from 'bun:test';
import type { ChartItem, Item, Rect, TextItem } from '../src/generator.ts';
import { TELEMETRY_TRACES, telemetryPage } from '../src/screens/pitwall.ts';
import { ds } from '../src/tokens.ts';

const PAGE = telemetryPage(1920, 1080);

/** A named item of the page. Every item the telemetry column draws is boxed; a layer would not be. */
const named = (name: string): Exclude<Item, { kind: 'layer' }> => {
  const item = PAGE.items.find((i) => i.name === name);
  if (!item || item.kind === 'layer') throw new Error(`no boxed item named ${name}`);
  return item;
};

const boxOf = (name: string): Rect => named(name).rect;

const textOf = (name: string): string => (named(name) as TextItem).text;

const chartsOf = (id: string): ChartItem[] => PAGE.items.filter((i): i is ChartItem => i.kind === 'chart' && i.name.startsWith(`telemetry.${id}.`));

describe('the telemetry traces', () => {
  test('stacks the canvas five, in the canvas order', () => {
    expect(TELEMETRY_TRACES.map((t) => t.id)).toEqual(['speed', 'rpm', 'gear', 'pedals', 'steering']);
  });

  test('gives every plot the height its artboard declares', () => {
    const built = Object.fromEntries(TELEMETRY_TRACES.map((t) => [t.id, [...new Set(chartsOf(t.id).map((c) => c.rect.height))]]));
    expect(built).toEqual({ speed: [180], rpm: [130], gear: [90], pedals: [180], steering: [110] });
  });

  test('titles the traces as the canvas writes them, units included', () => {
    expect(TELEMETRY_TRACES.map((t) => t.title)).toEqual(['Speed · km/h', 'RPM', 'Gear', 'Throttle, brake and clutch · %', 'Steering']);
  });

  test('draws the gear between the engine speed and the pedals, unlabelled and in the primary ink', () => {
    const gear = chartsOf('gear');
    expect(gear).toHaveLength(1);
    expect(gear[0]!.lineColor).toBe(ds.color.text.primary);
    expect(PAGE.items.some((i) => i.name.startsWith('telemetry.gear.trace') && i.name.endsWith('.legend'))).toBe(false);
    const top = (id: string): number => chartsOf(id)[0]!.rect.top;
    expect(top('rpm')).toBeLessThan(top('gear'));
    expect(top('gear')).toBeLessThan(top('pedals'));
  });

  test('spans the gear axis from reverse to a gear no car exceeds', () => {
    // Written rather than bound: a ChartItem's Maximum is a number in the file, so the axis cannot
    // follow the car's own gear count, and autoscaling it would move a gear up and down the plot.
    expect({ minimum: chartsOf('gear')[0]!.minimum, maximum: chartsOf('gear')[0]!.maximum }).toEqual({ minimum: -1, maximum: 8 });
  });

  test('samples a number for the gear, because SimHub reports it as a word', () => {
    const formula = chartsOf('gear')[0]!.bindings?.CurrentValue?.formula ?? '';
    expect(formula).toContain('[DataCorePlugin.GameData.Gear]');
    // Reverse below neutral, and the forward gears as themselves.
    expect(formula).toContain("'R'");
    for (const g of [1, 8]) expect(formula).toContain(`'${g}'`);
  });

  test('labels the pedals in the canvas order and keeps the legend inside the panel', () => {
    const legend = PAGE.items.filter((i): i is TextItem => i.kind === 'text' && i.name.startsWith('telemetry.pedals.') && i.name.endsWith('.legend'));
    expect(legend.map((i) => i.text)).toEqual(['THROTTLE', 'BRAKE', 'CLUTCH']);
    // The canvas puts the legend on the title row, beside the title. `trace` only draws it under
    // the plot, which is a `second/trace.ts` option this file does not own; what is pinned here is
    // that it stays within the panel it belongs to rather than landing on the rule or the panel
    // below. When the legend moves up, this expectation is the one to rewrite.
    const plot = chartsOf('pedals')[0]!.rect;
    const rule = boxOf('telemetry.pedals.rule');
    for (const entry of legend) {
      expect(entry.rect.top).toBeGreaterThanOrEqual(plot.top + plot.height);
      expect(entry.rect.top + entry.rect.height).toBeLessThanOrEqual(rule.top);
    }
  });

  test('names the axis honestly: it is time, not lap distance', () => {
    // A ChartItem appends one sample per tick and draws the buffer oldest to newest, so there is no
    // lap-distance axis to label and no window length the dashboard can promise. The canvas asks
    // for "0 %", "Lap distance" and "100 %"; the left label in particular read "0 %" over a time
    // axis, where a percentage of a lap means nothing.
    expect(['telemetry.axisStart', 'telemetry.axisName', 'telemetry.axisEnd'].map(textOf)).toEqual(['EARLIER', 'TIME', 'NOW']);
  });

  test('leaves the foot of the column as background rather than growing the last panel', () => {
    const footer = boxOf('telemetry.axisStart');
    const steering = boxOf('telemetry.steering.rule');
    expect(footer.top).toBeGreaterThan(steering.top);
    // What is left under the footer is spare, and belongs to nothing.
    expect(1080 - (footer.top + footer.height)).toBeGreaterThan(0);
    const plots = TELEMETRY_TRACES.flatMap((t) => chartsOf(t.id));
    expect(Math.max(...plots.map((p) => p.rect.top + p.rect.height))).toBeLessThan(footer.top);
  });
});
