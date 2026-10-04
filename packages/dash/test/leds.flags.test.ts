/**
 * The flag catalogue on the race lamp: which flag a strip shows, where it shows it, and what a
 * driver would read one as if it were drawn like another.
 *
 * The strip used to rank the six normalised `Flag_*` summaries while the box ranked the fifteen
 * conditions of `FLAG_CATALOGUE`, so the two surfaces could be told different things at once by the
 * same telemetry: `Flag_Yellow` folds four iRacing bits together and `Flag_Black` hides a furled
 * black and a disqualification. These tests are about that agreement rather than about appearance,
 * so most of them evaluate the emitted conditions over raised bits instead of reading the rows.
 *
 * `flagBox.test.ts` keeps the box's own ranking, and `FLAG_CATALOGUE` is the list both read.
 */
import { describe, expect, test } from 'bun:test';
import { ncalc, stableGuid, leds } from '../src/generator.ts';
import { ALL_SHAPES, BARE_RUN_LENGTHS, stripLength, type StripShape } from '../src/leds/strip.ts';
import { rpmStripProfile } from '../src/leds/rpmStrip.ts';
import { BLINK_OFF, FAST_BLINK_MS, FLAG_ROWS, SLOW_BLINK_MS, SPOTTER_EFFECTS, flagEffects, flagSpreads } from '../src/leds/effects.ts';
import { FLAG_TAKEOVER_MS } from '../src/components/flagStrip.ts';
import { FLAG_CATALOGUE, flagBit, type FlagCondition, type SessionFlagBit } from '../src/flags.ts';
import { drawnFlags } from '../src/leds/profile.ts';
import { lampsOf } from '../src/leds/lamps.ts';
import { ds } from '../src/tokens.ts';

const profileFor = (shape: StripShape): leds.LedProfile => rpmStripProfile(shape, stableGuid(`test/flags/${shape.id}`));
const textOf = (shape: StripShape): string => leds.serializeProfile(profileFor(shape));

/** Every container with the absolute run it paints, the offsets of its enclosing groups summed. */
interface Placed {
  description: string;
  start: number;
  count?: number;
  container: leds.LedContainer;
}
const placedOf = (cs: readonly leds.LedContainer[], offset = 0): Placed[] =>
  cs.flatMap((c) => {
    const start = offset + (c.startPosition ?? 1);
    return [{ description: c.description ?? '', start, count: leds.ledCountOf(c), container: c }, ...placedOf(leds.childrenOf(c), start - 1)];
  });

/** The null-safe read of one bit, exactly as the rows emit it, for substituting a truth into. */
const safeRead = (bit: SessionFlagBit): string => ncalc.isnull(flagBit(bit), ncalc.num(0));

/**
 * One of these conditions with the named bits raised. Handles exactly what the rows emit —
 * parenthesised `and`, `or`, `!` and `isnull([bit], 0) = 1` — and throws on anything wider rather
 * than guessing, so a change to how the conditions are built fails here instead of passing wrongly.
 */
const evaluate = (expression: string, raised: readonly SessionFlagBit[]): boolean => {
  let s = expression;
  for (const condition of FLAG_CATALOGUE) {
    for (const bit of condition.bits) s = s.split(safeRead(bit)).join(raised.includes(bit) ? '1' : '0');
  }
  s = s
    .replace(/\band\b/g, '&&')
    .replace(/\bor\b/g, '||')
    .replace(/([^!<>=])=([^=])/g, '$1===$2');
  if (!/^[\s()!&|=01]+$/.test(s)) throw new Error(`the condition holds something this cannot evaluate: ${s}`);
  // eslint-disable-next-line no-new-func
  return Boolean(new Function(`return (${s});`)());
};

