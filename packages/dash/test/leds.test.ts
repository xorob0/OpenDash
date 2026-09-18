/**
 * The LED profiles openDash generates: the strip shapes, the rev ladder and its styles, and the
 * effect catalogue. The rule under most of this is that a light which cannot come on is worse than
 * one that is absent — a dark LED reads as "not happening" rather than "not known" — so several of
 * these tests exist to prove openDash refuses to draw something rather than to prove it draws it.
 */
import { describe, expect, test } from 'bun:test';
import { ncalc, stableGuid, leds } from '../src/generator.ts';
import { MIRROR_COLOR_WIDTH, MIRROR_RUN_LENGTHS, PROPERTY_PREFIX, declaredProperties, flagBox, LED_CENTRES, LED_RPM_STYLES, RETIRED_LED_CENTRE, ledMirrorRunName, propertyName, setting, type LedCentre } from '../src/contract.ts';
import { ALL_SHAPES, BROW_SHAPES, STRIP_SHAPES, centreStart, deviceLength, reversedPositions, rightStart, shapeById, stripLength, type StripShape } from '../src/leds/strip.ts';
import { rpmStripFileName, rpmStripProfile, rpmStripProfileName } from '../src/leds/rpmStrip.ts';
import { bandOf, ladderColors, ladderOrder, overRev, OVER_REV_COLOR } from '../src/leds/ladder.ts';
import { canMirror, carCentre, mirrorRun } from '../src/leds/mirror.ts';
import {
  ALL_EFFECTS,
  BEST_EFFORT,
  BLINK_OFF,
  DROPPED,
  FAST_BLINK_MS,
  NO_PROPERTY,
  PIT_SPEEDING_MARGIN,
  SIDE_EFFECTS,
  SLOW_BLINK_MS,
  SPOTTER_EFFECTS,
  TURN_EFFECTS,
  effectContainer,
  effectContainers,
  flagEffects,
} from '../src/leds/effects.ts';
import { lampsOf } from '../src/leds/lamps.ts';
import { ignitionIsOn } from '../src/leds/gates.ts';
import * as values from '../src/second/values.ts';
import { fuelPercent } from '../src/second/values.ts';
// The flag box's own states, so that "the strip and the box compare the same thing" is asserted
// against the box rather than against a copy of what the box is believed to say.
import { warningStates } from '../src/leds/states.ts';
import { lastGear, SHIFT_RPM_PROPERTIES } from '../src/shift.ts';
import { SHIFT_TABLE, tabledStageLit, tabledOverRev, validateShiftTable, type CarShiftPoints } from '../src/leds/shiftPoints.ts';
import { ds } from '../src/tokens.ts';

const profileFor = (id: string): leds.LedProfile => {
  const shape = shapeById(id);
  if (!shape) throw new Error(`no shape ${id}`);
  return rpmStripProfile(shape, stableGuid(`test/leds/${id}`));
};

/** Every container in a profile, depth first, with the text of every expression it carries. */
const walk = (cs: readonly leds.LedContainer[]): leds.LedContainer[] => cs.flatMap((c) => [c, ...walk(leds.childrenOf(c))]);
const textOf = (p: leds.LedProfile): string => leds.serializeProfile(p);

const descriptionOf = (c: leds.LedContainer): string => String((c as { description?: unknown }).description ?? '');

/**
 * The LEDs one centre function draws on one shape. The first match is the centre itself: the
 * 3/10/3's two further runs repeat the same functions further along the device.
 */
const centreChildren = (shape: StripShape, which: LedCentre): readonly leds.LedContainer[] => {
  const group = walk(rpmStripProfile(shape, stableGuid(`t/centre/${shape.id}`)).containers).find((c) => descriptionOf(c) === `centre: ${which}`);
  if (!group) throw new Error(`no ${which} centre on ${shape.id}`);
  return leds.childrenOf(group);
};

describe('the strip shapes', () => {
  test('every shape has a unique id, a sane geometry and a file name with no path separator in it', () => {
    const ids = ALL_SHAPES.map((s) => s.id);
    expect(new Set(ids).size).toBe(ids.length);
    for (const s of ALL_SHAPES) {
      expect({ id: s.id, ok: s.left >= 0 && s.centre > 0 && s.right >= 0 }).toMatchObject({ ok: true });
      expect(stripLength(s)).toBe(s.left + s.centre + s.right);
      expect(centreStart(s)).toBe(s.left + 1);
      expect(rightStart(s)).toBe(s.left + s.centre + 1);
      // The label may carry slashes (4/14/4); the file name may not, on any platform the build runs on.
      expect(rpmStripFileName(s)).not.toContain('/');
      expect(rpmStripFileName(s)).not.toContain('\\');
    }
  });

  test('the named device families are the shapes people actually own', () => {
    expect(shapeById('3-9-3')?.devices).toContain('Fanatec ClubSport / Podium wheels');
    expect(shapeById('3-9-3-fanalab')?.devices).toContain('Fanatec ClubSport / Podium wheels driven through Fanalab');
    expect(shapeById('4-14-4')?.devices).toContain('SimRep Engineering MLD');
    // The shapes no maker's name attaches to are the ones someone wires themselves.
    for (const id of ['2-10-2', '4-9-4', '5-10-5', '0-8-0', '0-9-0', '0-10-0', '0-12-0', '0-16-0']) expect(shapeById(id)?.devices).toEqual(['generic WS2812b runs']);
    // A row with nothing beside it is a row the panel and the guide cannot describe, so none ships unattributed.
    for (const s of ALL_SHAPES) expect({ id: s.id, attributed: (s.devices?.length ?? 0) > 0 }).toMatchObject({ attributed: true });
    expect(shapeById('3-10-3')?.extraRuns).toEqual({ count: 2, length: 9 });
    // A brow is a strip with no sides, which is why it needs no module of its own.
    for (const b of BROW_SHAPES) expect({ id: b.id, left: b.left, right: b.right, placement: b.placement }).toMatchObject({ left: 0, right: 0, placement: 'brow' });
    expect(BROW_SHAPES.map((b) => b.centre)).toEqual([9, 12, 15, 16, 18, 20, 25]);
  });

  test('an extra run counts towards the device length, so the fit rule measures the whole device', () => {
    expect(deviceLength(shapeById('3-10-3')!)).toBe(16 + 18);
    expect(deviceLength(shapeById('4-14-4')!)).toBe(22);
    expect(stripLength(shapeById('3-10-3')!)).toBe(16);
  });

  test('reversed wiring is a remap, not a second profile', () => {
    expect(reversedPositions(4)).toEqual([4, 3, 2, 1]);
    const p = profileFor('4-14-4-reversed');
    expect(p.containers).toHaveLength(1);
    expect(leds.containerTypeOf(p.containers[0]!)).toBe('Groups.RemapGroup');
    // ...and the un-reversed sibling is the same tree without the wrapper.
    expect(leds.containerTypeOf(profileFor('4-14-4').containers[0]!)).toBe('Groups.GameRunningGroup');
  });

  test('the Fanalab order is a remap too, and it is no reversal of anything', () => {
    const shape = shapeById('3-9-3-fanalab')!;
    // The device presents the nine rev LEDs first, then the right flag LEDs from the outside in,
    // then the left. `positions[i]` is the physical LED logical `i` paints, so the centre's nine
    // land on physical 1..9, the right side's outermost on physical 10 and the left side's on 13.
    expect(shape.positions).toEqual([13, 14, 15, 1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 11, 10]);
    expect(shape.positions).not.toEqual(reversedPositions(15));
    // Every physical LED of the device is painted by exactly one logical one, or a lamp lands nowhere.
    expect(new Set(shape.positions!).size).toBe(deviceLength(shape));

    const p = profileFor('3-9-3-fanalab');
    expect(leds.containerTypeOf(p.containers[0]!)).toBe('Groups.RemapGroup');
    // The plain 3/9/3 keeps its own order: the Simucube, Cammus and Moza wheels of that shape are
    // wired in order, and a driver who installed it must not have it relit underneath them.
    expect(shapeById('3-9-3')?.positions).toBeUndefined();
    expect(leds.containerTypeOf(profileFor('3-9-3').containers[0]!)).toBe('Groups.GameRunningGroup');
  });

  test('only a shape the maker wired in an order of its own is remapped, and it covers every LED of the device', () => {
    // The gate is the shape's own list, so a remap cannot arrive on a strip wired in order: the cost
    // of one there is every lamp in the wrong place, which is the one fault a driver cannot debug.
    expect(ALL_SHAPES.filter((s) => s.positions).map((s) => s.id)).toEqual(['3-9-3-fanalab', '4-14-4-reversed']);
    // SetResultBase indexes Positions[i] for every lit LED, so a list shorter than the run throws
    // once per frame. The validator catches it, and this catches a row that forgot to grow.
    for (const s of ALL_SHAPES.filter((s) => s.positions)) {
      expect({ id: s.id, covered: s.positions!.length }).toMatchObject({ covered: deviceLength(s) });
      expect({ id: s.id, inRange: s.positions!.every((p) => p >= 1 && p <= deviceLength(s)) }).toMatchObject({ inRange: true });
    }
  });
});

