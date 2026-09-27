/** buildLayout: the two dashboards, the contract, the slot strategies, and nothing of the brand on the face. */
import { describe, expect, test } from 'bun:test';
import { existsSync } from 'node:fs';
import { buildLayout, buildPackage, fontsForPackage } from '../src/dashboard.ts';
import { CARD_CATALOGUE, CAR_LADDER_CHOSEN, CAR_LADDER_FLASHES, CAR_LADDER_LAMPS, CAR_LADDER_LIT, CAR_LADDER_OVER_REV,
  CAR_LADDER_STAGE, CAR_LADDER_TOP_RPM, dashProperties, DRIVER_NAME_FORMAT_SETTING, DRIVER_NAME_TEAM_SETTING,
  PROPERTY_PREFIX, zoneProperties, declaredProperties, defaultCardForSlot, secondScreenProperties } from '../src/contract.ts';
import { contains, rect } from '../src/design/geometry.ts';
import { IDLE_SCREEN_NAME } from '../src/idle.ts';
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
  test('is the layout size, with the racing face in front and the idle screen behind it', () => {
    expect(main.name).toBe('OpenDash slots 1920x480');
    expect(main.width).toBe(1920);
    expect(main.height).toBe(480);
    expect(main.backgroundColor).toBe('#0A0B0D');
    // Two screens since #763: the face, which is no longer the idle screen, and the idle screen,
    // which is last so that SimHub still previews the face.
    expect(main.screens).toHaveLength(2);
    expect(main.screens[0]).toMatchObject({ name: 'Main', inGame: true, idle: false, pit: true });
    expect(main.screens[1]).toMatchObject({ name: IDLE_SCREEN_NAME, inGame: false, idle: true, pit: false });
    expect(main.metadata).toMatchObject({ title: 'OpenDash slots 1920x480', author: 'OpenDash contributors', version: '0.0.0-test', simHubVersion: '9.12.6' });
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

  test('carries the hero: three rev bar layers, the gear alone, six flags, the pit limiter', () => {
    const names = main.screens[0]!.items.map((i) => i.name);
    expect(names.filter((n) => n.startsWith('hero.'))).toEqual(['hero.gear']);
    for (const n of ['revBar.shiftLightsCar', 'revBar.shiftLights', 'revBar.shiftLightsSimHub', 'revBar.rpmBar', 'hero.gear', 'pitLimiter', 'flag.black', 'flag.chequered', 'flag.yellow', 'flag.blue', 'flag.white', 'flag.green']) {
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
    // The card face reads the four modes and the twelve slots, the rev bar's own six, and nothing
    // else. The zone properties are declared beside them and are read by the zone face from #136; the
    // module switches and the pit wall's zone pages belong to the second screens.
    //
    // The seven belong to the lights rather than to a screen, and that is deliberate (#353): six of
    // them are one frame of the car's own measured bar, which the plugin computes once for every
    // surface that draws it, and the seventh is the rig's own answer to whether a surface should draw
    // it -- a reduction over the strips, published because the style is per strip and a screen has no
    // strip. A screen reading those is not a screen reading another screen's settings, which is what
    // `foreignProperties` is about and what this assertion is here to keep true.
    //
    // The two driver-name settings are shared and are not in the list, for a third reason: a card
    // face names nobody. No card lists other cars, so nothing on it asks how a driver is written,
    // where the zone face's leaderboard, relative and opponents pages all do. A shared property a
    // screen *may* read is not one it has to.
    const all = new Set([...propertiesIn(main), ...propertiesIn(cards)].filter((p) => p.startsWith('OpenDash.')));
    const unread = new Set([...zoneProperties(), `${PROPERTY_PREFIX}.${DRIVER_NAME_FORMAT_SETTING}`, `${PROPERTY_PREFIX}.${DRIVER_NAME_TEAM_SETTING}`]);
    const zoneProps = new Set(zoneProperties());
    const carBar = [CAR_LADDER_STAGE, CAR_LADDER_OVER_REV, CAR_LADDER_LIT, CAR_LADDER_LAMPS, CAR_LADDER_FLASHES, CAR_LADDER_CHOSEN].map((n) => `${PROPERTY_PREFIX}.${n}`);
    expect([...all].sort()).toEqual([...[...dashProperties()].filter((p) => !unread.has(p)), ...carBar].sort());
    // The last of the group, `CarLadderTopRpm`, is deliberately not here: it is the number printed
    // beside a bar rather than anything the bar itself needs, and the only page that prints one is the
    // companion's speedo. It is declared all the same, as the six above are.
    for (const p of [...carBar, `${PROPERTY_PREFIX}.${CAR_LADDER_TOP_RPM}`]) expect({ p, declared: declaredProperties().includes(p) }).toEqual({ p, declared: true });
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
  test('bundles only the face fonts, all present on disk', () => {
    // Three of them are the vendored condensed files renamed: the family a .djson asks for is
    // openDash Display, and a file called BarlowCondensed-Bold.ttf no longer carries that name.
    // Light is the idle screen's wordmark, which every face draws since #763.
    const fonts = fontsForPackage();
    expect(fonts.map((f) => f.split('/').pop())).toEqual([
      'openDashDisplay-SemiBold.ttf',
      'openDashDisplay-Bold.ttf',
      'openDashDisplay-Light.ttf',
      'Barlow-Medium.ttf',
      'Barlow-Bold.ttf',
    ]);
    for (const f of fonts) expect({ f, exists: existsSync(f) }).toEqual({ f, exists: true });
    const pkg = buildPackage(layout1920x480, opts);
    expect(pkg.folderName).toBe('OpenDash slots 1920x480');
    expect(pkg.dashboards.map((d) => d.name)).toEqual(['OpenDash slots 1920x480', 'cards']);
    expect(pkg.dashboards[0]!.name).toBe(pkg.folderName);
    expect(buildPackage(layout1920x480, { ...opts, strategy: 'inline' }).dashboards.map((d) => d.name)).toEqual(['OpenDash slots 1920x480']);
  });
});
