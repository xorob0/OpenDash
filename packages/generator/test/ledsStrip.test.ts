/**
 * The **strip** `.ledsprofile` model, serialiser and validator. The matrix half — the flag box —
 * is a different object model with its own tests in leds.test.ts. The key orders asserted here were read off
 * real profiles shipped with SimHub 9.12.6 and out of the decompiled `SimHub.Plugins.dll`; the
 * validator's rules each exist because the failure they catch is silent.
 */
import { describe, expect, test } from 'bun:test';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { leds, stableGuid } from '../src/index.ts';

const ID = stableGuid('test/leds/profile');

const profile = (containers: leds.LedContainer[], extra: Partial<leds.LedProfile> = {}): leds.LedProfile => ({
  name: 'openDash strip',
  profileId: ID,
  containers,
  ...extra,
});

describe('the profile object', () => {
  test('is the key order SimHub writes, with CarChoices always an array', () => {
    const o = leds.buildProfileObject(profile([{ kind: 'staticColor', ledCount: 3, color: '#00D96A' }]));
    expect(Object.keys(o)).toEqual(['CarChoices', 'LedContainers', 'Name', 'ProfileId']);
    expect(o.CarChoices).toEqual([]);
    expect(o.ProfileId).toBe(ID);
  });

  test('writes brightness and embedded javascript only when they are set', () => {
    expect(Object.keys(leds.buildProfileObject(profile([], { globalBrightness: 60, useProfileBrightness: true, embeddedJavascript: 'var x = 1;' })))).toEqual([
      'CarChoices',
      'EmbeddedJavascript',
      'GlobalBrightness',
      'LedContainers',
      'UseProfileBrightness',
      'Name',
      'ProfileId',
    ]);
  });

  test('serialises as two-space indented JSON, which is Formatting.Indented', () => {
    const text = leds.serializeProfile(profile([{ kind: 'staticColor', ledCount: 1, color: 'Red' }]));
    expect(text.startsWith('{\n  "CarChoices": []')).toBe(true);
    expect(text.endsWith('\n')).toBe(true);
    expect(JSON.parse(text)).toBeTruthy();
  });
});

