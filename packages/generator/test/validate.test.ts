/** validatePackage: every error and warning the spec lists, plus the happy path. */

import { describe, expect, test } from 'bun:test';
import type { DashPackage, Item } from '../src/model.ts';
import { ALLOWED_BINDING_TARGETS, formatIssues, propertyReferences, validatePackage } from '../src/validate.ts';
import { BARLOW_CONDENSED_SEMIBOLD, BARLOW_MEDIUM, DECLARED, dashboard, ellipse, label, layer, numeral, rect, samplePackage, screen, widget } from './fixtures.ts';

const OPTS = { declaredProperties: DECLARED, propertyPrefix: 'OpenDash' };

const codes = (issues: { code: string }[]): string[] => issues.map((i) => i.code);

const single = (items: Item[], fonts: string[] = [BARLOW_MEDIUM, BARLOW_CONDENSED_SEMIBOLD]): DashPackage => ({
  folderName: 'openDash',
  dashboards: [dashboard('openDash', [screen('Main', items)])],
  fonts,
});

describe('happy path', () => {
  test('the sample package is valid with no warnings', () => {
    const r = validatePackage(samplePackage(), OPTS);
    expect(r.errors).toEqual([]);
    expect(r.warnings).toEqual([]);
    expect(r.ok).toBe(true);
  });

  test('declared properties may carry the prefix or not', () => {
    const pkg = samplePackage();
    expect(validatePackage(pkg, { ...OPTS, declaredProperties: ['ShiftLights', 'Slot01', 'OpenDash.Slot02'] }).ok).toBe(true);
  });

  test('formatIssues renders one line per issue', () => {
    const r = validatePackage(single([rect('r', { backgroundColor: 'red' as never })]), OPTS);
    expect(formatIssues(r.errors)).toMatch(/^color\/invalid openDash\/openDash\/Main\/r#backgroundColor: /);
  });
});

describe('package level errors', () => {
  test('no dashboards', () => {
    const r = validatePackage({ folderName: 'openDash', dashboards: [], fonts: [] }, OPTS);
    expect(codes(r.errors)).toContain('package/empty');
    expect(r.ok).toBe(false);
  });

  test('first dashboard must be named after the folder', () => {
    const pkg = samplePackage();
    pkg.dashboards.reverse();
    expect(codes(validatePackage(pkg, OPTS).errors)).toContain('package/main-name');
    const ok = samplePackage();
    ok.folderName = 'Other';
    expect(codes(validatePackage(ok, OPTS).errors)).toContain('package/main-name');
  });

  test('two dashboards with the same file name', () => {
    const pkg = samplePackage();
    pkg.dashboards.push({ ...pkg.dashboards[1]!, name: 'Cards' });
    expect(codes(validatePackage(pkg, OPTS).errors)).toContain('package/duplicate-dashboard');
  });

  test('folder and dashboard names must be file names', () => {
    const pkg = samplePackage('open/Dash');
    expect(codes(validatePackage(pkg, OPTS).errors)).toContain('package/folder-name');
    const bad = samplePackage();
    bad.dashboards[1]!.name = 'ca:rds';
    expect(codes(validatePackage(bad, OPTS).errors)).toContain('name/invalid');
  });

  test('two font files with the same base name', () => {
    const pkg = samplePackage();
    pkg.fonts.push(`/elsewhere/${BARLOW_MEDIUM.split('/').pop()}`);
    expect(codes(validatePackage(pkg, OPTS).errors)).toContain('font/duplicate-file');
  });
});

describe('structure errors', () => {
  test('dashboard without screens and screen without items', () => {
    const noScreens: DashPackage = { folderName: 'openDash', dashboards: [dashboard('openDash', [])], fonts: [] };
    expect(codes(validatePackage(noScreens, OPTS).errors)).toContain('dashboard/no-screens');
    expect(codes(validatePackage(single([]), OPTS).errors)).toContain('screen/no-items');
  });

  test('a dashboard without metadata', () => {
    const pkg = single([rect('r')]);
    pkg.dashboards[0]!.metadata = undefined as never;
    const r = validatePackage(pkg, OPTS);
    expect(r.errors.filter((e) => e.code === 'metadata/missing').map((e) => e.path)).toEqual(['openDash/openDash']);
    expect(codes(r.errors)).not.toContain('metadata/version');
    expect(codes(r.errors)).not.toContain('metadata/simhub-version');
    expect(r.ok).toBe(false);
  });

  test('metadata must carry versions and a valid preview index', () => {
    const pkg = single([rect('r')]);
    pkg.dashboards[0]!.metadata = { title: 't', author: 'a', version: '', simHubVersion: '', mainPreviewIndex: 3 };
    const c = codes(validatePackage(pkg, OPTS).errors);
    expect(c).toContain('metadata/version');
    expect(c).toContain('metadata/simhub-version');
    expect(c).toContain('metadata/preview-index');
  });

  test('duplicate screen names', () => {
    const pkg: DashPackage = { folderName: 'openDash', dashboards: [dashboard('openDash', [screen('A', [rect('r')]), screen('A', [rect('r')])])], fonts: [] };
    const r = validatePackage(pkg, OPTS);
    expect(codes(r.errors)).toContain('name/duplicate');
    expect(codes(r.errors)).toContain('id/duplicate');
  });
});

describe('colours', () => {
  test('invalid hex anywhere is an error with the property in the path', () => {
    const r = validatePackage(single([
      label('t', 'X', { textColor: '#12' as never, backgroundColor: 'blue' as never, border: { color: '#GGGGGG' as never, top: 1 } }),
    ]), OPTS);
    const paths = r.errors.filter((e) => e.code === 'color/invalid').map((e) => e.path);
    expect(paths).toEqual([
      'openDash/openDash/Main/t#backgroundColor',
      'openDash/openDash/Main/t#border.color',
      'openDash/openDash/Main/t#textColor',
    ]);
  });

  test('dashboard, screen and gradient colours are checked too', () => {
    const pkg = single([rect('r', { bindings: { BackgroundColor: { mode: 'gradient', formula: '[X]', startColor: 'x' as never, startValue: 0, endColor: '#00FF00', endValue: 1, middleColor: '#1' as never } } })]);
    pkg.dashboards[0]!.backgroundColor = 'black' as never;
    pkg.dashboards[0]!.screens[0]!.backgroundColor = '#0000' as never;
    const paths = validatePackage(pkg, OPTS).errors.filter((e) => e.code === 'color/invalid').map((e) => e.path);
    expect(paths).toContain('openDash/openDash#backgroundColor');
    expect(paths).toContain('openDash/openDash/Main#backgroundColor');
    expect(paths).toContain('openDash/openDash/Main/r#Bindings.BackgroundColor#startColor');
    expect(paths).toContain('openDash/openDash/Main/r#Bindings.BackgroundColor#middleColor');
  });

  test('a required colour that is missing is color/missing; optional ones may be omitted', () => {
    const pkg = single([
      label('t', 'X', { textColor: undefined as never }),
      rect('r', { backgroundColor: undefined, bindings: { BackgroundColor: { mode: 'gradient', formula: '[X]', startValue: 0, endColor: '#00FF00', endValue: 1 } as never } }),
    ]);
    pkg.dashboards[0]!.backgroundColor = undefined as never;
    const r = validatePackage(pkg, OPTS);
    expect(r.errors.filter((e) => e.code === 'color/missing').map((e) => e.path)).toEqual([
      'openDash/openDash#backgroundColor',
      'openDash/openDash/Main/t#textColor',
      'openDash/openDash/Main/r#Bindings.BackgroundColor#startColor',
    ]);
    expect(codes(r.errors)).not.toContain('color/invalid');
    expect(r.ok).toBe(false);
  });
});

describe('ids and names', () => {
  test('duplicate item names within a screen, including across layers', () => {
    const r = validatePackage(single([layer('A', [rect('seg')]), layer('B', [rect('seg')])]), OPTS);
    const dup = r.errors.filter((e) => e.code === 'name/duplicate');
    expect(dup).toHaveLength(1);
    expect(dup[0]!.path).toBe('openDash/openDash/Main/B/seg');
  });

  test('the same name in different screens is fine', () => {
    const pkg: DashPackage = { folderName: 'openDash', dashboards: [dashboard('openDash', [screen('A', [rect('r')]), screen('B', [rect('r')])])], fonts: [] };
    expect(validatePackage(pkg, OPTS).errors).toEqual([]);
  });

  test('duplicate explicit ids across the package', () => {
    const id = '0123abcd-0123-4123-8123-0123456789ab';
    const pkg = samplePackage();
    pkg.dashboards[0]!.screens[0]!.items[0]!.id = id;
    pkg.dashboards[1]!.screens[0]!.items[0]!.id = id;
    const dup = validatePackage(pkg, OPTS).errors.filter((e) => e.code === 'id/duplicate');
    expect(dup).toHaveLength(1);
    expect(dup[0]!.message).toContain('openDash/openDash/Main/rule');
  });

  test('explicit ids must be GUIDs', () => {
    expect(codes(validatePackage(single([rect('r', { id: 'not-a-guid' })]), OPTS).errors)).toContain('id/invalid');
    expect(codes(validatePackage(single([rect('r', { id: '0123ABCD-0123-4123-8123-0123456789AB' })]), OPTS).errors)).toContain('id/invalid');
  });

  test('empty names', () => {
    expect(codes(validatePackage(single([rect('')]), OPTS).errors)).toContain('name/empty');
  });
});

describe('binding targets', () => {
  test('the allowed sets follow the spec', () => {
    expect(ALLOWED_BINDING_TARGETS.layer).toEqual(['Visible', 'Opacity', 'BlinkEnabled', 'Repetitions']);
    expect(ALLOWED_BINDING_TARGETS.widget).toEqual(['Left', 'Top', 'Width', 'Height', 'Visible', 'InitialScreenIndex']);
    expect(ALLOWED_BINDING_TARGETS.rect).not.toContain('Text');
    expect(ALLOWED_BINDING_TARGETS.rect).not.toContain('TextColor');
    expect(ALLOWED_BINDING_TARGETS.rect).not.toContain('FontSize');
    expect(ALLOWED_BINDING_TARGETS.text).toContain('FontSize');
    expect(ALLOWED_BINDING_TARGETS.ellipse).toEqual(['Left', 'Top', 'Width', 'Height', 'Visible', 'BackgroundColor', 'Opacity', 'BlinkEnabled', 'FillColor', 'EllipseColor']);
    expect(ALLOWED_BINDING_TARGETS.chart).toContain('CurrentValue');
    expect(ALLOWED_BINDING_TARGETS.linearGauge).toContain('Value');
    expect(ALLOWED_BINDING_TARGETS.webPage).toContain('StartAddress');
    // Repetitions is a Layer property; nothing else stamps rows.
    for (const kind of ['text', 'rect', 'chart', 'linearGauge', 'radar', 'staticMap', 'webPage', 'widget'] as const) {
      expect(ALLOWED_BINDING_TARGETS[kind]).not.toContain('Repetitions');
    }
    for (const kind of ['text', 'rect', 'layer', 'widget'] as const) {
      expect(ALLOWED_BINDING_TARGETS[kind]).not.toContain('BorderColor');
      expect(ALLOWED_BINDING_TARGETS[kind]).not.toContain('FillColor');
      expect(ALLOWED_BINDING_TARGETS[kind]).not.toContain('EllipseColor');
    }
  });

  test('FillColor and EllipseColor bind on ellipses only, as formulas or gradients', () => {
    const fill = { mode: 'formula', formula: "'#FFFFD400'" } as const;
    const ramp = { mode: 'gradient', formula: '[X]', startColor: '#000000', startValue: 0, endColor: '#FFFFFF', endValue: 1 } as const;
    const ok = validatePackage(single([ellipse('e', { bindings: { FillColor: fill, EllipseColor: ramp, Visible: { mode: 'formula', formula: 'true' } } })]), OPTS);
    expect(ok.errors).toEqual([]);
    const bad = validatePackage(single([
      rect('r', { bindings: { FillColor: fill } as never }),
      label('t', 'X', { bindings: { EllipseColor: fill } as never }),
      layer('l', [rect('c')], { bindings: { FillColor: fill } as never }),
      widget('w', { bindings: { EllipseColor: fill } as never }),
    ]), OPTS);
    expect(bad.errors.filter((e) => e.code === 'binding/unknown-target').map((e) => e.path)).toEqual([
      'openDash/openDash/Main/r#Bindings.FillColor',
      'openDash/openDash/Main/t#Bindings.EllipseColor',
      'openDash/openDash/Main/l#Bindings.FillColor',
      'openDash/openDash/Main/w#Bindings.EllipseColor',
    ]);
  });

  test('BorderColor is not bindable on any item: it lives on BorderStyle, so SimHub would ignore it', () => {
    const border = { mode: 'formula', formula: "'#FFFFFFFF'" };
    const gradient = { mode: 'gradient', formula: '[X]', startColor: '#000000', startValue: 0, endColor: '#FFFFFF', endValue: 1 };
    const r = validatePackage(single([
      label('t', 'X', { bindings: { BorderColor: border } as never }),
      rect('r', { bindings: { BorderColor: gradient } as never }),
    ]), OPTS);
    expect(r.errors.filter((e) => e.code === 'binding/unknown-target').map((e) => e.path)).toEqual([
      'openDash/openDash/Main/t#Bindings.BorderColor',
      'openDash/openDash/Main/r#Bindings.BorderColor',
    ]);
    expect(r.errors.filter((e) => e.code === 'binding/gradient-target').map((e) => e.path)).toEqual(['openDash/openDash/Main/r#Bindings.BorderColor']);
    expect(r.ok).toBe(false);
  });

  test('layer: only Visible, Opacity and BlinkEnabled', () => {
    const r = validatePackage(single([layer('L', [rect('c')], { bindings: { Left: { mode: 'formula', formula: '1' }, Visible: { mode: 'formula', formula: 'true' } } })]), OPTS);
    const bad = r.errors.filter((e) => e.code === 'binding/unknown-target');
    expect(bad.map((e) => e.path)).toEqual(['openDash/openDash/Main/L#Bindings.Left']);
  });

  test('widget: geometry, Visible and InitialScreenIndex only', () => {
    const pkg = samplePackage();
    pkg.dashboards[0]!.screens[0]!.items.push(widget('Slot03', { rect: { left: 513, top: 65, width: 255, height: 187 }, bindings: { Text: { mode: 'formula', formula: "'x'" }, Width: { mode: 'formula', formula: '255' } } }));
    const bad = validatePackage(pkg, OPTS).errors.filter((e) => e.code === 'binding/unknown-target');
    expect(bad.map((e) => e.path)).toEqual(['openDash/openDash/Main/Slot03#Bindings.Text']);
  });

  test('rectangle: no Text, TextColor or FontSize', () => {
    const r = validatePackage(single([rect('r', { bindings: { Text: { mode: 'formula', formula: "'x'" }, TextColor: { mode: 'formula', formula: "'#FFFFFFFF'" }, FontSize: { mode: 'formula', formula: '10' }, Width: { mode: 'formula', formula: '10' } } })]), OPTS);
    expect(r.errors.filter((e) => e.code === 'binding/unknown-target').map((e) => e.path.split('.').pop())).toEqual(['Text', 'TextColor', 'FontSize']);
  });

  test('a gradient can only drive a colour', () => {
    const r = validatePackage(single([rect('r', { bindings: { Left: { mode: 'gradient', formula: '[X]', startColor: '#000000', startValue: 0, endColor: '#FFFFFF', endValue: 1 } } })]), OPTS);
    expect(codes(r.errors)).toContain('binding/gradient-target');
  });

  test('an empty expression is a warning', () => {
    const r = validatePackage(single([rect('r', { bindings: { Visible: { mode: 'formula', formula: '  ' } } })]), OPTS);
    expect(codes(r.warnings)).toContain('binding/empty-expression');
    expect(r.ok).toBe(true);
  });

  test('a mode other than formula or gradient is an error', () => {
    const r = validatePackage(single([rect('r', { bindings: { Visible: { mode: 'expression', formula: 'true' } as never } })]), OPTS);
    const e = r.errors.filter((x) => x.code === 'binding/unknown-mode');
    expect(e.map((x) => x.path)).toEqual(['openDash/openDash/Main/r#Bindings.Visible']);
    expect(e[0]!.message).toContain('"expression"');
    expect(r.ok).toBe(false);
  });

  test('a non-string expression is an error, as a bare formula or inside a Formula object', () => {
    const r = validatePackage(single([
      rect('a', { bindings: { Visible: { mode: 'formula', formula: 42 as never } } }),
      rect('b', { bindings: { Width: { mode: 'formula', formula: { expression: ['[X]'] as never } } } }),
      rect('c', { bindings: { Visible: { mode: 'formula', formula: 'true' } } }),
    ]), OPTS);
    expect(r.errors.filter((x) => x.code === 'binding/invalid-expression').map((x) => x.path)).toEqual([
      'openDash/openDash/Main/a#Bindings.Visible',
      'openDash/openDash/Main/b#Bindings.Width',
    ]);
    expect(r.ok).toBe(false);
  });
});

describe('widgets', () => {
  test('fileName must be a dashboard of the package', () => {
    const pkg = samplePackage();
    pkg.dashboards[0]!.screens[0]!.items.push(widget('Slot03', { fileName: 'nope.djson' }));
    const r = validatePackage(pkg, OPTS);
    expect(r.errors.filter((e) => e.code === 'widget/missing-file').map((e) => e.path)).toEqual(['openDash/openDash/Main/Slot03#fileName']);
  });

  test('case differences are a warning, self reference an error, bad screen index an error', () => {
    const pkg = samplePackage();
    const items = pkg.dashboards[0]!.screens[0]!.items;
    items.push(widget('Slot03', { fileName: 'Cards.djson', initialScreenIndex: 2 }));
    items.push(widget('Slot04', { fileName: 'openDash.djson' }));
    const r = validatePackage(pkg, OPTS);
    expect(codes(r.warnings)).toContain('widget/file-case');
    expect(r.errors.filter((e) => e.code === 'widget/screen-index').map((e) => e.path)).toEqual(['openDash/openDash/Main/Slot03#initialScreenIndex']);
    expect(r.errors.filter((e) => e.code === 'widget/self-reference').map((e) => e.path)).toEqual(['openDash/openDash/Main/Slot04#fileName']);
  });
});

describe('plugin properties', () => {
  test('propertyReferences finds NCalc and JavaScript reads', () => {
    expect(propertyReferences("isnull([OpenDash.Slot01], 0) + $prop('OpenDash.ShiftLights') + $prop(\"A.B\")")).toEqual(['OpenDash.Slot01', 'OpenDash.ShiftLights', 'A.B']);
  });

  test('an undeclared [OpenDash.X] in a binding is an error', () => {
    const r = validatePackage(single([rect('r', { bindings: { Visible: { mode: 'formula', formula: 'isnull([OpenDash.Nope], true)' } } })]), OPTS);
    const e = r.errors.filter((x) => x.code === 'property/undeclared');
    expect(e).toHaveLength(1);
    expect(e[0]!.path).toBe('openDash/openDash/Main/r#Bindings.Visible');
    expect(e[0]!.message).toContain('[OpenDash.Nope]');
  });

  test('pre-expressions, javascript and screen enabled expressions are checked; other prefixes are not', () => {
    const pkg = single([
      rect('r', { bindings: { Visible: { mode: 'formula', formula: { expression: 'return a;', interpreter: 'js', preExpression: "var a = $prop('OpenDash.Missing');" } } } }),
      rect('s', { bindings: { Visible: { mode: 'formula', formula: '[DataCorePlugin.GameData.Gear] = [OtherPlugin.Thing]' } } }),
    ]);
    pkg.dashboards[0]!.screens[0]!.enabledExpression = '[OpenDash.AlsoMissing] = 1';
    const e = validatePackage(pkg, OPTS).errors.filter((x) => x.code === 'property/undeclared').map((x) => x.path);
    expect(e).toEqual(['openDash/openDash/Main#enabledExpression', 'openDash/openDash/Main/r#Bindings.Visible']);
  });
});

describe('fonts', () => {
  test('an empty or blank font family is an error, and is not reported again as unbundled', () => {
    const r = validatePackage(single([label('a', 'X', { font: '' }), label('b', 'Y', { font: '   ' }), numeral('v', '[X]')]), OPTS);
    expect(r.errors.filter((e) => e.code === 'font/empty').map((e) => e.path)).toEqual([
      'openDash/openDash/Main/a#font',
      'openDash/openDash/Main/b#font',
    ]);
    expect(r.warnings).toEqual([]);
    expect(r.ok).toBe(false);
  });
});

describe('ellipses', () => {
  test('fill and stroke colours are required and the thickness must not be negative', () => {
    const r = validatePackage(single([
      ellipse('a', { fillColor: undefined as never, strokeColor: 'red' as never }),
      ellipse('b', { strokeThickness: -1 }),
      ellipse('c', { strokeThickness: 0 }),
    ]), OPTS);
    expect(r.errors.map((e) => `${e.code} ${e.path}`)).toEqual([
      'color/missing openDash/openDash/Main/a#fillColor',
      'color/invalid openDash/openDash/Main/a#strokeColor',
      'ellipse/thickness openDash/openDash/Main/b#strokeThickness',
    ]);
  });

  test('a ring a few pixels inside the canvas raises no warning', () => {
    const pkg = single([ellipse('ring')]);
    pkg.dashboards[0]!.width = 480;
    pkg.dashboards[0]!.height = 480;
    const r = validatePackage(pkg, OPTS);
    expect(r.errors).toEqual([]);
    expect(r.warnings).toEqual([]);
  });
});

describe('rotation', () => {
  test('is accepted on text, rectangle and ellipse items and refused on layers and widgets', () => {
    const r = validatePackage(single([
      rect('r', { rotation: 12 }),
      label('t', 'X', { rotation: -84 }),
      ellipse('e', { rotation: 45 }),
      layer('l', [rect('c')], { rotation: 90 }),
      widget('w', { rotation: 5 }),
      layer('zero', [rect('d')], { rotation: 0 }),
      rect('nan', { rotation: Number.NaN }),
    ]), OPTS);
    // The fixture widget has no cards.djson to point at; that error is not what this test is about.
    expect(r.errors.filter((e) => e.code !== 'widget/missing-file').map((e) => `${e.code} ${e.path}`)).toEqual([
      'rotation/unsupported openDash/openDash/Main/l#rotation',
      'rotation/unsupported openDash/openDash/Main/w#rotation',
      'number/invalid openDash/openDash/Main/nan#rotation',
    ]);
  });

  test('the canvas check uses the rotated footprint', () => {
    const pkg = single([
      // 20 x 12 on the rim of a 480 face at 3 o clock: the unrotated box leaves the canvas, the rotated one does not.
      rect('tangent', { rect: { left: 464, top: 234, width: 20, height: 12 }, rotation: 90 }),
      rect('flat', { rect: { left: 464, top: 234, width: 20, height: 12 } }),
      rect('spun', { rect: { left: 460, top: 0, width: 20, height: 12 }, rotation: 45 }),
    ]);
    pkg.dashboards[0]!.width = 480;
    pkg.dashboards[0]!.height = 480;
    const w = validatePackage(pkg, OPTS).warnings.filter((x) => x.code === 'geometry/outside-canvas');
    expect(w.map((x) => x.path)).toEqual(['openDash/openDash/Main/flat#rect', 'openDash/openDash/Main/spun#rect']);
    expect(w[1]!.message).toContain('rotated 45 deg');
  });
});

describe('numbers', () => {
  test('non-finite geometry, font size, opacity and blink delay', () => {
    const r = validatePackage(single([
      label('t', 'X', { rect: { left: Number.NaN, top: 0, width: 10, height: 10 }, fontSize: 0, opacity: 120, blink: { delayMs: 0 } }),
      rect('r', { rect: { left: 0, top: 0, width: -1, height: 10 } }),
    ]), OPTS);
    const c = codes(r.errors);
    expect(c).toContain('number/invalid');
    expect(c).toContain('font/size');
    expect(c).toContain('opacity/range');
    expect(c).toContain('blink/delay');
    expect(c).toContain('size/negative');
  });
});

describe('warnings', () => {
  test('items outside the canvas', () => {
    const r = validatePackage(single([
      rect('in', { rect: { left: 0, top: 0, width: 1920, height: 480 } }),
      rect('right', { rect: { left: 1900, top: 0, width: 40, height: 10 } }),
      rect('above', { rect: { left: 0, top: -1, width: 10, height: 10 } }),
      layer('L', [rect('deep', { rect: { left: 0, top: 470, width: 10, height: 11 } })]),
    ]), OPTS);
    expect(r.warnings.filter((w) => w.code === 'geometry/outside-canvas').map((w) => w.path)).toEqual([
      'openDash/openDash/Main/right#rect',
      'openDash/openDash/Main/above#rect',
      'openDash/openDash/Main/L/deep#rect',
    ]);
    expect(r.ok).toBe(true);
  });

  test('the canvas is the dashboard being validated, so a cards widget is checked against its own size', () => {
    const pkg = samplePackage();
    pkg.dashboards[1]!.screens[0]!.items.push(rect('wide', { rect: { left: 0, top: 0, width: 256, height: 10 } }));
    const w = validatePackage(pkg, OPTS).warnings.filter((x) => x.code === 'geometry/outside-canvas');
    expect(w.map((x) => x.path)).toEqual(['openDash/cards/currentLap/wide#rect']);
  });

  test('zero-sized items', () => {
    const r = validatePackage(single([rect('r', { rect: { left: 0, top: 0, width: 0, height: 10 } })]), OPTS);
    expect(codes(r.warnings)).toContain('size/empty');
  });

  test('fonts not bundled: by family name from the TTF, once per family', () => {
    const r = validatePackage(single([label('a', 'X', { font: 'Segoe UI' }), label('b', 'Y', { font: 'Segoe UI' }), numeral('v', '[X]')], [BARLOW_CONDENSED_SEMIBOLD]), OPTS);
    const missing = r.warnings.filter((w) => w.code === 'font/missing');
    expect(missing).toHaveLength(1);
    expect(missing[0]!.path).toBe('openDash/openDash/Main/a#font');
    expect(missing[0]!.message).toContain('Segoe UI');
  });

  test('a bundled family without the requested weight', () => {
    const r = validatePackage(single([label('a', 'X', { fontWeight: 'Bold' })], [BARLOW_MEDIUM]), OPTS);
    expect(r.warnings.filter((w) => w.code === 'font/weight-missing').map((w) => w.path)).toEqual(['openDash/openDash/Main/a#fontWeight']);
    expect(validatePackage(single([label('a', 'X', { fontWeight: 'Medium' })], [BARLOW_MEDIUM]), OPTS).warnings).toEqual([]);
  });

  test('a font file that does not exist matches by its file name', () => {
    const r = validatePackage(single([numeral('v', '[X]')], ['/not/here/BarlowCondensed-SemiBold.ttf']), OPTS);
    expect(r.warnings).toEqual([]);
    const bold = validatePackage(single([numeral('v', '[X]', { fontWeight: 'Bold' })], ['/not/here/BarlowCondensed-SemiBold.ttf']), OPTS);
    expect(codes(bold.warnings)).toEqual(['font/weight-missing']);
  });

  test('no fonts at all warns for every family used', () => {
    const r = validatePackage(single([label('a', 'X'), numeral('v', '[X]')], []), OPTS);
    expect(r.warnings.filter((w) => w.code === 'font/missing').map((w) => w.message)).toEqual([
      expect.stringContaining('"Barlow"'),
      expect.stringContaining('"Barlow Condensed"'),
    ]);
  });
});

describe('formula null guard', () => {
  test('a null formula is reported, not thrown', async () => {
    const { validatePackage } = await import('../src/validate.ts');
    const { samplePackage } = await import('./fixtures.ts');
    const pkg = samplePackage();
    const screen = pkg.dashboards[0]!.screens[0]!;
    const item = screen.items[0]! as { bindings?: Record<string, unknown> };
    item.bindings = { Text: { mode: 'formula', formula: null } };
    const result = validatePackage(pkg, { declaredProperties: [], propertyPrefix: 'OpenDash' });
    expect(result.errors.some((e) => e.code === 'binding/invalid-expression')).toBe(true);
  });
});
