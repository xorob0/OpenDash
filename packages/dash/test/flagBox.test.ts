/**
 * The flag box profile: the tree's shape, the rules it inherits from the packages, and the thing
 * a matrix gets wrong that a screen does not — which of several live flags it shows.
 *
 * Nothing in the repository evaluates a binding (XOR-20), so `raised()` below carries a small
 * boolean evaluator for the subset of NCalc these conditions are built from, the way
 * barStrip.test.ts carries an arithmetic one. It is what makes the several-flags-at-once table
 * possible at all.
 */
import { describe, expect, test } from 'bun:test';
import { flagStrip, FLAG_PRIORITY } from '../src/components/flagStrip.ts';
import { rect } from '../src/design/geometry.ts';
import {
  declaredProperties,
  DEFAULT_OIL_TEMP,
  DEFAULT_WATER_TEMP,
  flagBoxMatrixProperties,
  flagBoxProperties,
  FLAG_BOX_MATRICES,
  FLAG_BOX_MATRIX_DEFAULTS,
  PROPERTY_PREFIX,
} from '../src/contract.ts';
import { conditionRaised, conditionShown, conditionVisible, FACE_FLAG_PRIORITY, FLAG_CATALOGUE, flagBit, flagCondition, type SessionFlagBit } from '../src/flags.ts';
import { buildContainerObject, serializeProfile, validateProfile, walkContainers, type Hex, type MatrixContainer, type MatrixFrame } from '../src/generator.ts';
import { revSegmentOptions, shiftBands } from '../src/components/revSegments.ts';
import { eitherLadder, GEAR_COUNT_PROPERTY, mirrorAvailable, SHIFT_RPM_PROPERTIES } from '../src/shift.ts';
import { GEARS, gearGrid } from '../src/leds/gear.ts';
import { overRev as overRevStrip } from '../src/leds/ladder.ts';
import { flagFrames, FLAG_PALETTE, HOLD_MS, ignitionOffFrames, STANDBY_PALETTE } from '../src/leds/glyphs.ts';
import { buildFlagBoxProfile, criticalOnly, drawnFlags, flagBoxTree, flagContainers, pruneEmpty, noFlagShowing } from '../src/leds/profile.ts';
import { CAR_BOTH, CAR_LEFT, CAR_RIGHT, pitStates, spotterStates, warningStates } from '../src/leds/states.ts';
import { ds } from '../src/tokens.ts';

/** Every colour `ds` resolves, so "is this a token" is a question about tokens.json rather than
 * about one branch of it. */
const TOKEN_COLOURS = new Set<string>();
(function collect(node: unknown): void {
  if (typeof node === 'string') {
    if (/^#[0-9A-F]{6}$/.test(node)) TOKEN_COLOURS.add(node);
    return;
  }
  if (node !== null && typeof node === 'object') for (const v of Object.values(node)) collect(v);
})(ds);

/**
 * The condition inside a segment's `if(lit, colour, unlit)`. A segment carries its test that way,
 * so this is how a band's threshold is compared against the bar's rather than eyeballed.
 */
const litOf = (colorBind: string | undefined): string => {
  const m = /^if\((.*), '#[0-9A-F]{6}', '#[0-9A-F]{6}'\)$/.exec(colorBind ?? '');
  if (m?.[1] === undefined) throw new Error(`not a segment colour bind: ${colorBind}`);
  return m[1];
};

/**
 * A shift expression evaluated against one frame of telemetry, a property absent from the frame
 * standing for a property the sim did not publish.
 *
 * String equality against the rev bar is what the flash is pinned by below, and it cannot tell a
 * digit and a bar that are wrong together from two that are right: that is exactly how the
 * fallback ladder kept flashing in the last gear through a green suite. Covers the whole of what
 * `shift.ts` emits and nothing besides -- property reads, `isnull`, `max`, the comparisons,
 * `and` / `or` / `!` and arithmetic -- and throws on anything wider rather than guessing.
 */
const evaluateShift = (expression: string, telemetry: Record<string, number>): boolean => {
  const js = expression
    .replace(/\[([A-Za-z0-9_.]+)\]/g, (_, name: string) => `P(${JSON.stringify(name)})`)
    .replace(/\bisnull\(/g, 'nz(')
    .replace(/\bmax\(/g, 'Math.max(')
    .replace(/\band\b/g, '&&')
    .replace(/\bor\b/g, '||')
    .replace(/ = /g, ' === ');
  const words = js.replace(/P\("[^"]*"\)/g, '0').match(/[A-Za-z_][A-Za-z_.]*/g) ?? [];
  const unknown = words.filter((w) => w !== 'nz' && w !== 'Math.max');
  if (unknown.length > 0) throw new Error(`evaluateShift does not cover ${unknown.join(', ')} in ${expression}`);
  const read = (name: string): number | null => telemetry[name] ?? null;
  const result: unknown = new Function('P', 'nz', `return (${js});`)(read, (v: number | null, d: number) => v ?? d);
  if (typeof result !== 'boolean') throw new Error(`not a condition: ${expression}`);
  return result;
};

const profile = buildFlagBoxProfile();
const all = [...walkContainers(profile.containers)];
const kinds = all.map((c) => c.kind);

/**
 * Evaluates one of these conditions with the named bits set. Handles exactly what flags.ts emits:
 * parenthesised `and`, `or`, `!`, and `[prop] = 1`. Anything else throws rather than guessing,
 * so a change to how the conditions are built fails here instead of passing wrongly.
 */
function evaluate(expression: string, set: readonly SessionFlagBit[], criticalOnly = false): boolean {
  const truths = new Map<string, string>();
  for (const condition of FLAG_CATALOGUE) {
    for (const bit of condition.bits) truths.set(flagBit(bit), set.includes(bit) ? '1' : '0');
  }
  let s = expression;
  for (const [property, value] of truths) s = s.split(property).join(value);
  s = s.split('isnull([OpenDash.FlagBoxCriticalOnly], false)').join(criticalOnly ? 'true' : 'false');
  // NCalc's operators to JavaScript's, on a string that now holds only literals and operators.
  s = s.replace(/\band\b/g, '&&').replace(/\bor\b/g, '||').replace(/([^!<>=])=([^=])/g, '$1===$2');
  if (!/^[\s()!&|=01a-z]+$/.test(s)) throw new Error(`the condition holds something this cannot evaluate: ${s}`);
  // eslint-disable-next-line no-new-func
  return Boolean(new Function(`return (${s});`)());
}

/** Which flag the box shows with these bits raised, or undefined when it shows none. */
function shown(set: readonly SessionFlagBit[], criticalOnly = false): string | undefined {
  const lit = flagContainers().filter((c) => {
    const formula = (c as Extract<MatrixContainer, { kind: 'when' }>).formula;
    return evaluate(typeof formula === 'string' ? formula : formula.expression, set, criticalOnly);
  });
  expect(lit.length).toBeLessThanOrEqual(1);
  return lit[0]?.description;
}

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
    const text = serializeProfile(profile);
    const reads = [...text.matchAll(/\[OpenDash\.[A-Za-z0-9]+\]/g)].map((m) => m[0]);
    expect(reads.length).toBeGreaterThan(0);
    for (const read of new Set(reads)) expect(text).toInclude(`isnull(${read},`);
  });

  test('colour comes from the tokens, so nothing here re-picks one', () => {
    for (const container of all) {
      if (container.kind !== 'animation') continue;
      for (const frame of container.frames) for (const row of frame.pixels) for (const c of row) if (c) expect(TOKEN_COLOURS.has(c)).toBe(true);
    }
  });

  test('the palettes are tokens and nothing else', () => {
    for (const colour of [...Object.values(FLAG_PALETTE), ...Object.values(STANDBY_PALETTE)]) expect(TOKEN_COLOURS.has(colour)).toBe(true);
  });
});