describe('containers', () => {
  test('ContainerType is written for every kind, and is last', () => {
    const kinds: leds.LedContainer[] = [
      { kind: 'group', children: [] },
      { kind: 'conditionalGroup', trigger: { expression: '1' }, children: [] },
      { kind: 'remapGroup', positions: [3, 2, 1], children: [] },
      { kind: 'staticColor', ledCount: 1, color: 'Red' },
      { kind: 'customStatus', ledCount: 1, color: 'Red', enabledFormula: { expression: '1' } },
      { kind: 'dynamicColor', ledCount: 1, colorFormula: { expression: "'Red'" } },
      { kind: 'scriptedContent', ledCount: 1, contentFormula: { expression: 'x', interpreter: 'javascript' } },
      { kind: 'rpmSegments', rpmMode: 'rpms', segments: [{ ledCount: 1, startValue: 7000, color: 'Lime' }] },
      { kind: 'animation', rows: 8, columns: 8, frames: [{ pixels: [['Red']], durationMs: 500 }] },
    ];
    expect(kinds.map((c) => leds.buildContainerObject(c).ContainerType)).toEqual([
      'Base.Group',
      'Groups.CustomConditionalGroup',
      'Groups.RemapGroup',
      'StaticColor',
      'CustomStatus',
      'DynamicColor',
      'ScriptedContent',
      'RPMSegments',
      'Animation',
    ]);
    for (const c of kinds) expect(Object.keys(leds.buildContainerObject(c)).at(-1)).toBe('ContainerType');
    // Every one of them is a type SimHub 9.12.6's RGB driver actually resolves.
    for (const c of kinds) expect(leds.KNOWN_CONTAINER_TYPES.has(leds.containerTypeOf(c))).toBe(true);
  });

  test('StartPosition and IsEnabled are omitted at their SimHub defaults', () => {
    expect(leds.buildContainerObject({ kind: 'staticColor', ledCount: 1, color: 'Red', startPosition: 1 })).toEqual({ LedCount: 1, Color: 'Red', ContainerType: 'StaticColor' });
    const set = leds.buildContainerObject({ kind: 'staticColor', ledCount: 1, color: 'Red', startPosition: 7, enabled: false, description: 'right' });
    expect(Object.keys(set)).toEqual(['Description', 'StartPosition', 'IsEnabled', 'LedCount', 'Color', 'ContainerType']);
  });

  test('an expression writes Interpreter only for Javascript, and never JSExt', () => {
    expect(leds.buildExpressionObject({ expression: 'isnull([OpenDash.X], 0)' })).toEqual({ Expression: 'isnull([OpenDash.X], 0)' });
    expect(leds.buildExpressionObject({ expression: 'return []', interpreter: 'javascript' })).toEqual({ Interpreter: 1, Expression: 'return []' });
  });

  test('a segment is ledCount;startValue;color;useBlink;blinkColor', () => {
    expect(leds.buildSegmentString({ ledCount: 3, startValue: 70, color: 'Lime' })).toBe('3;70;Lime;0;');
    expect(leds.buildSegmentString({ ledCount: 3, startValue: 85.5, color: '#FF2D46', blinkColor: 'Blue' })).toBe('3;85.5;#FF2D46;1;Blue');
  });

  test('a frame is row,col,colour triples, and an undefined pixel is not written at all', () => {
    const frame: leds.LedFrame = { pixels: [['Red', undefined, '#112233'], [undefined], ['Black']], durationMs: 250 };
    expect(leds.buildFrameColors(frame)).toBe('0,0,Red;0,2,#112233;2,0,Black');
  });

  test('a blinking colour brings BlinkEnabled with it, because the pair is what SimHub reads', () => {
    expect(leds.buildContainerObject({ kind: 'staticColor', ledCount: 2, color: 'Red', blinkColor: 'Blue', blinkDelayMs: 125 })).toEqual({
      LedCount: 2,
      Color: 'Red',
      BlinkingColor: 'Blue',
      BlinkEnabled: true,
      BlinkDelay: 125,
      ContainerType: 'StaticColor',
    });
  });

  test('a dynamic colour is LedCount and a ColorFormula, and nothing else', () => {
    // SimHub's own blink fields are deliberately not modelled: a mirrored bar blinks at the car's
    // interval, which is a number the profile does not have and the plugin does (ADR 0018).
    const o = leds.buildContainerObject({ kind: 'dynamicColor', ledCount: 1, colorFormula: { expression: "isnull([OpenDash.X], 'Transparent')" }, description: 'led 03' });
    expect(o).toEqual({
      Description: 'led 03',
      LedCount: 1,
      ColorFormula: { Expression: "isnull([OpenDash.X], 'Transparent')" },
      ContainerType: 'DynamicColor',
    });
  });

  test('a ColorFormula is validated like every other expression', () => {
    // It is the door the per-car mirror comes through, so an undeclared property in it has to fail
    // here rather than resolve to Color.Black on somebody's wheel.
    const opts = { ledCount: 4, propertyPrefix: 'OpenDash', declaredProperties: ['OpenDash.ShiftLights'] };
    const bad = leds.validateProfile(profile([{ kind: 'dynamicColor', ledCount: 1, colorFormula: { expression: 'isnull([OpenDash.Nope], 0)' } }]), opts);
    expect(bad.errors.map((e) => e.code)).toEqual(['leds/property-undeclared']);
    const good = leds.validateProfile(profile([{ kind: 'dynamicColor', ledCount: 1, colorFormula: { expression: "isnull([OpenDash.ShiftLights], 'Transparent')" } }]), opts);
    expect(good.ok).toBe(true);
  });

  test('a remap group writes LedPosition objects, padded to 64, and nests its children', () => {
    const o = leds.buildContainerObject({ kind: 'remapGroup', positions: [4, 3, 2, 1], children: [{ kind: 'staticColor', ledCount: 1, color: 'Red' }] });
    const positions = o.Positions as { Position: number }[];
    // Positions is an ObservableCollection<LedPosition>, not a list of numbers and not a string.
    expect(positions.slice(0, 4)).toEqual([{ Position: 4 }, { Position: 3 }, { Position: 2 }, { Position: 1 }]);
    // SetResultBase indexes Positions[i] for every lit LED below 64, so the rest is filled with the
    // natural position rather than left short — a short list throws once a frame instead of failing quietly.
    expect(positions).toHaveLength(leds.REMAP_POSITIONS);
    expect(positions[4]).toEqual({ Position: 5 });
    expect(positions[63]).toEqual({ Position: 64 });
    expect((o.LedContainers as unknown[]).length).toBe(1);
  });

  test('a remap shorter than the strip is an error, because the lookup throws rather than clipping', () => {
    const r = leds.validateProfile(
      { name: 'x', profileId: ID, containers: [{ kind: 'remapGroup', positions: [2, 1], children: [] }] },
      { ledCount: 16 },
    );
    expect(r.errors.map((e) => e.code)).toEqual(['leds/remap-short']);
  });

  test('raw carries any container SimHub has that the model does not spell out', () => {
    const o = leds.buildContainerObject({ kind: 'raw', containerType: 'Status.SpotterCarLeft', fields: { LedCount: 3, Color: 'Yellow' } });
    expect(o).toEqual({ LedCount: 3, Color: 'Yellow', ContainerType: 'Status.SpotterCarLeft' });
  });
});

