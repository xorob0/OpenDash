/**
 * The lamps of a side: where a condition lands, and who outranks whom once it is there.
 *
 * A strip is hardware. A lamp that moves between shapes, or a run wired in the wrong order, is not
 * a layout mistake the driver squints at: it is the wrong light, and it is read at two hundred
 * kilometres an hour. So these tests are about allocation rather than about appearance — one owner
 * per LED, the same role in the same place on every shape that has it, and nothing at an end hidden
 * by something that took its neighbour.
 */
import { describe, expect, test } from 'bun:test';
import { stableGuid, leds } from '../src/generator.ts';
import { ALL_SHAPES, rightStart, shapeById, stripLength, type StripShape } from '../src/leds/strip.ts';
import { rpmStripProfile } from '../src/leds/rpmStrip.ts';
import { ALL_EFFECTS, BLINK_OFF, FAST_BLINK_MS, lampConditions, PIT_EFFECTS, SIDE_EFFECTS, SPOTTER_EFFECTS, TURN_EFFECTS, flagEffects, type LedEffect } from '../src/leds/effects.ts';
import { lampsForSide, lampsOf } from '../src/leds/lamps.ts';
import { ds } from '../src/tokens.ts';

const profileFor = (shape: StripShape): leds.LedProfile => rpmStripProfile(shape, stableGuid(`test/lamps/${shape.id}`));

/**
 * The wrappers a strip profile puts over its tree, none of which decides where anything lands: the
 * remap on a strip wired from the far end, the gate on the sim running, the rig brightness, and the
 * gate on the car being switched on.
 */
const WRAPPERS: ReadonlySet<string> = new Set(['Groups.RemapGroup', 'Groups.GameRunningGroup', 'Groups.BrightnessFormulaGroup']);

/**
 * The ignition gate is a plain conditional group, which is also what a lamp and the over-rev layer
 * are, so it is recognised by what it says rather than by its type: matching the type would unwrap
 * the first real painting group of a shape that happened to have only one.
 */
const IGNITION_GATE = 'only while the car is switched on';

const isWrapper = (c: leds.LedContainer): boolean => WRAPPERS.has(leds.containerTypeOf(c)) || c.description === IGNITION_GATE;

/** The containers that actually paint: what is under those wrappers, whichever of them a shape has. */
const drawnTreeOf = (profile: leds.LedProfile): readonly leds.LedContainer[] => {
  let level: readonly leds.LedContainer[] = profile.containers;
  while (level.length === 1 && isWrapper(level[0]!)) level = leds.childrenOf(level[0]!);
  return level;
};

/** Every container of a profile with the absolute run it paints, which is what a fit rule reads. */
interface Placed {
  container: leds.LedContainer;
  description: string;
  /** Absolute 1-based first LED, offsets of the enclosing groups accumulated. */
  start: number;
  count?: number;
}

const placedOf = (cs: readonly leds.LedContainer[], offset = 0): Placed[] =>
  cs.flatMap((c) => {
    const start = offset + (c.startPosition ?? 1);
    const here: Placed = { container: c, description: c.description ?? '', start, count: leds.ledCountOf(c) };
    return [here, ...placedOf(leds.childrenOf(c), start - 1)];
  });

const lampsWithSides = ALL_SHAPES.filter((s) => lampsOf(s).length > 0);
const clears = (c: leds.LedContainer): boolean => c.kind === 'conditionalGroup' && c.clearBackgroundWhenActive === true;

