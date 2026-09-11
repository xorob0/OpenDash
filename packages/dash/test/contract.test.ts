/** contract.ts: the card catalogue, the property list and the isnull-wrapped setting reads. */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import {
  CARD_CATALOGUE,
  cardMeta,
  declaredProperties,
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
  test('declares the 16 properties', () => {
    const props = declaredProperties();
    expect(props).toHaveLength(4 + SLOT_MAX);
    expect(props.slice(0, 4)).toEqual(['OpenDash.ShiftLights', 'OpenDash.PositionMode', 'OpenDash.DeltaReference', 'OpenDash.SessionProgress']);
    expect(props[4]).toBe('OpenDash.Slot01');
    expect(props[15]).toBe('OpenDash.Slot12');
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
