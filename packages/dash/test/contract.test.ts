/** contract.ts: the card catalogue, the property list and the isnull-wrapped setting reads. */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import {
  BAR_SLOTS,
  FACE_SIZES,
  FACE_ZONE_LETTERS,
  facePrefix,
  facePropertyNames,
  faceForPrefix,
  CARD_CATALOGUE,
  cardMeta,
  declaredProperties,
  flagBoxProperties,
  FLAG_BOX_MATRICES,
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
  COMPANION_PREFIX,
  PIT_WALL_PREFIX,
  foreignProperties,
  screenPrefixes,
  screenProperties,
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
    // Per face, not per rig: every face that ships carries its own group, so a 1920 face and an
    // 850 face beside it are configured apart instead of sharing one set of zones.
    const perFace = FACE_ZONE_LETTERS.length * 3 + BAR_SLOTS.length + 1;
    // The last term is the flag box, which is not a screen but whose settings are properties for
    // the same reason: ADR 0003, and ADR 0013 for why the box is here at all. Eight global and
    // five per matrix, the way every face carries its own group.
    expect(flagBoxProperties()).toHaveLength(8 + FLAG_BOX_MATRICES.length * 5);
    expect(props).toHaveLength(4 + SLOT_MAX + FACE_SIZES.length * perFace + MODULE_COUNT + PIT_WALL_ZONE_LETTERS.length + 2 + flagBoxProperties().length);
    expect(new Set(props).size).toBe(props.length);
    expect(props.slice(0, 4)).toEqual(['OpenDash.ShiftLights', 'OpenDash.PositionMode', 'OpenDash.DeltaReference', 'OpenDash.SessionProgress']);
    expect(props[4]).toBe('OpenDash.Slot01');
    expect(props[15]).toBe('OpenDash.Slot12');
    // The zones are declared here and read by the face from XOR-85. Slot01 to Slot12 stay beside
    // them until the card path is retired, because ten faces still read them.
    expect(props).toContain('OpenDash.Face1920x480ZoneA');
    expect(props).toContain('OpenDash.Face1920x480ZoneDPages');
    expect(props).toContain('OpenDash.Face850x480ZoneCStart');
    expect(props).toContain('OpenDash.Face600x686BarLeft1');
    expect(props).toContain('OpenDash.Face800x286QuickGlance');
    // And nothing without a prefix, which is the promise: a bare ZoneA would be one face's
    // settings silently shared with every other.
    expect(props.filter((p) => /^OpenDash\.(Zone|Bar|QuickGlance)/.test(p))).toEqual([]);
    expect(props.slice(-(flagBoxProperties().length + 6), -flagBoxProperties().length)).toEqual(['OpenDash.PitWallZoneA', 'OpenDash.PitWallZoneB', 'OpenDash.PitWallZoneC', 'OpenDash.PitWallZoneD', 'OpenDash.PitWallWide', 'OpenDash.WebViewUrl']);
    // The flag box comes last, after the screens, because it is the one artefact the plugin does
    // not install; see ADR 0013.
    expect(props.slice(-flagBoxProperties().length)).toEqual(flagBoxProperties());
  });

  test('every face that ships has a group, and every group is complete', () => {
    const props = new Set(declaredProperties());
    for (const face of FACE_SIZES) {
      for (const name of facePropertyNames(face)) expect({ face: facePrefix(face), name, declared: props.has(`OpenDash.${name}`) }).toMatchObject({ declared: true });
    }
    // The prefix is concatenated and carries no dot of its own, because SimHub puts one in front
    // of every name and whether its parser accepts a second inside the name is unverified.
    for (const face of FACE_SIZES) expect(facePrefix(face)).toMatch(/^Face\d+x\d+$/);
    expect(faceForPrefix('Face1920x480')).toMatchObject({ width: 1920, height: 480 });
    expect(faceForPrefix('Face1x1')).toBeUndefined();
  });

  test('every property belongs to one screen or to every screen', () => {
    // The rule the build enforces is a partition of the contract: a name is one screen's or it is
    // shared by all of them, and never both. Without that a package could be told it may read
    // something no screen owns, or be denied one of its own.
    const declared = declaredProperties();
    const owned = screenPrefixes().flatMap(screenProperties);
    expect(new Set(owned).size).toBe(owned.length);
    for (const name of owned) expect({ name, declared: declared.includes(name) }).toMatchObject({ declared: true });

    // What is left over is what a rig shares and what its lights read. The four modes and the
    // twelve slots mean the same thing on the wheel, on the rim and on the pit wall, so they carry
    // no screen's name; the flag box's settings belong to no screen either, because a matrix is not
    // one. Three parts of one partition rather than two parts and an exception.
    const lights = flagBoxProperties();
    for (const name of lights) expect({ name, owned: owned.includes(name) }).toMatchObject({ owned: false });
    const shared = declared.filter((name) => !owned.includes(name) && !lights.includes(name));
    const fixed = ['ShiftLights', 'PositionMode', 'DeltaReference', 'SessionProgress'];
    expect(shared).toEqual([...fixed, ...Array.from({ length: SLOT_MAX }, (_, i) => slotSettingName(i + 1))].map((n) => `${PROPERTY_PREFIX}.${n}`));

    // The web view address is the pit wall's although its name carries no prefix: it was named
    // before the idiom, and no other screen has a browser page to point anywhere.
    expect(screenProperties(PIT_WALL_PREFIX)).toContain('OpenDash.WebViewUrl');
    expect(screenProperties(COMPANION_PREFIX)).toHaveLength(MODULE_COUNT);

    const own = facePrefix(FACE_SIZES[0]!);
    expect(foreignProperties(own)).not.toContain('OpenDash.Face1920x480ZoneA');
    expect(foreignProperties(own)).toContain('OpenDash.Face850x480ZoneA');
    expect(foreignProperties(own)).toContain('OpenDash.PitWallZoneA');
    // A card face owns nothing and every group is foreign to it.
    expect(foreignProperties()).toHaveLength(owned.length);
    expect(() => screenProperties('Face1x1')).toThrow(RangeError);
    expect(() => foreignProperties('Face1x1')).toThrow(RangeError);
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