describe('the lamps of a side', () => {
  test('a side is an ordered set of lamps, outermost first, one owner role each', () => {
    const roles = (count: number, side: 'left' | 'right' = 'right'): string[] => lampsForSide(count, side).map((l) => l.role);
    expect(roles(5)).toEqual(['side', 'race', 'car', 'aid', 'aid']);
    // The left of a five has nothing for a fifth LED: its aid is ABS alone, and DRS and push to pass
    // are the throttle's, on the right.
    expect(roles(5, 'left')).toEqual(['side', 'race', 'car', 'aid']);
    expect(roles(4)).toEqual(['side', 'race', 'car', 'aid']);
    expect(roles(3)).toEqual(['side', 'race', 'car']);
    expect(roles(2)).toEqual(['side', 'car']);
    expect(roles(0)).toEqual([]);
    // Below four a role shares rather than moves: the lamp keeps its owner and carries the rest
    // beneath it, so a lamp is in the same place on a wheel of three as on a wheel of five.
    expect(lampsForSide(5, 'right').map((l) => l.label)).toEqual(['side', 'race', 'fuel', 'throttle aid', 'second aid']);
    expect(lampsForSide(4, 'left').map((l) => l.label)).toEqual(['side', 'race', 'engine', 'brake aid']);
    expect(lampsForSide(3, 'left')[2]).toMatchObject({ label: 'car and brake aid', carries: ['car', 'aid'] });
    expect(lampsForSide(3, 'right')[2]).toMatchObject({ label: 'car and throttle aid', carries: ['car', 'aid'] });
    expect(lampsForSide(2, 'left')[1]).toMatchObject({ label: 'car and flag', carries: ['car', 'race'] });
  });

  test('no LED is claimed by two lamps, on any shape', () => {
    for (const shape of ALL_SHAPES) {
      const positions = lampsOf(shape).map((p) => p.position);
      expect({ shape: shape.id, unique: new Set(positions).size }).toMatchObject({ unique: positions.length });
      // ...and every one of them is on the side it belongs to rather than wandering into the centre.
      for (const p of lampsOf(shape)) {
        const onItsSide = p.side === 'left' ? p.position >= 1 && p.position <= shape.left : p.position >= rightStart(shape) && p.position <= stripLength(shape);
        expect({ shape: shape.id, side: p.side, position: p.position, onItsSide }).toMatchObject({ onItsSide: true });
      }
    }
  });

  test('a role sits at the same distance from the outside at both ends: on 4/14/4 race is LED 2 and 21, the aids LED 4 and 19', () => {
    const at = (id: string, label: string): number[] =>
      lampsOf(shapeById(id)!)
        .filter((p) => p.lamp.label === label)
        .map((p) => p.position)
        .sort((a, b) => a - b);
    expect(at('4-14-4', 'race')).toEqual([2, 21]);
    expect(at('4-14-4', 'side')).toEqual([1, 22]);
    // The car's own and the aids are the same distance in at both ends, and split by side: the
    // engine and the brake on the left, the fuel and the throttle on the right.
    expect(at('4-14-4', 'engine')).toEqual([3]);
    expect(at('4-14-4', 'fuel')).toEqual([20]);
    expect(at('4-14-4', 'brake aid')).toEqual([4]);
    expect(at('4-14-4', 'throttle aid')).toEqual([19]);
    // The five-lamp side reaches one further in on the right; the three- and two-lamp sides stop short.
    expect(at('5-10-5', 'second aid')).toEqual([16]);
    expect(at('3-9-3', 'car and brake aid')).toEqual([3]);
    expect(at('3-9-3', 'car and throttle aid')).toEqual([13]);
    expect(at('2-10-2', 'car and flag')).toEqual([2, 13]);
  });

  test('a shared lamp ranks by role, not by catalogue position: every car warning ahead of every aid', () => {
    const car = ['oilPressure', 'temperature', 'lowFuel'];
    for (const [side, aid] of [
      ['left', ['abs']],
      ['right', ['tc', 'drs', 'p2p']],
    ] as const) {
      const ids = lampConditions(lampsForSide(3, side)[2]!, side).map((e) => e.id);
      expect({ side, ids }).toEqual({ side, ids: [...car, ...aid] });
      // The rank is the order the list is in, highest first, so the last car warning still outranks
      // the first aid.
      expect(Math.max(...car.map((id) => ids.indexOf(id)))).toBeLessThan(Math.min(...aid.map((id) => ids.indexOf(id))));
    }
  });

  test('the aids are split by pedal and the car warnings mirrored at three a side, and split by kind at four (#694)', () => {
    const ids = (count: number, side: 'left' | 'right'): string[] =>
      lampsForSide(count, side).flatMap((l) => lampConditions(l, side).filter((e) => e.role === 'car' || e.role === 'aid').map((e) => e.id));
    // ABS is the brake's, so the left's; traction control, DRS and push to pass are the throttle's.
    expect(ids(3, 'left')).toEqual(['oilPressure', 'temperature', 'lowFuel', 'abs']);
    expect(ids(3, 'right')).toEqual(['oilPressure', 'temperature', 'lowFuel', 'tc', 'drs', 'p2p']);
    // At four the car lamp splits too: the engine left, the fuel right, each on one LED of its own.
    expect(ids(4, 'left')).toEqual(['oilPressure', 'temperature', 'abs']);
    expect(ids(4, 'right')).toEqual(['lowFuel', 'tc', 'drs', 'p2p']);
    // Every car warning and every aid is still somewhere on a strip of three or more.
    for (const count of [3, 4, 5]) {
      const all = new Set([...ids(count, 'left'), ...ids(count, 'right')]);
      expect({ count, all: [...all].sort() }).toEqual({ count, all: ['abs', 'drs', 'lowFuel', 'oilPressure', 'p2p', 'tc', 'temperature'] });
    }
  });

  test('at two lamps a side the aids are dropped altogether and the car warning outranks the flag', () => {
    const shared = lampsForSide(2, 'left')[1]!;
    const ids = lampConditions(shared, 'left').map((e) => e.id);
    expect(ids.slice(0, 3)).toEqual(['oilPressure', 'temperature', 'lowFuel']);
    expect(ids.slice(3)).toEqual(flagEffects().map((e) => e.id).reverse());
    // Dropped rather than shadowed: a two-LED side says nothing about ABS rather than saying it
    // where nobody can see it.
    const text = leds.serializeProfile(profileFor(shapeById('2-10-2')!));
    for (const absent of ['ABS active', 'Traction control', 'DRS', 'Push to pass']) {
      expect({ absent, inProfile: text.includes(absent) }).toMatchObject({ inProfile: false });
    }
  });

  test('within a lamp the lowest rank is written first and each is guarded by the ones above it', () => {
    const lamp = lampsForSide(4, 'left')[2]!;
    const ranked = lampConditions(lamp, 'left');
    const group = placedOf(profileFor(shapeById('4-14-4')!).containers).find((p) => p.description === 'left engine lamp')!;
    const children = leds.childrenOf(group.container);
    // Lowest rank first, so SimHub's merge leaves the highest on top...
    expect(children.map((c) => c.description)).toEqual([...ranked].reverse().map((e) => e.label));
    // ...and each also says so, which is the half a reader can check.
    for (const [i, effect] of ranked.entries()) {
      const child = children.find((c) => c.description === effect.label)! as Extract<leds.LedContainer, { kind: 'customStatus' }>;
      for (const higher of ranked.slice(0, i)) expect({ id: effect.id, guarded: child.enabledFormula.expression.includes(`!(${higher.when})`) }).toMatchObject({ guarded: true });
      // The top of a lamp answers to nobody, so it carries no guard at all.
      if (i === 0) expect(child.enabledFormula.expression).toBe(effect.when);
    }
  });
});