describe('one ordered list, shared with the face', () => {
  test('the catalogue is ranked in the canvas alert catalogue’s own numbering', () => {
    // The order, and the canvas number each entry carries in PagesAndAlerts. It is asserted as the
    // list rather than refreshed from the code, because this is the ranking three surfaces read and
    // a change to it is a change to what a driver is told first.
    expect(FLAG_CATALOGUE.map((c) => c.id)).toEqual([
      'red', // 3
      'disqualify', // 4
      'furled', // 5
      'black', // 6
      'meatball', // 18 in the canvas, kept here: a flag calling this car in
      'caution', // 7 SafetyCar
      'yellowWaving', // where 9 DoubleYellow and 10 YellowSector would be
      'yellow', // 11
      'debris', // 19
      'blue', // 20
      'white', // 16 in the canvas, kept below the blue: see the rule
      'green', // 21
      'startSet', // 22
      'startReady', // 22
      'chequered', // 23
    ]);
    // The rule the two departures answer to: no condition the critical-flags switch can silence
    // outranks one it cannot, or turning the switch off would hide a flag. Every critical condition
    // therefore comes first, which is also what moved the chequer off the second rank.
    const firstNews = FLAG_CATALOGUE.findIndex((c) => !c.critical);
    expect(FLAG_CATALOGUE.slice(firstNews).filter((c) => c.critical)).toEqual([]);
  });

  test('the face ranks from the catalogue rather than its own copy', () => {
    expect(FLAG_PRIORITY).toEqual(FACE_FLAG_PRIORITY);
    // The six SimHub normalises, in the catalogue's order: the chequer is last of them, where it
    // used to be second and hid a yellow thrown at a race finishing under one.
    expect(FACE_FLAG_PRIORITY).toEqual(['Flag_Black', 'Flag_Yellow', 'Flag_Blue', 'Flag_White', 'Flag_Green', 'Flag_Checkered']);
  });

  test('the box draws the catalogue in that same order', () => {
    const drawn = flagContainers().map((c) => c.description);
    expect(drawn).toEqual(drawnFlags(false).map((c) => c.id));
    // The face's six keep their relative rank inside the longer list, or the two would disagree.
    const faceIds = ['black', 'yellow', 'blue', 'white', 'green', 'chequered'];
    expect(drawn.filter((d) => faceIds.includes(d ?? ''))).toEqual(faceIds);
  });

  test('band D draws every one of them, so no condition is the box’s alone', () => {
    // The face used to draw the six SimHub normalises and the box all fifteen, so a red flag, a
    // disqualification, a furled black, a meatball, a full-course caution, a waved yellow, the
    // debris flag and the start gantry were invisible on a dash with no box beside it.
    const band = flagStrip(rect(0, 0, 1920, 60));
    expect(band.map((i) => i.name)).toEqual(FLAG_CATALOGUE.map((c) => `flag.${c.id}`));
  });

  test('every condition in the catalogue is either drawn or absent on purpose', () => {
    // The docs table is the other half of this: a condition with no glyph is listed there with
    // the reason, and this proves the code and that table are talking about the same list.
    for (const condition of FLAG_CATALOGUE) expect(flagFrames(condition.id)).toBeDefined();
  });
});

describe('several conditions true at once', () => {
  // iRacing raises more than one bit constantly: a caution is yellow plus caution plus
  // cautionWaving, and the last lap of a race under a black flag is three at once. The box shows
  // one picture, so the only question that matters is which.
  const cases: { name: string; bits: SessionFlagBit[]; expect: string | undefined }[] = [
    { name: 'nothing out', bits: [], expect: undefined },
    { name: 'green alone', bits: ['green'], expect: 'green' },
    { name: 'a local yellow', bits: ['yellow'], expect: 'yellow' },
    { name: 'a waved yellow also sets yellow', bits: ['yellow', 'yellowWaving'], expect: 'yellowWaving' },
    { name: 'a full-course caution sets all three', bits: ['yellow', 'yellowWaving', 'caution', 'cautionWaving'], expect: 'caution' },
    { name: 'debris under a yellow', bits: ['yellow', 'debris'], expect: 'yellow' },
    { name: 'debris alone', bits: ['debris'], expect: 'debris' },
    { name: 'red outranks everything', bits: ['red', 'yellow', 'caution', 'black'], expect: 'red' },
    { name: 'a black flag on the last lap', bits: ['black', 'white'], expect: 'black' },
    { name: 'disqualified outranks the black flag it comes with', bits: ['black', 'disqualify'], expect: 'disqualify' },
    { name: 'a furled black is not a black', bits: ['furled'], expect: 'furled' },
    { name: 'a meatball while being lapped', bits: ['repair', 'blue'], expect: 'meatball' },
    // The chequer is last of the fifteen, where the canvas numbers it 23 of 25: a blue flag while
    // the chequer is out is still an instruction to this car, and the chequer is news.
    { name: 'the chequer while being lapped', bits: ['checkered', 'blue'], expect: 'blue' },
    { name: 'the chequer alone', bits: ['checkered'], expect: 'chequered' },
    { name: 'a yellow thrown at a chequered finish', bits: ['checkered', 'yellow'], expect: 'yellow' },
    { name: 'the start gantry', bits: ['startReady'], expect: 'startReady' },
    { name: 'set outranks ready, because it is later', bits: ['startReady', 'startSet'], expect: 'startSet' },
  ];

  for (const c of cases) {
    test(c.name, () => {
      expect(shown(c.bits)).toBe(c.expect);
    });
  }

  test('exactly one flag is ever lit, whichever bits are set', () => {
    // Every pair in the catalogue, which is the case a hand-written list of examples misses.
    const bits = FLAG_CATALOGUE.flatMap((c) => c.bits);
    for (const a of bits) for (const b of bits) expect(() => shown([a, b])).not.toThrow();
  });
});

