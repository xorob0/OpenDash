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
import { FLAG_PRIORITY } from '../src/components/flagStrip.ts';
import { declaredProperties, flagBoxProperties, PROPERTY_PREFIX } from '../src/contract.ts';
import { FACE_FLAG_PRIORITY, FLAG_CATALOGUE, flagBit, type SessionFlagBit } from '../src/flags.ts';
import { buildContainerObject, serializeProfile, validateProfile, walkContainers, type MatrixContainer } from '../src/generator.ts';
import { revSegmentOptions, shiftBands } from '../src/components/revSegments.ts';
import { GEARS, gearGrid } from '../src/leds/gear.ts';
import { flagFrames, FLAG_PALETTE, ignitionOffFrames, STANDBY_PALETTE } from '../src/leds/glyphs.ts';
import { buildFlagBoxProfile, drawnFlags, flagBoxTree, flagContainers, pruneEmpty, restingCondition } from '../src/leds/profile.ts';
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
  const lit = flagContainers(criticalOnly).filter((c) => {
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
  test('the face ranks from the catalogue rather than its own copy', () => {
    expect(FLAG_PRIORITY).toEqual(FACE_FLAG_PRIORITY);
    expect(FACE_FLAG_PRIORITY).toEqual(['Flag_Black', 'Flag_Checkered', 'Flag_Yellow', 'Flag_Blue', 'Flag_White', 'Flag_Green']);
  });

  test('the box draws the catalogue in that same order', () => {
    const drawn = flagContainers(false).map((c) => c.description);
    expect(drawn).toEqual(drawnFlags(false).map((c) => c.id));
    // The face's six keep their relative rank inside the longer list, or the two would disagree.
    const faceIds = ['black', 'chequered', 'yellow', 'blue', 'white', 'green'];
    expect(drawn.filter((d) => faceIds.includes(d ?? ''))).toEqual(faceIds);
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
    { name: 'the chequer while being lapped', bits: ['checkered', 'blue'], expect: 'chequered' },
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
    // The chequer is not critical. With the switch on and the chequer out over a blue flag, the
    // box shows blue; a naive implementation shows nothing, because the chequer still wins and
    // then draws nothing.
    expect(shown(['checkered', 'blue'], false)).toBe('chequered');
    expect(shown(['checkered', 'blue'], true)).toBe('blue');
  });

  test('both branches are in the file, so the switch needs no rebuild', () => {
    const text = serializeProfile(profile);
    expect(text).toInclude('"Description": "Critical flags only"');
    expect(text).toInclude('"Description": "Every flag"');
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
    const black = flagFrames('black')?.[0]?.pixels ?? [];
    expect(black[0]?.every((p) => p !== null)).toBe(true);
    expect(black[3]?.slice(1, 7).every((p) => p === null)).toBe(true);
    const dq = flagFrames('disqualify')?.[0]?.pixels ?? [];
    expect(dq[0]?.[0]).toBe(ds.purpose.flag.black);
    expect(dq[0]?.[3]).toBeNull();
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

  test('a full-course caution is banded, so it never looks like a local yellow', () => {
    const caution = flagFrames('caution')?.[0]?.pixels ?? [];
    const yellow = flagFrames('yellow')?.[0]?.pixels ?? [];
    expect(caution).not.toEqual(yellow);
    expect(caution[2]?.every((p) => p === null)).toBe(true);
  });

  test('blue moves: two frames whose arrows are in different places', () => {
    const blue = flagFrames('blue') ?? [];
    expect(blue).toHaveLength(2);
    expect(blue[0]?.pixels).not.toEqual(blue[1]?.pixels);
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
    expect(flagBoxProperties()).toEqual([
      'OpenDash.LightsBrightness',
      'OpenDash.LightsNightBrightness',
      'OpenDash.LightsNightMode',
      'OpenDash.FlagBoxCriticalOnly',
      'OpenDash.FlagBoxGear',
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
    // One relationship learned once and read in two places. When XOR-230 replaces SimHub's
    // per-car bands with the sim's own DriverCarSL* values, it changes shiftBands() alone.
    const bands = shiftBands();
    expect(bands.map((b) => b.id)).toEqual(['redline', 'stage2', 'stage1', 'rest']);
    expect(bands.map((b) => b.colour)).toEqual([ds.purpose.shift.stage3, ds.purpose.shift.stage2, ds.purpose.shift.stage1, ds.color.text.primary]);
    const text = serializeProfile(profile);
    expect(text).toInclude('[DataCorePlugin.GameData.CarSettings_RPMRedLineReached]');
    expect(text).toInclude('[DataCorePlugin.GameData.CarSettings_RPMShiftLight1]');
    expect(text).toInclude('[DataCorePlugin.GameData.CarSettings_RPMShiftLight2]');
  });

  test('the bands the gear uses are the ones the rev bar segments light at', () => {
    // revSegmentOptions lights the first segment of stage 0 when RPMShiftLight1 leaves zero and
    // the first of stage 1 when RPMShiftLight2 does; shiftBands says the same thing for a digit.
    const bands = shiftBands();
    const first = revSegmentOptions(0, 15);
    const stage1Entry = bands.find((b) => b.id === 'stage1');
    expect(first.shift.colorBind).toInclude(ds.purpose.shift.stage1);
    expect(stage1Entry?.raised).toInclude('CarSettings_RPMShiftLight1');
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
      expect(evaluate(restingCondition(), condition.bits, false)).toBe(false);
      if (condition.critical) expect(evaluate(restingCondition(), condition.bits, true)).toBe(false);
    }
    expect(evaluate(restingCondition(), [], false)).toBe(true);
  });

  test('a flag the switch has silenced stops suppressing the gear', () => {
    // The chequer is not critical: with the switch on it is not shown, so it must not hold the
    // panel dark either.
    expect(evaluate(restingCondition(), ['checkered'], false)).toBe(false);
    expect(evaluate(restingCondition(), ['checkered'], true)).toBe(true);
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

  test('it is written once, not once per branch of the critical-flags switch', () => {
    expect(all.filter((c) => c.description === 'Gear')).toHaveLength(1);
  });
});

describe('the four matrix contents', () => {
  test('everything ships on matrix 1, a working single-box setup', () => {
    for (const container of all) expect(container.matrix ?? 1).toBe(1);
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
  test('the profile', () => {
    expect(serializeProfile(profile)).toMatchSnapshot();
  });
});
