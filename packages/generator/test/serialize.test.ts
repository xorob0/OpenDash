/** Serialiser: SimHub key order, $type first, colour normalisation, omitted defaults, stable ids. */

import { describe, expect, test } from 'bun:test';
import { stableGuid } from '../src/ids.ts';
import type { Item } from '../src/model.ts';
import { assertWholeNumbers, fractionalIntFields } from '../src/intFields.ts';
import {
  ITEM_TYPES,
  buildBindingObject,
  buildBorderObject,
  buildDashboardObject,
  buildItemObject,
  buildMetadataObject,
  buildScreenObject,
  serializeDashboard,
  serializeMetadata,
} from '../src/serialize.ts';
import { dashboard, ellipse, label, layer, numeral, rect, samplePackage, screen, widget } from './fixtures.ts';

const CTX = { packageName: 'OpenDash' };

const COMMON_TAIL = ['Left', 'Top', 'Width', 'Height', 'Visible', 'BackgroundColor', 'BorderStyle', 'Id', 'Name', 'RenderingSkip', 'MinimumRefreshIntervalMS'];

describe('document shape', () => {
  test('top-level keys follow the SimHub 9.12 order', () => {
    const doc = buildDashboardObject(dashboard('OpenDash', [screen('Main', [rect('r')])]), CTX);
    expect(Object.keys(doc)).toEqual([
      'Version', 'Id', 'BaseHeight', 'BaseWidth', 'BackgroundColor', 'Screens', 'SnapToGrid', 'HideLabels', 'ShowForeground',
      'ForegroundOpacity', 'ShowBackground', 'BackgroundOpacity', 'ShowBoundingRectangles', 'GridSize', 'Images', 'Metadata',
      'ShowOnScreenControls', 'IsOverlay', 'EnableClickThroughOverlay', 'EnableOnDashboardMessaging', 'UseStrictJSIsolation',
      'UseStrictJSIsolationWarning',
    ]);
    expect(doc.Version).toBe(2);
    expect(doc.BaseWidth).toBe(1920);
    expect(doc.BaseHeight).toBe(480);
    expect(doc.BackgroundColor).toBe('#FF0A0B0D');
    expect(doc.GridSize).toBe(5);
    expect(doc.Images).toEqual([]);
    expect(doc.EnableOnDashboardMessaging).toBe(false);
    expect(doc.UseStrictJSIsolation).toBe(true);
    // false, otherwise DashStudio shows the "Legacy Javascript isolation is enabled" banner.
    expect(doc.UseStrictJSIsolationWarning).toBe(false);
  });

  test('screen keys follow the SimHub 9.12 order and roles default to true', () => {
    const d = dashboard('OpenDash', [screen('Main', [rect('r')])]);
    const s = buildScreenObject(d.screens[0]!, d, CTX);
    expect(Object.keys(s)).toEqual([
      'RenderingSkip', 'Name', 'InGameScreen', 'IdleScreen', 'PitScreen', 'ScreenId', 'AllowOverlays', 'IsForegroundLayer',
      'IsOverlayLayer', 'OverlayTriggerExpression', 'ScreenEnabledExpression', 'OverlayMaxDuration', 'OverlayMinDuration',
      'IsBackgroundLayer', 'BackgroundColor', 'Background', 'MinimumRefreshIntervalMS', 'Items',
    ]);
    expect(s.InGameScreen).toBe(true);
    expect(s.IdleScreen).toBe(true);
    expect(s.PitScreen).toBe(true);
    expect(s.OverlayTriggerExpression).toEqual({ Expression: '' });
    expect(s.ScreenEnabledExpression).toEqual({ Expression: '' });
    expect(s.Background).toBe('None');
    // Screen background defaults to the dashboard's.
    expect(s.BackgroundColor).toBe('#FF0A0B0D');
  });

  test('screen roles, enabled expression and background can be set', () => {
    const d = dashboard('OpenDash', [screen('Idle', [rect('r')], { inGame: false, idle: true, pit: false, enabledExpression: '[X] = 1', backgroundColor: '#000000' })]);
    const s = buildScreenObject(d.screens[0]!, d, CTX);
    expect(s.InGameScreen).toBe(false);
    expect(s.PitScreen).toBe(false);
    expect(s.ScreenEnabledExpression).toEqual({ Expression: '[X] = 1' });
    expect(s.BackgroundColor).toBe('#FF000000');
  });

  test('serializeDashboard is 2-space indented JSON of the object', () => {
    const d = dashboard('OpenDash', [screen('Main', [rect('r')])]);
    const text = serializeDashboard(d, CTX);
    expect(text.startsWith('{\n  "Version": 2,\n  "Id": "')).toBe(true);
    expect(JSON.parse(text)).toEqual(buildDashboardObject(d, CTX));
  });
});

