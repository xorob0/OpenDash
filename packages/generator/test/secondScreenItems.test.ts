/**
 * The item kinds the second screens need: chart, linear gauge, radar, generated static map and
 * web page, plus a Layer that stamps its children into rows. Every expected shape here is the
 * one recorded in the SimHub 9.12.6 reference (docs/research), so a change to the serialiser
 * that drifts from SimHub shows up as a failing key rather than as a blank panel.
 */
import { describe, expect, test } from 'bun:test';
import type { ChartItem, Item, LayerItem, LinearGaugeItem, RadarItem, StaticMapItem, WebPageItem } from '../src/model.ts';
import { ITEM_TYPES, buildItemObject, DEFAULT_REPEAT_TOP_OFFSET } from '../src/serialize.ts';
import { validatePackage } from '../src/validate.ts';
import { dashboard, rect, samplePackage, screen } from './fixtures.ts';

const PATH = 'openDash/openDash/Main';
const build = (item: Item): Record<string, unknown> => buildItemObject(item, PATH) as Record<string, unknown>;

const chart = (over: Partial<ChartItem> = {}): ChartItem => ({
  kind: 'chart',
  name: 'throttle',
  rect: { left: 0, top: 0, width: 800, height: 120 },
  lineColor: '#00D96A',
  pointsCount: 300,
  bindings: { CurrentValue: { mode: 'formula', formula: '[DataCorePlugin.GameData.Throttle]' } },
  ...over,
});

const gauge = (over: Partial<LinearGaugeItem> = {}): LinearGaugeItem => ({
  kind: 'linearGauge',
  name: 'fuelLevel',
  rect: { left: 0, top: 0, width: 599, height: 4 },
  gaugeColor: '#F5F7FA',
  backgroundColor: '#1C1F24',
  ...over,
});

const radar = (over: Partial<RadarItem> = {}): RadarItem => ({
  kind: 'radar',
  name: 'radar',
  rect: { left: 0, top: 0, width: 294, height: 184 },
  scale: 1.25,
  ...over,
});

const map = (over: Partial<StaticMapItem> = {}): StaticMapItem => ({
  kind: 'staticMap',
  name: 'track',
  rect: { left: 0, top: 0, width: 300, height: 190 },
  trackColor: '#33383F',
  trackBorderColor: '#0A0B0D',
  ...over,
});

const web = (over: Partial<WebPageItem> = {}): WebPageItem => ({
  kind: 'webPage',
  name: 'web',
  rect: { left: 0, top: 0, width: 599, height: 184 },
  ...over,
});

describe('chart', () => {
  test('carries SimHub keys, the LineTickness spelling included', () => {
    const o = build(chart());
    expect(o.$type).toBe(ITEM_TYPES.chart);
    expect(Object.keys(o)[0]).toBe('$type');
    expect(o).toMatchObject({
      ChartSuspended: false,
      ChartEnabled: true,
      CurrentValue: 0,
      Minimum: 0,
      UseMinimum: true,
      UseMaximum: true,
      LineColor: '#FF00D96A',
      LineTickness: 2,
      Maximum: 100,
      PointsCount: 300,
    });
    expect(o.Bindings).toMatchObject({ CurrentValue: { Mode: 2 } });
  });

  test('an autoscaled top writes UseMaximum false', () => {
    expect(build(chart({ useMaximum: false }))).toMatchObject({ UseMaximum: false });
  });
});

describe('linear gauge', () => {
  test('orientation and alignment serialise as SimHub numbers its enums', () => {
    const o = build(gauge({ orientation: 'vertical', alignment: 'end', maximum: 1, value: 0.5 }));
    expect(o.$type).toBe(ITEM_TYPES.linearGauge);
    expect(o).toMatchObject({ IsLinearGauge: true, GaugeOrientation: 1, GaugeAlignment: 2, Maximum: 1, Value: 0.5 });
  });

  test('the track colour is always written, since SimHub would otherwise make it blue', () => {
    expect(build(gauge())).toMatchObject({ BackgroundColor: '#FF1C1F24', GaugeColor: '#FFF5F7FA' });
    expect(build(gauge({ backgroundColor: undefined }))).toMatchObject({ BackgroundColor: '#00FFFFFF' });
  });

  test('the alternate colour defaults to the fill, so an unset alternate style changes nothing', () => {
    expect(build(gauge())).toMatchObject({ AlternateGaugeColor: '#FFF5F7FA', UseAlternateStyle: false });
  });
});