describe('critical flags only', () => {
  test('it is a contract property and it defaults to off', () => {
    expect(flagBoxProperties()).toContain('OpenDash.FlagBoxCriticalOnly');
    expect(serializeProfile(profile)).toInclude('isnull([OpenDash.FlagBoxCriticalOnly], false)');
  });

  test('with it on, the box keeps the flags that mean slow down or are addressed to you', () => {
    const quiet = drawnFlags(true).map((c) => c.id);
    expect(quiet).toContain('red');
    expect(quiet).toContain('black');
    expect(quiet).toContain('yellow');
    expect(quiet).toContain('blue');
  });

  test('with it on, the news goes quiet', () => {
    const quiet = drawnFlags(true).map((c) => c.id);
    expect(quiet).not.toContain('chequered');
    expect(quiet).not.toContain('green');
    expect(quiet).not.toContain('startReady');
  });

  test('a suppressed flag stops outranking the ones below it, rather than blanking the box', () => {
    // The switch guards each condition rather than excluding it from the ranking, so a suppressed
    // flag never wins and then draws nothing. It is now unobservable in this catalogue, because no
    // condition the switch can silence outranks one it cannot, and that ordering is asserted with
    // the catalogue above; what is asserted here is the mechanism, which survives a reordering.
    const white = flagCondition('white');
    const green = flagCondition('green');
    expect(conditionVisible(green, criticalOnly())).toInclude(`!(${conditionShown(white, criticalOnly())})`);
    expect(conditionVisible(green, criticalOnly())).not.toInclude(`!(${conditionRaised(white)})`);
    // And the switch still takes the news away rather than moving it.
    expect(shown(['checkered', 'blue'], false)).toBe('blue');
    expect(shown(['checkered'], true)).toBeUndefined();
    expect(shown(['checkered', 'blue'], true)).toBe('blue');
  });

  test('the switch is a guard on each flag, not a second copy of the catalogue', () => {
    // Carrying the list twice reads better in the file and costs twice the glyphs, and the tree is
    // already written once per matrix, so the doubling would be eightfold by the time it hits disk.
    expect(flagContainers()).toHaveLength(drawnFlags(false).length);
    const text = serializeProfile(profile);
    expect(text).toInclude('isnull([OpenDash.FlagBoxCriticalOnly], false)');
  });

  test('a critical flag is shown whatever the switch says', () => {
    for (const condition of FLAG_CATALOGUE.filter((c) => c.critical)) {
      expect(shown(condition.bits, true)).toBe(condition.id);
    }
  });
});

describe('sixty-four pixels', () => {
  test('every glyph is eight by eight', () => {
    for (const condition of FLAG_CATALOGUE) {
      for (const frame of flagFrames(condition.id) ?? []) {
        expect(frame.pixels).toHaveLength(8);
        for (const row of frame.pixels) expect(row).toHaveLength(8);
      }
    }
  });

  test('black is an outline and a disqualification is a cross, because black is unlit', () => {
    const black = flagFrames('black') ?? [];
    for (const frame of black) {
      // Both halves of the wave are outlines: a filled panel would be a flag of another colour.
      expect(frame.pixels.flat().filter((p) => p !== null).length).toBeLessThan(64);
    }
    expect(black[0]?.pixels[0]?.every((p) => p !== null)).toBe(true);
    expect(black[0]?.pixels[3]?.slice(1, 7).every((p) => p === null)).toBe(true);
    // The smaller flag of the wave is the same outline inset by one, so the panel's edge goes dark.
    expect(black[1]?.pixels[0]?.every((p) => p === null)).toBe(true);
    expect(black[1]?.pixels[1]?.slice(1, 7).every((p) => p === ds.purpose.flag.black)).toBe(true);

    // The cross closes from the middle outwards, so the corners are the last pixels to arrive.
    const dq = flagFrames('disqualify') ?? [];
    expect(dq[0]?.pixels[0]?.every((p) => p === null)).toBe(true);
    const closed = dq[dq.length - 1]?.pixels ?? [];
    expect(closed[0]?.[0]).toBe(ds.purpose.flag.black);
    expect(closed[0]?.[3]).toBeNull();
  });

  test('a waved yellow is the yellow flag blinking, which is what tells them apart', () => {
    const yellow = flagFrames('yellow') ?? [];
    const waving = flagFrames('yellowWaving') ?? [];
    expect(yellow).toHaveLength(1);
    expect(waving).toHaveLength(2);
    expect(waving[0]?.pixels).toEqual(yellow[0]?.pixels);
    expect(waving[1]?.pixels.flat().every((p) => p === null)).toBe(true);
  });

  test('the blink is the flagBand rate, so the box pulses with the band on the face', () => {
    const half = Math.round(1000 / ds.indicator.flagBand.flashHz / 2);
    for (const frame of flagFrames('yellowWaving') ?? []) expect(frame.durationMs).toBe(half);
  });

  test('a full-course caution is two flags waved in turn, so it never looks like a local yellow', () => {
    // The whole-track condition and the local one are both the yellow flag, so the only thing that
    // can separate them is the pattern: two half panels alternating against one solid panel.
    const caution = flagFrames('caution') ?? [];
    const yellow = flagFrames('yellow')?.[0]?.pixels ?? [];
    expect(caution).toHaveLength(2);
    const picture = (frame: MatrixFrame | undefined): string[] => (frame?.pixels ?? []).map((row) => row.map((p) => (p === null ? '.' : 'Y')).join(''));
    expect(picture(caution[0])).toEqual(Array.from({ length: 8 }, () => 'YYYY....'));
    expect(picture(caution[1])).toEqual(Array.from({ length: 8 }, () => '....YYYY'));
    for (const frame of caution) {
      expect(frame.pixels).not.toEqual(yellow);
      expect(new Set(frame.pixels.flat().filter((p) => p !== null))).toEqual(new Set([ds.purpose.flag.yellow]));
    }
  });

  test('blue is held: one full panel with nothing cut out of it', () => {
    // Movement means act, and a blue flag informs. It is the flag a driver sees most often, so a
    // moving one would teach them that movement is ordinary, which is the whole rule undone.
    const blue = flagFrames('blue') ?? [];
    expect(blue).toHaveLength(1);
    expect(blue[0]?.pixels.flat().every((p) => p === ds.purpose.flag.blue)).toBe(true);
  });

  test('a flag that moves has more than one frame and a flag that is held has exactly one', () => {
    // The rule the canvas states, turned into something a later edit cannot quietly break: the
    // catalogue says which flags move and the drawings have to agree with it, in both directions.
    for (const condition of FLAG_CATALOGUE) {
      const count = (flagFrames(condition.id) ?? []).length;
      expect({ id: condition.id, moves: count > 1 }).toEqual({ id: condition.id, moves: condition.motion === 'moves' });
    }
  });

  test('red and the meatball grow in 100 ms steps and then stay', () => {
    // SimHub has no play-once: the sequence loops, so "plays once, then holds" is a last frame that
    // outlasts the other three by two hundred times.
    for (const id of ['red', 'meatball']) {
      expect({ id, durations: (flagFrames(id) ?? []).map((f) => f.durationMs) }).toEqual({ id, durations: [100, 100, 100, HOLD_MS] });
    }
  });

  test('a flag that walks steps at the band rate, so the whole box keeps one pulse', () => {
    const half = Math.round(1000 / ds.indicator.flagBand.flashHz / 2);
    for (const id of ['disqualify', 'furled', 'debris', 'black', 'chequered', 'caution']) {
      for (const frame of flagFrames(id) ?? []) expect({ id, ms: frame.durationMs }).toEqual({ id, ms: half });
    }
  });

  test('no two conditions draw the same picture', () => {
    // Two identical glyphs mean the driver cannot tell two conditions apart, which is the failure
    // this whole file is about.
    const seen = new Map<string, string>();
    for (const condition of FLAG_CATALOGUE) {
      const key = JSON.stringify(flagFrames(condition.id));
      const clash = seen.get(key);
      expect({ id: condition.id, clash }).toEqual({ id: condition.id, clash: undefined });
      seen.set(key, condition.id);
    }
  });
});