describe('metadata', () => {
  test('has every field of the sidecar with the screen roles as indexes', () => {
    const d = dashboard('OpenDash', [
      screen('A', [rect('r')], { inGame: true, idle: false, pit: true }),
      screen('B', [rect('r')], { inGame: false, idle: true, pit: false }),
      screen('C', [rect('r')]),
    ], { metadata: { title: 'OpenDash', author: 'OpenDash contributors', version: '0.1.0', simHubVersion: '9.12.6', description: 'd', category: 'Race', mainPreviewIndex: 2 } });
    const m = buildMetadataObject(d);
    expect(Object.keys(m)).toEqual([
      'SimHubVersion', 'Category', 'Title', 'Description', 'Author', 'Width', 'Height', 'DashboardVersion', 'ScreenCount',
      'InGameScreensIndexs', 'IdleScreensIndexs', 'PitScreensIndexs', 'MainPreviewIndex', 'IsOverlay', 'OverlaySizeWarning',
      'MetadataVersion', 'EnableOnDashboardMessaging', 'PreferredTouchMode',
    ]);
    expect(m).toMatchObject({
      SimHubVersion: '9.12.6', Category: 'Race', Title: 'OpenDash', Description: 'd', Author: 'OpenDash contributors',
      Width: 1920, Height: 480, DashboardVersion: '0.1.0', ScreenCount: 3,
      InGameScreensIndexs: [0, 2], IdleScreensIndexs: [1, 2], PitScreensIndexs: [0, 2],
      MainPreviewIndex: 2, IsOverlay: false, OverlaySizeWarning: true, MetadataVersion: 2, EnableOnDashboardMessaging: false, PreferredTouchMode: 0,
    });
  });

  test('optional fields are null and the preview index defaults to 0', () => {
    const m = buildMetadataObject(dashboard('OpenDash', [screen('Main', [rect('r')])]));
    expect(m.Category).toBeNull();
    expect(m.Description).toBeNull();
    expect(m.MainPreviewIndex).toBe(0);
  });

  test('the sidecar equals the Metadata key of the document', () => {
    const d = dashboard('OpenDash', [screen('Main', [rect('r')])]);
    expect(JSON.parse(serializeMetadata(d))).toEqual(buildDashboardObject(d, CTX).Metadata);
  });
});