describe('what a driver can tell one condition from another by', () => {
  /**
   * What an LED shows of a colour at a glance: a hue family, white, or dark.
   *
   * Hex equality was the rule, and it let the caution amber `#FFB300` sit beside the flag yellow
   * `#FFD400` as two colours, which on an RGB LED they are not: ABS, the spotter and the temperature
   * warning all read as a yellow flag (#694). The families are the hues an LED keeps apart in
   * peripheral vision; a colour belongs to the nearest one, so amber is yellow and only an orange a
   * third of the way to red is orange.
   */
  const family = (hex: string): string => {
    const [r, g, b] = [1, 3, 5].map((i) => Number.parseInt(hex.slice(i, i + 2), 16) / 255) as [number, number, number];
    const max = Math.max(r, g, b);
    const min = Math.min(r, g, b);
    if (max < 0.15) return 'dark';
    if ((max - min) / max < 0.25) return 'white';
    const d = max - min;
    const hue = (max === r ? 60 * (((g - b) / d + 6) % 6) : max === g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4)) % 360;
    const centres: Record<string, number> = { red: 355, orange: 25, yellow: 52, green: 140, cyan: 190, blue: 220, purple: 275 };
    const apart = (a: number, c: number): number => Math.min(Math.abs(a - c), 360 - Math.abs(a - c));
    return Object.entries(centres).reduce((best, [name, c]) => (apart(hue, c) < apart(hue, centres[best]!) ? name : best), 'red');
  };

  /**
   * All a lamp has to say it with: the hue families it shows and a rhythm, a steady light being a
   * rhythm of its own. The two phases of a blink are a set, because a glance does not see phase.
   */
  const appearance = (e: LedEffect): string =>
    e.blinkWhen ? `${[family(e.color), family(e.blinkColor ?? BLINK_OFF)].sort().join('/')} ${String(e.blinkDelayMs)} ms` : `${family(e.color)} steady`;

  test('an LED is told a hue family rather than a hex: the caution amber is the flag yellow, and the orange is not', () => {
    expect(family(ds.color.caution.primary)).toBe(family(ds.purpose.flag.yellow));
    expect(family(ds.purpose.light.abs)).toBe('orange');
    expect(family(ds.purpose.light.spotter)).toBe('purple');
    expect(family(ds.purpose.flag.red)).toBe('red');
    expect(family(ds.purpose.flag.white)).toBe('white');
    expect(family(BLINK_OFF)).toBe('dark');
  });

  test('nothing beside a flag on a strip is drawn in the flag yellow', () => {
    // The yellow is the one colour a driver reads as a flag before reading anything else, so it is
    // the flags' alone: a car alongside, an aid and a car warning are each another family.
    const yellow = family(ds.purpose.flag.yellow);
    for (const e of ALL_EFFECTS().filter((x) => x.role !== 'race')) {
      expect({ id: e.id, family: family(e.color), blink: e.blinkColor === undefined ? undefined : family(e.blinkColor) }).not.toMatchObject({ family: yellow });
    }
  });

  test('no two conditions on one lamp share a hue and a rate, at any side count', () => {
    // One LED drawn the same way by two conditions is one light with two meanings, and the driver
    // reads whichever of them they learned first. The scope is the lamp rather than the strip: the
    // same orange may be ABS on the aid lamp and the meatball on the race lamp, because those are
    // never the same LED on a side long enough to carry the aids.
    //
    // The white flag and the chequer are the single pair that breaks the rule: both are white
    // against dark at 2 Hz, in antiphase, which is a distinction in the file and not at a glance.
    // The chequer pattern across a side's LEDs is what separates them, and is its own ticket; the
    // pair is named here rather than the rule being skipped, so that every other clash still fails.
    const known = ['flag.chequered flag.white'];
    for (const count of [1, 2, 3, 4, 5]) {
      for (const [side, lamp] of (['left', 'right'] as const).flatMap((side) => lampsForSide(count, side).map((l) => [side, l] as const))) {
        const seen = new Map<string, string>();
        for (const e of lampConditions(lamp, side)) {
          const clashed = seen.get(appearance(e));
          const pair = clashed === undefined ? undefined : [clashed, e.id].sort().join(' ');
          expect({ count, lamp: lamp.label, clash: pair !== undefined && known.includes(pair) ? undefined : pair }).toMatchObject({ clash: undefined });
          seen.set(appearance(e), e.id);
        }
      }
    }
  });

  test('ABS intervening: one orange LED at the left end of a 4/14/4, steady, with the ladder untouched', () => {
    const placed = placedOf(profileFor(shapeById('4-14-4')!).containers);
    const abs = placed.filter((p) => p.description === 'ABS active');
    // The brake's side, and only it: traction control is the right's (#694).
    expect(abs.map((p) => p.start).sort((a, b) => a - b)).toEqual([4]);
    expect(placed.filter((p) => p.description === 'Traction control').map((p) => p.start)).toEqual([19]);
    for (const p of abs) {
      const c = p.container as Extract<leds.LedContainer, { kind: 'customStatus' }>;
      expect({ start: p.start, count: p.count, color: c.color, blink: c.blinkFormula }).toMatchObject({
        count: 1,
        color: ds.purpose.light.abs,
        blink: undefined,
      });
    }
    // The lamp it sits in is a plain group, so the rev rungs keep showing underneath it.
    expect(placed.filter((p) => p.description.endsWith('aid lamp')).map((p) => clears(p.container))).toEqual([false, false]);
  });

  test('oil pressure with low fuel: the car lamp is oil pressure and the two beneath it are guarded out', () => {
    // A 3/9/3, where the car's warnings share one lamp on each side; a 4/14/4 puts the fuel on a lamp
    // of its own on the right, where nothing can guard it out.
    const placed = placedOf(profileFor(shapeById('3-9-3')!).containers);
    const oil = placed.filter((p) => p.description === 'Oil pressure warning');
    expect(oil.map((p) => p.start).sort((a, b) => a - b)).toEqual([3, 13]);
    const top = oil[0]!.container as Extract<leds.LedContainer, { kind: 'customStatus' }>;
    expect({ count: oil[0]!.count, color: top.color, delay: top.blinkDelayMs }).toMatchObject({
      count: 1,
      color: ds.purpose.light.oilPressure,
      delay: FAST_BLINK_MS,
    });
    // Rank 1 answers to nobody, so its condition is written bare...
    const oilWhen = ALL_EFFECTS().find((e) => e.id === 'oilPressure')!.when;
    expect(top.enabledFormula.expression).toBe(oilWhen);
    // ...and both rows under it carry its negation, which is what makes the rank a fact about the
    // emitted profile rather than about the order the catalogue happens to be written in.
    for (const label of ['Water or oil temperature warning', 'Low fuel']) {
      const lower = placed.find((p) => p.description === label)!.container as Extract<leds.LedContainer, { kind: 'customStatus' }>;
      expect({ label, guarded: lower.enabledFormula.expression.includes(`!(${oilWhen})`) }).toMatchObject({ guarded: true });
    }
  });
});