describe('the rev ladder and its styles', () => {
  test('leftToRight is one rung per LED; meetInMiddle lights both ends and halves the rungs', () => {
    const l2r = ladderOrder('leftToRight', 14);
    expect(l2r.rungs).toBe(14);
    expect([0, 6, 13].map(l2r.rungOf)).toEqual([0, 6, 13]);

    const mid = ladderOrder('meetInMiddle', 14);
    expect(mid.rungs).toBe(7);
    // The outermost pair is rung 0 and the innermost pair is the last rung: they meet in the middle.
    expect([0, 13].map(mid.rungOf)).toEqual([0, 0]);
    expect([6, 7].map(mid.rungOf)).toEqual([6, 6]);
    // An odd centre gives the middle LED a rung of its own rather than dropping it.
    expect(ladderOrder('meetInMiddle', 9).rungs).toBe(5);
    expect([0, 4, 8].map(ladderOrder('meetInMiddle', 9).rungOf)).toEqual([0, 4, 0]);
  });

  test('f1 is the same order in green and red, and keeps blue for the over-rev', () => {
    expect(ladderOrder('f1', 14).rungOf(5)).toBe(ladderOrder('leftToRight', 14).rungOf(5));
    // This asserted [green, red, blue] and the whole bar flashing under f1 alone, and both halves
    // are reversed on purpose. Blue was the third band's ordinary colour, so it lit on the way up
    // the ladder and could not also be what over-rev means; and the flash is no longer a property
    // of a style at all, so there is nothing here to ask about it.
    expect(ladderColors('f1')).toEqual([ds.color.good.primary, ds.color.good.primary, ds.color.danger.primary]);
    expect(ladderColors('f1')).not.toContain(OVER_REV_COLOR);
    expect(ladderColors('leftToRight')).toEqual([ds.purpose.shift.stage1, ds.purpose.shift.stage2, ds.purpose.shift.stage3]);
  });

  test('the fourteen LEDs are coloured as the sheets draw them: f1 green to the shift point, the others in thirds', () => {
    const letters: Record<string, string> = { [ds.purpose.shift.stage1]: 'G', [ds.purpose.shift.stage2]: 'A', [ds.purpose.shift.stage3]: 'R' };
    const drawn = (style: (typeof LED_RPM_STYLES)[number]): string => {
      const order = ladderOrder(style, 14);
      const colors = ladderColors(style);
      return Array.from({ length: 14 }, (_, k) => letters[colors[bandOf(order.rungOf(k), order.rungs)]!] ?? '?').join('');
    };
    // leftToRight: five green, five amber, four red, so part way is GGGGGA and shift-now GGGGGAAAAAR.
    expect(drawn('leftToRight')).toBe('GGGGGAAAAARRRR');
    // f1: ten green and four red, so part way is six green and shift-now ten green and one red.
    expect(drawn('f1')).toBe('GGGGGGGGGGRRRR');
    // meetInMiddle is the same thirds read inwards from both ends.
    expect(drawn('meetInMiddle')).toBe('GGGAARRRRAAGGG');
  });

  test('the bands are thirds with the last taking the remainder', () => {
    expect([0, 4, 5, 9, 10, 13].map((r) => bandOf(r, 14))).toEqual([0, 0, 1, 1, 2, 2]);
    expect([0, 1, 2].map((r) => bandOf(r, 3))).toEqual([0, 1, 2]);
  });

  test('over the blink RPM the whole bar turns one colour and flashes at 4 Hz, in every style', () => {
    // It was the top band alone in two styles of the three, in that band's own red, and the whole
    // bar only under f1 — where the lower bands alternated their own colour with the top band's,
    // which reads as a bar changing colour rather than as a bar flashing.
    expect(1000 / (FAST_BLINK_MS * 2)).toBe(4);
    const profile = profileFor('4-14-4');
    for (const style of LED_RPM_STYLES) {
      const group = walk(profile.containers).find((c) => c.description === `style: ${style}`)!;
      // `car` keeps its ladders one level further down, as the fallback beside the car's own bar.
      // That bar is not a ladder and is not this test's subject: its colours and its over-rev flash
      // are the fetched table's rather than openDash's, so what is asserted here is the ladder the
      // strip falls back to, which is the same tree the other three styles are.
      const fallback = leds.childrenOf(group).find((c) => c.description?.startsWith('no table for this car'));
      const ladders = style === 'car' ? leds.childrenOf(fallback!) : leds.childrenOf(group);
      for (const ladder of ladders) {
        const children = leds.childrenOf(ladder);
        const last = children[children.length - 1]!;
        // After the rungs, so it takes the bar from whatever they were drawing.
        expect({ style, ladder: ladder.description, last: last.description }).toMatchObject({ last: 'over-rev' });
        const over = leds.childrenOf(last) as Extract<leds.LedContainer, { kind: 'customStatus' }>[];
        expect({ style, ladder: ladder.description, leds: over.length }).toMatchObject({ leds: 14 });
        expect(new Set(over.map((c) => c.color))).toEqual(new Set([OVER_REV_COLOR]));
        expect(new Set(over.map((c) => c.blinkColor))).toEqual(new Set([BLINK_OFF]));
        expect(new Set(over.map((c) => c.blinkDelayMs))).toEqual(new Set([FAST_BLINK_MS]));
        // ...and no rung under it flashes any more, because the flash is the layer.
        const rungs = children.slice(0, -1) as Extract<leds.LedContainer, { kind: 'customStatus' }>[];
        expect({ style, ladder: ladder.description, flashing: rungs.filter((c) => c.blinkFormula !== undefined).length }).toMatchObject({ flashing: 0 });
      }
    }
  });

  test('no over-rev flashes in the last gear, on either ladder or on a measured gear', () => {
    // A flash is an instruction, and in the gear there is nothing to shift out of it asks for a
    // shift that cannot be made. The exception belongs to the flash rather than to one source of
    // thresholds, so all three carry the same negation rather than three readings of one rule.
    const guard = `!(${lastGear()})`;
    const points = { first: 6000, shift: 7000, last: 7500, blink: 7800 };
    for (const [which, expression] of [
      ['mirror', overRev('mirror')],
      ['simhub', overRev('simhub')],
      ['measured', tabledOverRev(points)],
    ] as const) {
      expect({ which, guarded: expression.includes(guard) }).toMatchObject({ guarded: true });
    }
    // ...and every over-rev layer the build emits is triggered by one of those three, measured
    // gears included, so there is nowhere left for a flash without the exception on it.
    const model = 'examplecar';
    SHIFT_TABLE[model] = { name: 'Example', source: 'test', gears: { '3': points } };
    try {
      for (const shape of ALL_SHAPES) {
        const layers = walk(rpmStripProfile(shape, stableGuid(`t/lastgear/${shape.id}`)).containers).filter((c) => c.description === 'over-rev');
        expect({ shape: shape.id, layers: layers.length > 0 }).toMatchObject({ layers: true });
        for (const layer of layers) {
          const trigger = (layer as Extract<leds.LedContainer, { kind: 'conditionalGroup' }>).trigger.expression;
          expect({ shape: shape.id, guarded: trigger.includes(guard) }).toMatchObject({ guarded: true });
        }
      }
    } finally {
      delete SHIFT_TABLE[model];
    }
  });

  test('a style decides the look and never the when: every style reads the same four thresholds', () => {
    const text = textOf(profileFor('4-14-4'));
    for (const style of LED_RPM_STYLES) expect({ style, present: text.includes(`style: ${style}`) }).toMatchObject({ present: true });
    for (const name of Object.values(SHIFT_RPM_PROPERTIES)) expect({ name, present: text.includes(name) }).toMatchObject({ present: true });
  });

  test('both ladders are present, so a car that publishes none still lights', () => {
    const text = textOf(profileFor('3-9-3'));
    expect(text).toContain("the car's own shift lights");
    expect(text).toContain("SimHub's bands, for a car that publishes no ladder");
    // The fallback is null-safe: a bare read lights every LED when the sim is closed, because
    // CustomStatusContainer swallows the throw and falls back to 1.0.
    expect(text).toContain('isnull([DataCorePlugin.GameData.CarSettings_RPMShiftLight1], 0)');
    expect(text).not.toMatch(/"Expression": "[^"]*\[DataCorePlugin\.GameData\.CarSettings_RPMShiftLight1\](?!,)/);
  });
});