describe('items', () => {
  test('$type is the first key of every kind', () => {
    const items: Item[] = [label('t', 'X'), rect('r'), ellipse('e'), layer('l', [rect('c')]), widget('w')];
    for (const item of items) {
      const o = buildItemObject(item, 'p');
      expect(Object.keys(o)[0]).toBe('$type');
      expect(o.$type).toBe(ITEM_TYPES[item.kind]);
    }
  });

  test('type strings are the SimHub.Plugins class names', () => {
    expect(ITEM_TYPES.text).toBe('SimHub.Plugins.OutputPlugins.GraphicalDash.Models.TextItem, SimHub.Plugins');
    expect(ITEM_TYPES.rect).toBe('SimHub.Plugins.OutputPlugins.GraphicalDash.Models.RectangleItem, SimHub.Plugins');
    expect(ITEM_TYPES.ellipse).toBe('SimHub.Plugins.OutputPlugins.GraphicalDash.Models.EllipseItem, SimHub.Plugins');
    expect(ITEM_TYPES.layer).toBe('SimHub.Plugins.OutputPlugins.GraphicalDash.Models.Layer, SimHub.Plugins');
    expect(ITEM_TYPES.widget).toBe('SimHub.Plugins.OutputPlugins.GraphicalDash.Models.WidgetItem, SimHub.Plugins');
  });

  test('text item keys are in spec order', () => {
    const o = buildItemObject(label('t', 'CURRENT'), 'p');
    expect(Object.keys(o)).toEqual([
      '$type', 'IsTextItem', 'Font', 'FontWeight', 'FontStyle', 'FontSize', 'Text', 'TextColor', 'HorizontalAlignment',
      'VerticalAlignment', 'TextWrapping', ...COMMON_TAIL,
    ]);
    expect(o).toMatchObject({
      IsTextItem: true, Font: 'Barlow', FontWeight: 'Medium', FontStyle: 'Normal', FontSize: 15, Text: 'CURRENT',
      TextColor: '#FF8A9099', HorizontalAlignment: 0, VerticalAlignment: 0, TextWrapping: 'NoWrap',
      Left: 16, Top: 50, Width: 223, Height: 18, Visible: true, BackgroundColor: '#00FFFFFF', BorderStyle: {},
      Name: 't', RenderingSkip: 0, MinimumRefreshIntervalMS: 0,
    });
  });

  test('text alignment maps to SimHub enums', () => {
    const o = buildItemObject(label('t', 'X', { hAlign: 'center', vAlign: 'bottom' }), 'p');
    expect(o.HorizontalAlignment).toBe(1);
    expect(o.VerticalAlignment).toBe(2);
    expect(buildItemObject(label('t', 'X', { hAlign: 'right', vAlign: 'center' }), 'p')).toMatchObject({ HorizontalAlignment: 2, VerticalAlignment: 1 });
  });

  test('monospace writes the cell widths after the alignment and always writes SpecialChars', () => {
    // SimHub 9.12.6's own default is ".,:;" (with the semicolon), so the key can never be omitted.
    const o = buildItemObject(numeral('v', '[X]'), 'p');
    const keys = Object.keys(o);
    expect(keys.slice(keys.indexOf('VerticalAlignment'), keys.indexOf('TextWrapping') + 1)).toEqual([
      'VerticalAlignment', 'UseMonospacedText', 'CharWidth', 'SpecialCharsWidth', 'SpecialChars', 'TextWrapping',
    ]);
    expect(o.CharWidth).toBe(29);
    expect(o.SpecialCharsWidth).toBe(17);
    expect(o.SpecialChars).toBe('.,:');
    const explicit = buildItemObject(numeral('v', '[X]', { monospace: { charWidth: 29, specialCharsWidth: 17, specialChars: '.,:' } }), 'p');
    expect(explicit.SpecialChars).toBe('.,:');
    const custom = buildItemObject(numeral('v', '[X]', { monospace: { charWidth: 29, specialCharsWidth: 17, specialChars: '.,: ' } }), 'p');
    expect(custom.SpecialChars).toBe('.,: ');
    expect(buildItemObject(label('t', 'X'), 'p')).not.toHaveProperty('SpecialChars');
  });

  test('padding writes all four sides when any is set, else nothing', () => {
    expect(buildItemObject(label('t', 'X'), 'p')).not.toHaveProperty('TextPadding');
    expect(buildItemObject(label('t', 'X', { padding: { top: 0, left: 0 } }), 'p')).not.toHaveProperty('TextPadding');
    const o = buildItemObject(label('t', 'X', { padding: { right: 21 } }), 'p');
    expect(o.TextPadding).toEqual({ PaddingTop: 0, PaddingBottom: 0, PaddingLeft: 0, PaddingRight: 21 });
    const keys = Object.keys(o);
    expect(keys.indexOf('TextPadding')).toBe(keys.indexOf('FontWeight') + 1);
    expect(keys.indexOf('FontStyle')).toBe(keys.indexOf('TextPadding') + 1);
  });

  test('wrap and italic', () => {
    const o = buildItemObject(label('t', 'X', { wrap: true, fontStyle: 'Italic' }), 'p');
    expect(o.TextWrapping).toBe('Wrap');
    expect(o.FontStyle).toBe('Italic');
  });

  test('rectangle item keys', () => {
    const o = buildItemObject(rect('r'), 'p');
    expect(Object.keys(o)).toEqual(['$type', 'IsRectangleItem', ...COMMON_TAIL]);
    expect(o.IsRectangleItem).toBe(true);
    expect(o.BackgroundColor).toBe('#FF1C1F24');
  });

  test('ellipse item keys: fill, stroke colour and thickness before the DrawableItem keys, as a 9.12 export writes them', () => {
    const o = buildItemObject(ellipse('ring'), 'p');
    expect(Object.keys(o)).toEqual(['$type', 'FillColor', 'EllipseColor', 'EllipseThickness', ...COMMON_TAIL]);
    expect(o).toMatchObject({
      FillColor: '#00FFFFFF', EllipseColor: '#FFFFD400', EllipseThickness: 12,
      Left: 6, Top: 6, Width: 468, Height: 468, Visible: true, BackgroundColor: '#00FFFFFF', BorderStyle: {}, Name: 'ring',
    });
    expect(o).not.toHaveProperty('IsRectangleItem');
    expect(o).not.toHaveProperty('IsTextItem');
  });

  test('Rotation follows Height on text, rectangle and ellipse items and is omitted when 0', () => {
    for (const item of [label('t', 'X', { rotation: -84 }), rect('r', { rotation: 12.5 }), ellipse('e', { rotation: 90 })]) {
      const o = buildItemObject(item, 'p');
      const keys = Object.keys(o);
      expect(keys.indexOf('Rotation')).toBe(keys.indexOf('Height') + 1);
      expect(keys.indexOf('Visible')).toBe(keys.indexOf('Rotation') + 1);
      expect(o.Rotation).toBe(item.rotation);
    }
    expect(buildItemObject(rect('r'), 'p')).not.toHaveProperty('Rotation');
    expect(buildItemObject(rect('r', { rotation: 0 }), 'p')).not.toHaveProperty('Rotation');
    expect(buildItemObject(layer('l', [rect('c')]), 'p')).not.toHaveProperty('Rotation');
  });

  test('a widget never writes Rotation: SimHub is not verified to honour it there', () => {
    expect(buildItemObject(widget('w', { rotation: 45 }), 'p')).not.toHaveProperty('Rotation');
  });

  test('layer has no geometry or background and nests children under Childrens', () => {
    const o = buildItemObject(layer('L', [rect('a'), label('b', 'X')], { opacity: 50, blink: { enabled: true, delayMs: 250 } }), 'p');
    expect(Object.keys(o)).toEqual(['$type', 'Group', 'Visible', 'Opacity', 'BlinkEnabled', 'Childrens', 'Id', 'Name', 'RenderingSkip', 'MinimumRefreshIntervalMS']);
    expect(o.Group).toBe(true);
    for (const k of ['Left', 'Top', 'Width', 'Height', 'BackgroundColor', 'BorderStyle']) expect(o).not.toHaveProperty(k);
    const children = o.Childrens as Record<string, unknown>[];
    expect(children).toHaveLength(2);
    expect(Object.keys(children[0]!)[0]).toBe('$type');
    expect(children[1]!.Name).toBe('b');
  });

  test('widget item keys and defaults', () => {
    const o = buildItemObject(widget('Slot01', { initialScreenIndex: 4 }), 'p');
    expect(Object.keys(o)).toEqual([
      '$type', 'NextScreenCommand', 'PreviousScreenCommand', 'AutoSize', 'FileName', 'InitialScreenIndex', 'FreezePageChanges',
      'EnableScreenRolesAndActivation', 'IgnoreSavedScreensEx', 'Left', 'Top', 'Width', 'Height', 'Visible', 'BackgroundColor',
      'Id', 'Name', 'RenderingSkip', 'MinimumRefreshIntervalMS',
    ]);
    expect(o).toMatchObject({
      NextScreenCommand: 0, PreviousScreenCommand: 0, AutoSize: true, FileName: 'cards.djson', InitialScreenIndex: 4,
      FreezePageChanges: false, EnableScreenRolesAndActivation: false, IgnoreSavedScreensEx: true, BackgroundColor: '#00FFFFFF',
    });
    expect(buildItemObject(widget('w', { autoSize: false }), 'p').AutoSize).toBe(false);
  });
});