/** Which flag the strip shows with these bits raised, or undefined when it shows none. */
const shown = (raised: readonly SessionFlagBit[]): string | undefined => {
  const lit = flagEffects().filter((e) => evaluate(e.when, raised));
  // One flag at a time: the ranking is in the conditions themselves rather than in who composes
  // last, which is what lets a brow keep the flags on the whole run without two of them fighting.
  expect({ raised: raised.join(' ') || 'nothing raised', lit: lit.map((e) => e.id).slice(1) }).toMatchObject({ lit: [] });
  return lit[0]?.id;
};

/** Which row draws each catalogue condition, keyed by the condition's id. */
const rowOf = new Map<string, string>(FLAG_ROWS.flatMap((row) => row.conditions.map((id) => [id, `flag.${row.id}`] as const)));

/**
 * The two conditions the strip does not draw: the start gantry, whose ready and set the box draws in
 * outlined green. On a lamp they would be the green flag held, and a start sequence across the side
 * LEDs is its own ticket. The red flag was here until #694 moved low fuel off its red; it is a row
 * now. This list is pinned rather than the gap being left silent.
 */
const UNDRAWN: readonly string[] = ['startSet', 'startReady'];

/** The conditions the strip draws, highest rank first. */
const drawnOnStrip = (): FlagCondition[] => FLAG_CATALOGUE.filter((c) => !UNDRAWN.includes(c.id));

describe('the catalogue reaches the strip', () => {
  test('the rows are the catalogue in its own order, minus the start gantry', () => {
    const covered = flagEffects()
      .slice()
      .reverse()
      .flatMap((e) => FLAG_CATALOGUE.filter((c) => rowOf.get(c.id) === e.id).map((c) => c.id));
    // Highest rank first, every drawn condition once, and in the catalogue's order rather than in
    // an order of the strip's own: this is what makes "the box and the strip cannot disagree" a
    // property of the list rather than an intention.
    expect(covered).toEqual(drawnOnStrip().map((c) => c.id));
    expect(FLAG_CATALOGUE.filter((c) => !covered.includes(c.id)).map((c) => c.id)).toEqual([...UNDRAWN]);
    // The box draws all fifteen, so the two are a gap against it rather than a shared refusal.
    expect(drawnFlags(false).map((c) => c.id)).toEqual(FLAG_CATALOGUE.map((c) => c.id));
  });

  test('every drawn condition reaches every profile, and no undrawn one pretends to', () => {
    const bitsOf = (ids: readonly string[]): SessionFlagBit[] => FLAG_CATALOGUE.filter((c) => ids.includes(c.id)).flatMap((c) => [...c.bits]);
    for (const shape of ALL_SHAPES) {
      const text = textOf(shape);
      for (const bit of bitsOf(drawnOnStrip().map((c) => c.id))) {
        expect({ shape: shape.id, bit, present: text.includes(flagBit(bit)) }).toMatchObject({ present: true });
      }
      for (const bit of bitsOf(UNDRAWN)) {
        expect({ shape: shape.id, bit, present: text.includes(flagBit(bit)) }).toMatchObject({ present: false });
      }
    }
  });

  test('every flag bit is read through isnull, because a CustomStatus answers a throw with on', () => {
    // IsActiveBase catches a throwing expression and returns its default of 1.0, so a bare read on
    // a sim that publishes no SessionFlagsDetails does not leave the lamp dark: it raises every flag
    // at once. The box's when group tolerates the same throw, which is why this is the strip's test.
    for (const shape of ALL_SHAPES) {
      const text = textOf(shape);
      for (const condition of drawnOnStrip()) {
        for (const bit of condition.bits) {
          const bare = text.split(flagBit(bit)).length - 1;
          const wrapped = text.split(safeRead(bit)).length - 1;
          expect({ shape: shape.id, bit, bare, wrapped }).toMatchObject({ bare: wrapped });
        }
      }
    }
  });
});