describe('what the box does when nobody is racing', () => {
  test('not racing is dark, and that is the decision rather than a gap', () => {
    // Idle screens are a refusal in scope.md; a glowing logo on somebody's desk when nothing is
    // running is the hardest version of it to defend.
    const declared = [...walkContainers(flagBoxTree())].find((c) => c.kind === 'gameNotRunning');
    expect(declared).toBeDefined();
    expect(declared && 'children' in declared ? declared.children : ['something']).toEqual([]);
    expect(kinds).not.toContain('gameNotRunning');
  });

  test('ignition off is visibly distinct from a profile that failed to load', () => {
    const text = serializeProfile(profile);
    expect(text).toInclude('"Description": "Ignition off"');
    const standby = all.find((c) => c.description === 'Standby');
    expect(standby?.kind).toBe('animation');
    const lit = ignitionOffFrames()[0]?.pixels.flat().filter((p) => p !== null) ?? [];
    expect(lit.length).toBeGreaterThan(0);
    expect(lit.length).toBeLessThan(8);
    expect(new Set(lit)).toEqual(new Set([ds.purpose.shift.unlit]));
  });

  test('the flags only run with the ignition on', () => {
    const text = serializeProfile(profile);
    expect(text).toInclude('"Description": "Ignition on"');
    expect(text).toInclude('[DataCorePlugin.GameData.EngineIgnitionOn]');
  });

  test('nothing reads an image from the user\'s disk', () => {
    // Every picture is frames in the file. A custom idle image is personalisation, which ADR 0011
    // owes an answer before anything here builds it.
    const text = serializeProfile(profile);
    for (const word of ['AnimationPath', 'FileName', 'ledanimation', 'UserOverrides', 'C:\\\\']) expect(text).not.toInclude(word);
  });
});

describe('brightness', () => {
  test('day, night and the switch are all contract properties', () => {
    expect(flagBoxProperties().slice(0, 8)).toEqual([
      'OpenDash.LightsBrightness',
      'OpenDash.LightsNightBrightness',
      'OpenDash.LightsNightMode',
      'OpenDash.FlagBoxCriticalOnly',
      'OpenDash.FlagBoxGear',
      'OpenDash.FlagBoxLowFuelLaps',
      'OpenDash.FlagBoxOilTemp',
      'OpenDash.FlagBoxWaterTemp',
    ]);
  });

  test('they are named for the rig, not for this box', () => {
    // A driver who owns a flag box probably owns other lights, and "how bright, and is it night"
    // is one answer for a rig. Naming them per device now means renaming a public interface later.
    for (const name of flagBoxProperties().slice(0, 3)) expect(name).toStartWith('OpenDash.Lights');
  });

  test('night mode picks the night value, and it is dimmer', () => {
    const text = serializeProfile(profile);
    expect(text).toInclude('if(');
    expect(text).toInclude('isnull([OpenDash.LightsNightMode], false)');
    expect(text).toInclude('isnull([OpenDash.LightsNightBrightness], 25)');
    expect(text).toInclude('isnull([OpenDash.LightsBrightness], 100)');
  });

  test('one brightness answers for everything, because it is the root', () => {
    expect(profile.containers[0]?.kind).toBe('brightnessFormula');
    expect(all.filter((c) => c.kind === 'brightnessFormula' || c.kind === 'brightness')).toHaveLength(1);
  });
});

