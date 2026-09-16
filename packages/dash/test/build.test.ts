/** buildLayout: the two dashboards, the contract, the slot strategies, and nothing of the brand on the face. */
import { describe, expect, test } from 'bun:test';
import { existsSync } from 'node:fs';
import { buildLayout, buildPackage, fontsForPackage } from '../src/dashboard.ts';
import { CARD_CATALOGUE, dashProperties,
  zoneProperties, declaredProperties, defaultCardForSlot, secondScreenProperties } from '../src/contract.ts';
import { contains, rect } from '../src/design/geometry.ts';
import { layout1920x480 } from '../src/layouts/1920x480.ts';
import { CARDS_FILE } from '../src/slots.ts';
import { itemsOf, propertiesIn, walkItems } from '../src/walk.ts';
import type { Dashboard } from '../src/generator.ts';

const opts = { version: '0.0.0-test' };
const { main, cards } = buildLayout(layout1920x480, opts);
const inline = buildLayout(layout1920x480, { ...opts, strategy: 'inline' }).main;
const BRAND = /#(33D9F2|5CE1F5|22909F)/i;

const uniqueNamesPerScreen = (d: Dashboard): void => {
  for (const screen of d.screens) {
    const names = [...walkItems(screen.items)].map((i) => i.name);
    expect(new Set(names).size).toBe(names.length);
  }
};

describe('main dashboard', () => {
  test('is the layout size with one screen for every role', () => {
    expect(main.name).toBe('openDash slots 1920x480');
    expect(main.width).toBe(1920);
    expect(main.height).toBe(480);
    expect(main.backgroundColor).toBe('#0A0B0D');
    expect(main.screens).toHaveLength(1);
    expect(main.screens[0]).toMatchObject({ name: 'Main', inGame: true, idle: true, pit: true });
    expect(main.metadata).toMatchObject({ title: 'openDash slots 1920x480', author: 'OpenDash contributors', version: '0.0.0-test', simHubVersion: '9.12.6' });
  });

  test('has one widget per slot bound to its slot setting', () => {
    const widgets = main.screens[0]!.items.filter((i) => i.kind === 'widget');
    expect(widgets).toHaveLength(12);
    widgets.forEach((w, i) => {
      if (w.kind !== 'widget') return;
      expect(w.name).toBe(`Slot${String(i + 1).padStart(2, '0')}`);
      expect(w.fileName).toBe(CARDS_FILE);
      expect(w.rect).toEqual(layout1920x480.slots[i]!);
      expect(w.initialScreenIndex).toBe(defaultCardForSlot(i + 1));
      expect(w.bindings?.InitialScreenIndex).toEqual({ mode: 'formula', formula: `isnull([OpenDash.Slot${String(i + 1).padStart(2, '0')}], ${defaultCardForSlot(i + 1)})` });
    });
  });

  test('every item lies inside the canvas and names are unique', () => {
    const canvas = rect(0, 0, 1920, 480);
    for (const item of itemsOf(main)) if ('rect' in item) expect({ name: item.name, inside: contains(canvas, item.rect) }).toEqual({ name: item.name, inside: true });
    uniqueNamesPerScreen(main);
  });

  test('carries the hero: two rev bar layers, the gear alone, six flags, the pit limiter', () => {
    const names = main.screens[0]!.items.map((i) => i.name);
    expect(names.filter((n) => n.startsWith('hero.'))).toEqual(['hero.gear']);
    for (const n of ['revBar.shiftLights', 'revBar.shiftLightsSimHub', 'revBar.rpmBar', 'hero.gear', 'pitLimiter', 'flag.black', 'flag.chequered', 'flag.yellow', 'flag.blue', 'flag.white', 'flag.green']) {
      expect(names).toContain(n);
    }
  });
});

