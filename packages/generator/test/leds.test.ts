/**
 * The `.ledsprofile` serialiser against the facts in docs/research/simhub-leds-format.md. The
 * three that cost a whole profile when wrong have a test of their own: the bare-class-name
 * ContainerType, the Ex position spelling on groups, and the row-then-column pixel string.
 */
import { describe, expect, test } from 'bun:test';
import {
  buildContainerObject,
  buildProfileObject,
  CONTAINER_TYPE,
  DEVICE_KIND,
  DEVICE_SIZE,
  encodeFrameColour,
  encodeFramePixels,
  isGroup,
  MAX_MATRICES,
  serializeProfile,
  validateProfile,
  walkContainers,
  type MatrixContainer,
  type MatrixProfile,
} from '../src/leds.ts';
import type { Hex } from '../src/model.ts';

const RED = '#FF0000';
const GREEN = '#00FF00';

// Off the diagonal on purpose: a symmetric fixture cannot tell a row from a column.
const animation = (over: Partial<Extract<MatrixContainer, { kind: 'animation' }>> = {}): MatrixContainer => ({
  kind: 'animation',
  frames: [{ durationMs: 100, pixels: [[null, RED], [GREEN, null]] }],
  ...over,
});

const profileOf = (containers: MatrixContainer[]): MatrixProfile => ({ name: 'Test', deviceKind: 'matrix8x8', containers });

describe('ContainerType is the bare class name', () => {
  // The strip driver trims the namespace and the Container suffix; the matrix driver does not,
  // and it has no UnknownContainer fallback, so a wrong name throws and takes the profile with it.
  test('every kind maps to a name ending in Container', () => {
    for (const name of Object.values(CONTAINER_TYPE)) expect(name).toMatch(/^[A-Za-z]+Container$/);
  });

  test('the names are SimHub class names, not the strip driver spelling', () => {
    expect(CONTAINER_TYPE.animation).toBe('AnimationContainer');
    expect(CONTAINER_TYPE.when).toBe('CustomConditionalGroupContainer');
    expect(CONTAINER_TYPE.gameNotRunning).toBe('GameNotRunningGroupContainer');
    expect(CONTAINER_TYPE.animation).not.toBe('Animation');
  });
});

describe('groups rename their position', () => {
  // A group that writes StartPositionX lands at the origin and never says so.
  test('a group writes the Ex spelling and no other', () => {
    const o = buildContainerObject({ kind: 'group', x: 3, y: 4, children: [] }, 'p/0', 'matrix8x8');
    expect(o.StartPositionXEx).toBe(3);
    expect(o.StartPositionYEx).toBe(4);
    expect(o.StartPositionX).toBeUndefined();
    expect(o.StartPositionY).toBeUndefined();
  });

  test('a leaf writes the plain spelling and no other', () => {
    const o = buildContainerObject(animation({ x: 3, y: 4 }), 'p/0', 'matrix8x8');
    expect(o.StartPositionX).toBe(3);
    expect(o.StartPositionY).toBe(4);
    expect(o.StartPositionXEx).toBeUndefined();
  });

  test('isGroup agrees with the kinds that carry children', () => {
    expect(isGroup({ kind: 'group', children: [] })).toBe(true);
    expect(isGroup({ kind: 'when', formula: '1', children: [] })).toBe(true);
    expect(isGroup(animation())).toBe(false);
  });
});