describe('radar and map', () => {
  test('a radar writes both dot styles in full', () => {
    const o = build(radar({ playerStyle: { dotColor: '#F5F7FA', dotRadius: 14 } }));
    expect(o.$type).toBe(ITEM_TYPES.radar);
    expect(o).toMatchObject({ UseSmoothedPlayerAngle: true, Scale: 1.25 });
    expect(o.PlayerStyle).toEqual({
      LabelFont: 'Segoe UI',
      LabelFontSize: 14,
      LabelColor: '#FFFFFFFF',
      DotColor: '#FFF5F7FA',
      DotBorderThickness: 0,
      DotBordercolor: '#FFFFFFFF',
      DotRadius: 14,
    });
    expect(Object.keys(o.OpponentStyle as object)).toHaveLength(7);
  });

  test('a static map keeps the class colours off and writes the track colours', () => {
    const o = build(map({ trackWidth: 6, startLine: { color: '#F5F7FA', width: 3, height: 14 } }));
    expect(o.$type).toBe(ITEM_TYPES.staticMap);
    expect(o).toMatchObject({
      OverrideColorsWithCarClassColors: false,
      DisplayPerClassPosition: false,
      MapShadow: false,
      TrackColor: '#FF33383F',
      TrackWidth: 6,
      TrackBorderColor: '#FF0A0B0D',
    });
    expect(o.StartLine).toEqual({ Color: '#FFF5F7FA', Enabled: true, Height: 14, Width: 3 });
  });
});

describe('web page', () => {
  test('the address is written even when empty, and click-through is off', () => {
    const o = build(web({ bindings: { StartAddress: { mode: 'formula', formula: "isnull([OpenDash.WebViewUrl], '')" } } }));
    expect(o.$type).toBe(ITEM_TYPES.webPage);
    expect(o).toMatchObject({ StartAddress: '', AllowTransparency: false, ClickThrough: false });
    expect(o.Bindings).toMatchObject({ StartAddress: { Mode: 2 } });
  });
});

describe('a repeated layer', () => {
  const row = (over: Partial<LayerItem> = {}): LayerItem => ({
    kind: 'layer',
    name: 'rows',
    children: [rect('rowBackground')],
    repetitions: 6,
    repeatTopOffset: 26,
    ...over,
  });

  test('writes the copy count, the pitch and PrepareRepetitions', () => {
    const o = build(row());
    expect(o).toMatchObject({ Repetitions: 6, RepeatTopOffset: 26, PrepareRepetitions: true });
    expect(o).not.toHaveProperty('RepeatLeftOffset');
  });

  test('writes the pitch even when it is SimHubs own default, which is not zero', () => {
    expect(build(row({ repeatTopOffset: undefined }))).toMatchObject({ RepeatTopOffset: DEFAULT_REPEAT_TOP_OFFSET });
    expect(build(row({ repeatTopOffset: 0, repeatLeftOffset: 40 }))).toMatchObject({ RepeatTopOffset: 0, RepeatLeftOffset: 40 });
  });

  test('a layer that does not repeat writes none of the keys', () => {
    const o = build(row({ repetitions: 0, repeatTopOffset: undefined }));
    expect(o).not.toHaveProperty('Repetitions');
    expect(o).not.toHaveProperty('PrepareRepetitions');
  });

  test('a bound row count marks itself bound', () => {
    const o = build(row({ repetitions: 0, bindings: { Repetitions: { mode: 'formula', formula: 'getleaderboardcarclassopponentscount(1)' } } }));
    expect(o).toMatchObject({ Repetitions: 0, RepetitionsBound: true, PrepareRepetitions: true });
  });
});

describe('validation of the new kinds', () => {
  const validateItems = (items: Item[]) =>
    validatePackage({ ...samplePackage(), dashboards: [dashboard('openDash', [screen('Main', items)])] }, { declaredProperties: ['OpenDash.WebViewUrl'], propertyPrefix: 'OpenDash' });

  test('a package holding every new kind is valid', () => {
    const result = validateItems([chart(), gauge(), radar(), map(), web()]);
    expect(result.errors).toEqual([]);
    expect(result.ok).toBe(true);
  });

  test('a binding target that belongs to another kind is an error', () => {
    const result = validateItems([chart({ bindings: { Value: { mode: 'formula', formula: '1' } } })]);
    expect(result.errors.map((e) => e.code)).toContain('binding/unknown-target');
  });

  test('a gauge with no range and a chart with one point are errors', () => {
    const result = validateItems([gauge({ minimum: 5, maximum: 5 }), chart({ name: 'trace', pointsCount: 1 })]);
    expect(result.errors.map((e) => e.code).sort()).toEqual(['chart/points', 'gauge/empty-range']);
  });

  test('a widget inside a repeated layer is an error, because SimHub drops it from the copies', () => {
    const widgetChild: Item = { kind: 'widget', name: 'slot', rect: { left: 0, top: 0, width: 10, height: 10 }, fileName: 'cards.djson', initialScreenIndex: 0 };
    const result = validateItems([{ kind: 'layer', name: 'rows', children: [widgetChild], repetitions: 3, repeatTopOffset: 26 }]);
    expect(result.errors.map((e) => e.code)).toContain('layer/repeated-widget');
  });
});