describe('which flag is out', () => {
  /** Every condition alone, and every pair of them, by the first bit of each. */
  const scenarios = (): SessionFlagBit[][] => [
    [],
    ...FLAG_CATALOGUE.map((c) => [c.bits[0]!]),
    ...FLAG_CATALOGUE.flatMap((a, i) => FLAG_CATALOGUE.slice(i + 1).map((b) => [a.bits[0]!, b.bits[0]!])),
  ];

  test('the strip shows the highest-ranked flag it draws, which is the one the box shows or the next one down', () => {
    for (const raised of scenarios()) {
      // What the box shows: the catalogue's own top raised condition, the switch being off.
      const boxPick = FLAG_CATALOGUE.find((c) => c.bits.some((b) => raised.includes(b)));
      // What the strip shows: the same, skipping the conditions it has no row for. A condition with
      // no row is skipped rather than allowed to hold the lamp dark, which is the rule drawnFlags()
      // already applies to a condition with no glyph.
      const stripPick = drawnOnStrip().find((c) => c.bits.some((b) => raised.includes(b)));
      const at = raised.join(' ') || 'nothing raised';
      expect({ at, shown: shown(raised) }).toMatchObject({ shown: stripPick === undefined ? undefined : rowOf.get(stripPick.id) });
      // ...and the two only ever differ by the start gantry.
      if (boxPick !== undefined && boxPick !== stripPick) expect({ at, skipped: boxPick.id }).toMatchObject({ skipped: expect.stringMatching(/^(startSet|startReady)$/) });
    }
  });

  test('a condition the summaries folded away now reaches the lamp on its own', () => {
    // These six were invisible on a strip: Flag_Black is only the black bit, and Flag_Yellow folds
    // the caution and the waved yellow in with the plain one.
    expect(shown(['disqualify'])).toBe('flag.black');
    expect(shown(['furled'])).toBe('flag.black');
    // The meatball is a row of its own since #694, in the orange of its disc.
    expect(shown(['repair'])).toBe('flag.meatball');
    // The red flag reaches the lamp since #694, and outranks everything: the session is stopped.
    expect(shown(['red'])).toBe('flag.red');
    expect(shown(['red', 'black'])).toBe('flag.red');
    expect(shown(['caution'])).toBe('flag.caution');
    expect(shown(['yellowWaving'])).toBe('flag.yellow');
    expect(shown(['debris'])).toBe('flag.debris');
    // The whole-track caution outranks the corner yellow, and the black family outranks both.
    expect(shown(['caution', 'yellow'])).toBe('flag.caution');
    expect(shown(['black', 'caution'])).toBe('flag.black');
  });
});