describe('what a lamp is never yielded to', () => {
  test('nothing but the pit family, and a car alongside when asked for, blanks the outermost LED of a side', () => {
    // The spotter's whole-strip groups are the driver's own switch and are off unless they ask: the
    // group carries the setting in its trigger, so with it off it never fires and the lamp below is
    // what shows. It is named here rather than the rule being loosened, so that anything else
    // reaching the outermost LED still fails.
    const asked = SPOTTER_EFFECTS.map((e) => `${e.label}, whole strip`);
    for (const shape of lampsWithSides) {
      const ends = [1, stripLength(shape)];
      const blanking = placedOf(profileFor(shape).containers)
        .filter((p) => clears(p.container))
        .filter((p) => placedOf(leds.childrenOf(p.container), p.start - 1).some(({ start, count }) => count !== undefined && ends.some((e) => e >= start && e <= start + count - 1)));
      expect({ shape: shape.id, blanking: blanking.map((p) => p.description) }).toMatchObject({ blanking: [...asked, ...PIT_EFFECTS.map((e) => e.label)] });
      for (const p of blanking.filter((q) => asked.includes(String(q.description)))) {
        const trigger = p.container.kind === 'conditionalGroup' ? p.container.trigger.expression : '';
        expect({ shape: shape.id, gated: trigger.includes('LedSpotterWhole') }).toMatchObject({ gated: true });
      }
    }
  });

  test('a flag takes one LED of each side and never the centre, so the ladder survives every flag', () => {
    for (const shape of lampsWithSides) {
      const race = lampsOf(shape).filter((p) => p.lamp.carries.includes('race'));
      expect({ shape: shape.id, sides: race.length }).toMatchObject({ sides: 2 });
      const placed = placedOf(profileFor(shape).containers);
      for (const flag of flagEffects()) {
        const drawn = placed.filter((p) => p.description === flag.label);
        expect({ shape: shape.id, flag: flag.id, positions: drawn.map((p) => p.start).sort((a, b) => a - b) }).toMatchObject({
          positions: race.map((p) => p.position).sort((a, b) => a - b),
        });
        for (const d of drawn) expect({ shape: shape.id, flag: flag.id, count: d.count }).toMatchObject({ count: 1 });
        // Nothing named after a flag clears what is beneath it any more.
        expect({ shape: shape.id, flag: flag.id, clearing: placed.some((p) => p.description === flag.label && clears(p.container)) }).toMatchObject({ clearing: false });
      }
      // ...and the centre is still there to survive: the rev ladder is emitted whole.
      const text = leds.serializeProfile(profileFor(shape));
      expect({ shape: shape.id, centre: text.includes('centre: rpm') }).toMatchObject({ centre: true });
    }
  });

  test('the pit family is last on every shape, and is the only thing that takes the whole run', () => {
    for (const shape of ALL_SHAPES) {
      const tree = drawnTreeOf(profileFor(shape));
      // The 3/10/3's two further runs of nine are a different strip on the same device, so they sit
      // outside the main run and outside this ranking.
      const main = tree.filter((c) => (c.startPosition ?? 1) <= stripLength(shape));
      // Still last: the spotter's whole-strip groups sit under the pit family, which is the one thing
      // nothing paints over.
      expect({ shape: shape.id, last: main.slice(-PIT_EFFECTS.length).map((c) => c.description) }).toMatchObject({ last: PIT_EFFECTS.map((e) => e.label) });
      const whole = placedOf(profileFor(shape).containers).filter(
        (p) => clears(p.container) && placedOf(leds.childrenOf(p.container), p.start - 1).some((c) => c.count === stripLength(shape)),
      );
      // A bare run has no lamps, so everything a lamp would have carried keeps the whole run: the
      // flags, then what is happening beside the car over them, then the pit family over everything.
      // A shape with lamps has the pit family and the spotter's own switch, which is off unless the
      // driver asks for it.
      const asked = SPOTTER_EFFECTS.map((e) => `${e.label}, whole strip`);
      const beside = [...TURN_EFFECTS, ...SPOTTER_EFFECTS].map((e) => e.label);
      const expected = lampsOf(shape).length > 0
        ? [...asked, ...PIT_EFFECTS.map((e) => e.label)]
        : [...flagEffects().map((e) => e.label), ...beside, ...PIT_EFFECTS.map((e) => e.label)];
      expect({ shape: shape.id, whole: whole.map((p) => p.description) }).toMatchObject({ whole: expected });
    }
  });

  test('speeding stays ranked above the limiter, by its position within the pit family', () => {
    const ids = PIT_EFFECTS.map((e) => e.id);
    expect(ids.indexOf('pit.speeding')).toBeGreaterThan(ids.indexOf('pit.limiter'));
  });
});