describe('a frame is one string, row then column', () => {
  test('pixels are row,column,#colour and unlit pixels are absent', () => {
    // Deliberately off the diagonal. A fixture whose lit pixels are at (0,0) and (1,1) reads the
    // same whichever way round the pair is written, so it proves nothing about the one fact this
    // test exists for: GetPixel(x, y) reads Colors[y][x], so the row is written first.
    const pixels: (Hex | null)[][] = [
      [null, RED, null],
      [null, null, null],
      [GREEN, null, null],
    ];
    expect(encodeFramePixels({ durationMs: 1, pixels })).toBe('0,1,#FF0000;2,0,#00FF00');
  });

  test('a wide frame is not transposed, which a square one would hide', () => {
    expect(encodeFramePixels({ durationMs: 1, pixels: [[null, null, null, RED]] })).toBe('0,3,#FF0000');
  });

  test('an opaque colour loses its alpha pair, a translucent one keeps it', () => {
    expect(encodeFrameColour('#FF0000')).toBe('#FF0000');
    expect(encodeFrameColour('#FFFF0000')).toBe('#FF0000');
    expect(encodeFrameColour('#80FF0000')).toBe('#80FF0000');
  });

  test('a frame carries its own duration', () => {
    const o = buildContainerObject(animation(), 'p/0', 'matrix8x8');
    expect(o.Animation).toMatchObject({ Rows: 8, Columns: 8, Frames: [{ Colors: '0,1,#FF0000;1,0,#00FF00', FrameDuration: 100 }] });
  });

  test('a frame taller than the matrix is refused rather than clipped', () => {
    const tall = animation({ frames: [{ durationMs: 1, pixels: Array.from({ length: 9 }, () => [RED]) }] });
    expect(() => buildContainerObject(tall, 'p/0', 'matrix8x8')).toThrow(/9-row|row 8/);
  });
});

describe('the device kinds', () => {
  test('8x8 is 2, and its metadata is eight by eight', () => {
    expect(DEVICE_KIND.matrix8x8).toBe(2);
    expect(DEVICE_SIZE.matrix8x8).toEqual({ rows: 8, columns: 8 });
  });

  test('the kind is NOT written on a container, because SimHub drops it', () => {
    // MatrixContainerBase.DeviceKind has a private setter and no [JsonProperty], so Json.NET marks
    // it non-writable and skips it on load. Emitting it was six hundred dead fields in the file.
    expect(buildContainerObject(animation(), 'p/0', 'matrix8x8').DeviceKind).toBeUndefined();
  });
});

describe('the four matrices', () => {
  test('four is the cap, because MultiMatrixResult holds four', () => {
    expect(MAX_MATRICES).toBe(4);
  });

  test('a container names the matrix it paints, one-based', () => {
    expect(buildContainerObject(animation(), 'p/0', 'matrix8x8').StartPositionMatrix).toBe(1);
    expect(buildContainerObject(animation({ matrix: 2 }), 'p/0', 'matrix8x8').StartPositionMatrix).toBe(2);
  });

  test('a fifth matrix is refused', () => {
    expect(() => buildContainerObject(animation({ matrix: 5 }), 'p/0', 'matrix8x8')).toThrow(/1\.\.4/);
  });
});

describe('the optional fields SimHub omits by default', () => {
  test('ClearBackgroundWhenActive is written only when true', () => {
    expect(buildContainerObject({ kind: 'when', formula: '1', children: [] }, 'p/0', 'matrix8x8').ClearBackgroundWhenActive).toBeUndefined();
    expect(buildContainerObject({ kind: 'when', formula: '1', clearBackground: true, children: [] }, 'p/0', 'matrix8x8').ClearBackgroundWhenActive).toBe(true);
  });

  test('a description is omitted rather than written null', () => {
    expect('Description' in buildContainerObject(animation(), 'p/0', 'matrix8x8')).toBe(false);
    expect(buildContainerObject(animation({ description: 'Green' }), 'p/0', 'matrix8x8').Description).toBe('Green');
  });

  test('a condition is the same ExpressionValue shape a .djson binding uses', () => {
    const o = buildContainerObject({ kind: 'when', formula: '[X] = 1', children: [] }, 'p/0', 'matrix8x8');
    expect(o.TriggerFormula).toEqual({ Expression: '[X] = 1' });
  });

  test('a JavaScript condition declares the interpreter', () => {
    const o = buildContainerObject({ kind: 'when', formula: { expression: 'x', interpreter: 'js' }, children: [] }, 'p/0', 'matrix8x8');
    expect(o.TriggerFormula).toEqual({ Interpreter: 1, JSExt: 0, Expression: 'x' });
  });
});

describe('ids are stable', () => {
  test('the same tree twice gives the same ids', () => {
    const once = serializeProfile(profileOf([animation()]));
    expect(serializeProfile(profileOf([animation()]))).toBe(once);
  });

  test('siblings get different ids', () => {
    const o = buildProfileObject(profileOf([animation(), animation()]));
    const ids = (o.LedContainers as { ContainerId: string }[]).map((c) => c.ContainerId);
    expect(new Set(ids).size).toBe(2);
  });
});

