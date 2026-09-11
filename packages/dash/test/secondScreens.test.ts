/**
 * The companion and the pit wall: that they are built, that they fit, and that they agree with the
 * settings contract. The text checks are the same ones the dash face gets, because SimHub hands
 * every box to WPF as a hard clip whatever screen it is on.
 */
import { describe, expect, test } from 'bun:test';
import { measureText, type MeasuredFace } from '../src/design/advances.ts';
import { LINE_SPACING } from '../src/design/metrics.ts';
import {
  MODULE_CATALOGUE,
  MODULE_COUNT,
  WIDE_ZONE_PAGES,
  ZONE_PAGES,
  dashProperties,
  declaredProperties,
  moduleSettingName,
  secondScreen,
  secondScreenProperties,
} from '../src/contract.ts';
import { validatePackage, type Dashboard, type Item, type TextItem } from '../src/generator.ts';
import { PROPERTY_PREFIX } from '../src/contract.ts';
import { MODULES } from '../src/modules/index.ts';
import { SCREEN_PACKAGES, buildScreenPackage, zoneDashboardName } from '../src/screens/index.ts';
import { itemsOf, propertiesIn } from '../src/walk.ts';
import { ds } from '../src/tokens.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const PACKAGES = SCREEN_PACKAGES.map((def) => ({ def, pkg: buildScreenPackage(def, OPTS) }));
const BRAND = /#00E5FF/i;

const faceOf = (item: TextItem): MeasuredFace => (item.font === 'Barlow' ? 'BarlowMedium' : 'BarlowCondensedSemiBold');

/** Width of what an item draws: its cells when monospaced, the measured advances otherwise. */
function drawnWidth(item: TextItem): number {
  const mono = item.monospace;
  if (!mono) return measureText(faceOf(item), item.text, item.fontSize);
  const specials = [...item.text].filter((c) => mono.specialChars?.includes(c) ?? false).length;
  return (item.text.length - specials) * mono.charWidth + specials * mono.specialCharsWidth;
}

const textsOf = (dashboard: Dashboard): TextItem[] => itemsOf(dashboard).filter((i): i is TextItem => i.kind === 'text');

describe('the packages are built and valid', () => {
  test('four packages: two companions and two pit walls', () => {
    expect(SCREEN_PACKAGES.map((d) => d.folder)).toEqual(['openDash Companion', 'openDash Companion portrait', 'openDash Pit wall', 'openDash Pit wall portrait']);
    expect(SCREEN_PACKAGES.filter((d) => d.kind === 'companion')).toHaveLength(2);
    expect(SCREEN_PACKAGES.filter((d) => d.kind === 'pitwall')).toHaveLength(2);
  });

  for (const { def, pkg } of PACKAGES) {
    test(`${def.folder} validates with no error or warning`, () => {
      const result = validatePackage(pkg, { declaredProperties: declaredProperties(), propertyPrefix: PROPERTY_PREFIX });
      expect(result.errors).toEqual([]);
      expect(result.warnings).toEqual([]);
    });

    test(`${def.folder} draws no brand colour`, () => {
      expect(JSON.stringify(pkg.dashboards)).not.toMatch(BRAND);
    });

    test(`${def.folder} fits every text in its box`, () => {
      for (const dashboard of pkg.dashboards) {
        for (const item of textsOf(dashboard)) {
          const width = drawnWidth(item);
          expect({ dashboard: dashboard.name, item: item.name, text: item.text, width, box: item.rect.width, fits: width <= item.rect.width }).toMatchObject({ fits: true });
          const line = LINE_SPACING * item.fontSize;
          expect({ item: item.name, line, box: item.rect.height, fits: line <= item.rect.height }).toMatchObject({ fits: true });
        }
      }
    });

    test(`${def.folder} keeps every item on its canvas`, () => {
      for (const dashboard of pkg.dashboards) {
        for (const item of itemsOf(dashboard)) {
          if (item.kind === 'layer') continue;
          const r = item.rect;
          const inside = r.left >= 0 && r.top >= 0 && r.left + r.width <= dashboard.width && r.top + r.height <= dashboard.height;
          expect({ dashboard: dashboard.name, item: item.name, rect: r, inside }).toMatchObject({ inside: true });
        }
      }
    });

    test(`${def.folder} names every item once per screen`, () => {
      for (const dashboard of pkg.dashboards) {
        for (const screen of dashboard.screens) {
          const names = itemsOf({ ...dashboard, screens: [screen] }).map((i) => i.name);
          expect({ screen: screen.name, duplicates: names.filter((n, i) => names.indexOf(n) !== i) }).toMatchObject({ duplicates: [] });
        }
      }
    });
  }
});