describe('defaults are omitted', () => {
  test('Opacity 100, BlinkDelay 250, BlinkEnabled false, BlinkPhasisInverted false', () => {
    const o = buildItemObject(rect('r', { opacity: 100, blink: { enabled: false, delayMs: 250, phaseInverted: false } }), 'p');
    for (const k of ['Opacity', 'BlinkEnabled', 'BlinkDelay', 'BlinkPhasisInverted', 'Bindings']) expect(o).not.toHaveProperty(k);
  });

  test('non-default opacity and blink are written in order', () => {
    const o = buildItemObject(rect('r', { opacity: 40, blink: { enabled: true, delayMs: 62, phaseInverted: true } }), 'p');
    const keys = Object.keys(o);
    expect(keys.slice(keys.indexOf('BackgroundColor'), keys.indexOf('BorderStyle') + 1)).toEqual([
      'BackgroundColor', 'Opacity', 'BlinkEnabled', 'BlinkDelay', 'BlinkPhasisInverted', 'BorderStyle',
    ]);
    expect(o.Opacity).toBe(40);
    expect(o.BlinkDelay).toBe(62);
  });

  test('a blink delay is written even when BlinkEnabled is left to a binding', () => {
    const o = buildItemObject(rect('r', { blink: { delayMs: 62 }, bindings: { BlinkEnabled: { mode: 'formula', formula: '[X] = 1' } } }), 'p');
    expect(o).not.toHaveProperty('BlinkEnabled');
    expect(o.BlinkDelay).toBe(62);
    expect(o.Bindings).toEqual({ BlinkEnabled: { Formula: { Expression: '[X] = 1' }, Mode: 2 } });
  });

  test('empty BorderStyle is {} and zero widths are omitted', () => {
    expect(buildBorderObject(undefined)).toEqual({});
    expect(buildBorderObject({ top: 0, radius: 0 })).toEqual({});
    expect(buildBorderObject({ color: '#F5F7FA', top: 3, bottom: 3, left: 3, right: 3 })).toEqual({
      BorderColor: '#FFF5F7FA', BorderTop: 3, BorderBottom: 3, BorderLeft: 3, BorderRight: 3,
    });
    expect(buildBorderObject({ radius: 2 })).toEqual({ RadiusTopLeft: 2, RadiusTopRight: 2, RadiusBottomLeft: 2, RadiusBottomRight: 2 });
    expect(buildBorderObject({ radius: { topLeft: 5, topRight: 0, bottomLeft: 0, bottomRight: 5 } })).toEqual({ RadiusTopLeft: 5, RadiusBottomRight: 5 });
    expect(Object.keys(buildBorderObject({ color: '#000000', top: 1, bottom: 1, left: 1, right: 1, radius: 4 }))).toEqual([
      'BorderColor', 'BorderTop', 'BorderBottom', 'BorderLeft', 'BorderRight', 'RadiusTopLeft', 'RadiusTopRight', 'RadiusBottomLeft', 'RadiusBottomRight',
    ]);
  });

  test('empty Bindings is omitted', () => {
    expect(buildItemObject(rect('r', { bindings: {} }), 'p')).not.toHaveProperty('Bindings');
    expect(buildItemObject(rect('r', { bindings: { Visible: undefined } }), 'p')).not.toHaveProperty('Bindings');
  });

  test('RenderingSkip and MinimumRefreshIntervalMS are written and overridable', () => {
    const o = buildItemObject(rect('r', { renderingSkip: 2, minimumRefreshIntervalMs: 100 }), 'p');
    expect(o.RenderingSkip).toBe(2);
    expect(o.MinimumRefreshIntervalMS).toBe(100);
  });
});