describe('the file', () => {
  test('is indented JSON with no $id, $ref or $type', () => {
    const text = serializeProfile(profileOf([animation()]));
    expect(text).toStartWith('{\n  "');
    expect(text).not.toInclude('$id');
    expect(text).not.toInclude('$ref');
    expect(text).not.toInclude('$type');
  });

  test('does not flip a SimHub default it has no reason to touch', () => {
    // RGBMatrixProfile declares UseStrictJSIsolation [DefaultValue(true)] with
    // DefaultValueHandling.Populate. We carry no JavaScript, so false was inert -- and a trap for
    // the first profile that does.
    expect(buildProfileObject(profileOf([animation()])).UseStrictJSIsolation).toBe(true);
  });

  test('carries the fields RGBMatrixProfile reads back', () => {
    const o = buildProfileObject(profileOf([animation()]));
    expect(o).toMatchObject({ Name: 'Test', UseProfileBrightness: false, GlobalBrightness: 100, CarChoices: [] });
    expect(o.ProfileId).toMatch(/^[0-9a-f-]{36}$/);
  });
});

describe('validateProfile', () => {
  const opts = { declaredProperties: ['OpenDash.Known'], propertyPrefix: 'OpenDash' };

  test('accepts a profile that reads a declared property', () => {
    const result = validateProfile(profileOf([{ kind: 'when', formula: 'isnull([OpenDash.Known], 1)', children: [animation()] }]), opts);
    expect(result.ok).toBe(true);
    expect(result.errors).toEqual([]);
  });

  test('rejects an undeclared property, which is what stops the profile and the panel drifting', () => {
    const result = validateProfile(profileOf([{ kind: 'when', formula: '[OpenDash.Invented] = 1', children: [animation()] }]), opts);
    expect(result.ok).toBe(false);
    expect(result.errors[0]?.code).toBe('property/undeclared');
  });

  test('rejects a function SimHub does not dispatch', () => {
    const result = validateProfile(profileOf([{ kind: 'when', formula: 'nosuchfunction(1)', children: [animation()] }]), opts);
    expect(result.errors.some((e) => e.code.startsWith('expression/'))).toBe(true);
  });

  test('warns about an empty group rather than failing the build', () => {
    const result = validateProfile(profileOf([{ kind: 'group', children: [] }]), opts);
    expect(result.ok).toBe(true);
    expect(result.warnings[0]?.code).toBe('leds/empty-group');
  });

  test('rejects a profile with nothing in it', () => {
    expect(validateProfile(profileOf([]), opts).errors[0]?.code).toBe('leds/empty');
  });
});

describe('walkContainers', () => {
  test('yields parents before children, in file order', () => {
    const tree: MatrixContainer[] = [{ kind: 'group', description: 'a', children: [animation({ description: 'b' })] }, animation({ description: 'c' })];
    expect([...walkContainers(tree)].map((c) => c.description)).toEqual(['a', 'b', 'c']);
  });
});

describe('ids do not churn when a sibling changes', () => {
  // A generated file whose ids move on an unrelated edit makes every diff unreadable, and this
  // tree grows one effect at a time.
  const idsOf = (containers: MatrixContainer[]): Record<string, string> => {
    const out: Record<string, string> = {};
    const walk = (list: readonly unknown[]): void => {
      for (const raw of list) {
        const c = raw as { Description?: string; ContainerId: string; LedContainers?: unknown[] };
        if (c.Description !== undefined) out[c.Description] = c.ContainerId;
        walk(c.LedContainers ?? []);
      }
    };
    walk(buildProfileObject(profileOf(containers)).LedContainers as unknown[]);
    return out;
  };

  test('inserting a described sibling leaves the others alone', () => {
    const before = idsOf([animation({ description: 'a' }), animation({ description: 'b' })]);
    const after = idsOf([animation({ description: 'a' }), animation({ description: 'inserted' }), animation({ description: 'b' })]);
    expect(after.a).toBe(before.a);
    expect(after.b).toBe(before.b);
  });

  test('removing a described sibling leaves the others alone', () => {
    const before = idsOf([animation({ description: 'a' }), animation({ description: 'gone' }), animation({ description: 'b' })]);
    const after = idsOf([animation({ description: 'a' }), animation({ description: 'b' })]);
    expect(after.b).toBe(before.b);
  });
});