describe('where a flag is drawn', () => {
  const sided = ALL_SHAPES.filter((s) => lampsOf(s).length > 0);
  const bare = ALL_SHAPES.filter((s) => s.left === 0 && s.right === 0);
  /** Two or more lamps a side: enough room for the flags to have an LED nothing else wants. */
  const roomy = sided.filter((s) => s.left >= 2);
  const oneLamp = sided.filter((s) => s.left === 1);

  test('on a shape with sides a flag is one LED, second in from each end', () => {
    for (const shape of roomy) {
      const placed = placedOf(profileFor(shape).containers);
      for (const flag of flagEffects()) {
        const drawn = placed.filter((p) => p.description === flag.label);
        expect({ shape: shape.id, flag: flag.id, at: drawn.map((p) => p.start).sort((a, b) => a - b) }).toMatchObject({ at: [2, stripLength(shape) - 1] });
        for (const p of drawn) expect({ shape: shape.id, flag: flag.id, count: p.count }).toMatchObject({ count: 1 });
      }
    }
  });

  test('a side of one carries the flags on its one lamp rather than losing them', () => {
    // A strip with no sides gives the flags the whole run, so a side of one that kept only the
    // outermost role would have *lost* the flags by gaining an LED at each end: the driver who
    // bought a one-lamp wheel would see a car alongside and never a yellow.
    expect(oneLamp.length).toBeGreaterThan(0);
    for (const shape of oneLamp) {
      const placed = placedOf(profileFor(shape).containers);
      for (const flag of flagEffects()) {
        const drawn = placed.filter((p) => p.description === flag.label);
        expect({ shape: shape.id, flag: flag.id, at: drawn.map((p) => p.start).sort((a, b) => a - b) }).toMatchObject({ at: [1, stripLength(shape)] });
      }
    }
  });

  test('the spotter and the flag are never the same LED, so a yellow cannot paint out a car alongside', () => {
    // Except where there is only one lamp a side, which is the one shape that has no second LED to
    // put a flag on; there the ranking on that lamp decides, and the test above is what holds it.
    for (const shape of roomy) {
      const placed = placedOf(profileFor(shape).containers);
      // The lamp drawings alone. A spotter also has a whole-strip copy, which is the driver's own
      // switch and off unless they ask for it, and it covers the run by design; it is excluded by its
      // extent rather than by its name, so a lamp drawing that grew to the whole run would still fail.
      const covers = (labels: readonly string[]): Set<number> => {
        const lit = new Set<number>();
        for (const p of placed.filter((q) => labels.includes(q.description) && q.count !== stripLength(shape))) {
          for (let i = 0; i < (p.count ?? 0); i += 1) lit.add(p.start + i);
        }
        return lit;
      };
      const spotters = covers(SPOTTER_EFFECTS.map((e) => e.label));
      const flags = covers(flagEffects().map((e) => e.label));
      expect({ shape: shape.id, spotters: [...spotters] }).toMatchObject({ spotters: [1, stripLength(shape)] });
      expect({ shape: shape.id, shared: [...flags].filter((i) => spotters.has(i)) }).toMatchObject({ shared: [] });
    }
  });

  test('on a shape with no sides a flag keeps the whole run, as it did before the lamps arrived', () => {
    // The carve-out was a coincidence of `placement: 'all'` rather than a rule anything pinned, so
    // the extent would have changed silently the day the flag placement was reworked. A bare run is a
    // marshal panel rather than a shift cluster, which is why it keeps the whole run — and a brow is
    // a bare run, which is the whole of why the grid needs no idea of a brow.
    for (const centre of BARE_RUN_LENGTHS) expect(bare.map((s) => s.id)).toContain(`0-${centre}-0`);
    expect(bare.every((s) => s.left === 0 && s.right === 0)).toBe(true);
    for (const shape of bare) {
      const placed = placedOf(profileFor(shape).containers);
      for (const flag of flagEffects()) {
        const drawn = placed.filter((p) => p.description === flag.label && p.container.kind === 'customStatus');
        expect({ shape: shape.id, flag: flag.id, drawn: drawn.length }).toMatchObject({ drawn: 1 });
        expect({ shape: shape.id, flag: flag.id, start: drawn[0]!.start, count: drawn[0]!.count }).toMatchObject({ start: 1, count: stripLength(shape) });
      }
    }
  });
});