describe('validation, which exists because every one of these fails silently', () => {
  const codes = (r: { errors: { code: string }[] }) => r.errors.map((e) => e.code);

  test('a good profile is ok', () => {
    const r = leds.validateProfile(profile([{ kind: 'staticColor', ledCount: 3, color: '#00D96A' }]), { ledCount: 16 });
    expect(r).toEqual({ ok: true, errors: [], warnings: [] });
  });

  test('an unknown ContainerType is an error, because it loads as a disabled UnknownContainer', () => {
    const r = leds.validateProfile(profile([{ kind: 'raw', containerType: 'Status.Nonexistent', fields: {} }]));
    expect(codes(r)).toEqual(['leds/container-type']);
    expect(r.errors[0]!.message).toContain('UnknownContainer');
  });

  test('an effect past the end of the strip is an error: the LED version of the text-fit rule', () => {
    const r = leds.validateProfile(profile([{ kind: 'staticColor', ledCount: 4, color: 'Red', startPosition: 14 }]), { ledCount: 16 });
    expect(codes(r)).toEqual(['leds/off-strip']);
    expect(r.errors[0]!.message).toBe('covers LEDs 14..17 of a 16-LED strip; the last 1 would not be drawn');
    // ...and it fits exactly at the end.
    expect(leds.validateProfile(profile([{ kind: 'staticColor', ledCount: 3, color: 'Red', startPosition: 14 }]), { ledCount: 16 }).ok).toBe(true);
  });

  test("a child's StartPosition is relative to its group, and the fit rule accumulates it", () => {
    // GetGroupResult builds the group's buffer at its own position and merges each child at the
    // child's, so a child at 1 inside a group at 12 is absolutely at 12 — not at 1, and not at 13.
    const nested = (groupAt: number, childAt: number, ledCount: number): leds.LedProfile =>
      profile([
        { kind: 'group', startPosition: groupAt, children: [{ kind: 'staticColor', ledCount, color: 'Red', startPosition: childAt }] },
      ]);
    expect(leds.validateProfile(nested(12, 1, 5), { ledCount: 16 }).ok).toBe(true);
    const r = leds.validateProfile(nested(12, 1, 6), { ledCount: 16 });
    expect(codes(r)).toEqual(['leds/off-strip']);
    expect(r.errors[0]!.message).toBe('covers LEDs 12..17 of a 16-LED strip; the last 1 would not be drawn');
    expect(r.errors[0]!.path).toBe('openDash strip/Base.Group[0]/StaticColor[0]');
  });

  test('a remap group renumbers rather than offsets, so its children start from 1 again', () => {
    const r = leds.validateProfile(
      profile([{ kind: 'remapGroup', positions: [4, 3, 2, 1], startPosition: 1, children: [{ kind: 'staticColor', ledCount: 4, color: 'Red', startPosition: 1 }] }]),
      { ledCount: 4 },
    );
    expect(r.ok).toBe(true);
  });

  test('three-digit shorthand is rejected, because SimHub reads it as a transparent near-black', () => {
    const r = leds.validateProfile(profile([{ kind: 'staticColor', ledCount: 1, color: '#F80' }]));
    expect(codes(r)).toEqual(['leds/color']);
    expect(r.errors[0]!.message).toContain('three-digit shorthand');
    for (const ok of ['#FF8800', '#80FF8800', 'Lime', 'Transparent', '255, 136, 0']) expect(leds.isLedColor(ok)).toBe(true);
    for (const bad of ['#F80', '', 'rgb(1,2,3)', '#GGGGGG']) expect(leds.isLedColor(bad)).toBe(false);
  });

  test('an animation pixel is checked too, since sixty-four of them is where a typo hides', () => {
    const r = leds.validateProfile(profile([{ kind: 'animation', rows: 8, columns: 8, frames: [{ pixels: [['#0F0']], durationMs: 500 }] }]));
    expect(codes(r)).toEqual(['leds/color']);
    expect(r.errors[0]!.path).toBe('openDash strip/Animation[0]#Frames[0].0,0');
  });

  test('a mis-arity or unknown NCalc call is an error: SimHub evaluates either to nothing', () => {
    const status = (expression: string): leds.LedContainer => ({ kind: 'customStatus', ledCount: 1, color: 'Red', enabledFormula: { expression } });
    expect(codes(leds.validateProfile(profile([status('round([X], 1, 2)')])))).toEqual(['leds/ncalc-arity']);
    expect(codes(leds.validateProfile(profile([status('nosuchfn([X])')])))).toEqual(['leds/ncalc-unknown']);
    // isnull takes one argument or two, and both are how a profile stays safe without the plugin.
    expect(leds.validateProfile(profile([status('isnull([X])')]), { ledCount: 4 }).ok).toBe(true);
    expect(leds.validateProfile(profile([status('isnull([X], 0) > 0')]), { ledCount: 4 }).ok).toBe(true);
  });

  test('a Javascript body is not checked as NCalc', () => {
    const r = leds.validateProfile(profile([{ kind: 'scriptedContent', ledCount: 4, contentFormula: { expression: "return ['#00FF00']", interpreter: 'javascript' } }]), { ledCount: 16 });
    expect(r.ok).toBe(true);
  });

  test('an undeclared OpenDash property is an error, and a declared one is not', () => {
    const opts = { declaredProperties: ['OpenDash.ShiftLights'], propertyPrefix: 'OpenDash', ledCount: 16 };
    const bad = leds.validateProfile(profile([{ kind: 'customStatus', ledCount: 1, color: 'Red', enabledFormula: { expression: 'isnull([OpenDash.Nope], 0)' } }]), opts);
    expect(codes(bad)).toEqual(['leds/property-undeclared']);
    const good = leds.validateProfile(profile([{ kind: 'customStatus', ledCount: 1, color: 'Red', enabledFormula: { expression: 'isnull([OpenDash.ShiftLights], true)' } }]), opts);
    expect(good.ok).toBe(true);
  });

  test('a profile needs a name, a GUID and something to light', () => {
    const r = leds.validateProfile({ name: '  ', profileId: 'not-a-guid', containers: [] });
    expect(codes(r).sort()).toEqual(['leds/empty', 'leds/name', 'leds/profile-id']);
  });

  test('a remap position past the end of the strip is an error, and a repeat is a warning', () => {
    const r = leds.validateProfile(profile([{ kind: 'remapGroup', positions: [1, 1, 99], children: [] }]), { ledCount: 3 });
    expect(codes(r)).toEqual(['leds/remap-position']);
    expect(r.warnings.map((w) => w.code)).toEqual(['leds/remap-duplicate']);
  });
});

describe('writing', () => {
  test('writes <name>.ledsprofile and rebuilds byte-identically', () => {
    const a = mkdtempSync(join(tmpdir(), 'leds-a-'));
    const b = mkdtempSync(join(tmpdir(), 'leds-b-'));
    try {
      const p = profile([{ kind: 'staticColor', ledCount: 3, color: '#00D96A' }]);
      const pathA = leds.writeLedsProfile(p, a, 'openDash strip');
      const pathB = leds.writeLedsProfile(p, b, 'openDash strip');
      expect(pathA.endsWith('openDash strip.ledsprofile')).toBe(true);
      expect(readFileSync(pathA, 'utf8')).toBe(readFileSync(pathB, 'utf8'));
    } finally {
      rmSync(a, { recursive: true, force: true });
      rmSync(b, { recursive: true, force: true });
    }
  });
});