describe('the gear, as the resting state', () => {
  test('every gear iRacing reports has a glyph, reverse and neutral included', () => {
    expect(GEARS).toEqual(['R', 'N', '1', '2', '3', '4', '5', '6', '7', '8', '9']);
    for (const gear of GEARS) {
      const grid = gearGrid(gear);
      expect(grid).toHaveLength(8);
      for (const row of grid) expect(row).toHaveLength(8);
    }
  });

  test('no two gears draw the same glyph, and 6 and 8 differ in more than one place', () => {
    const seen = new Map<string, string>();
    for (const gear of GEARS) {
      const key = gearGrid(gear).join('|');
      expect({ gear, clash: seen.get(key) }).toEqual({ gear, clash: undefined });
      seen.set(key, gear);
    }
    const six = gearGrid('6').join('');
    const eight = gearGrid('8').join('');
    const differences = [...six].filter((ch, i) => ch !== eight[i]).length;
    expect(differences).toBeGreaterThan(1);
  });

  test('the colour is the shift model, taken from the rev bar', () => {
    // One relationship learned once and read in two places. XOR-230 replaced SimHub's per-car
    // bands with the sim's own DriverCarSL* values and changed shiftBands() alone, so the box now
    // carries both ladders: the car's own, and SimHub's for a car that publishes none.
    const bands = shiftBands();
    expect(bands.map((b) => b.id)).toEqual(['redline', 'stage2', 'stage1', 'rest']);
    expect(bands.map((b) => b.colour)).toEqual([ds.purpose.shift.stage3, ds.purpose.shift.stage2, ds.purpose.shift.stage1, ds.color.text.primary]);
    const text = serializeProfile(profile);
    expect(text).toInclude('[DataCorePlugin.GameData.CarSettings_RPMRedLineReached]');
    expect(text).toInclude('[DataCorePlugin.GameData.CarSettings_RPMShiftLight1]');
    expect(text).toInclude('[DataCorePlugin.GameData.CarSettings_RPMShiftLight2]');
    // All four are read: First, Shift and Last are the band boundaries, and Blink is the over-rev
    // flash the digit shares with the bar. The test below is the one that pins the fourth.
    for (const property of Object.values(SHIFT_RPM_PROPERTIES)) expect(text).toInclude(`[${property}]`);
  });

  test('the bands the gear uses are the ones the rev bar segments light at', () => {
    // The real invariant, and the one ADR 0014 turns on: a band is entered at *exactly* the
    // expression the rev bar's first segment of that stage lights at — under both ladders, because
    // since XOR-230 the bar picks between them per frame and the digit has to pick the same way.
    // String equality rather than a shared colour token: a colour in common would still pass with
    // the two sides reading different properties, which is what this test was doing before.
    const raisedOf = (id: string): string => shiftBands().find((b) => b.id === id)?.raised ?? '';
    const stages = [
      { stage: 0, id: 'stage1' },
      { stage: 1, id: 'stage2' },
      { stage: 2, id: 'redline' },
    ] as const;
    for (const { stage, id } of stages) {
      // Segment 0, 5 and 10 of fifteen are the first segments of the three stages.
      const first = revSegmentOptions(stage * 5, 15);
      const raised = raisedOf(id);
      // The two halves are the bar's own two layers, chosen by the bar's own test.
      expect({ id, raised }).toEqual({ id, raised: eitherLadder(litOf(first.shift.colorBind), litOf(first.simhub.colorBind)) });
      expect(raised).toInclude(mirrorAvailable());
    }
    // A band the digit shows is a band the bar is in: nothing here is the plain RPM bar's.
    for (const { id } of stages) expect(raisedOf(id)).not.toInclude('CurrentDisplayedRPMPercent');
  });

  test("the digit flashes on the bar's over-rev threshold, not merely on the top band", () => {
    // XOR-233's review, and the defect ADR 0014 says cannot exist. The digit used to flash the
    // moment the top band was entered -- `Rpms >= LastRPM` -- while the bar's top band flashes at
    // `max(BlinkRPM, LastRPM)` and stops in the last gear. On a rig with a box and a screen the
    // digit strobed against a solid bar, and went on strobing in top gear where the bar
    // deliberately does not.
    //
    // String equality against the expressions both sides actually emit. A shared colour token
    // would pass with the two reading different properties, which is how this survived the test
    // above it.
    const top = revSegmentOptions(14, 15);
    const bar = eitherLadder(top.shift.blinkBind ?? '', top.simhub.blinkBind ?? '');

    const overRev = all.find((c) => c.description === 'Gear redline over-rev');
    const steady = all.find((c) => c.description === 'Gear redline steady');
    expect(overRev?.kind).toBe('when');
    expect(steady?.kind).toBe('when');
    const flash = overRev?.kind === 'when' ? String(overRev.formula) : '';
    expect(flash).toBe(bar);
    // And the two halves partition the band, so there is no RPM at which the digit is neither.
    expect(steady?.kind === 'when' ? String(steady.formula) : '').toBe(`!(${bar})`);

    // The two properties whose absence was the bug: the over-rev RPM, and the gear count the last
    // gear is found from. Neither appeared anywhere in this profile before.
    const text = serializeProfile(profile);
    expect(flash).toInclude(SHIFT_RPM_PROPERTIES.blink);
    expect(flash).toInclude(GEAR_COUNT_PROPERTY);
    expect(text).toInclude(flash);
    // Entering the top band is a different question from flashing in it, and stays one.
    expect(flash).not.toBe(shiftBands().find((b) => b.id === 'redline')?.raised);
  });

  test('the digit is steady in the last gear on the fallback ladder as well as on the car own', () => {
    // The defect the pin above could not see. It compares the digit's string with the bar's, so
    // one wrong expression on both sides agrees with itself and passes; the last-gear exception sat
    // on the mirror half only, and a car publishing zeros for its four RPMs is on the other half by
    // construction -- `mirrorAvailable` asks for a first light above zero. So this asks the
    // expression what it answers, frame by frame, rather than what it is spelled like.
    const overRev = all.find((c) => c.description === 'Gear redline over-rev');
    const flash = overRev?.kind === 'when' ? String(overRev.formula) : '';

    const zeros = Object.fromEntries(Object.values(SHIFT_RPM_PROPERTIES).map((p) => [p, 0]));
    const frame = (props: Record<string, number>): Record<string, number> => ({ ...zeros, [GEAR_COUNT_PROPERTY]: 6, ...props });
    const GEAR = 'DataCorePlugin.GameRawData.Telemetry.Gear';
    const RPMS = 'DataCorePlugin.GameData.Rpms';
    const REDLINE_REACHED = 'DataCorePlugin.GameData.CarSettings_RPMRedLineReached';
    const ladder = { [SHIFT_RPM_PROPERTIES.first]: 6000, [SHIFT_RPM_PROPERTIES.shift]: 7000, [SHIFT_RPM_PROPERTIES.last]: 7500, [SHIFT_RPM_PROPERTIES.blink]: 7800 };

    // The four RPMs at zero is the fallback, which is the half the guard was missing.
    const fallback = frame({ [REDLINE_REACHED]: 1, [GEAR]: 5 });
    expect(evaluateShift(mirrorAvailable(), fallback)).toBe(false);
    expect(evaluateShift(flash, fallback)).toBe(true);
    expect(evaluateShift(flash, { ...fallback, [GEAR]: 6 })).toBe(false);
    // Off redline it is steady whatever the gear, so the guard has not swallowed the flash itself.
    expect(evaluateShift(flash, { ...fallback, [REDLINE_REACHED]: 0 })).toBe(false);

    // And the car's own ladder, which behaved, goes on behaving: over the blink RPM and below the
    // last gear it flashes, and in the last gear it does not.
    const own = frame({ ...ladder, [RPMS]: 7900, [GEAR]: 5 });
    expect(evaluateShift(mirrorAvailable(), own)).toBe(true);
    expect(evaluateShift(flash, own)).toBe(true);
    expect(evaluateShift(flash, { ...own, [GEAR]: 6 })).toBe(false);
    // A car that declares no gear count keeps flashing, which is what `lastGear` promises.
    expect(evaluateShift(flash, { ...own, [GEAR]: 6, [GEAR_COUNT_PROPERTY]: 0 })).toBe(true);

    // One expression rather than three: the strip's fallback over-rev is the digit's, character
    // for character, and the bar's is pinned against the digit by the test above.
    expect(revSegmentOptions(14, 15).simhub.blinkBind).toBe(overRevStrip('simhub'));
  });

  test('the flashing glyphs are the ones under the over-rev condition', () => {
    // The blink is a condition on the matrix rather than a binding on a glyph, because an
    // AnimationContainer just loops its frames. So the pin above is only worth something if the
    // two-frame glyphs are the ones inside that group and the one-frame glyphs are the others.
    const framesUnder = (description: string): number[] => {
      const group = all.find((c) => c.description === description);
      return [...walkContainers(group ? [group] : [])].filter((c) => c.kind === 'animation').map((c) => (c.kind === 'animation' ? c.frames.length : 0));
    };
    expect(new Set(framesUnder('Gear redline over-rev'))).toEqual(new Set([2]));
    expect(new Set(framesUnder('Gear redline steady'))).toEqual(new Set([1]));
    // Every other band is steady throughout, which is what `blink: null` means.
    for (const band of shiftBands().filter((b) => b.id !== 'redline')) {
      expect({ band: band.id, blink: band.blink }).toEqual({ band: band.id, blink: null });
      expect(new Set(framesUnder(`Gear ${band.id}`))).toEqual(new Set([1]));
    }
  });

  test('the redline band blinks the digit rather than filling the panel', () => {
    // A filled panel is a flag's vocabulary, and the box has to keep the two apart.
    const redline = all.find((c) => c.description === 'Gear redline');
    expect(redline).toBeDefined();
    const glyph = [...walkContainers(redline ? [redline] : [])].find((c) => c.kind === 'animation');
    expect(glyph?.kind === 'animation' ? glyph.frames : []).toHaveLength(2);
    const lit = glyph?.kind === 'animation' ? (glyph.frames[0]?.pixels.flat().filter((x) => x !== null) ?? []) : [];
    expect(lit.length).toBeLessThan(64);
    expect(new Set(lit)).toEqual(new Set([ds.purpose.shift.stage3]));
  });

  test('every flag outranks it, so the panel is never two things at once', () => {
    // With any flag raised the resting condition is false, whichever way the switch is set.
    for (const condition of FLAG_CATALOGUE) {
      expect(evaluate(noFlagShowing(), condition.bits, false)).toBe(false);
      if (condition.critical) expect(evaluate(noFlagShowing(), condition.bits, true)).toBe(false);
    }
    expect(evaluate(noFlagShowing(), [], false)).toBe(true);
  });

  test('a flag the switch has silenced stops suppressing the gear', () => {
    // The chequer is not critical: with the switch on it is not shown, so it must not hold the
    // panel dark either.
    expect(evaluate(noFlagShowing(), ['checkered'], false)).toBe(false);
    expect(evaluate(noFlagShowing(), ['checkered'], true)).toBe(true);
  });

  test('the gear switch is a contract property, and off means dark', () => {
    expect(flagBoxProperties()).toContain('OpenDash.FlagBoxGear');
    const text = serializeProfile(profile);
    expect(text).toInclude('isnull([OpenDash.FlagBoxGear], true)');
    // Off leaves the panel dark rather than showing something else: the switch is the gear
    // group's own condition, and there is no sibling to take its place.
    const gear = all.find((c) => c.description === 'Gear');
    expect(gear?.kind).toBe('when');
    const resting = all.find((c) => c.description === 'Resting');
    expect(resting && 'children' in resting ? resting.children.map((c) => c.description) : []).toEqual(['Gear']);
  });

  test('it is written once per matrix, and no more often than that', () => {
    // Once per matrix is forced: SimHub has no way to bind which matrix a container paints, and a
    // group's StartPositionMatrix is a static offset. Twice per matrix would not be.
    expect(all.filter((c) => c.description === 'Gear')).toHaveLength(FLAG_BOX_MATRICES.length);
  });
});

