/**
 * The demo's scene parser against the `.djson` files the build writes (#395): every item is read
 * as a kind the renderer draws, every binding is kept with its expression, the widgets a package
 * includes are all in its folder, and every face and weight a text names is one the demo loads.
 */
import { describe, expect, test } from 'bun:test';
import { THEME_CATALOGUE } from '../../packages/dash/src/contract.ts';
import { DEMO_FACES as DEMO_FONT_FACES, familyFor, WEIGHTS } from '../lib/demo/fonts.ts';
import { itemsOf, parseDashboard, parsePaint, shortType, widgetFiles, SceneError } from '../lib/demo/scene.ts';
import { builtFaces, HAS_BUILD } from './demoBuild.ts';

/** Every item object in a raw document, layers' children included. */
function rawItems(json: unknown): Record<string, unknown>[] {
  const out: Record<string, unknown>[] = [];
  const walk = (items: unknown) => {
    if (!Array.isArray(items)) return;
    for (const item of items as Record<string, unknown>[]) {
      out.push(item);
      walk(item.Childrens);
    }
  };
  for (const screen of (json as { Screens: { Items: unknown }[] }).Screens) walk(screen.Items);
  return out;
}

describe('colours', () => {
  test('reads the forms ColorConverter reads, and refuses the rest', () => {
    expect(parsePaint('#FF102030')).toEqual({ css: 'rgba(16,32,48,1)', alpha: 1 });
    expect(parsePaint('#00FFFFFF')?.alpha).toBe(0);
    expect(parsePaint('#102030')?.css).toBe('rgba(16,32,48,1)');
    expect(parsePaint('#8F00')?.alpha).toBeCloseTo(0x88 / 255, 5);
    expect(parsePaint('DeepSkyBlue')?.css).toBe('deepskyblue');
    expect(parsePaint('#12345')).toBeNull();
    expect(parsePaint('12, 34')).toBeNull();
  });
});

describe('a document that is not a dashboard', () => {
  test('is refused with the file named', () => {
    expect(() => parseDashboard([], 'x.djson')).toThrow(SceneError);
    expect(() => parseDashboard({ BaseWidth: 1 }, 'x.djson')).toThrow(/x\.djson has no Screens/);
  });
});

describe('the built packages', () => {
  const faces = builtFaces();

  test.if(!HAS_BUILD)('skipped: build/manifest.json is absent or stale, run bun run build at the repository root', () => {
    expect(HAS_BUILD).toBe(false);
  });

  test.if(HAS_BUILD)('are the ten faces of the default look, the two companions and the two pit walls, and no superseded package', () => {
    const of = (group: string) => faces.filter((f) => f.group === group).map((f) => f.folder);
    expect(of('face').length).toBe(10);
    expect(of('face')).toContain('OpenDash 850x480');
    expect(of('companion')).toEqual(['OpenDash Companion', 'OpenDash Companion portrait']);
    expect(of('pitwall')).toEqual(['OpenDash Pit wall', 'OpenDash Pit wall portrait']);
    expect(faces.some((f) => /slots/.test(f.folder))).toBe(false);
  });

  // A plain `bun run build` writes the default theme alone; `--all-themes` writes the car themes too.
  test.if(HAS_BUILD)('and every face of every car theme the build wrote, at each size the catalogue claims for it', () => {
    for (const theme of THEME_CATALOGUE.filter((t) => t.id !== 'default')) {
      const built = faces.filter((f) => f.group === 'theme' && f.theme === theme.id);
      if (built.length === 0) continue;
      expect(built.map((f) => `${f.width}x${f.height}`).sort()).toEqual(theme.sizes.map((s) => `${s.width}x${s.height}`).sort());
    }
  });

  /**
   * Every face and weight a built text names is one the demo loads, so that a run is drawn in the
   * weight it was measured in and not in one the browser synthesises: the Porsche draws its numerals
   * in Barlow Medium (`DATA_IN_LABEL_FAMILY` in packages/dash/src/design/metrics.ts), the house in the
   * condensed display's SemiBold and Bold.
   */
  test.each(faces.map((f) => [f.folder, f] as const))('%s: every font and weight it names is one the demo loads', (_folder, face) => {
    const missing = new Set<string>();
    for (const json of face.raw.values()) {
      for (const item of rawItems(json)) {
        if (shortType(String(item.$type)) !== 'TextItem') continue;
        const family = familyFor(String(item.Font ?? 'Segoe UI'));
        const weight = WEIGHTS[String(item.FontWeight ?? 'Normal')];
        if (!DEMO_FONT_FACES.some((f) => f.family === family && f.weight === weight)) missing.add(`${String(item.Font)} ${String(item.FontWeight)}`);
      }
    }
    expect([...missing]).toEqual([]);
  });

  test.each(faces.map((f) => [f.folder, f] as const))('%s: every item is kept, as a kind the renderer draws', (_folder, face) => {
    for (const [file, json] of face.raw) {
      const scene = face.library.get(file)!;
      const parsed = scene.screens.flatMap((s) => [...itemsOf(s.items)]);
      const raw = rawItems(json);
      expect({ file, items: parsed.length }).toEqual({ file, items: raw.length });
      expect({ file, unknown: scene.unknownTypes }).toEqual({ file, unknown: [] });
      expect(scene.width).toBe((json as { BaseWidth: number }).BaseWidth);
      // The kinds line up one for one with the `$type`s, in document order.
      const kinds: string[] = parsed.map((i) => i.kind);
      const expected = raw.map((r) => {
        const t = shortType(String(r.$type));
        return { TextItem: 'text', RectangleItem: 'rect', EllipseItem: 'ellipse', Layer: 'layer', WidgetItem: 'widget', ChartItem: 'chart', LinearGaugeItem: 'gauge', ImageItem: 'image' }[t] ?? 'standIn';
      });
      expect(kinds).toEqual(expected);
    }
  });

  test.each(faces.map((f) => [f.folder, f] as const))('%s: every binding keeps its target and its expression', (_folder, face) => {
    for (const [file, json] of face.raw) {
      const scene = face.library.get(file)!;
      const parsed = scene.screens.flatMap((s) => [...itemsOf(s.items)]);
      const raw = rawItems(json);
      raw.forEach((r, i) => {
        const bindings = (r.Bindings ?? {}) as Record<string, { Formula: { Expression: string } }>;
        const item = parsed[i]!;
        expect(item.bindings.map((b) => [b.target, b.expression])).toEqual(Object.entries(bindings).map(([k, v]) => [k, v.Formula.Expression]));
        const border = (r.BorderStyle as { Bindings?: { BorderColor?: { Formula: { Expression: string } } } } | undefined)?.Bindings?.BorderColor;
        if (border && item.kind !== 'layer') expect(item.border?.colorBinding?.expression).toBe(border.Formula.Expression);
      });
    }
  });

  test.each(faces.map((f) => [f.folder, f] as const))('%s: the main dashboard, its screens, and every widget it includes is in the folder', (_folder, face) => {
    const main = face.library.get(face.main);
    expect(main).toBeDefined();
    expect({ width: main!.width, height: main!.height }).toEqual({ width: face.width, height: face.height });
    expect(main!.screens.length).toBeGreaterThan(0);
    const missing = [...face.library.values()].flatMap((d) => widgetFiles(d)).filter((f) => !face.library.has(f));
    expect(missing).toEqual([]);
  });
});