describe('the companion', () => {
  const companion = PACKAGES.find((p) => p.def.folder === 'openDash Companion')!;
  const main = companion.pkg.dashboards[0]!;

  test('has one screen per module, in catalogue order', () => {
    expect(main.screens).toHaveLength(MODULE_COUNT);
    expect(main.screens.map((s) => s.name)).toEqual(MODULE_CATALOGUE.map((m) => m.id));
    expect(MODULES.map((m) => m.id)).toEqual(MODULE_CATALOGUE.map((m) => m.id));
  });

  test('switches each screen on its own plugin setting', () => {
    main.screens.forEach((screen, i) => {
      expect(screen.enabledExpression).toBe(secondScreen.moduleEnabled(i + 1));
      expect(screen.enabledExpression).toContain(moduleSettingName(i + 1));
    });
    // Energy, damage and track rivals default to off, which is a 0 in the expression.
    expect(main.screens[5]!.enabledExpression).toContain(', 0)');
    expect(main.screens[0]!.enabledExpression).toContain(', 1)');
  });

  test('gives every screen the same roles, so the page ring works in and out of a session', () => {
    // SimHub only filters screens by role when the roles differ between them; identical roles keep
    // every enabled screen in the Next/Previous ring whatever the game is doing.
    const roles = new Set(main.screens.map((s) => `${s.inGame};${s.idle};${s.pit}`));
    expect([...roles]).toEqual(['true;true;false']);
  });

  test('draws the header, the dots and the flag band on every screen', () => {
    for (const screen of main.screens) {
      const names = itemsOf({ ...main, screens: [screen] }).map((i) => i.name);
      expect(names.some((n) => n.includes('header.module'))).toBe(true);
      expect(names.filter((n) => n.includes('.dots.dot'))).toHaveLength(MODULE_COUNT);
      expect(names.some((n) => n.includes('flag.yellow'))).toBe(true);
    }
  });

  test('the portrait package draws the same modules in a taller box', () => {
    const portrait = PACKAGES.find((p) => p.def.folder === 'openDash Companion portrait')!.pkg.dashboards[0]!;
    expect(portrait.width).toBe(480);
    expect(portrait.height).toBe(850);
    expect(portrait.screens.map((s) => s.name)).toEqual(main.screens.map((s) => s.name));
  });
});

describe('the pit wall', () => {
  const landscape = PACKAGES.find((p) => p.def.folder === 'openDash Pit wall')!;
  const main = landscape.pkg.dashboards[0]!;

  test('has three pages', () => {
    expect(main.screens.map((s) => s.name)).toEqual(['race', 'tower', 'telemetry']);
  });

  test('carries a zone dashboard for every rectangle its pages embed', () => {
    const widgets = itemsOf(main).filter((i) => i.kind === 'widget');
    expect(widgets.length).toBeGreaterThan(0);
    const files = new Set(widgets.map((w) => (w.kind === 'widget' ? w.fileName : '')));
    const built = new Set(landscape.pkg.dashboards.slice(1).map((d) => `${d.name}.djson`));
    expect([...files].sort()).toEqual([...built].sort());
    for (const widget of widgets) {
      if (widget.kind !== 'widget') continue;
      const kind = widget.fileName.startsWith('zones-wide') ? 'wide' : 'standard';
      expect(widget.fileName).toBe(`${zoneDashboardName(kind, { width: widget.rect.width, height: widget.rect.height })}.djson`);
    }
  });

  test('binds every zone to its own plugin setting', () => {
    const bound = itemsOf(main)
      .filter((i) => i.kind === 'widget')
      .map((i) => i.bindings?.InitialScreenIndex?.formula)
      .filter((f): f is string => typeof f === 'string');
    expect(bound).toContain(secondScreen.zonePage('A'));
    expect(bound).toContain(secondScreen.zonePage('B'));
    expect(bound).toContain(secondScreen.zonePage('C'));
    expect(bound).toContain(secondScreen.zonePage('D'));
    expect(bound).toContain(secondScreen.wideZonePage());
  });

  test('a zone dashboard holds every page of its kind, in the order the plugin lists them', () => {
    for (const dashboard of landscape.pkg.dashboards.slice(1)) {
      const pages = dashboard.name.startsWith('zones-wide') ? WIDE_ZONE_PAGES : ZONE_PAGES;
      expect(dashboard.screens.map((s) => s.name)).toEqual(pages.map((p) => p.id));
    }
  });

  test('the portrait page is one screen with four zones', () => {
    const portrait = PACKAGES.find((p) => p.def.folder === 'openDash Pit wall portrait')!.pkg.dashboards[0]!;
    expect(portrait.screens.map((s) => s.name)).toEqual(['portrait']);
    expect(itemsOf(portrait).filter((i) => i.kind === 'widget')).toHaveLength(4);
  });
});