describe('the four matrix contents', () => {
  test('no container carries a DeviceKind, which SimHub would drop anyway', () => {
    expect(serializeProfile(profile)).not.toInclude('DeviceKind');
  });

  test('each matrix has a group of its own, offset onto it', () => {
    // A group's StartPositionMatrix is an offset applied when its children's results are merged,
    // so the subtree below stays at matrix 1 and the group shifts it.
    const groups = all.filter((c) => c.description?.startsWith('Matrix '));
    expect(groups.map((c) => c.matrix)).toEqual([...FLAG_BOX_MATRICES]);
    for (const container of all) {
      if (container.description?.startsWith('Matrix ')) continue;
      expect(container.matrix ?? 1).toBe(1);
    }
  });

  test('a matrix nobody has switched on costs one expression, not a hundred', () => {
    // ConditionnalGroupContainer does not descend when it is false, so the gate is the whole cost.
    const second = all.find((c) => c.description === 'Matrix 2');
    expect(second?.kind).toBe('when');
    const formula = (second as Extract<MatrixContainer, { kind: 'when' }>).formula;
    const text = typeof formula === 'string' ? formula : formula.expression;
    expect(text).toInclude('OpenDash.FlagBoxMatrix2Flags');
    expect(text).toInclude('OpenDash.FlagBoxMatrix2Rest');
  });

  test('the defaults are a working single-box setup: matrix 1 does everything, 2 to 4 are off', () => {
    expect(FLAG_BOX_MATRIX_DEFAULTS[1]).toEqual({ rest: 'gear', flags: true, pit: true, spotter: true, warnings: true, side: 'both' });
    for (const matrix of [2, 3, 4] as const) {
      expect(FLAG_BOX_MATRIX_DEFAULTS[matrix]).toEqual({ rest: 'dark', flags: false, pit: false, spotter: false, warnings: false, side: 'both' });
    }
  });

  test('every per-matrix setting is declared and defaulted', () => {
    for (const matrix of FLAG_BOX_MATRICES) {
      for (const name of flagBoxMatrixProperties(matrix)) {
        expect(declaredProperties()).toContain(`OpenDash.${name}`);
        expect(serializeProfile(profile)).toInclude(`isnull([OpenDash.${name}],`);
      }
    }
  });

  test('rotation and serpentine are not ours, so they are nowhere in the settings', () => {
    // They are SimHub device settings decided by the corner the data cable enters; a second place
    // to set them is a second place to disagree.
    for (const name of declaredProperties()) {
      expect(name.toLowerCase()).not.toInclude('rotation');
      expect(name.toLowerCase()).not.toInclude('serpentine');
    }
  });
});