describe("the car's own lights", () => {
  test('the plugin publishes a run for every length a shape actually has', () => {
    // The silent failure this closes: a shape whose centre is not in the list would read a property
    // nobody attaches, draw Color.Black on every LED of its mirror layer, and validate cleanly --
    // because the property IS declared, just never for that length. The build refuses instead.
    const needed = new Set<number>();
    for (const s of ALL_SHAPES) {
      needed.add(s.centre);
      if (s.extraRuns) needed.add(s.extraRuns.length);
    }
    for (const length of needed) expect({ length, published: canMirror(length) }).toMatchObject({ published: true });
    // And nothing published that no shape uses, so the list stays a description of the hardware.
    for (const length of MIRROR_RUN_LENGTHS) expect({ length, used: needed.has(length) }).toMatchObject({ used: true });
  });

  test('a run nobody publishes is refused rather than generated', () => {
    expect(() => mirrorRun(13)).toThrow(/MIRROR_RUN_LENGTHS/);
  });

  test('each LED slices its own colour out of the one string the plugin publishes', () => {
    const run = mirrorRun(14);
    expect(run).toHaveLength(14);
    run.forEach((c, k) => {
      expect(leds.containerTypeOf(c)).toBe('DynamicColor');
      // left(value, startIndex, length) -- SimHub's three-argument form, not value-and-length.
      expect((c as { colorFormula: { expression: string } }).colorFormula.expression).toBe(
        `left(isnull([${propertyName(ledMirrorRunName(14))}], ''), ${k * MIRROR_COLOR_WIDTH}, ${MIRROR_COLOR_WIDTH})`,
      );
      // One LED each, in order, so the run the plugin packs is the run the strip draws.
      expect({ start: (c as { startPosition?: number }).startPosition, count: (c as { ledCount: number }).ledCount }).toMatchObject({ start: k + 1, count: 1 });
    });
  });

  test('the fallback is the exact complement of the mirror, so there is never both or neither', () => {
    const [mirror, fallback] = carCentre(9, [{ kind: 'staticColor', ledCount: 9, color: 'Red' }]);
    const ready = (mirror as { trigger: { expression: string } }).trigger.expression;
    expect((fallback as { trigger: { expression: string } }).trigger.expression).toBe(`!(${ready})`);
    // The mirror owns its run: an LED the car does not light is dark, not whatever was underneath.
    expect(mirror).toMatchObject({ clearBackgroundWhenActive: true });
  });

  test('every profile carries the mirror above the two ladders, and the ladders unchanged beneath it', () => {
    for (const shape of ALL_SHAPES) {
      const text = textOf(rpmStripProfile(shape, stableGuid(`t/${shape.id}`)));
      // The car's run, for this shape's centre.
      expect({ shape: shape.id, has: text.includes(propertyName(ledMirrorRunName(shape.centre))) }).toMatchObject({ has: true });
      // And the published ladder still underneath it, because a car with no table loses nothing.
      expect({ shape: shape.id, has: text.includes(SHIFT_RPM_PROPERTIES.first) }).toMatchObject({ has: true });
    }
  });

  test("the car's own bar is what a strip shows unless the driver says otherwise", () => {
    // ADR 0018: openDash's opinion is that the car is right. The three openDash styles stay, for a
    // driver who wants one look in every car -- and for every car with no table, which is what the
    // fallback inside `car` draws.
    expect(LED_RPM_STYLES[0]).toBe('car');
    const text = textOf(profileFor('4-14-4'));
    expect(text).toContain("isnull([OpenDash.LedRpmStyle], 'car')");
    for (const style of LED_RPM_STYLES) expect(text).toContain(`style: ${style}`);
  });
});