describe('what one flag looks like beside another', () => {
  const effect = (id: string) => flagEffects().find((e) => e.id === id)!;

  test('the black family and the whole-track yellow take the fast tier and every other flag the flag band rate of 2 Hz', () => {
    // Fast is what is addressed to this car: the black, the disqualification, the furled black and
    // the meatball all say come in, and the debris takes it for the same urgency on the road. The full
    // course yellow takes it to be told from the yellow in one corner, which is the same yellow.
    for (const id of ['flag.black', 'flag.meatball', 'flag.debris', 'flag.caution']) {
      expect({ id, delay: effect(id).blinkDelayMs }).toMatchObject({ delay: FAST_BLINK_MS });
    }
    // The red flag is FIA 3504's 2 Hz, which is also what tells it from oil pressure's 4 Hz red.
    for (const id of ['flag.red', 'flag.chequered', 'flag.yellow', 'flag.blue', 'flag.white', 'flag.green']) {
      expect({ id, delay: effect(id).blinkDelayMs }).toMatchObject({ delay: SLOW_BLINK_MS });
    }
    for (const id of ['black', 'disqualify', 'furled']) expect({ id, row: rowOf.get(id) }).toMatchObject({ row: 'flag.black' });
    // The meatball has a row of its own, in the orange of its disc rather than the black's white.
    expect({ row: rowOf.get('meatball'), color: effect('flag.meatball').color }).toEqual({ row: 'flag.meatball', color: ds.purpose.light.meatball });
  });

  test('the black and the chequer exchange their two colours, which is what antiphase is in this format', () => {
    // Both resolve to #F5F7FA and neither used to blink at all, so one lamp said two things the
    // same way. SimHub fills the run with BlinkingColor while blinking and with Color otherwise, so
    // exchanging the fields is the whole of the phase control this needs.
    const black = effect('flag.black');
    const chequer = effect('flag.chequered');
    expect({ color: black.color, blink: black.blinkColor ?? BLINK_OFF }).toEqual({ color: ds.purpose.flag.black, blink: BLINK_OFF });
    expect({ color: chequer.color, blink: chequer.blinkColor }).toEqual({ color: BLINK_OFF, blink: ds.purpose.flag.chequer });
    expect(black.blinkDelayMs).toBe(FAST_BLINK_MS);
    expect(chequer.blinkDelayMs).toBe(SLOW_BLINK_MS);
  });

  test('the full course yellow is the flag yellow at the fast rate, to say the whole track rather than this corner', () => {
    // It alternated the yellow with the caution amber, and an RGB LED draws the two as one colour, so
    // the lamp a driver saw was a yellow that did not blink. The rate is what an LED can show.
    const caution = effect('flag.caution');
    expect({ color: caution.color, blink: caution.blinkColor ?? BLINK_OFF, delay: caution.blinkDelayMs }).toEqual({
      color: ds.purpose.flag.yellow,
      blink: BLINK_OFF,
      delay: FAST_BLINK_MS,
    });
    expect({ color: effect('flag.yellow').color, delay: effect('flag.yellow').blinkDelayMs }).toEqual({ color: ds.purpose.flag.yellow, delay: SLOW_BLINK_MS });
  });

  test('the debris flag alternates its yellow with its stripes’ red, so the lamp is never the yellow flag', () => {
    // The band draws the stripes and the lamp alternates their two colours, at the fast rate the
    // debris flag already had. The yellow alone at that rate was the yellow flag to a glance (#498).
    const debris = effect('flag.debris');
    expect({ color: debris.color, blink: debris.blinkColor, delay: debris.blinkDelayMs }).toEqual({
      color: ds.purpose.flag.debris,
      blink: ds.purpose.flag.debrisStripe,
      delay: FAST_BLINK_MS,
    });
    expect(debris.blinkWhen).toBe(debris.when);
    expect(new Set([debris.color, debris.blinkColor])).not.toEqual(new Set([effect('flag.yellow').color, BLINK_OFF]));
  });

  test('every flag draws its own token, and the ones with no second colour blink against the ground', () => {
    const drawn: Record<string, string> = {
      'flag.yellow': ds.purpose.flag.yellow,
      'flag.blue': ds.purpose.flag.blue,
      'flag.white': ds.purpose.flag.white,
      'flag.green': ds.purpose.flag.green,
      'flag.red': ds.purpose.flag.red,
    };
    for (const [id, color] of Object.entries(drawn)) {
      const e = effect(id);
      expect({ id, color: e.color, blinkColor: e.blinkColor ?? BLINK_OFF }).toMatchObject({ color, blinkColor: BLINK_OFF });
      // A flag that does not move is a flag the driver stops seeing, and a blink whose two colours
      // are equal has never moved: that was every flag but the yellow, and the yellow's off phase
      // was its own hex.
      expect({ id, blinks: e.blinkWhen !== undefined }).toMatchObject({ blinks: true });
    }
  });
});

