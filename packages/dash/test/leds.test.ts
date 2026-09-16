/**
 * The LED profiles openDash generates: the strip shapes, the rev ladder and its styles, and the
 * effect catalogue. The rule under most of this is that a light which cannot come on is worse than
 * one that is absent — a dark LED reads as "not happening" rather than "not known" — so several of
 * these tests exist to prove openDash refuses to draw something rather than to prove it draws it.
 */
import { describe, expect, test } from 'bun:test';
import { stableGuid, leds } from '../src/generator.ts';
import { MIRROR_COLOR_WIDTH, MIRROR_RUN_LENGTHS, PROPERTY_PREFIX, declaredProperties, LED_CENTRES, LED_RPM_STYLES, ledMirrorRunName, propertyName } from '../src/contract.ts';
import { ALL_SHAPES, BROW_SHAPES, STRIP_SHAPES, centreStart, deviceLength, reversedPositions, rightStart, shapeById, stripLength } from '../src/leds/strip.ts';
import { rpmStripFileName, rpmStripProfile, rpmStripProfileName } from '../src/leds/rpmStrip.ts';
import { bandOf, ladderColors, ladderOrder, rungFlashes } from '../src/leds/ladder.ts';
import { canMirror, carCentre, mirrorRun } from '../src/leds/mirror.ts';
import { ALL_EFFECTS, BEST_EFFORT, NO_PROPERTY, PIT_SPEEDING_MARGIN, SIDE_EFFECTS, SPOTTER_EFFECTS, TURN_EFFECTS, flagEffects } from '../src/leds/effects.ts';
import { SHIFT_RPM_PROPERTIES } from '../src/shift.ts';
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
    expect(shapeById('4-14-4')?.devices).toContain('SimRep Engineering MLD');
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

  test('f1 is the same order in different colours, and flashes the whole bar rather than the top band', () => {
    expect(ladderOrder('f1', 14).rungOf(5)).toBe(ladderOrder('leftToRight', 14).rungOf(5));
    expect(ladderColors('f1')).toEqual([ds.color.good.primary, ds.color.danger.primary, ds.color.info.primary]);
    expect(ladderColors('leftToRight')).toEqual([ds.purpose.shift.stage1, ds.purpose.shift.stage2, ds.purpose.shift.stage3]);
    // Every rung flashes under f1; only the top band does otherwise.
    expect([0, 5, 13].map((r) => rungFlashes('f1', r, 14))).toEqual([true, true, true]);
    expect([0, 5, 13].map((r) => rungFlashes('leftToRight', r, 14))).toEqual([false, false, true]);
  });

  test('the bands are thirds with the last taking the remainder', () => {
    expect([0, 4, 5, 9, 10, 13].map((r) => bandOf(r, 14))).toEqual([0, 0, 1, 1, 2, 2]);
    expect([0, 1, 2].map((r) => bandOf(r, 3))).toEqual([0, 1, 2]);
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
    // ADR 0017: openDash's opinion is that the car is right. The three openDash styles stay, for a
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

  test('the flags are ranked exactly as the face ranks them, so the two cannot disagree', () => {
    // flagEffects composes highest priority last, which on a strip is what wins a tie.
    expect(flagEffects().map((e) => e.id)).toEqual(['flag.green', 'flag.white', 'flag.blue', 'flag.yellow', 'flag.checkered', 'flag.black']);
    // ...and each carries the face's own flagVisible, which is what makes "the same ranking" true
    // rather than merely intended: black beating yellow is one expression shared by both.
    const yellow = flagEffects().find((e) => e.id === 'flag.yellow')!;
    expect(yellow.when).toContain('[DataCorePlugin.GameData.Flag_Black]) = (0)');
    expect(yellow.when).toContain('[DataCorePlugin.GameData.Flag_Yellow]) = (1)');
    expect(yellow.blinkWhen).toBeTruthy();
    for (const e of flagEffects()) expect({ id: e.id, exclusive: e.exclusive }).toMatchObject({ exclusive: true });
  });

  test('the spotters light the side the car is actually on', () => {
    const [left, right] = SPOTTER_EFFECTS;
    expect(left!.placement).toBe('left');
    expect(right!.placement).toBe('right');
    expect(left!.when).toContain('SpotterCarLeft');
    expect(right!.when).toContain('SpotterCarRight');
    // Both sides at once is the conjunction, because SimHub publishes no "both" of any kind.
    expect(left!.blinkWhen).toContain('SpotterCarLeft');
    expect(left!.blinkWhen).toContain('SpotterCarRight');
  });

  test('pit speeding is composed, because SimHub publishes no speeding property', () => {
    const speeding = ALL_EFFECTS().find((e) => e.id === 'pit.speeding')!;
    expect(speeding.when).toContain('IsInPitLane');
    expect(speeding.when).toContain('PitLimiterSpeed');
    expect(speeding.when).toContain('SpeedLocal');
    expect(speeding.when).toContain(`(${PIT_SPEEDING_MARGIN})`);
    expect(speeding.source).toContain('SimHub publishes no speeding property');
  });

  test('the TC light degrades rather than going dark: steady on the dial, blinking on the intervention', () => {
    const tc = SIDE_EFFECTS.find((e) => e.id === 'tc')!;
    // TCLevel is filled on every sim including iRacing, so the light is never simply absent...
    expect(tc.when).toContain('TCLevel');
    // ...and TCActive is the intervention, which iRacing does not fill, so on iRacing it never blinks.
    expect(tc.blinkWhen).toContain('TCActive');
    expect(BEST_EFFORT.map((b) => b.effect)).toContain('TC intervening');
  });

  test('the best-effort effects ship and read a real property, rather than being refused', () => {
    // The user's call: an LED that stays dark asserts nothing, unlike a readout drawing 0.00, so an
    // effect whose property exists ships and simply does not light on a sim that leaves it zero.
    const ids = ALL_EFFECTS().map((e) => e.id);
    expect(ids).toContain('ers');
    expect(ids).toContain('turn.left');
    expect(ids).toContain('turn.right');
    for (const b of BEST_EFFORT) {
      expect({ effect: b.effect, prop: b.property.startsWith('DataCorePlugin.') }).toMatchObject({ prop: true });
      expect(b.reason.length).toBeGreaterThan(30);
    }
    // ...and each reaches a profile.
    const text = textOf(profileFor('4-14-4'));
    for (const name of ['TCActive', 'TurnIndicatorLeft', 'TurnIndicatorRight', 'ERSPercent']) {
      expect({ name, inProfile: text.includes(name) }).toMatchObject({ inProfile: true });
    }
  });

  test('the turn indicators signal on the side being signalled, like the spotters', () => {
    expect(TURN_EFFECTS.map((e) => e.placement)).toEqual(['left', 'right']);
    expect(TURN_EFFECTS[0]!.when).toContain('TurnIndicatorLeft');
    expect(TURN_EFFECTS[1]!.when).toContain('TurnIndicatorRight');
  });

  test('what has no property anywhere is absent, and says what the nearest thing is', () => {
    expect(NO_PROPERTY.map((n) => n.effect)).toEqual(
      expect.arrayContaining(['Headlights on, off, low or high beam', 'Water pressure', 'Oil temperature warning']),
    );
    for (const n of NO_PROPERTY) {
      expect(n.reason.length).toBeGreaterThan(30);
      expect(n.nearest.length).toBeGreaterThan(10);
    }
    // KERS is not here: SimHub has no KERS member at all and folds every hybrid store into ERS,
    // so it ships as the ERS light rather than as an absence.
    expect(NO_PROPERTY.map((n) => n.effect)).not.toContain('KERS');
    expect(BEST_EFFORT.map((b) => b.effect).join(' ')).toContain('KERS');
    // Nothing that has no property reaches a profile. Headlights need care: the flash-to-pass is a
    // real raw variable and IS shipped, so what must be absent is a headlight *state* read, which
    // would have to come through the normalised layer that has no such member.
    const text = textOf(profileFor('4-14-4'));
    for (const dead of ['WaterPressure', 'KERS', 'GameData.Headlight']) expect({ dead, inProfile: text.includes(dead) }).toMatchObject({ inProfile: false });
    // ...and the flash itself is there, read from the only place it exists.
    expect(text).toContain('GameRawData.Telemetry.dcHeadlightFlash');
  });

  test('a sides effect is dropped on a shape with no sides rather than moved onto the rev LEDs', () => {
    const brow = textOf(profileFor('brow-25'));
    // The spotters and the assists have nowhere to go on a brow...
    expect(brow).not.toContain('Car alongside, left');
    expect(brow).not.toContain('ABS active');
    // ...but a whole-strip effect still applies, because a brow has a whole strip.
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

  test('is gated on the sim running, because a CustomStatus that throws is ON rather than off', () => {
    for (const shape of ALL_SHAPES) {
      const p = rpmStripProfile(shape, stableGuid(`t/${shape.id}`));
      const outer = shape.reversed ? leds.childrenOf(p.containers[0]!)[0]! : p.containers[0]!;
      expect({ shape: shape.id, type: leds.containerTypeOf(outer) }).toMatchObject({ type: 'Groups.GameRunningGroup' });
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

  test('offers every centre function and carries a stable id', () => {
    const text = textOf(profileFor('4-14-4'));
    for (const centre of LED_CENTRES) {
      if (centre === 'rpm' || centre === 'rpmOnly') continue;
      expect({ centre, present: text.includes(`centre: ${centre}`) }).toMatchObject({ present: true });
    }
    expect(text).toContain('centre: rpm or rpmOnly');
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
    // The top band lights together at `last`, and the flash is at `blink`.
    expect(tabledStageLit(points, 2, 0, 5)).toBe(`(${rpm}) >= (7500)`);
    expect(tabledOverRev(points)).toBe(`(${rpm}) >= (7800)`);
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