describe('cards dashboard', () => {
  test('is slot sized with one screen per card in catalogue order', () => {
    expect(cards.name).toBe('cards');
    expect(`${cards.name}.djson`).toBe(CARDS_FILE);
    expect(cards.width).toBe(255);
    expect(cards.height).toBe(187);
    expect(cards.screens.map((s) => s.name)).toEqual(CARD_CATALOGUE.map((c) => c.id));
    for (const s of cards.screens) {
      expect(s).toMatchObject({ inGame: true, idle: true, pit: true });
      expect(s.items.length).toBeGreaterThan(0);
      for (const item of walkItems(s.items)) if ('rect' in item) expect(contains(rect(0, 0, 255, 187), item.rect)).toBe(true);
    }
    uniqueNamesPerScreen(cards);
  });
});

describe('contract', () => {
  test('every OpenDash property referenced by any expression is declared', () => {
    const declared = new Set(declaredProperties());
    for (const d of [main, cards, inline]) {
      const used = propertiesIn(d).filter((p) => p.startsWith('OpenDash.'));
      expect(used.length).toBeGreaterThan(0);
      for (const p of used) expect({ p, declared: declared.has(p) }).toEqual({ p, declared: true });
    }
    // The card face reads the four modes and the twelve slots, and nothing else. The zone properties are declared
    // beside them and are read by the zone face from #136; the module switches and the pit wall's zone pages
    // belong to the second screens.
    const all = new Set([...propertiesIn(main), ...propertiesIn(cards)].filter((p) => p.startsWith('OpenDash.')));
    const zoneProps = new Set(zoneProperties());
    expect([...all].sort()).toEqual([...dashProperties()].filter((p) => !zoneProps.has(p)).sort());
    for (const p of secondScreenProperties()) expect(all.has(p)).toBe(false);
    for (const p of zoneProps) expect(all.has(p)).toBe(false);
  });

  test('no brand colour reaches the face', () => {
    expect(JSON.stringify(main)).not.toMatch(BRAND);
    expect(JSON.stringify(cards)).not.toMatch(BRAND);
    expect(JSON.stringify(inline)).not.toMatch(BRAND);
  });
});

describe('inline strategy', () => {
  test('emits every card in every slot behind a visibility binding', () => {
    const layers = inline.screens[0]!.items.filter((i) => i.kind === 'layer' && i.name.startsWith('Slot'));
    expect(layers).toHaveLength(12);
    layers.forEach((slotLayer, i) => {
      if (slotLayer.kind !== 'layer') return;
      expect(slotLayer.children).toHaveLength(CARD_CATALOGUE.length);
      slotLayer.children.forEach((cardLayer, n) => {
        if (cardLayer.kind !== 'layer') throw new Error('card layer');
        expect(cardLayer.name).toBe(`${slotLayer.name}.${CARD_CATALOGUE[n]!.id}`);
        expect(cardLayer.bindings?.Visible).toEqual({ mode: 'formula', formula: `(isnull([OpenDash.Slot${String(i + 1).padStart(2, '0')}], ${defaultCardForSlot(i + 1)})) = (${n})` });
        for (const item of walkItems(cardLayer.children)) if ('rect' in item) expect(contains(layout1920x480.slots[i]!, item.rect)).toBe(true);
      });
    });
    expect(inline.screens[0]!.items.some((i) => i.kind === 'widget')).toBe(false);
    uniqueNamesPerScreen(inline);
  });
});

describe('package', () => {
  test('bundles only the three face fonts, all present on disk', () => {
    // Two of the three are the vendored condensed files renamed: the family a .djson asks for is
    // openDash Display, and a file called BarlowCondensed-Bold.ttf no longer carries that name.
    const fonts = fontsForPackage();
    expect(fonts.map((f) => f.split('/').pop())).toEqual(['openDashDisplay-SemiBold.ttf', 'openDashDisplay-Bold.ttf', 'Barlow-Medium.ttf']);
    for (const f of fonts) expect({ f, exists: existsSync(f) }).toEqual({ f, exists: true });
    const pkg = buildPackage(layout1920x480, opts);
    expect(pkg.folderName).toBe('openDash slots 1920x480');
    expect(pkg.dashboards.map((d) => d.name)).toEqual(['openDash slots 1920x480', 'cards']);
    expect(pkg.dashboards[0]!.name).toBe(pkg.folderName);
    expect(buildPackage(layout1920x480, { ...opts, strategy: 'inline' }).dashboards.map((d) => d.name)).toEqual(['openDash slots 1920x480']);
  });
});
