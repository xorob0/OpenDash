/** contract.ts: the card catalogue, the property list and the isnull-wrapped setting reads. */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import {
  BAR_SLOTS,
  FACE_ZONE_LETTERS,
  CARD_CATALOGUE,
  cardMeta,
  declaredProperties,
  PIT_WALL_DEFAULT_WIDE_ZONE_PAGE,
  PIT_WALL_DEFAULT_ZONE_PAGES,
  MODULE_CATALOGUE,
  MODULE_COUNT,
  moduleAt,
  moduleMeta,
  moduleSettingName,
  secondScreen,
  PIT_WALL_WIDE_ZONE_PAGES,
  PIT_WALL_ZONE_LETTERS,
  PIT_WALL_ZONE_PAGES,
  pitWallZoneSettingName,
  defaultCardForSlot,
  DEFAULT_SLOT_CARDS,
  DEFAULTS,
  DELTA_REFERENCES,
  POSITION_MODES,
  PROPERTY_PREFIX,
  SESSION_PROGRESS_MODES,
  setting,
  SLOT_MAX,
  slotSettingName,
} from '../src/contract.ts';
import { CARDS } from '../src/cards/index.ts';

describe('card catalogue', () => {
  test('has 13 entries numbered 0..12 in order with unique ids', () => {
    expect(CARD_CATALOGUE).toHaveLength(13);
    CARD_CATALOGUE.forEach((c, i) => expect(c.number).toBe(i));
    expect(new Set(CARD_CATALOGUE.map((c) => c.id)).size).toBe(13);
    expect(CARD_CATALOGUE.map((c) => c.id)).toEqual([
      'currentLap', 'lastLap', 'bestLap', 'delta', 'position', 'session', 'fuel', 'fuelLaps', 'tc', 'abs', 'tyreTemps', 'tyrePressures', 'speed',
    ]);
  });

  test('the card modules follow the catalogue', () => {
    expect(CARDS.map((c) => c.id)).toEqual(CARD_CATALOGUE.map((c) => c.id));
    CARDS.forEach((c, i) => expect(c).toMatchObject(CARD_CATALOGUE[i]!));
    expect(() => cardMeta('nope')).toThrow();
  });
});