describe('the pit family, the spotter and the warnings', () => {
  test('the limiter in the lane and out of it differ in shape, not only in colour', () => {
    // XOR-78: meaning cannot rest on colour alone, and these two share purpose.pitLimiter.
    const inLane = pitStates().find((s) => s.id === 'limiterInLane');
    const out = pitStates().find((s) => s.id === 'limiterOutOfLane');
    expect(inLane?.grid).not.toEqual(out?.grid);
    // Both are held, so the shape is the whole of the difference and the assertion above is the
    // only thing standing between them.
    expect(inLane?.blink).toBe(false);
    expect(out?.blink).toBe(false);
    // Neither is a filled panel: purpose.pitLimiter is pure white, so a filled one would be the
    // white flag. The frame says "contained"; the exclamation mark says "stop".
    for (const state of [inLane, out]) {
      const lit = (state?.grid ?? []).join('').split('').filter((c) => c === 'P').length;
      expect(lit).toBeGreaterThan(0);
      expect(lit).toBeLessThan(64);
    }
  });

  test('speeding outranks both, and is a different picture again', () => {
    expect(pitStates()[0]?.id).toBe('speeding');
    expect(pitStates()[0]?.grid).not.toEqual(pitStates()[1]?.grid);
  });

  test('nothing below the flags moves, because movement is reserved for a flag that interrupts the race', () => {
    // A limiter left on and a temperature climbing are conditions a driver lives with for minutes,
    // so a picture that strobed for those minutes would spend the box's only attention signal on
    // the states least able to give it back.
    for (const state of [...pitStates(), ...spotterStates(1), ...warningStates()]) {
      expect({ state: state.id, blink: state.blink }).toEqual({ state: state.id, blink: false });
    }
  });

  test('speeding is a comparison of two published numbers, not state between frames', () => {
    const text = serializeProfile(profile);
    expect(text).toInclude('[DataCorePlugin.GameData.PitLimiterSpeedMs]');
    expect(text).toInclude('[DataCorePlugin.GameData.SpeedKmh]');
    expect(text).toInclude('[DataCorePlugin.GameData.IsInPitLane]');
  });

  test('a box knows which side of the rig it is on', () => {
    // A box to the left of the wheel lighting for a car on the right is worse than no box.
    const left = spotterStates(2).find((s) => s.id === 'carLeft');
    const text = typeof left?.raised === 'string' ? left.raised : '';
    expect(text).toInclude('OpenDash.FlagBoxMatrix2Side');
    expect(spotterStates(1).find((s) => s.id === 'carLeft')?.grid).toEqual(CAR_LEFT);
  });

  test('a single box shows both sides, on the edge each is actually on', () => {
    expect(CAR_LEFT[0]).toStartWith('WW');
    expect(CAR_RIGHT[0]).toEndWith('WW');
    expect(CAR_BOTH[0]).toBe('WW....WW');
  });

  test('the warnings sit below the flags, so a low fuel light cannot hide a yellow', () => {
    // The failure this ordering exists to prevent, and the one that gets somebody hurt.
    const warnings = all.find((c) => c.description === 'Warnings');
    expect(warnings).toBeDefined();
    const below = all.find((c) => c.description === 'Below the flags');
    const ids = below && 'children' in below ? [...walkContainers(below.children)].map((c) => c.description) : [];
    expect(ids).toContain('Warnings');
    const formula = (below as Extract<MatrixContainer, { kind: 'when' }>).formula;
    const text = typeof formula === 'string' ? formula : formula.expression;
    for (const condition of FLAG_CATALOGUE) expect(text).toInclude(condition.bits[0] ?? '');
  });

  test('the pit family has its own switch, not the flags\' one', () => {
    // A driver who turns flags off on a panel has not asked to lose the pit limiter warning.
    const pit = all.find((c) => c.description === 'Pit');
    const formula = (pit as Extract<MatrixContainer, { kind: 'when' }>).formula;
    const text = typeof formula === 'string' ? formula : formula.expression;
    expect(text).toInclude('FlagBoxMatrix1Pit');
    expect(text).not.toInclude('FlagBoxMatrix1Flags');
  });

  test('a panel that does not show flags is not blacked out by one', () => {
    // "Below the flags" used to be gated on the flag conditions alone, so a live flag suppressed the
    // spotter, the warnings and the gear even on a panel with Flags switched off.
    const below = all.find((c) => c.description === 'Below the flags');
    const formula = (below as Extract<MatrixContainer, { kind: 'when' }>).formula;
    const text = typeof formula === 'string' ? formula : formula.expression;
    expect(text).toInclude('FlagBoxMatrix1Flags');
  });

  test('speeding is compared in one unit, and an unpublished limit never fires', () => {
    // PitLimiterSpeed is converted to the user's local speed unit, so comparing it with SpeedKmh
    // reads as speeding from a standstill for anyone on MPH -- and that term gates everything below
    // the flags, so it would black out the panel rather than merely light the wrong picture.
    const text = serializeProfile(profile);
    expect(text).toInclude('PitLimiterSpeedMs');
    expect(text).not.toInclude('GameData.PitLimiterSpeed]');
    expect(text).toInclude('isnull([DataCorePlugin.GameData.PitLimiterSpeedMs], 999)');
  });

  test('the pit family outranks the spotter, which outranks the warnings', () => {
    const below = all.find((c) => c.description === 'Below the flags');
    const order = below && 'children' in below ? below.children.map((c) => c.description) : [];
    expect(order).toEqual(['Pit', 'Spotter', 'Warnings', 'Resting']);
  });

  test('low fuel is measured in laps, because litres mean nothing without the car', () => {
    expect(serializeProfile(profile)).toInclude('isnull([OpenDash.FlagBoxLowFuelLaps], 2)');
    expect(serializeProfile(profile)).toInclude('Fuel_RemainingLaps');
  });

  test('a temperature threshold defaults correctly in every unit SimHub reports', () => {
    // A driver in Fahrenheit who sets 120 and gets a Celsius threshold has a broken feature.
    const text = serializeProfile(profile);
    expect(text).toInclude('[DataCorePlugin.GameData.TemperatureUnit]');
    for (const value of [DEFAULT_OIL_TEMP.Celcius, DEFAULT_OIL_TEMP.Fahrenheit, DEFAULT_OIL_TEMP.Kelvin]) {
      expect(text).toInclude(String(value));
    }
    for (const value of [DEFAULT_WATER_TEMP.Celcius, DEFAULT_WATER_TEMP.Fahrenheit, DEFAULT_WATER_TEMP.Kelvin]) {
      expect(text).toInclude(String(value));
    }
  });

  test('a car that reports no temperature never trips a warning', () => {
    const text = serializeProfile(profile);
    expect(text).toInclude('isnull([DataCorePlugin.GameData.OilTemperature], 0)');
    expect(text).toInclude('isnull([DataCorePlugin.GameData.WaterTemperature], 0)');
  });

  test('no two of these states draw the same picture', () => {
    const grids = [...pitStates(), ...spotterStates(1), ...warningStates()].map((s) => s.grid.join('|'));
    expect(new Set(grids).size).toBe(grids.length);
  });

  test('nothing anywhere in the profile draws the same picture as anything else', () => {
    // Across the families, not only within one. Two things this caught: the limiter in the lane was
    // the black flag's outline in another colour, and the limiter out of the lane was a filled
    // panel in purpose.pitLimiter -- which is pure white, so it was the white flag with a blink.
    // Telling a driver "last lap" when you mean "your limiter is on" is what XOR-78 is about.
    const seen = new Map<string, string>();
    for (const container of all) {
      if (container.kind !== 'animation' || container.description === undefined) continue;
      const picture = JSON.stringify(container.frames.map((f) => f.pixels));
      const name = container.description.replace(/ glyph$/, '');
      const clash = seen.get(picture);
      expect({ name, clash: clash === name ? undefined : clash }).toEqual({ name, clash: undefined });
      seen.set(picture, name);
    }
  });

  test('a solid flag is told apart by colour, which is what a flag is', () => {
    // The counterpart of the test above: yellow, blue, white and green are the same shape on
    // purpose, and red is that shape again once it has finished growing. A racing flag *is* a
    // colour, and inventing a pattern for each would be worse. Five now rather than four, because
    // blue lost its arrow and red gained the three frames it grows through.
    const shapeOf = (pixels: readonly (readonly (Hex | null)[])[] = []): string => pixels.map((r) => r.map((c) => (c === null ? '.' : '#')).join('')).join('|');
    const held = ['yellow', 'blue', 'white', 'green'];
    const panels = [...held.map((id) => flagFrames(id)?.[0]?.pixels), flagFrames('red')?.at(-1)?.pixels];
    expect(panels).toHaveLength(5);
    expect(new Set(panels.map((p) => shapeOf(p))).size).toBe(1);
    expect(new Set(panels.map((p) => p?.[0]?.[0])).size).toBe(5);
  });

  test('the same glyph on matrix 2 differs only in StartPositionMatrix', () => {
    const glyph: MatrixContainer = { kind: 'animation', description: 'g', frames: flagFrames('green') ?? [] };
    const a = buildContainerObject(glyph, 'p/0', 'matrix8x8');
    const b = buildContainerObject({ ...glyph, matrix: 2 }, 'p/0', 'matrix8x8');
    expect(b.StartPositionMatrix).toBe(2);
    expect(JSON.stringify({ ...b, StartPositionMatrix: 1 })).toBe(JSON.stringify(a));
  });
});