describe('how far a flag reaches (#694)', () => {
  const spreadOf = (id: string) => flagSpreads(undefined).find((e) => e.id === `flag.${id}.spread`);
  const customStatusAt = (shape: StripShape, label: string): Placed[] =>
    placedOf(profileFor(shape).containers).filter((p) => p.description === label && p.container.kind === 'customStatus');
  const four = ALL_SHAPES.find((s) => s.id === '4-14-4')!;

  test('the red flag takes every LED of both sides for as long as it is out, and never the centre', () => {
    const red = spreadOf('red')!;
    // Out is enough: no window on it.
    expect(red.when).not.toContain('changed(');
    const at = customStatusAt(four, 'Red flag, spread').map((p) => p.start).sort((a, b) => a - b);
    // Every side LED but the race lamp's, which draws the red flag itself.
    expect(at).toEqual([1, 3, 4, 19, 20, 22]);
    for (const shape of ALL_SHAPES.filter((s) => s.left > 1)) {
      for (const p of customStatusAt(shape, 'Red flag, spread')) {
        const onASide = p.start <= shape.left || p.start > shape.left + shape.centre;
        expect({ shape: shape.id, start: p.start, onASide }).toMatchObject({ onASide: true });
      }
    }
  });

  test('the black family, the meatball, the full course yellow and the waved yellow spread only while they are new', () => {
    for (const id of ['black', 'meatball', 'caution', 'yellow']) {
      const spread = spreadOf(id)!;
      expect({ id, window: spread.when.includes(`changed(${String(FLAG_TAKEOVER_MS)}, `) }).toMatchObject({ window: true });
    }
    // The standing yellow does not: only the waved one is the danger in front of the car now.
    expect(spreadOf('yellow')!.when).toContain('IsyellowWaving');
    // Everything else keeps its one LED, the blue above all, which comes out every lap in multiclass.
    for (const id of ['debris', 'blue', 'white', 'green', 'chequered']) expect({ id, spreads: spreadOf(id) }).toMatchObject({ spreads: undefined });
  });

  test('a flag borrows an idle LED and takes none: the spotter and the car warnings keep theirs, the aids give theirs up', () => {
    const groups = placedOf(profileFor(four).containers).filter((p) => p.description.endsWith(' lamp'));
    const child = (lampLabel: string, label: string): Extract<leds.LedContainer, { kind: 'customStatus' }> =>
      leds.childrenOf(groups.find((g) => g.description === lampLabel)!.container).find((c) => c.description === label)! as Extract<
        leds.LedContainer,
        { kind: 'customStatus' }
      >;
    const spotter = SPOTTER_EFFECTS.find((e) => e.id === 'spotter.left')!;
    // Guarded by the spotter on the outermost LED...
    expect(child('left side lamp', 'Red flag, spread').enabledFormula.expression).toContain('SpotterCarLeft');
    // ...and by oil pressure on the engine's LED...
    expect(child('left engine lamp', 'Red flag, spread').enabledFormula.expression).toContain('EngineWarnings');
    // ...while ABS is guarded by the flag on the brake's LED.
    expect(child('left brake aid lamp', 'ABS active').enabledFormula.expression).toContain('Isred');
    expect(spotter.label).toBe('Car alongside, left');
  });

  test('on a side of two every flag also takes the outer LED while a car warning holds the inner one', () => {
    const two = ALL_SHAPES.find((s) => s.id === '2-10-2')!;
    // The blue flag spreads nowhere else, so its spread here is the overflow alone...
    const blue = customStatusAt(two, 'Blue flag, spread');
    expect(blue.map((p) => p.start).sort((a, b) => a - b)).toEqual([1, stripLength(two)]);
    const when = (blue[0]!.container as Extract<leds.LedContainer, { kind: 'customStatus' }>).enabledFormula.expression;
    expect(when).toContain('LightsLowFuelLaps');
    expect(when).toContain('SpotterCarLeft');
    // ...and on a side of three, where the flag has an LED of its own, it has none.
    expect(customStatusAt(ALL_SHAPES.find((s) => s.id === '3-9-3')!, 'Blue flag, spread')).toEqual([]);
    // A side of one has the flag on its only LED and nothing to borrow.
    expect(customStatusAt(ALL_SHAPES.find((s) => s.id === '1-10-1')!, 'Red flag, spread')).toEqual([]);
  });
});