describe('the tables', () => {
  const all: Item[] = PACKAGES.flatMap((p) => p.pkg.dashboards.flatMap((d) => itemsOf(d)));
  const repeated = all.filter((i) => i.kind === 'layer' && (i.repetitions ?? 0) > 0);

  test('stamp their rows from one row definition', () => {
    expect(repeated.length).toBeGreaterThan(0);
    for (const layer of repeated) {
      if (layer.kind !== 'layer') continue;
      expect({ name: layer.name, pitch: layer.repeatTopOffset }).toMatchObject({ pitch: expect.any(Number) });
      expect(layer.repeatTopOffset).toBeGreaterThan(0);
      expect(layer.repeatLeftOffset).toBe(0);
      // SimHub drops widgets from the copies it stamps, so a repeated row must not hold one.
      expect(itemsOf({ screens: [{ name: 'x', items: layer.children }] } as Dashboard).some((i) => i.kind === 'widget')).toBe(false);
    }
  });

  test('hide a row that has no car, from inside the repeat context', () => {
    for (const layer of repeated) {
      if (layer.kind !== 'layer') continue;
      // The repeated layer itself is evaluated once, so the per-row test lives on its child.
      expect(layer.bindings?.Visible).toBeUndefined();
      const child = layer.children[0];
      expect(child?.kind).toBe('layer');
      expect(typeof child?.bindings?.Visible?.formula).toBe('string');
    }
  });
});

describe('the contract', () => {
  test('every property the second screens read is declared', () => {
    const declared = new Set(declaredProperties());
    for (const { pkg } of PACKAGES) {
      for (const dashboard of pkg.dashboards) {
        for (const p of propertiesIn(dashboard).filter((n) => n.startsWith('OpenDash.'))) {
          expect({ p, declared: declared.has(p) }).toEqual({ p, declared: true });
        }
      }
    }
  });

  test('the second screens read every second-screen property, and the face reads none of them', () => {
    const used = new Set<string>();
    for (const { pkg } of PACKAGES) {
      for (const dashboard of pkg.dashboards) {
        for (const screen of dashboard.screens) if (screen.enabledExpression) used.add(screen.enabledExpression);
        for (const p of propertiesIn(dashboard)) used.add(p);
      }
    }
    const text = [...used].join(' ');
    for (const p of secondScreenProperties()) expect({ p, read: text.includes(p) }).toEqual({ p, read: true });
    // The dash settings are shared: the second screens read the modes too, but never a slot.
    expect(text).toContain('OpenDash.PositionMode');
    for (const p of dashProperties().filter((n) => n.includes('.Slot'))) expect(text).not.toContain(p);
  });
});

describe('what iRacing cannot answer', () => {
  test('the three unavailable modules say so instead of drawing zeros', () => {
    const companion = PACKAGES[0]!.pkg.dashboards[0]!;
    for (const id of ['energy', 'damage', 'trackRivals']) {
      const screen = companion.screens.find((s) => s.name === id)!;
      const texts = itemsOf({ ...companion, screens: [screen] }).filter((i): i is TextItem => i.kind === 'text');
      const placeholder = texts.find((t) => t.name.endsWith('.placeholder'));
      expect({ id, text: placeholder?.text }).toMatchObject({ text: expect.stringContaining('NOT A') });
      expect(placeholder?.textColor).toBe(ds.color.text.dim);
    }
  });
});