describe('the emitted file', () => {
  // Not the JSON. The tree is written once per matrix, so the file is three quarters of a megabyte
  // and a snapshot of it could not be read, which is the only thing a snapshot is for. These two
  // are what a reviewer actually needs: the shape, and the pictures.

  test('the tree', () => {
    const lines: string[] = [];
    const walk = (containers: readonly MatrixContainer[], depth: number): void => {
      for (const c of containers) {
        const matrix = c.matrix !== undefined && c.matrix !== 1 ? ` -> matrix ${c.matrix}` : '';
        lines.push(`${'  '.repeat(depth)}${c.kind} ${c.description ?? ''}${matrix}`.trimEnd());
        walk('children' in c ? c.children : [], depth + 1);
      }
    };
    walk(profile.containers, 0);
    expect(lines.join('\n')).toMatchSnapshot();
  });

  test('the glyphs', () => {
    // Every distinct picture as a grid, which is how they were drawn and the only way to review
    // them. A changed snapshot here is a changed picture: look at it.
    const ink = new Map<string, string>();
    const key = (c: string | null): string => {
      if (c === null) return '.';
      if (!ink.has(c)) ink.set(c, 'ABCDEFGHIJKLMNOP'[ink.size] ?? '?');
      return ink.get(c) ?? '?';
    };
    const seen = new Set<string>();
    const blocks: string[] = [];
    for (const container of all) {
      if (container.kind !== 'animation' || container.description === undefined) continue;
      const name = container.description.replace(/ glyph$/, '');
      if (seen.has(name)) continue;
      seen.add(name);
      const frames = container.frames.map((f) => f.pixels.map((row) => row.map(key).join('')).join('\n'));
      blocks.push(`${name}\n${frames.join('\n  --\n')}`);
    }
    const legend = [...ink].map(([colour, ch]) => `${ch} ${colour}`).join('\n');
    expect(`${blocks.join('\n\n')}\n\nlegend\n${legend}`).toMatchSnapshot();
  });
});
