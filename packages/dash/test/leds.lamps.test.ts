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
import { lampConditions, PIT_EFFECTS, flagEffects } from '../src/leds/effects.ts';
import { lampsForSide, lampsOf } from '../src/leds/lamps.ts';

const profileFor = (shape: StripShape): leds.LedProfile => rpmStripProfile(shape, stableGuid(`test/lamps/${shape.id}`));

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
    const roles = (count: number): string[] => lampsForSide(count).map((l) => l.role);
    expect(roles(5)).toEqual(['side', 'race', 'car', 'aid', 'aid']);
    expect(roles(4)).toEqual(['side', 'race', 'car', 'aid']);
    expect(roles(3)).toEqual(['side', 'race', 'car']);
    expect(roles(2)).toEqual(['side', 'car']);
    expect(roles(0)).toEqual([]);
    // Below four a role shares rather than moves: the lamp keeps its owner and carries the rest
    // beneath it, so a lamp is in the same place on a wheel of three as on a wheel of five.
    expect(lampsForSide(5).map((l) => l.label)).toEqual(['side', 'race', 'car', 'aid', 'second aid']);
    expect(lampsForSide(3)[2]).toMatchObject({ label: 'car and aid', carries: ['car', 'aid'] });
    expect(lampsForSide(2)[1]).toMatchObject({ label: 'car and flag', carries: ['car', 'race'] });
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

  test('a role sits at the same distance from the outside at both ends: on 4/14/4 race is LED 2 and 21, aid is LED 4 and 19', () => {
    const at = (id: string, label: string): number[] =>
      lampsOf(shapeById(id)!)
        .filter((p) => p.lamp.label === label)
        .map((p) => p.position)
        .sort((a, b) => a - b);
    expect(at('4-14-4', 'race')).toEqual([2, 21]);
    expect(at('4-14-4', 'aid')).toEqual([4, 19]);
    expect(at('4-14-4', 'side')).toEqual([1, 22]);
    expect(at('4-14-4', 'car')).toEqual([3, 20]);
    // The five-lamp side reaches one further in; the three- and two-lamp sides stop short.
    expect(at('5-10-5', 'second aid')).toEqual([5, 16]);
    expect(at('3-9-3', 'car and aid')).toEqual([3, 13]);
    expect(at('2-10-2', 'car and flag')).toEqual([2, 13]);
  });

  test('a shared lamp ranks by role, not by catalogue position: every car warning ahead of every aid', () => {
    const shared = lampsForSide(3)[2]!;
    const ids = lampConditions(shared, 'left').map((e) => e.id);
    const car = ['oilPressure', 'waterTemp', 'lowFuel'];
    const aid = ['headlightFlash', 'p2p', 'drs', 'ers', 'tc', 'abs'];
    expect(ids).toEqual([...car, ...aid]);
    // The rank is the order the list is in, highest first, so the last car warning still outranks
    // the first aid.
    expect(Math.max(...car.map((id) => ids.indexOf(id)))).toBeLessThan(Math.min(...aid.map((id) => ids.indexOf(id))));
  });

  test('at two lamps a side the aids are dropped altogether and the car warning outranks the flag', () => {
    const shared = lampsForSide(2)[1]!;
    const ids = lampConditions(shared, 'left').map((e) => e.id);
    expect(ids.slice(0, 3)).toEqual(['oilPressure', 'waterTemp', 'lowFuel']);
    expect(ids.slice(3)).toEqual(flagEffects().map((e) => e.id).reverse());
    // Dropped rather than shadowed: a two-LED side says nothing about ABS rather than saying it
    // where nobody can see it.
    const text = leds.serializeProfile(profileFor(shapeById('2-10-2')!));
    for (const absent of ['ABS active', 'Traction control', 'DRS', 'Push to pass']) {
      expect({ absent, inProfile: text.includes(absent) }).toMatchObject({ inProfile: false });
    }
  });

  test('within a lamp the lowest rank is written first and each is guarded by the ones above it', () => {
    const lamp = lampsForSide(4)[2]!;
    const ranked = lampConditions(lamp, 'left');
    const group = placedOf(profileFor(shapeById('4-14-4')!).containers).find((p) => p.description === 'left car lamp')!;
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

describe('what a lamp is never yielded to', () => {
  test('nothing but the pit family blanks the outermost LED of a side', () => {
    for (const shape of lampsWithSides) {
      const ends = [1, stripLength(shape)];
      const blanking = placedOf(profileFor(shape).containers)
        .filter((p) => clears(p.container))
        .filter((p) => placedOf(leds.childrenOf(p.container), p.start - 1).some(({ start, count }) => count !== undefined && ends.some((e) => e >= start && e <= start + count - 1)));
      expect({ shape: shape.id, blanking: blanking.map((p) => p.description) }).toMatchObject({ blanking: PIT_EFFECTS.map((e) => e.label) });
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
      const tree = leds.childrenOf(shape.reversed ? leds.childrenOf(profileFor(shape).containers[0]!)[0]! : profileFor(shape).containers[0]!);
      // The 3/10/3's two further runs of nine are a different strip on the same device, so they sit
      // outside the main run and outside this ranking.
      const main = tree.filter((c) => (c.startPosition ?? 1) <= stripLength(shape));
      expect({ shape: shape.id, last: main.slice(-PIT_EFFECTS.length).map((c) => c.description) }).toMatchObject({ last: PIT_EFFECTS.map((e) => e.label) });
      const whole = placedOf(profileFor(shape).containers).filter(
        (p) => clears(p.container) && placedOf(leds.childrenOf(p.container), p.start - 1).some((c) => c.count === stripLength(shape)),
      );
      // A brow has no lamps, so its flags keep the whole run as well; every shape with lamps is the
      // pit family alone.
      const expected = lampsOf(shape).length > 0 ? PIT_EFFECTS.map((e) => e.label) : [...flagEffects().map((e) => e.label), ...PIT_EFFECTS.map((e) => e.label)];
      expect({ shape: shape.id, whole: whole.map((p) => p.description) }).toMatchObject({ whole: expected });
    }
  });

  test('speeding stays ranked above the limiter, by its position within the pit family', () => {
    const ids = PIT_EFFECTS.map((e) => e.id);
    expect(ids.indexOf('pit.speeding')).toBeGreaterThan(ids.indexOf('pit.limiter'));
  });
});