describe('colours', () => {
  test('#RRGGBB becomes #FFRRGGBB everywhere', () => {
    const d = dashboard('OpenDash', [screen('Main', [
      label('t', 'X', { textColor: '#33d9f2', backgroundColor: '#0a0b0d', border: { color: '#ffffff', top: 1 } }),
    ], { backgroundColor: '#14161a' })], { backgroundColor: '#060708' });
    const doc = buildDashboardObject(d, CTX);
    const s = (doc.Screens as Record<string, unknown>[])[0]!;
    const t = (s.Items as Record<string, unknown>[])[0]!;
    expect(doc.BackgroundColor).toBe('#FF060708');
    expect(s.BackgroundColor).toBe('#FF14161A');
    expect(t.TextColor).toBe('#FF33D9F2');
    expect(t.BackgroundColor).toBe('#FF0A0B0D');
    expect((t.BorderStyle as Record<string, unknown>).BorderColor).toBe('#FFFFFFFF');
  });

  test('#AARRGGBB is kept', () => {
    expect(buildItemObject(rect('r', { backgroundColor: '#80ffb300' }), 'p').BackgroundColor).toBe('#80FFB300');
  });

  test('an invalid colour throws', () => {
    expect(() => buildItemObject(rect('r', { backgroundColor: '#12345' as never }), 'p')).toThrow(TypeError);
  });
});