describe('the effect catalogue', () => {
  test('every effect names the property it reads, and every one of those is a real SimHub property', () => {
    for (const e of ALL_EFFECTS()) {
      expect({ id: e.id, source: e.source }).toMatchObject({ source: expect.any(String) });
      expect(e.source.length).toBeGreaterThan(0);
      // A source is either a full property path or a stated composition of them.
      expect({ id: e.id, ok: /DataCorePlugin\.|SimHub publishes no/.test(e.source) }).toMatchObject({ ok: true });
    }
  });

  test('the flags are ranked by the catalogue the box ranks, so the two cannot disagree', () => {
    // This asserted the six normalised Flag_* summaries in the face's order, and it is replaced
    // rather than extended: the strip no longer reads them at all. Flag_Yellow folds four iRacing
    // bits together and Flag_Black hides a furled black and a disqualification, so ranking them was
    // ranking a lossy copy of the list the box ranks -- which is how the two could disagree about
    // which flag was out. flagEffects composes highest priority last, which on a strip wins a tie.
    expect(flagEffects().map((e) => e.id)).toEqual([
      // The chequer composes first because it ranks last: the catalogue puts it below every
      // instruction, so a yellow thrown at a race finishing under one is what the lamp shows.
      'flag.chequered',
      'flag.green',
      'flag.white',
      'flag.blue',
      'flag.debris',
      'flag.yellow',
      'flag.caution',
      'flag.black',
    ]);
    const yellow = flagEffects().find((e) => e.id === 'flag.yellow')!;
    expect(yellow.when).toContain('SessionFlagsDetails.Isyellow');
    // ...and it is the catalogue's own ranking, so a black flag still beats a yellow.
    expect(yellow.when).toContain('SessionFlagsDetails.Isblack');
    expect(yellow.blinkWhen).toBeTruthy();
    // Nothing anywhere in a profile reads a Flag_* summary any more.
    for (const shape of ALL_SHAPES) {
      const text = leds.serializeProfile(rpmStripProfile(shape, stableGuid(`t/flags/${shape.id}`)));
      expect({ shape: shape.id, summaries: text.includes('DataCorePlugin.GameData.Flag_') }).toMatchObject({ summaries: false });
    }
    // A flag lives on the race lamp and on nothing else, so none of them takes the strip any more:
    // that is what stopped a blue flag held for a minute from taking the rev ladder with it.
    for (const e of flagEffects()) expect({ id: e.id, role: e.role }).toMatchObject({ role: 'race' });
    // ...and the race lamp carries flags and nothing else ever, which is the other half of it: a
    // lamp that is the flags on one shape and something else on another is the wrong light.
    expect(ALL_EFFECTS().filter((e) => e.role === 'race').map((e) => e.id)).toEqual(flagEffects().map((e) => e.id));
  });

  test('with the flag animation switched off a flag is held, and never held dark', () => {
    // The switch is on the movement rather than on the flags. A driver who finds a blinking rim
    // distracting is asking for a rim that stops moving, and is still owed the flag: it was declared,
    // attached and drawn in the panel with nothing reading it, which is a switch that changes nothing.
    const moves = ncalc.eq(setting.ledFlagAnimation(), 'true');
    for (const flag of flagEffects()) {
      const pair = effectContainers(flag, 1, 1) as Extract<leds.LedContainer, { kind: 'customStatus' }>[];
      expect({ id: flag.id, containers: pair.length }).toMatchObject({ containers: 2 });
      const [moving, held] = pair;
      expect({ id: flag.id, on: moving!.enabledFormula.expression.includes(moves) }).toMatchObject({ on: true });
      expect({ id: flag.id, blink: moving!.blinkFormula?.expression.includes(moves) }).toMatchObject({ blink: true });
      // Held: lit while the switch is off, steady, and on a colour that is actually lit. The
      // chequered row's own Color is the off phase — it is written as a dark ground blinking white so
      // as not to be the white flag — so holding that field would turn the one flag off.
      expect({ id: flag.id, off: held!.enabledFormula.expression.includes(ncalc.not(moves)) }).toMatchObject({ off: true });
      expect({ id: flag.id, blink: held!.blinkFormula }).toMatchObject({ blink: undefined });
      expect({ id: flag.id, dark: held!.color === BLINK_OFF }).toMatchObject({ dark: false });
      expect({ id: flag.id, lit: held!.color === flag.color || held!.color === flag.blinkColor }).toMatchObject({ lit: true });
    }
    // ...and on every shape, so there is no device that draws a flag it cannot hold.
    const labels = new Set(flagEffects().map((e) => e.label));
    for (const shape of ALL_SHAPES) {
      const profile = rpmStripProfile(shape, stableGuid(`t/held/${shape.id}`));
      // The lit containers alone: on a shape with no lamps a flag is also the name of the group that
      // blanks the run for it, and that group is not a second drawing of the flag.
      const drawn = walk(profile.containers)
        .filter((c) => c.kind === 'customStatus')
        .map((c) => c.description ?? '');
      const moving = drawn.filter((d) => labels.has(d)).length;
      expect({ shape: shape.id, moving: moving > 0 }).toMatchObject({ moving: true });
      expect({ shape: shape.id, held: drawn.filter((d) => d.endsWith(', held')).length }).toMatchObject({ held: moving });
      expect({ shape: shape.id, reads: leds.serializeProfile(profile).includes('[OpenDash.LedFlagAnimation]') }).toMatchObject({ reads: true });
    }
  });

  test('the strip and the box read one low-fuel threshold, so a rig has one answer to "am I low"', () => {
    const strip = ALL_EFFECTS().find((e) => e.id === 'lowFuel')!;
    const box = warningStates(1).find((s) => s.id === 'lowFuel')!;
    expect(strip.when).toBe(box.raised);
    expect(strip.when).toContain('[OpenDash.LightsLowFuelLaps]');
    // It read CarSettings_FuelAlertActive, which is SimHub's own alert and what the native container
    // reads. That is a different question from the one the box asks, so the number in the panel moved
    // the box and left the strip where SimHub had put it.
    expect(strip.when).not.toContain('CarSettings_FuelAlertActive');
    expect(strip.source).toContain('Fuel_RemainingLaps');
    for (const shape of ALL_SHAPES) {
      const text = leds.serializeProfile(rpmStripProfile(shape, stableGuid(`t/fuel/${shape.id}`)));
      expect({ shape: shape.id, native: text.includes('CarSettings_FuelAlertActive') }).toMatchObject({ native: false });
    }
  });

  test('the spotters light the side the car is actually on, steadily', () => {
    const [left, right] = SPOTTER_EFFECTS;
    expect({ role: left!.role, side: left!.side }).toEqual({ role: 'side', side: 'left' });
    expect({ role: right!.role, side: right!.side }).toEqual({ role: 'side', side: 'right' });
    expect(left!.when).toContain('SpotterCarLeft');
    expect(right!.when).toContain('SpotterCarRight');
    // This used to assert the both-sides blink, which read SpotterCarLeft and SpotterCarRight
    // together and flashed both sides red. It is deliberately gone rather than relaxed: a side lamp
    // is one LED now, so two of them lit already say what the blink was saying, and DROPPED records
    // it as a judgement rather than leaving a reader to find an effect that quietly lost a field.
    for (const e of SPOTTER_EFFECTS) expect({ id: e.id, blink: e.blinkWhen }).toMatchObject({ blink: undefined });
    expect(DROPPED.map((d) => d.effect)).toContain('Car alongside on both sides');
  });

  test('pit speeding is composed, because SimHub publishes no speeding property', () => {
    const speeding = ALL_EFFECTS().find((e) => e.id === 'pit.speeding')!;
    expect(speeding.when).toContain('IsInPitLane');
    expect(speeding.when).toContain('PitLimiterSpeed');
    expect(speeding.when).toContain('SpeedLocal');
    expect(speeding.when).toContain(`(${PIT_SPEEDING_MARGIN})`);
    expect(speeding.source).toContain('SimHub publishes no speeding property');
    // Speeding and the limiter are the two states that take the whole strip, so between them the
    // rate is most of what the driver reads: fast for a penalty accruing now, slow for a state they
    // chose and are holding. They ran at 62 ms and 186 ms, which were a tier above the fast one and
    // a rate belonging to neither.
    const limiter = ALL_EFFECTS().find((e) => e.id === 'pit.limiter')!;
    expect({ speeding: speeding.blinkDelayMs, limiter: limiter.blinkDelayMs }).toEqual({ speeding: FAST_BLINK_MS, limiter: SLOW_BLINK_MS });
  });

  test('the TC light reads the intervention alone, and is therefore dark on iRacing', () => {
    const tc = SIDE_EFFECTS.find((e) => e.id === 'tc')!;
    // This test used to assert the opposite — steady on TCLevel, blinking on TCActive — and the
    // reversal is deliberate rather than a loosening. A light on the dial is on from the green flag
    // to the flag on iRacing, which fills TCLevel and not TCActive, and a lamp that is always lit
    // reports the setting rather than the car. The canvas forbids the dial in as many words.
    expect(tc.when).toContain('TCActive');
    expect(tc.when).not.toContain('TCLevel');
    expect({ blink: tc.blinkWhen, color: tc.color }).toMatchObject({ blink: undefined, color: ds.color.info.primary });
    // ...and the record says dark rather than steady, so the honesty note matches the behaviour.
    const record = BEST_EFFORT.find((b) => b.effect === 'TC intervening')!;
    expect(record.reason).toContain('dark');
    expect(record.reason).not.toContain('is steady and never blinks');
  });

  test('the best-effort effects ship and read a real property, rather than being refused', () => {
    // The user's call: an LED that stays dark asserts nothing, unlike a readout drawing 0.00, so an
    // effect whose property exists ships and simply does not light on a sim that leaves it zero.
    const ids = ALL_EFFECTS().map((e) => e.id);
    expect(ids).toContain('turn.left');
    expect(ids).toContain('turn.right');
    for (const b of BEST_EFFORT) {
      expect({ effect: b.effect, prop: b.property.startsWith('DataCorePlugin.') }).toMatchObject({ prop: true });
      expect(b.reason.length).toBeGreaterThan(30);
    }
    // ...and each reaches a profile.
    const text = textOf(profileFor('4-14-4'));
    for (const name of ['TCActive', 'TurnIndicatorLeft', 'TurnIndicatorRight']) {
      expect({ name, inProfile: text.includes(name) }).toMatchObject({ inProfile: true });
    }
  });

  test('the aid lamp draws the pair the way UN R121 and the manuals do: ABS amber, traction control blue', () => {
    const by = (id: string): (typeof SIDE_EFFECTS)[number] => SIDE_EFFECTS.find((e) => e.id === id)!;
    // The build drew the two the other way round, and the reversal is the point of this test: a
    // driver who has read either lamp on a road car or in any other sim reads the swap as the other
    // system intervening. Both are steady, because an intervention is a state and not an event.
    expect({ abs: by('abs').color, tc: by('tc').color }).toEqual({ abs: ds.color.caution.primary, tc: ds.color.info.primary });
    expect({ abs: by('abs').blinkWhen, tc: by('tc').blinkWhen }).toMatchObject({ abs: undefined, tc: undefined });
  });

  test('a condition with two states draws the second in a second colour, and one with one state in darkness', () => {
    const drs = SIDE_EFFECTS.find((e) => e.id === 'drs')!;
    const p2p = SIDE_EFFECTS.find((e) => e.id === 'p2p')!;
    // Push to pass carries two facts: blue while one is in hand, green while one is being spent.
    // Both were white before, which made the lamp one colour and the blink invisible on top of it.
    expect({ color: p2p.color, blinkColor: p2p.blinkColor, delay: p2p.blinkDelayMs }).toMatchObject({
      color: ds.color.info.primary,
      blinkColor: ds.color.good.primary,
      delay: FAST_BLINK_MS,
    });
    // DRS carries one fact in two rhythms, so its off phase is darkness rather than a second hue.
    expect({ color: drs.color, blinkColor: drs.blinkColor, delay: drs.blinkDelayMs }).toMatchObject({
      color: ds.color.good.primary,
      blinkColor: undefined,
      delay: FAST_BLINK_MS,
    });
  });

  test('every blink has an off phase, which is the whole of what a blink is', () => {
    // Defect 1 of the lights review: StaticColorContainerBase alternates Color with BlinkingColor,
    // and every blinking effect wrote its own colour into both, so nothing on the strip has ever
    // flashed. The off phase must also be opaque: the merge drops transparent pixels, and the rev
    // ladder underneath would show through the gap.
    for (const e of ALL_EFFECTS()) {
      if (!e.blinkWhen) continue;
      const c = effectContainer(e, 1, 1) as Extract<leds.LedContainer, { kind: 'customStatus' }>;
      expect({ id: e.id, same: c.blinkColor === c.color }).toMatchObject({ same: false });
      expect({ id: e.id, opaque: /^#[0-9A-F]{6}$/.test(String(c.blinkColor)) }).toMatchObject({ opaque: true });
    }
  });

  test('the car lamp reads both temperature bits in amber, and oil pressure in red only while the engine turns', () => {
    const temperature = SIDE_EFFECTS.find((e) => e.id === 'temperature')!;
    expect({ color: temperature.color, delay: temperature.blinkDelayMs }).toMatchObject({ color: ds.color.caution.primary, delay: FAST_BLINK_MS });
    // Bit 1 is water and 0x0040 is oil; one lamp takes both, because a single LED cannot say which
    // fluid it is and the driver's answer to either is to lift and watch the gauge.
    expect(temperature.when).toContain('EngineWarnings');
    expect(temperature.when).toContain('(64)');
    const oil = SIDE_EFFECTS.find((e) => e.id === 'oilPressure')!;
    expect({ color: oil.color, delay: oil.blinkDelayMs }).toMatchObject({ color: ds.color.danger.primary, delay: FAST_BLINK_MS });
    // A stopped engine sets the bit as readily as a failing one, so the lamp would otherwise be red
    // in every garage and on every grid.
    expect(oil.when).toContain('Rpms');
  });

  test('what is dropped has a property and no lamp, which is neither best effort nor an absence', () => {
    // ERS and the headlight flash used to ship and used to be asserted present here. Both are gone
    // on purpose: a store that empties is a bar rather than a lamp and could never light on iRacing,
    // and the flash reports the driver's own button. Recording them as dropped rather than deleting
    // the rows is what keeps the reversal readable — the claim being reversed is that any property
    // that exists is worth an LED.
    const ids = ALL_EFFECTS().map((e) => e.id);
    for (const gone of ['ers', 'headlightFlash']) expect({ gone, shipped: ids.includes(gone) }).toMatchObject({ shipped: false });
    expect(DROPPED.map((d) => d.effect)).toEqual(['ERS charge, and KERS with it', 'Headlight flash', 'Car alongside on both sides']);
    for (const d of DROPPED) {
      expect({ effect: d.effect, prop: d.property.startsWith('DataCorePlugin.') }).toMatchObject({ prop: true });
      expect(d.reason.length).toBeGreaterThan(30);
    }
    // Nothing dropped reaches a profile, on any shape.
    for (const shape of ALL_SHAPES) {
      const text = leds.serializeProfile(rpmStripProfile(shape, stableGuid(`t/dropped/${shape.id}`)));
      for (const dead of ['ERSPercent', 'dcHeadlightFlash']) expect({ shape: shape.id, dead, inProfile: text.includes(dead) }).toMatchObject({ inProfile: false });
    }
  });

  test('the turn indicators signal on the side being signalled, like the spotters, and in the indicator green', () => {
    expect(TURN_EFFECTS.map((e) => [e.role, e.side])).toEqual([
      ['side', 'left'],
      ['side', 'right'],
    ]);
    expect(TURN_EFFECTS[0]!.when).toContain('TurnIndicatorLeft');
    expect(TURN_EFFECTS[1]!.when).toContain('TurnIndicatorRight');
    // They used to be the spotter's amber at a multiple of the over-rev constant, which made rank 2
    // of the side lamp the same light as rank 1 and tied its rate to the shift lights.
    for (const e of TURN_EFFECTS) {
      expect({ id: e.id, color: e.color, delay: e.blinkDelayMs }).toMatchObject({ color: ds.color.good.primary, delay: SLOW_BLINK_MS });
    }
    expect(SLOW_BLINK_MS).toBe(250);
    expect(FAST_BLINK_MS).toBe(125);
  });

  test('what has no property anywhere is absent, and says what the nearest thing is', () => {
    expect(NO_PROPERTY.map((n) => n.effect)).toEqual(expect.arrayContaining(['Headlights on, off, low or high beam', 'Water pressure']));
    for (const n of NO_PROPERTY) {
      expect(n.reason.length).toBeGreaterThan(30);
      expect(n.nearest.length).toBeGreaterThan(10);
    }
    // The oil temperature warning was here, denied on the claim that EngineWarnings has no such bit.
    // It has carried 0x0040 since 2021 season 2, so the row was a false claim about iRacing rather
    // than a real absence, and the bit is now read by the temperature lamp. This is a correction
    // rather than a relaxation: the list has lost a row because the row was wrong.
    expect(NO_PROPERTY.map((n) => n.effect)).not.toContain('Oil temperature warning');
    const temperature = SIDE_EFFECTS.find((e) => e.id === 'temperature')!;
    expect(temperature.source).toContain('64');
    // KERS is not here either: SimHub has no KERS member at all and folds every hybrid store into
    // ERS, so it is dropped with ERS rather than recorded as an absence.
    expect(NO_PROPERTY.map((n) => n.effect)).not.toContain('KERS');
    expect(DROPPED.map((d) => d.effect).join(' ')).toContain('KERS');
    // Nothing that has no property reaches a profile.
    const text = textOf(profileFor('4-14-4'));
    for (const dead of ['WaterPressure', 'KERS', 'GameData.Headlight']) expect({ dead, inProfile: text.includes(dead) }).toMatchObject({ inProfile: false });
  });

  test('a shape with no sides has no lamps, so what needs one is dropped rather than moved onto the rev LEDs', () => {
    expect(lampsOf(shapeById('brow-25')!)).toEqual([]);
    const brow = textOf(profileFor('brow-25'));
    // The spotters and the assists have nowhere to go on a brow...
    expect(brow).not.toContain('Car alongside, left');
    expect(brow).not.toContain('ABS active');
    expect(brow).not.toContain('lamp');
    // ...but the flags keep the whole run there, which is what every shape did before the lamps
    // arrived and is what a brow keeps until whether a bare run derives lamps of its own is decided.
    expect(brow).toContain('Yellow flag');
    expect(brow).toContain('Pit limiter on');
  });
});

describe('every generated profile', () => {
  test('validates against the contract, with the fit rule measuring the whole device', () => {
    for (const shape of ALL_SHAPES) {
      const result = leds.validateProfile(rpmStripProfile(shape, stableGuid(`test/leds/${shape.id}`)), {
        declaredProperties: declaredProperties(),
        propertyPrefix: PROPERTY_PREFIX,
        ledCount: deviceLength(shape),
      });
      expect({ shape: shape.id, errors: result.errors.map((e) => `${e.code} ${e.path}`) }).toMatchObject({ errors: [] });
    }
  });

  test('uses only ContainerTypes SimHub 9.12.6 resolves', () => {
    for (const shape of ALL_SHAPES) {
      for (const c of walk(rpmStripProfile(shape, stableGuid(`t/${shape.id}`)).containers)) {
        const type = leds.containerTypeOf(c);
        expect({ shape: shape.id, type, known: leds.KNOWN_CONTAINER_TYPES.has(type) }).toMatchObject({ known: true });
      }
    }
  });

  test('nests the four gates outside in, and nothing of the tree escapes above them', () => {
    // The sim-running gate is a correctness fix rather than tidiness: a CustomStatus that throws is
    // ON rather than off, so with the sim closed a bare one lights. The ignition gate is the second
    // half of the same sentence, and the strip had been missing it while the box beside it had it.
    for (const shape of ALL_SHAPES) {
      const p = rpmStripProfile(shape, stableGuid(`t/${shape.id}`));
      // The remap is outermost and only on a shape the maker wired in an order of its own.
      expect({ shape: shape.id, roots: p.containers.length }).toMatchObject({ roots: 1 });
      const outermost = p.containers[0]!;
      if (shape.positions) {
        expect({ shape: shape.id, type: leds.containerTypeOf(outermost) }).toMatchObject({ type: 'Groups.RemapGroup' });
      } else {
        expect({ shape: shape.id, remapped: leds.containerTypeOf(outermost) === 'Groups.RemapGroup' }).toMatchObject({ remapped: false });
      }
      const running = shape.positions ? leds.childrenOf(outermost)[0]! : outermost;
      // Each gate is the only child of the one above it, so a gate near the top answers for
      // everything below it rather than for one branch of it.
      const chain = [running];
      while (leds.childrenOf(chain[chain.length - 1]!).length === 1) chain.push(leds.childrenOf(chain[chain.length - 1]!)[0]!);
      expect({ shape: shape.id, gates: chain.slice(0, 3).map((c) => leds.containerTypeOf(c)) }).toMatchObject({
        gates: ['Groups.GameRunningGroup', 'Groups.BrightnessFormulaGroup', 'Groups.CustomConditionalGroup'],
      });
      const ignition = chain[2]!;
      expect({ shape: shape.id, gate: ignition.description }).toMatchObject({ gate: 'only while the car is switched on' });
      expect({ shape: shape.id, when: (ignition as Extract<leds.LedContainer, { kind: 'conditionalGroup' }>).trigger.expression }).toMatchObject({
        when: ignitionIsOn(),
      });
      // The fit rule accumulates offsets down the tree, so a gate carrying a startPosition would
      // move every LED beneath it.
      for (const gate of [outermost, ...chain.slice(0, 3)]) {
        expect({ shape: shape.id, gate: gate.description, at: (gate as { startPosition?: number }).startPosition }).toMatchObject({ at: undefined });
      }
      // ...and nothing paints outside them: every customStatus in the profile is under all three.
      const under = new Set(walk(leds.childrenOf(ignition)));
      for (const c of walk(p.containers)) {
        if (c.kind !== 'customStatus') continue;
        expect({ shape: shape.id, led: c.description, inside: under.has(c) }).toMatchObject({ inside: true });
      }
    }
  });

  test('obeys the rig brightness, which the flag box had to itself until now', () => {
    // LightsBrightness, LightsNightBrightness and LightsNightMode are captioned "for every light
    // openDash drives", and a wheel strip and a brow read none of the three: the composed expression
    // had one reader, the matrix. The assertion is against contract.ts rather than against a copy of
    // what it is believed to emit, so the strip and the box cannot come to hold two brightnesses.
    for (const shape of ALL_SHAPES) {
      const p = rpmStripProfile(shape, stableGuid(`t/bright/${shape.id}`));
      const bright = walk(p.containers).filter((c) => leds.containerTypeOf(c) === 'Groups.BrightnessFormulaGroup');
      expect({ shape: shape.id, groups: bright.length }).toMatchObject({ groups: 1 });
      // One group over the whole tree: the run under it is everything the profile draws.
      const outer = shape.positions ? leds.childrenOf(p.containers[0]!)[0]! : p.containers[0]!;
      expect({ shape: shape.id, under: leds.childrenOf(outer).map((c) => leds.containerTypeOf(c)) }).toMatchObject({ under: ['Groups.BrightnessFormulaGroup'] });
      const fields = (bright[0] as Extract<leds.LedContainer, { kind: 'raw' }>).fields ?? {};
      expect({ shape: shape.id, formula: fields.BrightnessFormula }).toMatchObject({ formula: { Expression: flagBox.brightness() } });
      // ...and it reaches the file, with each read defaulted so a strip works with no plugin at all.
      const text = leds.serializeProfile(p);
      for (const read of ['isnull([OpenDash.LightsNightMode], false)', 'isnull([OpenDash.LightsNightBrightness], 25)', 'isnull([OpenDash.LightsBrightness], 100)']) {
        expect({ shape: shape.id, read, present: text.includes(read) }).toMatchObject({ present: true });
      }
    }
  });

  test('takes every colour from a token and never from a literal of its own', () => {
    const tokens = new Set<string>([
      ...Object.values(ds.purpose.shift),
      ...Object.values(ds.purpose.flag),
      ...Object.values(ds.purpose.fuel),
      ...Object.values(ds.purpose.alert),
      ds.purpose.pitLimiter,
      ds.color.good.primary,
      ds.color.caution.primary,
      ds.color.danger.primary,
      ds.color.info.primary,
      ds.color.neutral.primary,
      // The middle mark of an odd throttle-and-brake centre, which is the text colour rather than a
      // pedal's: it is the one LED of that bar that is not reporting a pedal at all.
      ds.color.text.primary,
      // The off phase of a blink is a colour like any other and comes from the sheet like any other.
      ds.color.surface.base,
    ]);
    for (const shape of ALL_SHAPES) {
      for (const c of walk(rpmStripProfile(shape, stableGuid(`t/${shape.id}`)).containers)) {
        for (const key of ['color', 'blinkColor'] as const) {
          const value = (c as unknown as Record<string, unknown>)[key];
          if (typeof value !== 'string') continue;
          expect({ shape: shape.id, key, value, fromTokens: tokens.has(value) }).toMatchObject({ fromTokens: true });
        }
      }
    }
  });

  test('gives every blink an off phase, on a rung as much as on a lamp', () => {
    // The catalogue has a test of its own for this, and it could not see the rev ladder: `rungs()`
    // wrote the top band's own red into both Color and BlinkingColor, SimHub's StaticColorContainerBase
    // alternated a hex with itself, and the over-rev flash was never a flash. Walking the containers
    // is what closes that, since it measures what is emitted rather than what the catalogue declares.
    for (const shape of ALL_SHAPES) {
      for (const c of walk(rpmStripProfile(shape, stableGuid(`t/${shape.id}`)).containers)) {
        const row = c as unknown as Record<string, unknown>;
        if (row.blinkFormula === undefined) continue;
        const at = `${shape.id} ${String(row.description)}`;
        expect({ at, blinkColor: row.blinkColor }).toMatchObject({ blinkColor: expect.any(String) });
        expect({ at, same: row.blinkColor === row.color }).toMatchObject({ same: false });
        // Not transparent either: the merge drops transparent pixels, so the rev ladder underneath
        // would show through the gap and the off phase would read as whatever is below it.
        expect({ at, transparent: row.blinkColor === leds.LED_TRANSPARENT }).toMatchObject({ transparent: false });
        expect({ at, opaque: /^#[0-9A-Fa-f]{6}$/.test(String(row.blinkColor)) }).toMatchObject({ opaque: true });
      }
    }
  });

  test('blinks at two rates and no third, because a strip is hardware', () => {
    // Four rates shipped: 62, 124, 186 and 248 ms, each a multiple of the over-rev constant, plus the
    // flag band's own 250. Two is what the eye sorts at speed, and a rate that is neither is read as
    // one of them anyway, so a third is a distinction the driver cannot collect.
    const delays = new Set<number>();
    for (const shape of ALL_SHAPES) {
      for (const c of walk(rpmStripProfile(shape, stableGuid(`t/${shape.id}`)).containers)) {
        const delay = (c as unknown as Record<string, unknown>).blinkDelayMs;
        if (typeof delay === 'number') delays.add(delay);
      }
    }
    expect([...delays].sort((a, b) => a - b)).toEqual([FAST_BLINK_MS, SLOW_BLINK_MS]);
  });

  test('an odd centre keeps the two pedals equal and spends the spare LED on a white middle mark', () => {
    // It gave the odd LED to brake and painted it red, so on half the shapes the two pedals read at
    // different scales: a foot flat on each filled one side one LED further than the other.
    const odd = ALL_SHAPES.filter((s) => s.centre % 2 === 1);
    expect(odd.map((s) => s.id)).toEqual(['3-9-3', '3-9-3-fanalab', '4-9-4', '0-9-0', 'brow-9', 'brow-15', 'brow-25']);
    for (const shape of odd) {
      const kids = centreChildren(shape, 'throttleBrake');
      const of = (prefix: string): leds.LedContainer[] => kids.filter((c) => String(descriptionOf(c)).startsWith(prefix));
      const half = (shape.centre - 1) / 2;
      expect({ shape: shape.id, brake: of('brake ').length, throttle: of('throttle ').length }).toMatchObject({ brake: half, throttle: half });
      const mark = of('centre mark');
      expect({ shape: shape.id, marks: mark.length }).toMatchObject({ marks: 1 });
      const row = mark[0] as Extract<leds.LedContainer, { kind: 'customStatus' }>;
      // White, from the token, and always lit: it is where the middle is, not a reading.
      expect({ shape: shape.id, colour: row.color, at: row.startPosition, when: row.enabledFormula.expression }).toMatchObject({
        colour: ds.color.text.primary,
        at: half + 1,
        when: 'true',
      });
      // The two halves are mirrored about it, so neither pedal reaches further than the other.
      expect({ shape: shape.id, brakeAt: of('brake ').map((c) => (c as { startPosition: number }).startPosition) }).toMatchObject({
        brakeAt: Array.from({ length: half }, (_, k) => half - k),
      });
      expect({ shape: shape.id, throttleAt: of('throttle ').map((c) => (c as { startPosition: number }).startPosition) }).toMatchObject({
        throttleAt: Array.from({ length: half }, (_, k) => half + 2 + k),
      });
    }
    // An even centre has no spare LED to spend: its two halves already meet in the middle.
    for (const shape of ALL_SHAPES.filter((s) => s.centre % 2 === 0)) {
      const kids = centreChildren(shape, 'throttleBrake');
      expect({ shape: shape.id, marks: kids.filter((c) => descriptionOf(c) === 'centre mark').length }).toMatchObject({ marks: 0 });
    }
  });

  test('the fuel centre raises itself on the one low-fuel threshold, at the flag band rate', () => {
    // Five percent of the tank was a third answer to "am I low" beside the box's and the lamp's, and
    // it is not one a driver can act on: it is two laps in one car and half a lap in another.
    const box = warningStates(1).find((s) => s.id === 'lowFuel')!;
    for (const shape of ALL_SHAPES) {
      const kids = centreChildren(shape, 'fuel') as Extract<leds.LedContainer, { kind: 'customStatus' }>[];
      expect({ shape: shape.id, leds: kids.length }).toMatchObject({ leds: shape.centre });
      for (const row of kids) {
        const at = `${shape.id} ${String(row.description)}`;
        expect({ at, threshold: row.blinkFormula?.expression.includes(box.raised) }).toMatchObject({ threshold: true });
        // The *height* is still FuelPercent, which is what a fuel bar is; only the threshold moved.
        expect({ at, height: row.enabledFormula.expression.includes('FuelPercent') }).toMatchObject({ height: true });
        expect({ at, fivePercent: row.blinkFormula?.expression.includes(ncalc.gt(ncalc.num(5), fuelPercent())) }).toMatchObject({ fivePercent: false });
        // 250 ms, the flag band's own half period, so the bar pulses with every other slow blink on
        // the strip rather than at a multiple of the shift constant that would move with it.
        expect({ at, delay: row.blinkDelayMs }).toMatchObject({ delay: SLOW_BLINK_MS });
      }
    }
    expect(SLOW_BLINK_MS).toBe(Math.round(1000 / ds.indicator.flagBand.flashHz / 2));
  });

  test('the brake reaches a strip through the centre alone: no centre value fills a side with it', () => {
    // The sides were a brake gradient under the default centre, and a group filled red by the pedal
    // is a group on which an oil warning cannot come on. The gradient is gone rather than recoloured:
    // the ends are lamps, and brake is still offered where it can be read, as a centre function.
    // This is the assertion that keeps the decision from being undone by a later edit.
    const brake = values.brake();
    for (const shape of ALL_SHAPES) {
      const p = rpmStripProfile(shape, stableGuid(`t/sides/${shape.id}`));
      const trail = (cs: readonly leds.LedContainer[], path: readonly string[]): { at: string; path: readonly string[] }[] =>
        cs.flatMap((c) => {
          const here = [...path, descriptionOf(c)];
          const reads = JSON.stringify([(c as { enabledFormula?: unknown }).enabledFormula, (c as { trigger?: unknown }).trigger]).includes(brake);
          return [...(reads ? [{ at: descriptionOf(c), path: here }] : []), ...trail(leds.childrenOf(c), here)];
        });
      for (const found of trail(p.containers, [])) {
        // Under the centre group, or under one of the 3/10/3's further runs, which repeat it.
        const root = found.path.find((d) => d === 'centre' || d.startsWith('extra run '));
        expect({ shape: shape.id, at: found.at, under: root }).toMatchObject({ under: expect.any(String) });
      }
      // ...and the group itself is gone by name, so a reader does not find a dead comment about it.
      expect({ shape: shape.id, group: leds.serializeProfile(p).includes('sides: brake') }).toMatchObject({ group: false });
    }
  });

  test('offers every centre function and carries a stable id', () => {
    const text = textOf(profileFor('4-14-4'));
    for (const centre of LED_CENTRES) {
      expect({ centre, present: text.includes(`centre: ${centre}`) }).toMatchObject({ present: true });
    }
    // Four functions and no fifth: rpmOnly was retired into rpm, so nothing gates on it any more.
    expect(text).not.toContain(RETIRED_LED_CENTRE);
    // Ids are stableGuid of a path, so a rebuild never churns them and SimHub never sees a duplicate.
    const ids = ALL_SHAPES.map((s) => rpmStripProfile(s, stableGuid(`openDash/leds/${s.id}`)).profileId);
    expect(new Set(ids).size).toBe(ids.length);
    expect(rpmStripProfile(STRIP_SHAPES[0]!, stableGuid('openDash/leds/3-9-3')).profileId).toBe(stableGuid('openDash/leds/3-9-3'));
  });

  test('names itself after the shape, in the form SimHub lists', () => {
    expect(rpmStripProfileName(shapeById('4-14-4')!)).toBe('openDash 4/14/4');
    expect(rpmStripProfileName(shapeById('brow-25')!)).toBe('openDash brow 25');
    expect(rpmStripFileName(shapeById('4-14-4')!)).toBe('openDash 4-14-4');
  });
});

describe('the per-gear shift table', () => {
  test('the shipped table is valid, and being empty is a legitimate state', () => {
    expect(validateShiftTable()).toEqual([]);
    // Empty is deliberate: openDash does not carry measurements it has not made, and a competitor's
    // tables are theirs. The mechanism ships so a measured car can arrive as a pull request.
    expect(Object.keys(SHIFT_TABLE)).toEqual([]);
  });

  test('a contributed entry has to be traceable and ordered, or the build refuses it', () => {
    const good: Record<string, CarShiftPoints> = {
      examplecar: { name: 'Example', source: 'Measured on track, 2026-09-13', gears: { '1': { first: 6000, shift: 7000, last: 7500, blink: 7800 } } },
    };
    expect(validateShiftTable(good)).toEqual([]);

    // No provenance: a number nobody can trace puts a shift light in the wrong place with confidence.
    expect(validateShiftTable({ examplecar: { ...good.examplecar!, source: '  ' } })).toEqual([expect.stringContaining('needs a source')]);
    // Out of order: the bands would invert and the ladder would run backwards.
    expect(
      validateShiftTable({ examplecar: { ...good.examplecar!, gears: { '1': { first: 7000, shift: 6000, last: 7500, blink: 7800 } } } }),
    ).toEqual([expect.stringContaining('first <= shift <= last <= blink')]);
    // A gear that is not a forward gear, and a car with no gears at all.
    expect(validateShiftTable({ examplecar: { ...good.examplecar!, gears: { R: { first: 1, shift: 2, last: 3, blink: 4 } } } })).toEqual([
      expect.stringContaining('not a forward gear'),
    ]);
    expect(validateShiftTable({ examplecar: { ...good.examplecar!, gears: {} } })).toEqual([expect.stringContaining('overrides nothing')]);
  });

  test('a measured gear folds to one comparison, because its thresholds are known at build time', () => {
    const points = { first: 6000, shift: 7000, last: 7500, blink: 7800 };
    const rpm = 'isnull([DataCorePlugin.GameData.Rpms], 0)';
    // Band 0 runs 6000 to 7000 over five rungs: rung 0 at 6000, rung 2 at 6400.
    expect(tabledStageLit(points, 0, 0, 5)).toBe(`(${rpm}) > (6000)`);
    expect(tabledStageLit(points, 0, 2, 5)).toBe(`(${rpm}) > (6400)`);
    // The top band lights together at `last`, and the flash is at `blink`. The flash also carries
    // the last-gear exception, which this used to assert the absence of: a measured gear has no
    // more claim to flash in the gear there is nothing to shift out of than either derived ladder
    // has, so the reversal is the point of the line rather than a loosening of it.
    expect(tabledStageLit(points, 2, 0, 5)).toBe(`(${rpm}) >= (7500)`);
    expect(tabledOverRev(points)).toBe(`((${rpm}) >= (7800)) and (!(${lastGear()}))`);
  });

  test('an entry reaches a profile as a gear-and-car override that composes over the derived ladder', () => {
    // Proven by building with a stubbed table rather than by shipping a car, so that the mechanism
    // is covered while the shipped table stays honestly empty.
    const model = 'examplecar';
    SHIFT_TABLE[model] = { name: 'Example', source: 'test', gears: { '3': { first: 6000, shift: 7000, last: 7500, blink: 7800 } } };
    try {
      const text = leds.serializeProfile(rpmStripProfile(shapeById('4-14-4')!, stableGuid('t/tabled')));
      expect(text).toContain('Example, gear 3');
      expect(text).toContain("(isnull([DataCorePlugin.GameData.CarModel], '')) = ('examplecar')");
      expect(text).toContain('(isnull([DataCorePlugin.GameRawData.Telemetry.Gear], 0)) = (3)');
      expect(text).toContain('(7500)');
      // It blanks what is under it, so the derived ladder does not show through the measured one.
      expect(text).toContain('"ClearBackgroundWhenActive": true');
    } finally {
      delete SHIFT_TABLE[model];
    }
    // ...and with the table empty again, nothing of it remains.
    expect(leds.serializeProfile(rpmStripProfile(shapeById('4-14-4')!, stableGuid('t/empty')))).not.toContain('examplecar');
  });
});

