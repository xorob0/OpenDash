/**
 * The flag box profile: the tree's shape, the rules it inherits from the packages, and the one
 * thing this ticket exists to prove — that a matrix lights at all, from a property, in the right
 * order. The glyphs themselves are XOR-227.
 */
import { describe, expect, test } from 'bun:test';
import { FLAG_PRIORITY } from '../src/components/flagStrip.ts';
import { declaredProperties, flagBoxProperties, PROPERTY_PREFIX } from '../src/contract.ts';
import { buildContainerObject, serializeProfile, validateProfile, walkContainers, type MatrixContainer } from '../src/generator.ts';
import { buildFlagBoxProfile, flagBoxTree, flagContainers, pruneEmpty } from '../src/leds/profile.ts';
import { ds } from '../src/tokens.ts';

const profile = buildFlagBoxProfile();
const all = [...walkContainers(profile.containers)];
const kinds = all.map((c) => c.kind);

describe('the tree is evaluated outside in', () => {
  test('brightness is the root, so one setting answers for everything below it', () => {
    expect(profile.containers).toHaveLength(1);
    expect(profile.containers[0]?.kind).toBe('brightnessFormula');
  });

  test('the running and not-running branches are both declared', () => {
    const declared = [...walkContainers(flagBoxTree())].map((c) => c.kind);
    expect(declared).toContain('gameRunning');
    expect(declared).toContain('gameNotRunning');
  });

  test('a branch with nothing in it yet is pruned rather than written', () => {
    // The box at rest is XOR-229. Until it draws something, an empty GameNotRunning group would
    // be a dead node in a file somebody has to read and a warning in a clean build.
    expect(kinds).toContain('gameRunning');
    expect(kinds).not.toContain('gameNotRunning');
    expect(pruneEmpty([{ kind: 'group', children: [] }])).toEqual([]);
  });

  test('it is an 8x8 profile', () => {
    expect(profile.deviceKind).toBe('matrix8x8');
  });
});

describe('the rules it inherits from the packages', () => {
  test('every property it reads is declared, so the profile and the panel cannot drift', () => {
    const result = validateProfile(profile, { declaredProperties: declaredProperties(), propertyPrefix: PROPERTY_PREFIX });
    expect(result.errors).toEqual([]);
    expect(result.ok).toBe(true);
  });

  test('the flag box properties are part of what the plugin declares', () => {
    for (const name of flagBoxProperties()) expect(declaredProperties()).toContain(name);
  });

  test('every property read is wrapped in isnull with a default, so the box works with no plugin', () => {
    const reads = [...serializeProfile(profile).matchAll(/\[OpenDash\.[A-Za-z0-9]+\]/g)].map((m) => m[0]);
    expect(reads.length).toBeGreaterThan(0);
    const text = serializeProfile(profile);
    for (const read of new Set(reads)) {
      expect(text).toInclude(`isnull(${read},`);
    }
  });

  test('colour comes from the tokens, so nothing here re-picks one', () => {
    const colours = new Set<string>();
    for (const container of all) {
      if (container.kind !== 'animation') continue;
      for (const frame of container.frames) for (const row of frame.pixels) for (const c of row) if (c) colours.add(c);
    }
    const tokenColours = new Set<string>(Object.values(ds.purpose.flag).filter((v) => typeof v === 'string'));
    for (const colour of colours) expect(tokenColours.has(colour)).toBe(true);
  });
});

describe('the flags are ranked the way the face ranks them', () => {
  test('the order is FLAG_PRIORITY itself, not a second list', () => {
    const drawn = flagContainers().map((c) => c.description);
    const expected = FLAG_PRIORITY.filter((f) => drawn.includes(f));
    expect(drawn).toEqual([...expected]);
  });

  test('a flag only shows when no higher-priority flag is out', () => {
    for (const container of flagContainers()) {
      const index = FLAG_PRIORITY.indexOf(container.description as (typeof FLAG_PRIORITY)[number]);
      const formula = (container as Extract<MatrixContainer, { kind: 'when' }>).formula;
      const text = typeof formula === 'string' ? formula : formula.expression;
      for (const higher of FLAG_PRIORITY.slice(0, index)) expect(text).toInclude(`${higher}]) = (0)`);
    }
  });

  test('an undrawn flag is absent rather than approximated', () => {
    // A drawn alert that never fires is worse than an absent one, so the catalogue grows in
    // XOR-227 by adding glyphs, never by colouring something in and hoping.
    const drawn = flagContainers().map((c) => c.description);
    expect(drawn).toEqual(['Flag_Green']);
  });

  test('the green flag paints the green token', () => {
    const green = flagContainers()[0] as Extract<MatrixContainer, { kind: 'when' }>;
    const glyph = green.children[0] as Extract<MatrixContainer, { kind: 'animation' }>;
    const lit = glyph.frames[0]?.pixels.flat() ?? [];
    expect(lit).toHaveLength(64);
    expect(new Set(lit)).toEqual(new Set([ds.purpose.flag.green]));
  });
});

describe('the four matrix contents', () => {
  // XOR-226's last acceptance line: the split has to work before anything depends on it. The
  // shipped profile puts everything on matrix 1 (XOR-231 settles the settings); this proves the
  // mechanism, which is what a second box configured as content 2 exercises on the VM.
  test('everything ships on matrix 1, a working single-box setup', () => {
    for (const container of all) expect(container.matrix ?? 1).toBe(1);
  });

  test('the same glyph on matrix 2 differs only in StartPositionMatrix', () => {
    const one = flagContainers()[0] as Extract<MatrixContainer, { kind: 'when' }>;
    const glyph = one.children[0] as MatrixContainer;
    const a = buildContainerObject(glyph, 'p/0', 'matrix8x8');
    const b = buildContainerObject({ ...glyph, matrix: 2 } as MatrixContainer, 'p/0', 'matrix8x8');
    expect(b.StartPositionMatrix).toBe(2);
    expect(JSON.stringify({ ...b, StartPositionMatrix: 1 })).toBe(JSON.stringify(a));
  });
});

describe('the emitted file', () => {
  test('the profile', () => {
    expect(serializeProfile(profile)).toMatchSnapshot();
  });
});