describe('bindings', () => {
  test('formula binding is Mode 2 with the expression', () => {
    expect(buildBindingObject({ mode: 'formula', formula: '[DataCorePlugin.GameData.Gear]' })).toEqual({
      Formula: { Expression: '[DataCorePlugin.GameData.Gear]' },
      Mode: 2,
    });
  });

  test('a format string sits beside Formula', () => {
    const o = buildBindingObject({ mode: 'formula', formula: '[X]', formatString: '0.0' });
    expect(Object.keys(o)).toEqual(['FormatString', 'Formula', 'Mode']);
    expect(o.FormatString).toBe('0.0');
  });

  test('javascript formulas add Interpreter 1 and JSExt 0, and a pre-expression', () => {
    const o = buildBindingObject({ mode: 'formula', formula: { expression: 'return 1;', interpreter: 'js', preExpression: 'var a = 1;' } });
    expect(o.Formula).toEqual({ Interpreter: 1, JSExt: 0, Expression: 'return 1;', PreExpression: 'var a = 1;' });
    expect(Object.keys(o.Formula as object)).toEqual(['Interpreter', 'JSExt', 'Expression', 'PreExpression']);
    const ncalc = buildBindingObject({ mode: 'formula', formula: { expression: '[X]', interpreter: 'ncalc' } });
    expect(ncalc.Formula).toEqual({ Expression: '[X]' });
  });

  test('gradient binding is Mode 4 with the colour ramp', () => {
    const o = buildBindingObject({ mode: 'gradient', formula: '[X]', startColor: '#000000', startValue: 0, endColor: '#ff0000', endValue: 1 });
    expect(Object.keys(o)).toEqual(['Formula', 'StartColor', 'EnableMiddleColor', 'MiddleColor', 'MiddleColorValue', 'EndColor', 'StartColorValue', 'EndColorValue', 'Mode']);
    expect(o).toMatchObject({ StartColor: '#FF000000', EnableMiddleColor: false, EndColor: '#FFFF0000', StartColorValue: 0, EndColorValue: 1, Mode: 4 });
    const mid = buildBindingObject({ mode: 'gradient', formula: '[X]', startColor: '#000000', startValue: -1, endColor: '#ff0000', endValue: 1, middleColor: '#ffffff', middleValue: 0 });
    expect(mid).toMatchObject({ EnableMiddleColor: true, MiddleColor: '#FFFFFFFF', MiddleColorValue: 0 });
  });

  test('bindings keep the declaration order and target names', () => {
    const o = buildItemObject(numeral('v', '[X]', {
      bindings: {
        TextColor: { mode: 'formula', formula: "if([X] < 0, '#FF00FF00', '#FFFF0000')" },
        Text: { mode: 'formula', formula: 'format([X], \'0.00\')' },
        Left: { mode: 'formula', formula: '16 + 29' },
      },
    }), 'p');
    expect(Object.keys(o.Bindings as object)).toEqual(['TextColor', 'Text', 'Left']);
    const keys = Object.keys(o);
    expect(keys.indexOf('Bindings')).toBe(keys.indexOf('BorderStyle') + 1);
    expect(keys.indexOf('Id')).toBe(keys.indexOf('Bindings') + 1);
  });
});

describe('stable ids', () => {
  test('dashboard, screen and item ids derive from their paths', () => {
    const d = dashboard('OpenDash', [screen('Main', [rect('rule'), layer('L', [rect('inner')])])]);
    const doc = buildDashboardObject(d, CTX);
    expect(doc.Id).toBe(stableGuid('OpenDash/OpenDash'));
    const s = (doc.Screens as Record<string, unknown>[])[0]!;
    expect(s.ScreenId).toBe(stableGuid('OpenDash/OpenDash/Main'));
    const items = s.Items as Record<string, unknown>[];
    expect(items[0]!.Id).toBe(stableGuid('OpenDash/OpenDash/Main/rule'));
    expect(items[1]!.Id).toBe(stableGuid('OpenDash/OpenDash/Main/L'));
    expect((items[1]!.Childrens as Record<string, unknown>[])[0]!.Id).toBe(stableGuid('OpenDash/OpenDash/Main/L/inner'));
  });

  test('the package name is part of the path', () => {
    const d = dashboard('OpenDash', [screen('Main', [rect('r')])]);
    expect(buildDashboardObject(d, { packageName: 'a' }).Id).not.toBe(buildDashboardObject(d, { packageName: 'b' }).Id);
  });

  test('explicit ids are kept and children of an explicitly identified layer still derive from the path', () => {
    const id = '0123abcd-0123-4123-8123-0123456789ab';
    const d = dashboard('OpenDash', [screen('Main', [layer('L', [rect('c')], { id })], { id })], { id });
    const doc = buildDashboardObject(d, CTX);
    expect(doc.Id).toBe(id);
    const s = (doc.Screens as Record<string, unknown>[])[0]!;
    expect(s.ScreenId).toBe(id);
    const l = (s.Items as Record<string, unknown>[])[0]!;
    expect(l.Id).toBe(id);
    expect((l.Childrens as Record<string, unknown>[])[0]!.Id).toBe(stableGuid('OpenDash/OpenDash/Main/L/c'));
  });

  test('two serialisations are byte-identical', () => {
    const pkg = samplePackage();
    const a = serializeDashboard(pkg.dashboards[0]!, { packageName: pkg.folderName });
    const b = serializeDashboard(samplePackage().dashboards[0]!, { packageName: pkg.folderName });
    expect(a).toBe(b);
  });
});