describe('settings', () => {
  test('declares the dash, the zones, the companion and the pit wall', () => {
    const props = declaredProperties();
    // Four per zone -- page, mask, start and the class filter -- plus the bar's ends and the glance.
    const zoneCount = FACE_ZONE_LETTERS.length * 4 + BAR_SLOTS.length + 1;
    expect(props).toHaveLength(4 + SLOT_MAX + zoneCount + MODULE_COUNT + PIT_WALL_ZONE_LETTERS.length + 2);
    expect(new Set(props).size).toBe(props.length);
    expect(props.slice(0, 4)).toEqual(['OpenDash.ShiftLights', 'OpenDash.PositionMode', 'OpenDash.DeltaReference', 'OpenDash.SessionProgress']);
    expect(props[4]).toBe('OpenDash.Slot01');
    expect(props[15]).toBe('OpenDash.Slot12');
    // The zones are declared here and read by the face from XOR-85. Slot01 to Slot12 stay beside
    // them until the card path is retired, because ten faces still read them.
    expect(props).toContain('OpenDash.ZoneA');
    expect(props).toContain('OpenDash.ZoneDPages');
    expect(props).toContain('OpenDash.ZoneCStart');
    expect(props).toContain('OpenDash.ZoneBClassOnly');
    expect(props).toContain('OpenDash.BarLeft1');
    expect(props).toContain('OpenDash.QuickGlance');
    expect(props.slice(-6)).toEqual(['OpenDash.PitWallZoneA', 'OpenDash.PitWallZoneB', 'OpenDash.PitWallZoneC', 'OpenDash.PitWallZoneD', 'OpenDash.PitWallWide', 'OpenDash.WebViewUrl']);
  });

  test('slot names and defaults', () => {
    expect(slotSettingName(1)).toBe('Slot01');
    expect(slotSettingName(12)).toBe('Slot12');
    // Speed leads so that even a two-slot round face shows it beside the gear.
    expect(defaultCardForSlot(1)).toBe(12);
    expect(defaultCardForSlot(12)).toBe(10);
    expect(DEFAULT_SLOT_CARDS).toEqual([12, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    // Tyre pressures is the one card no slot shows by default.
    expect(DEFAULT_SLOT_CARDS).not.toContain(11);
    expect(() => slotSettingName(0)).toThrow(RangeError);
    expect(() => slotSettingName(13)).toThrow(RangeError);
  });

  test('every read falls back to the default without the plugin', () => {
    expect(setting.shiftLights()).toBe('isnull([OpenDash.ShiftLights], true)');
    expect(setting.positionMode()).toBe("isnull([OpenDash.PositionMode], 'overall')");
    expect(setting.deltaReference()).toBe("isnull([OpenDash.DeltaReference], 'session')");
    expect(setting.sessionProgress()).toBe("isnull([OpenDash.SessionProgress], 'auto')");
    expect(setting.slot(1)).toBe('isnull([OpenDash.Slot01], 12)');
    expect(setting.slot(7)).toBe('isnull([OpenDash.Slot07], 5)');
  });
});

/** The plugin sources mirror contract.ts; a missing file fails here rather than skipping. */
const pluginSource = (file: string): string => readFileSync(path.resolve(import.meta.dir, '../../../plugin/OpenDash', file), 'utf8');
const csArray = (values: readonly string[]): string => `{ ${values.map((v) => `"${v}"`).join(', ')} }`;

describe('plugin mirror', () => {
  test('Cards.cs lists the catalogue: number, id, label and display name, in order', () => {
    const source = pluginSource('Cards.cs');
    const cards = [...source.matchAll(/new Card\((\d+), "([^"]*)", "([^"]*)", "([^"]*)", "[^"]*"\)/g)].map((m) => ({
      number: Number(m[1]),
      id: m[2],
      label: m[3],
      displayName: m[4],
    }));
    expect(cards).toEqual(CARD_CATALOGUE.map(({ number, id, label, displayName }) => ({ number, id, label, displayName })));
    expect(source).toContain(`public const int Count = ${CARD_CATALOGUE.length};`);
  });

  test('Contract.cs carries the prefix, the slot count, the property names, the value sets and the defaults', () => {
    const source = pluginSource('Contract.cs');
    expect(source).toContain(`public const string Prefix = "${PROPERTY_PREFIX}";`);
    expect(source).toContain(`public const int SlotCount = ${SLOT_MAX};`);
    for (const name of ['ShiftLights', 'PositionMode', 'DeltaReference', 'SessionProgress']) expect(source).toContain(`public const string ${name} = "${name}";`);
    expect(source).toContain(`PositionModes = ${csArray(POSITION_MODES)};`);
    expect(source).toContain(`DeltaReferences = ${csArray(DELTA_REFERENCES)};`);
    expect(source).toContain(`SessionProgressModes = ${csArray(SESSION_PROGRESS_MODES)};`);
    expect(source).toContain(`DefaultShiftLights = ${String(DEFAULTS.ShiftLights)};`);
    expect(source).toContain(`DefaultPositionMode = "${DEFAULTS.PositionMode}";`);
    expect(source).toContain(`DefaultDeltaReference = "${DEFAULTS.DeltaReference}";`);
    expect(source).toContain(`DefaultSessionProgress = "${DEFAULTS.SessionProgress}";`);
  });
});


describe('the second screens', () => {
  test('21 modules, numbered in page order, three of them off', () => {
    expect(MODULE_CATALOGUE).toHaveLength(21);
    expect(MODULE_COUNT).toBe(21);
    MODULE_CATALOGUE.forEach((m, i) => expect(m.number).toBe(i + 1));
    expect(new Set(MODULE_CATALOGUE.map((m) => m.id)).size).toBe(21);
    // Virtual energy, damage and segment rivals: the three iRacing cannot fill.
    expect(MODULE_CATALOGUE.filter((m) => !m.enabled).map((m) => m.id)).toEqual(['energy', 'damage', 'trackRivals']);
    // Module 17 is the gear alone, as the hero is: a module shows one thing.
    expect(moduleAt(17)).toMatchObject({ id: 'gear', name: 'Gear' });
    expect(moduleMeta('gear').number).toBe(17);
    expect(() => moduleAt(0)).toThrow(RangeError);
    expect(() => moduleAt(22)).toThrow(RangeError);
    expect(() => moduleMeta('nope')).toThrow();
  });

  test('module settings are two-digit and zero-padded', () => {
    expect(moduleSettingName(1)).toBe('CompanionModule01');
    expect(moduleSettingName(21)).toBe('CompanionModule21');
    expect(pitWallZoneSettingName('A')).toBe('PitWallZoneA');
  });

  test('every zone page names a module, save the web view', () => {
    expect(PIT_WALL_ZONE_PAGES).toHaveLength(11);
    expect(PIT_WALL_WIDE_ZONE_PAGES).toHaveLength(6);
    PIT_WALL_ZONE_PAGES.forEach((p, i) => expect(p.number).toBe(i));
    PIT_WALL_WIDE_ZONE_PAGES.forEach((p, i) => expect(p.number).toBe(i));
    const ids = new Set(MODULE_CATALOGUE.map((m) => m.id));
    for (const page of PIT_WALL_ZONE_PAGES) expect(page.id === 'web' || ids.has(page.id)).toBe(true);
    // The wide car-telemetry page is the one that is not a module: a trace beside the settings grid.
    for (const page of PIT_WALL_WIDE_ZONE_PAGES) expect(page.id === 'web' || page.id === 'carTelemetry' || ids.has(page.id)).toBe(true);
  });

  test('the defaults name real pages', () => {
    for (const letter of PIT_WALL_ZONE_LETTERS) expect(PIT_WALL_ZONE_PAGES[PIT_WALL_DEFAULT_ZONE_PAGES[letter]]).toBeDefined();
    expect(PIT_WALL_ZONE_PAGES[PIT_WALL_DEFAULT_ZONE_PAGES.A]?.id).toBe('fuel');
    expect(PIT_WALL_ZONE_PAGES[PIT_WALL_DEFAULT_ZONE_PAGES.C]?.id).toBe('relative');
    expect(PIT_WALL_WIDE_ZONE_PAGES[PIT_WALL_DEFAULT_WIDE_ZONE_PAGE]?.id).toBe('carTelemetry');
  });

  test('a module reads as a number, so SimHub can treat it as a screen switch', () => {
    // SimHub enables a screen when its expression evaluates above zero; a boolean converts to 1.
    expect(secondScreen.moduleEnabled(1)).toBe('isnull([OpenDash.CompanionModule01], 1)');
    expect(secondScreen.moduleEnabled(6)).toBe('isnull([OpenDash.CompanionModule06], 0)');
    expect(secondScreen.zonePage('C')).toBe('isnull([OpenDash.PitWallZoneC], 4)');
    expect(secondScreen.wideZonePage()).toBe('isnull([OpenDash.PitWallWide], 5)');
    expect(secondScreen.webViewUrl()).toBe("isnull([OpenDash.WebViewUrl], '')");
  });
});