describe('round trip', () => {
  const firstKeyIsType = (items: unknown[]): number => {
    let count = 0;
    for (const item of items as Record<string, unknown>[]) {
      expect(Object.keys(item)[0]).toBe('$type');
      expect(typeof item.$type).toBe('string');
      count++;
      if (Array.isArray(item.Childrens)) count += firstKeyIsType(item.Childrens);
    }
    return count;
  };

  test('every item of a parsed document has $type as its first key', () => {
    const pkg = samplePackage();
    let total = 0;
    for (const d of pkg.dashboards) {
      const parsed = JSON.parse(serializeDashboard(d, { packageName: pkg.folderName })) as Record<string, unknown>;
      for (const s of parsed.Screens as Record<string, unknown>[]) total += firstKeyIsType(s.Items as unknown[]);
    }
    // main: rule, shiftLights, seg00, seg01, Slot01, Slot02; cards: 2 screens x (label, value)
    expect(total).toBe(10);
  });

  test('all colours in the output are #AARRGGBB', () => {
    const pkg = samplePackage();
    const text = serializeDashboard(pkg.dashboards[0]!, { packageName: pkg.folderName });
    const colours = text.match(/"#[0-9A-Fa-f]+"/g) ?? [];
    expect(colours.length).toBeGreaterThan(0);
    for (const c of colours) expect(c).toMatch(/^"#[0-9A-F]{8}"$/);
  });
});

/**
 * The guard that stands between a fraction and a dashboard that draws nothing.
 *
 * `JsonTextReader.ReadAsInt32` throws on "3.6" rather than truncating it, and the throw unwinds
 * SimHub's whole `LoadFromFile`, so one bad corner radius costs the entire file. That is how
 * 0.3.0-rc.1 shipped with every zone B, every zone C, every companion and every pit wall zone
 * blank: the sub-dashboards holding the modules each carried one.
 */
describe('a field SimHub reads as an integer', () => {
  test('is reported wherever it holds a fraction, with the path SimHub would name', () => {
    const found = fractionalIntFields({ Screens: [{ Items: [{ BorderStyle: { RadiusTopLeft: 3.6, RadiusTopRight: 4 } }] }] });
    expect(found).toEqual([{ path: 'Screens[0].Items[0].BorderStyle.RadiusTopLeft', value: 3.6 }]);
  });

  test('is not reported for a name that is floating point on another class', () => {
    // Left, Top, Width and Height are double on DrawableItem and int elsewhere, so they cannot be
    // judged by name and are deliberately outside the set.
    expect(fractionalIntFields({ Left: 12.5, Top: 0.5, Width: 33.3, Height: 7.25 })).toEqual([]);
  });

  test('stops a document being written at all, rather than leaving SimHub to refuse the file', () => {
    // serializeDashboard calls this on the built document, so a fraction that reached any integer
    // field fails the build instead of reaching DashTemplates.
    expect(() => assertWholeNumbers({ Screens: [{ RenderingSkip: 1.5 }] }, 'the dashboard test')).toThrow(/RenderingSkip/);
    expect(() => assertWholeNumbers({ Screens: [{ RenderingSkip: 1 }] }, 'the dashboard test')).not.toThrow();
  });

  test('is rounded by the serialiser wherever a border carries it', () => {
    const border = buildBorderObject({ radius: 3.5999999999999996, top: 1.4 });
    expect(border).toEqual({ RadiusTopLeft: 4, RadiusTopRight: 4, RadiusBottomLeft: 4, RadiusBottomRight: 4, BorderTop: 1 });
  });
});
