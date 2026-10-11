/** The NCalc that reaches the file: digit counts, h:mm:ss, the shift lights and the card rules. */
import { describe, expect, test } from 'bun:test';
import { leds, ncalc, stableGuid } from '../src/generator.ts';
import { revBar, REDLINE_BLINK_MS } from '../src/components/revBar.ts';
import { stageOf } from '../src/components/revSegments.ts';
import { GEAR_COUNT_PROPERTY, SHIFT_RPM_PROPERTIES, carLadderSegmentLit, lastGear, redlineRpm } from '../src/shift.ts';
import { MODULES } from '../src/modules/index.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { readFileSync } from 'node:fs';
import { flagVisible } from '../src/components/flagStrip.ts';
import { bandRaised, conditionVisible, flagCondition, FLAG_CATALOGUE, flagsAllowedHere } from '../src/flags.ts';
import { flagBox, setting } from '../src/contract.ts';
import { flagBoxTree } from '../src/leds/profile.ts';
import { ignitionIsOff, ignitionIsOn } from '../src/leds/gates.ts';
import { rpmStripProfile } from '../src/leds/rpmStrip.ts';
import { shapeById } from '../src/leds/strip.ts';
import { CARDS, cardByNumber } from '../src/cards/index.ts';
import { rect } from '../src/design/geometry.ts';
import { expressionsOf, walkItems } from '../src/walk.ts';
import { sectorIsSlower, sectorIsZero } from '../src/second/sectors.ts';
import { temperatureColour } from '../src/second/wheel.ts';
import * as values from '../src/second/values.ts';
import type { TextItem } from '../src/generator.ts';
import { evalNcalc } from './ncalcEval.ts';

const slot = rect(0, 0, 255, 187);
const textItem = (id: string, name: string): TextItem => {
  const item = cardByNumber(CARDS.findIndex((c) => c.id === id)).build(slot, `${id}.`).find((i) => i.name === `${id}.${name}`);
  if (!item || item.kind !== 'text') throw new Error(`${id}.${name} is not a text item`);
  return item;
};
/**
 * A numeric NCalc formula evaluated in JavaScript, every property read standing for `value`.
 *
 * A formula that is arithmetic rather than a string is worth what it computes, and the dial's two
 * are the whole of its movement. The sine and cosine are .NET's and therefore JavaScript's to six
 * decimals, and the clamp's `min` and `max` answer in their left operand's type as NCalc's do, which
 * the shared evaluator models (#1046).
 */
const evaluateNumber = (formula: string, value: number): number => {
  const props = Object.fromEntries([...formula.matchAll(/\[([^\]]+)\]/g)].map(([, name]) => [name, value]));
  return Number(evalNcalc(formula, props));
};

const formulaOf = (item: TextItem, target: 'Text' | 'TextColor' | 'Left' | 'Visible'): string => {
  const b = item.bindings?.[target];
  if (!b || b.mode !== 'formula' || typeof b.formula !== 'string') throw new Error(`${item.name} has no ${target} formula`);
  return b.formula;
};

describe('ncalc helpers', () => {
  test('digitCount nests ifs from the largest threshold down', () => {
    expect(ncalc.digitCount('[X]', 1)).toBe('1');
    expect(ncalc.digitCount('[X]', 2)).toBe('if(([X]) >= (10), 2, 1)');
    expect(ncalc.digitCount('[X]', 3)).toBe('if(([X]) >= (100), 3, if(([X]) >= (10), 2, 1))');
  });

  test('signed replaces the hyphen .NET writes with the typographic minus', () => {
    expect(ncalc.signed('[X]', '0.00')).toBe("replace(format([X], '0.00', true), '-', '\u2212')");
  });

  test('hms formats seconds as h:mm:ss without TimeSpan format strings', () => {
    const e = ncalc.hms('[S]');
    expect(e).toContain('max(0.0, [S])');
    expect(e).toContain('/ (3600)');
    expect(e).toContain("format(truncate(((max(0.0, [S])) % (3600)) / (60)), '00')");
    expect(e).toContain("format(truncate((max(0.0, [S])) % (60)), '00')");
    expect(e).not.toContain('\\');
  });
});

describe('card expressions', () => {
  test('position follower Left is x + digitCount * cell + gap', () => {
    const left = formulaOf(textItem('position', 'denominator'), 'Left');
    expect(left).toMatch(/^\(16\) \+ \(\(if\(.* >= \(10\), 2, 1\)\) \* \(31\)\) \+ \(8\)$/);
    expect(left).toContain('driverclassposition(getplayerleaderboardposition())');
    expect(formulaOf(textItem('position', 'denominator'), 'Text')).toContain("('/ ') + (format(");
  });

  test('session resolves its mode from the setting and the 0 < time left <= 86400 guard', () => {
    const mode = "isnull([OpenDash.SessionProgress], 'auto')";
    const secs = 'timespantoseconds([DataCorePlugin.GameData.SessionTimeLeft])';
    const timed = `((${secs}) > (0)) and ((${secs}) <= (86400))`;
    const time = `((${mode}) = ('time')) or (((${mode}) = ('auto')) and (${timed}))`;
    expect(formulaOf(textItem('session', 'label'), 'Text')).toBe(`if(${time}, 'Time left', 'Lap')`);
    const value = formulaOf(textItem('session', 'value'), 'Text');
    expect(value).toBe(`if(${time}, if(${timed}, ${ncalc.hms(secs)}, '-:--:--'), format([DataCorePlugin.GameData.CurrentLap], '0'))`);
    expect(value).toContain('/ (3600)');
    // Dim is the clock nobody is counting, which is the session that has not started. An untimed
    // session is the third state and draws the `∞` mark beside this clock at full strength, so it is
    // excluded from the dim here rather than folded in with the absence (#439).
    const untimed = `(${secs}) > (86400)`;
    expect(formulaOf(textItem('session', 'value'), 'TextColor')).toBe(`if((${time}) and (!(${timed})) and (!(${untimed})), '#33383F', '#F5F7FA')`);
    const mark = textItem('session', 'mark');
    expect({ text: mark.text, mono: mark.monospace, bound: mark.bindings?.Text }).toMatchObject({ text: '∞', mono: undefined, bound: undefined });
    expect(formulaOf(mark, 'Visible')).toBe(`(${time}) and (${untimed})`);
    expect(formulaOf(textItem('session', 'value'), 'Visible')).toBe(`!((${time}) and (${untimed}))`);
    const denominator = textItem('session', 'denominator');
    expect(formulaOf(denominator, 'Text')).toBe("('/ ') + (format([DataCorePlugin.GameData.TotalLaps], '0'))");
    expect(formulaOf(denominator, 'Visible')).toBe(`(!(${time})) and (([DataCorePlugin.GameData.TotalLaps]) > (0))`);
    expect(formulaOf(denominator, 'Left')).toContain('>= (100), 3');
    for (const item of ['label', 'value', 'mark', 'denominator'] as const) {
      const expressions = Object.values(textItem('session', item).bindings ?? {}).map((b) => (b && typeof b.formula === 'string' ? b.formula : ''));
      for (const e of expressions) expect(e.replace(/isnull\(\[OpenDash\.SessionProgress\], 'auto'\)/g, '')).not.toContain('OpenDash.');
    }
  });

  // #382. The card drew its own gate, `Fuel_RemainingLaps <= 0`, where band D's fuel page and the
  // fuel module draw the same property behind a completed lap, so on the out lap the main face
  // named a figure that moves every frame while the second screen beside it drew its absence. The
  // absence is `--` here too: `-.-` was the one card writing a no-data glyph of its own.
  // And since #791 it is red under the rig's one low-fuel threshold rather than under a lap of its
  // own, the threshold the strip, the box and the fuel module read.
  test('fuel laps wait for the lap that says what one costs, then go red under the rig threshold', () => {
    const laps = 'isnull([DataCorePlugin.Computed.Fuel_RemainingLaps], 0)';
    const settled = values.fuelIsSettled();
    const threshold = 'isnull([OpenDash.LightsLowFuelLaps], isnull([OpenDash.FlagBoxLowFuelLaps], 2))';
    expect(formulaOf(textItem('fuelLaps', 'value'), 'Text')).toBe(`if(${settled}, format(${laps}, '0.0'), '${values.NO_VALUE}')`);
    expect(formulaOf(textItem('fuelLaps', 'value'), 'TextColor')).toBe(`if(${settled}, if((${laps}) < (${threshold}), '#FF2D46', '#F5F7FA'), '#33383F')`);
  });

  test('fuel unit Left adds one digit cell for the decimal and one special for the point', () => {
    const left = formulaOf(textItem('fuel', 'unit'), 'Left');
    expect(left).toMatch(/\+ \(1\)\) \* \(31\)\) \+ \(17\) \+ \(8\)$/);
    // Through `fuelUnit`, the one place the sim's enum becomes the symbol a driver reads, and in the
    // symbol's own case: nothing upper-cases a unit any longer.
    expect(formulaOf(textItem('fuel', 'unit'), 'Text')).toBe(values.fuelUnit());
    expect(formulaOf(textItem('fuel', 'unit'), 'Text')).not.toContain('ucase');
  });

  test('lap times use toshorttime with forced minutes and dim no-data glyphs', () => {
    expect(formulaOf(textItem('currentLap', 'value'), 'Text')).toBe(
      "if((timespantoseconds([DataCorePlugin.GameData.CurrentLapTime])) <= (0), '−:−−.−', toshorttime([DataCorePlugin.GameData.CurrentLapTime], 1, false, true))",
    );
    // One spelling of the placeholder, shared with the module pages, and a true minus in every
    // cell: the cards wrote theirs with hyphens and one glyph fewer than the time it stands in for.
    expect(values.NO_TIME).toBe('−:−−.−−−');
    expect(formulaOf(textItem('lastLap', 'value'), 'Text')).toContain(`'${values.NO_TIME}'`);
    expect(formulaOf(textItem('bestLap', 'value'), 'Text')).toContain(`'${values.NO_TIME}'`);
    expect(formulaOf(textItem('lastLap', 'value'), 'Text')).toContain('toshorttime([DataCorePlugin.GameData.LastLapTime], 3, false, true)');
    expect(formulaOf(textItem('lastLap', 'value'), 'TextColor')).toContain("< (0.0005), '#B14BFF'");
    expect(formulaOf(textItem('bestLap', 'value'), 'TextColor')).not.toContain('#B14BFF');
  });

  test('delta reads the reference setting and colours by sign, level within half a unit of its last place', () => {
    const text = formulaOf(textItem('delta', 'value'), 'Text');
    expect(text).toContain("isnull([OpenDash.DeltaReference], 'session')");
    expect(text).toContain('[PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds]');
    // The card reads the one reading the second screens draw, so the third reference reaches it too.
    expect(text).toContain(values.referenceDelta());
    expect(text).toContain('[DataCorePlugin.GameRawData.Telemetry.LapDeltaToSessionLastlLap]');
    // Two literal patterns with the precision choosing between them, `format` taking only a literal.
    expect(text).toContain(values.referenceDeltaText(values.referenceDelta()));
    expect(text).toContain("'0.00', true)");
    expect(text).toContain("'0.000', true)");
    expect(text).toContain("'-', '−'");
    const colour = formulaOf(textItem('delta', 'value'), 'TextColor');
    expect(colour).toContain("< (0), '#00D96A'");
    expect(colour).toContain("'#FF2D46'");
    // The band decides the text and the colour together, so a level delta cannot be drawn as
    // "+0.00" in the resting white: it is the bare "0.00" the canvas draws, or "0.000" at
    // thousandths. Half a unit of the last place, and strictly inside it, because 0.005 is drawn
    // "+0.01" at hundredths and is not level. #322.
    const band = "if((isnull([OpenDash.DeltaPrecision], 'hundredths')) = ('thousandths'), 0.0005, 0.005)";
    expect(values.referenceDeltaBand()).toBe(band);
    const deadband = `if((abs(${values.referenceDelta()})) < (${band}), `;
    expect(text.startsWith(deadband)).toBe(true);
    expect(colour.startsWith(deadband)).toBe(true);
    expect(text).toContain(`${deadband}if((isnull([OpenDash.DeltaPrecision], 'hundredths')) = ('thousandths'), '0.000', '0.00')`);
    expect(colour).toContain(`${deadband}'#F5F7FA'`);
  });

  test('assists show -- without the raw field, OFF at zero', () => {
    expect(formulaOf(textItem('tc', 'value'), 'Text')).toBe(
      "if(isnull([DataCorePlugin.GameRawData.Telemetry.dcTractionControl]), '--', if(([DataCorePlugin.GameData.TCLevel]) = (0), 'OFF', format([DataCorePlugin.GameData.TCLevel], '0')))",
    );
    expect(formulaOf(textItem('abs', 'value'), 'Text')).toContain('dcABS');
    expect(formulaOf(textItem('abs', 'value'), 'TextColor')).toBe(
      "if(isnull([DataCorePlugin.GameRawData.Telemetry.dcABS]), '#33383F', if(([DataCorePlugin.GameData.ABSLevel]) = (0), '#8A9099', '#F5F7FA'))",
    );
  });

  test('tyre temps convert thresholds per unit and tyre pressures spell the unit as its symbol', () => {
    const colour = formulaOf(textItem('tyreTemps', 'fr'), 'TextColor');
    // The card and the wheel cell had a threshold table each, written with the same numbers and
    // free to drift apart; there is one table now, and this is what says so.
    expect(colour).toBe(temperatureColour('FrontRight'));
    expect(colour).toContain("('Fahrenheit'), 140");
    expect(colour).toContain("('Kelvin'), 333, 60");
    expect(colour).toContain("('Fahrenheit'), 212");
    expect(colour).toContain("('Kelvin'), 373, 100");
    expect(formulaOf(textItem('tyreTemps', 'label'), 'Text')).toContain("'°F'");
    expect(formulaOf(textItem('tyrePressures', 'label'), 'Text')).toBe(
      `('Pressures ') + (${values.pressureUnit()}) + (' · last stop')`,
    );
    expect(formulaOf(textItem('tyrePressures', 'rr'), 'Text')).toContain("format(isnull([DataCorePlugin.GameData.TyrePressureRearRight], 0), '0.0')");
  });
});

describe('second-screen values', () => {
  test('a relative gap is three decimals signed with the typographic minus', () => {
    const gap = values.carRelativeGap('1');
    expect(gap).toContain("format(driverrelativegaptoplayer(1), '0.000', true)");
    expect(gap).toContain("'-', '−'");
  });

  test('the no-data lap time is one placeholder of the same shape as the time it stands in for', () => {
    expect(values.NO_TIME).toBe('−:−−.−−−');
    expect(values.NO_TIME).toHaveLength('1:42.905'.length);
    expect(values.noTime(1)).toHaveLength('1:42.3'.length);
    expect(values.lapTime('[T]', 1)).toContain(`'${values.noTime(1)}'`);
  });

  test('a missing lap time draws the placeholder rather than throwing (#454)', () => {
    // SimHub's timespantoseconds answers null for the number 0, and NCalc's `null > 0` throws, which
    // SimHub draws as an empty field. So the null guard wraps the conversion and never sits inside it.
    expect(values.hasTime('[T]')).toBe('(isnull(timespantoseconds([T]), 0)) > (0)');
    expect(values.lapTime('[T]')).not.toContain('timespantoseconds(isnull(');
    expect(values.sectorTime('[T]')).not.toContain('timespantoseconds(isnull(');
  });

  test('a unit reaches the screen as the word the face draws, not the name of its enum', () => {
    expect(values.speedUnit()).toBe("if((isnull([DataCorePlugin.GameData.SpeedLocalUnit], 'KMH')) = ('MPH'), 'mph', 'km/h')");
    expect(values.fuelUnit()).toContain("'gal'");
    expect(values.fuelUnit()).toContain("'L'");
    expect(values.pressureUnit()).toContain("'kPa'");
    // The three temperature marks, `Celcius`'s own spelling included, and no degree sign on Kelvin.
    expect(values.temperatureMark()).toContain("'°C'");
    expect(values.temperatureMark()).toContain("'°F'");
    expect(values.temperatureMark()).toContain("'K'");
    expect(values.temperatureMark()).toContain("'Celcius'");
    expect(values.temperatureMark()).not.toContain("'°K'");
  });

  test('the five-lap average reads the five history slots and waits for all five', () => {
    const average = values.average5();
    // From slot zero, which is the lap just completed: `lapHistory` draws that slot as row one, so
    // an average starting at one was the mean of laps two to six and moved a lap late.
    for (const slot of [0, 1, 2, 3, 4]) expect(average).toContain(`('PersistantTrackerPlugin.PreviousLap_') + (format(${slot}, '00'))`);
    expect(average).not.toContain("format(5, '00')");
    expect(average).toContain('/ (5)');
    expect(average).toContain(`'${values.NO_TIME}'`);
  });

  test("a tyre wears to its worst section, and to SimHub's own figure where there are no sections", () => {
    const wear = values.tyreWearMin('FrontLeft');
    expect(wear).toBe(
      'if(isnull([DataCorePlugin.GameRawData.Telemetry.LFwearM]), isnull([DataCorePlugin.GameData.TyreWearFrontLeft], 0), ' +
        '(min([DataCorePlugin.GameRawData.Telemetry.LFwearL], min([DataCorePlugin.GameRawData.Telemetry.LFwearM], ' +
        '[DataCorePlugin.GameRawData.Telemetry.LFwearR]))) * (100))',
    );
  });

  test('the grip status is drawn as the sim words it, and Moderate is the word a box is cut for', () => {
    expect(values.trackGrip()).toBe("isnull([DataCorePlugin.GameData.TrackGripStatus], '--')");
    expect(values.GRIP_WIDEST).toBe('Moderate');
  });

  test('a low tank is one sentence, against the threshold every light reads', () => {
    expect(values.tankIsLow()).toBe(
      `(${values.fuelIsSettled()}) and ((isnull([DataCorePlugin.Computed.Fuel_RemainingLaps], 999)) < (${flagBox.lowFuelLaps()}))`,
    );
    // And the gate is a completed lap, not merely an estimate: before the first crossing SimHub
    // extrapolates the partial lap, so every figure derived from it moves every frame.
    expect(values.fuelIsSettled()).toContain('CompletedLaps');
    expect(values.fuelIsSettled()).toContain('Fuel_LitersPerLap');
    // The laps a warning is raised on default high where the laps a field draws default to zero, so
    // a sim that computes none leaves the warning away rather than raising it on every car.
    expect(values.fuelLapsLeft()).toContain(', 0)');
    expect(values.tankIsLow()).not.toContain(values.fuelLapsLeft());
  });

  test('a low tank waits for the sim to know what a lap costs, so an idle screen raises nothing', () => {
    // SimHub publishes Fuel_RemainingLaps as zero, not null, before a lap has been run, so the
    // threshold alone was true at every idle screen and lit the telltale, the pop-up, the flag box
    // and every strip at once. The consumption gate is the one the fuel module's own "est. laps"
    // already draws behind.
    expect(values.tankIsLow()).toContain(values.fuelPerLap());
  });

  test('the box and the strip read one ignition gate, and an unpublished ignition means on', () => {
    expect(values.ignitionOn()).toBe('[DataCorePlugin.GameData.EngineIgnitionOn]');
    const tree = JSON.stringify(flagBoxTree());
    expect(tree).toContain(ignitionIsOff());
    expect(tree).toContain(ignitionIsOn());
    // The strip had no gate of any kind, so a wheel lit the garage beside a box showing standby.
    const strip = leds.serializeProfile(rpmStripProfile(shapeById('4-14-4')!, stableGuid('t/ignition')));
    expect(strip).toContain(ignitionIsOn());
    // Defaulted to on, which is the reverse of the convention: a sim that publishes no ignition
    // must not have every light OpenDash drives blacked out for the whole of a session.
    expect(ignitionIsOn()).toContain(`isnull(${values.ignitionOn()}, 1)`);
    expect(ignitionIsOff()).toContain(`isnull(${values.ignitionOn()}, 1)`);
  });
});

describe('hero expressions', () => {
  const RPMS = 'isnull([DataCorePlugin.GameData.Rpms], 0)';
  const SL = (n: string) => `isnull([DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarSL${n}RPM], 0)`;
  const MIRROR = `((${SL('First')}) > (0)) and ((${SL('Last')}) > (${SL('First')})) and ((${SL('Shift')}) >= (${SL('First')})) and ((${SL('Last')}) >= (${SL('Shift')}))`;
  const ON = setting.revBarIs('shift');
  const GEARS = 'isnull([DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarGearNumForward], 0)';
  const LAST_GEAR = `((${GEARS}) > (0)) and ((isnull([DataCorePlugin.GameRawData.Telemetry.Gear], 0)) >= (${GEARS}))`;
  // The car's own measured bar, as the plugin publishes it: the gate a screen draws it behind, the
  // two numbers it draws it from, the RPM it reddens at, and its flash. Spelled out rather than
  // called, for the reason MIRROR is: a shared helper name would pass with the bar and the readout
  // beside it reading different properties, which is the defect ADR 0014 exists over.
  const LIT = 'isnull([OpenDash.CarLadderLit], 0)';
  const LAMPS = 'isnull([OpenDash.CarLadderLamps], 0)';
  const TOP_RPM = 'isnull([OpenDash.CarLadderTopRpm], 0)';
  const CAR = `((isnull([OpenDash.CarLadderChosen], false)) = (true)) and ((isnull([OpenDash.CarLadderStage], -1)) >= (0))`;
  const CAR_OWN_FLASH = `((isnull([OpenDash.CarLadderOverRev], false)) = (true)) and (!(${LAST_GEAR}))`;
  // The two derived flashes and the choice between them, which is what the measured bar falls back to
  // for a car whose table carries no flash -- 47 of the 85. Spelled out here too, for the same reason.
  // The blink's fallback is the double 0.0, so the max keeps the last light's own type (#1046).
  const BLINK = 'isnull([DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarSLBlinkRPM], 0.0)';
  const MIRROR_FLASH = `((${RPMS}) >= (max(${BLINK}, ${SL('Last')}))) and (!(${LAST_GEAR}))`;
  const SIMHUB_FLASH = `((isnull([DataCorePlugin.GameData.CarSettings_RPMRedLineReached], 0)) = (1)) and (!(${LAST_GEAR}))`;
  const EITHER_FLASH = `((${MIRROR}) and (${MIRROR_FLASH})) or ((!(${MIRROR})) and (${SIMHUB_FLASH}))`;
  const FLASHES = `(isnull([OpenDash.CarLadderFlashes], false)) = (true)`;
  const CAR_FLASH = `(${CAR_OWN_FLASH}) or ((!(${FLASHES})) and (${EITHER_FLASH}))`;

  const segOf = (layer: { children: readonly unknown[] }, k: number) => {
    const s = layer.children[k];
    if (!s || typeof s !== 'object' || (s as { kind?: string }).kind !== 'rect') throw new Error('segment');
    return s as Extract<import('../src/generator.ts').Item, { kind: 'rect' }>;
  };

  /** A field of the speedo page, drawn at the one shape that keeps all three of them. */
  const speedoField = (id: string): TextItem => {
    const speedo = MODULES.find((m) => m.id === 'speedo');
    if (!speedo) throw new Error('no speedo module');
    const box = rect(0, 0, SHAPE_ARCHETYPES.wide.width, SHAPE_ARCHETYPES.wide.height);
    const item = [...walkItems(speedo.build({ frame: box, density: 'companion', prefix: 'speedo.' }))].find((i) => i.name === `speedo.${id}.value`);
    if (!item || item.kind !== 'text') throw new Error(`speedo.${id}.value is not a text item`);
    return item;
  };

  test('the rev bar is four layers, and exactly one of them is visible at a time', () => {
    const layers = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    expect(layers.map((l) => l.kind)).toEqual(['layer', 'layer', 'layer', 'layer']);
    expect(layers.map((l) => l.name)).toEqual(['revBar.shiftLightsCar', 'revBar.shiftLights', 'revBar.shiftLightsSimHub', 'revBar.rpmBar']);
    // The gate is the tri-state RevBar (ADR 0004, #189), not the deprecated ShiftLights boolean,
    // so `rpm` and `off` both land on the plain bar and the ladder split applies only within `shift`.
    expect(ON).toContain('[OpenDash.RevBar]');
    // Which ladder a car is on is which layer is visible, which is how it is seen in Dash Studio.
    // Three of them now: the car's own measured bar first (#353), then the two derived ladders behind
    // it, whose own per-frame choice is unchanged. The four tests are exhaustive and disjoint, so no
    // state draws two bars or none.
    expect(layers[0]!.bindings?.Visible).toEqual({ mode: 'formula', formula: `(${ON}) and (${CAR})` });
    expect(layers[1]!.bindings?.Visible).toEqual({ mode: 'formula', formula: `(${ON}) and ((!(${CAR})) and (${MIRROR}))` });
    expect(layers[2]!.bindings?.Visible).toEqual({ mode: 'formula', formula: `(${ON}) and ((!(${CAR})) and (!(${MIRROR})))` });
    expect(layers[3]!.bindings?.Visible).toEqual({ mode: 'formula', formula: `!(${ON})` });
    // The gate is the rig's own answer to whose lights these are and something publishing a bar, both
    // of them: a driver who chose one of OpenDash's own styles keeps the derived ladders, and a driver
    // who has never fetched the tables has asked for the car's and has nothing behind it.
    //
    // `CarLadderChosen` and not `LedRpmStyle`, which is the review finding on #353's first cut: the
    // rig-wide style is a field the panel has not written since the styles went per bar, so a driver
    // who set their one strip to F1 left it at its default and got the car's instants on every screen.
    // The plugin reduces the bars to this one boolean because a bar is added at runtime and its own
    // property name cannot appear in an expression a package was built with.
    expect(CAR).toContain('[OpenDash.CarLadderChosen]');
    expect(CAR).not.toContain('[OpenDash.LedRpmStyle]');
    expect(CAR).toContain('[OpenDash.CarLadderStage]');
  });

  test("the car's own measured bar: the car's instants, OpenDash's colours, and no table on the screen", () => {
    const [car] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    if (car?.kind !== 'layer') throw new Error('layer');
    const seg = (k: number) => segOf(car, k);

    // Segment k of fifteen lights once the car has lit as much of its own bar, as a
    // cross-multiplication in integers: nothing divides, and a segment lights on the frame the car
    // lights its own LED rather than a rounding either side of it. The first reduces to the count
    // leaving zero, the way the first segment of a band does on either derived ladder.
    expect(expressionsOf(seg(0))).toEqual([`if((${LIT}) > (0), '#00D96A', '#33383F')`]);
    expect(expressionsOf(seg(4))).toEqual([`if(((${LIT}) * (15)) > ((4) * (${LAMPS})), '#00D96A', '#33383F')`]);
    expect(expressionsOf(seg(5))).toEqual([`if(((${LIT}) * (15)) > ((5) * (${LAMPS})), '#FFB300', '#33383F')`]);
    // The top band's segments carry the flash as a second expression, which is why this one is the
    // colour rather than the whole list.
    expect(seg(10).bindings?.BackgroundColor).toEqual({ mode: 'formula', formula: `if(((${LIT}) * (15)) > ((10) * (${LAMPS})), '#FF2D46', '#33383F')` });
    // The colours are the three bands' -- thirds of the segments, out of design/tokens.json -- and
    // never the car's. That is the whole of #353's answer, and the reason this layer is the same
    // fifteen segments as the two behind it rather than a mirror: a 992's cornflower blue belongs on
    // the strip, which is a copy of its bar.
    for (const k of [0, 4, 5, 10, 14]) {
      const formula = String(seg(k).bindings?.BackgroundColor?.formula ?? '');
      expect({ k, unlit: formula.endsWith(", '#33383F')") }).toEqual({ k, unlit: true });
    }
    // The flash is the car's own redline for the gear it is in -- a threshold of its own, not the top
    // band -- and it stops in the last gear, on the same terms as both derived ladders.
    //
    // With one fallback, which is a review finding on this ticket's first cut: `CarLightMirror.OverRev`
    // is false outright for a car whose table carries no blink interval, no redline or no blink colour,
    // and that is 47 of the 85 measured cars. A strip mirroring one of them does not blink, which is
    // right, because a strip is a copy of the car's bar. This bar is OpenDash's own and its top band has
    // flashed at redline since ADR 0004, so where the car says it never flashes the threshold is the
    // published one -- exactly what this segment blinked on before the tables reached it. Only where the
    // car says so, never merely where it is not flashing yet, or a car with a flash above the published
    // threshold would flash early and the two would fight.
    expect(seg(14).bindings?.BlinkEnabled).toEqual({ mode: 'formula', formula: CAR_FLASH });
    expect(CAR_FLASH).toContain('[OpenDash.CarLadderFlashes]');
    expect(CAR_FLASH).toContain(EITHER_FLASH);
    expect(seg(14).blink).toEqual({ delayMs: 62 });
    expect(seg(9).blink).toBeUndefined();
    // And what lights the segments is the plugin's answer and nothing else: no threshold, no colour,
    // no gear, nothing of the table. One definition, read twice, which is what the ticket asked for.
    const text = JSON.stringify(car);
    expect(text).toContain(LIT);
    expect(text).toContain(LAMPS);
    const colours = Array.from({ length: 15 }, (_, k) => String(seg(k).bindings?.BackgroundColor?.formula ?? '')).join('\n');
    expect(colours).not.toContain('DriverCarSL');
    expect(colours).not.toContain('CarSettings_RPMShiftLight');
    // The published RPMs reach this layer in exactly one place, the top band's flash, and only inside
    // the branch that exists for a car whose table carries no flash at all. Naming it here rather than
    // forbidding it: `not.toContain('DriverCarSL')` over the whole layer read as "nothing of the
    // published ladder is in here", and after the fallback that is true of the lights and not of the
    // flash.
    expect(text).toContain('DriverCarSLBlinkRPM');
    expect(String(seg(14).bindings?.BlinkEnabled?.formula ?? '')).toContain('DriverCarSLBlinkRPM');
  });

  test("the car's own ladder: nothing below First, bands at First and Shift, the last band at Last, the flash at Blink", () => {
    const [, shift] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    if (shift?.kind !== 'layer') throw new Error('layer');
    const seg = (k: number) => segOf(shift, k);

    // The first segment of a band is the band's entry test and nothing more.
    expect(expressionsOf(seg(0))).toEqual([`if((${RPMS}) > (${SL('First')}), '#00D96A', '#33383F')`]);
    expect(expressionsOf(seg(5))).toEqual([`if((${RPMS}) > (${SL('Shift')}), '#FFB300', '#33383F')`]);
    // ...and the rest carry the band's progress as a cross-multiplication, so a zero-width band divides by nothing.
    expect(expressionsOf(seg(4))).toEqual([
      `if(((${RPMS}) > (${SL('First')})) and ((((${RPMS}) - (${SL('First')})) * (5)) > ((4) * ((${SL('Shift')}) - (${SL('First')})))), '#00D96A', '#33383F')`,
    ]);
    // The last band lights together at the last light, and flashes above the blink RPM rather than at redline.
    const blink = `max(${BLINK}, ${SL('Last')})`;
    expect(seg(10).bindings?.BackgroundColor).toEqual({ mode: 'formula', formula: `if((${RPMS}) >= (${SL('Last')}), '#FF2D46', '#33383F')` });
    expect(seg(14).bindings?.BlinkEnabled).toEqual({ mode: 'formula', formula: `((${RPMS}) >= (${blink})) and (!(${LAST_GEAR}))` });
    expect(seg(14).blink).toEqual({ delayMs: 62 });
    expect(seg(9).blink).toBeUndefined();
    expect(REDLINE_BLINK_MS).toBe(62);
  });

  test("SimHub's bands are unchanged for a car that publishes no ladder of its own, and flash at redline outside the last gear", () => {
    const [, , simhub, rpm] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    if (simhub?.kind !== 'layer' || rpm?.kind !== 'layer') throw new Error('layers');
    const seg = (k: number) => segOf(simhub, k);
    // Null-safe: with no sim these are null, and an LED reading a bare band lights up, because
    // CustomStatusContainer catches the throw and falls back to 1.0. ADR 0003's rule, applied late.
    const B = (n: 1 | 2) => `isnull([DataCorePlugin.GameData.CarSettings_RPMShiftLight${n}], 0)`;
    const REDLINE = '(isnull([DataCorePlugin.GameData.CarSettings_RPMRedLineReached], 0)) = (1)';
    // The first segment of a band is the band's entry test and nothing more, the same reduction the
    // car's own ladder makes above: `x * 5 > 0` and `x > 0` are one test, and writing it once is
    // what lets anything with a single thing to colour ask the same question (`stageEntered`).
    expect(expressionsOf(seg(0))).toEqual([`if((${B(1)}) > (0), '#00D96A', '#33383F')`]);
    expect(expressionsOf(seg(4))).toEqual([`if(((${B(1)}) * (5)) > (4), '#00D96A', '#33383F')`]);
    expect(expressionsOf(seg(5))).toEqual([`if((${B(2)}) > (0), '#FFB300', '#33383F')`]);
    expect(seg(10).bindings?.BackgroundColor).toEqual({ mode: 'formula', formula: `if(${REDLINE}, '#FF2D46', '#33383F')` });
    // ADR 0004's band is unchanged; its flash is not. The last-gear exception belongs to the flash
    // rather than to the ladder, so the fallback half carries it too: a car that publishes zeros
    // for its four RPMs lands here, and this is the half that went on strobing in top gear.
    expect(seg(14).bindings?.BlinkEnabled).toEqual({ mode: 'formula', formula: `(${REDLINE}) and (!(${LAST_GEAR}))` });
    expect(seg(14).blink).toEqual({ delayMs: 62 });

    expect(expressionsOf(segOf(rpm, 3))).toEqual(["if(([DataCorePlugin.GameData.CarSettings_CurrentDisplayedRPMPercent]) > (20), '#8A9099', '#33383F')"]);
    expect(segOf(rpm, 14).blink).toBeUndefined();
  });

  test('every rev segment keeps its 2 px radius in all three layers', () => {
    for (const s of walkItems(revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 }))) {
      if (s.kind === 'rect') expect(s.border).toEqual({ radius: 2 });
    }
  });

  test('the last gear stops the flash but not the light: one ladder per car, no per-gear table', () => {
    const [, shift, simhub] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    if (shift?.kind !== 'layer' || simhub?.kind !== 'layer') throw new Error('layers');

    // The top band is still lit at Last in every gear: the bar still says the engine is at its limit.
    expect(expressionsOf(segOf(shift, 14))[0]).toContain(`(${RPMS}) >= (${SL('Last')})`);
    // Only the flash is suppressed, and only there.
    expect(segOf(shift, 14).bindings?.BlinkEnabled?.formula).toContain(`!(${LAST_GEAR})`);
    expect(segOf(shift, 14).bindings?.BackgroundColor?.formula).not.toContain('GearNumForward');

    // The gear count comes from the same DriverInfo block as the four RPMs, so there is no new source...
    expect(GEAR_COUNT_PROPERTY).toBe('DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarGearNumForward');
    // ...and a car that does not publish one keeps flashing, because the guard is count > 0.
    expect(lastGear()).toBe(LAST_GEAR);

    // The bands themselves are gear-independent: no gear appears in any segment's colour.
    for (const k of [0, 4, 5, 9, 10]) expect(segOf(shift, k).bindings?.BackgroundColor?.formula).not.toContain('Gear');
    // And the same on SimHub's fallback, which is the half that was missing it: the gear count is
    // published by the sim rather than by the ladder, so which ladder a car is on cannot decide
    // whether there is a shift to ask for. Its band is still gear-independent, as above.
    expect(segOf(simhub, 14).bindings?.BlinkEnabled?.formula).toContain(`!(${LAST_GEAR})`);
    expect(segOf(simhub, 14).bindings?.BackgroundColor?.formula).not.toContain('Gear');
  });

  test("the speedo's Redline prints the RPM the rev bar's top band lights at", () => {
    // ADR 0014 applied to a readout, and the defect that hid behind the bar being right. The field
    // printed `CarSettings_CurrentGearRedLineRPM` unconditionally -- SimHub's number, stock
    // `DriverCarRedLine * 95/100` -- directly under a bar whose top band goes red at
    // `DriverCarSLLastRPM`. Two answers to one question, side by side, on the same page.
    //
    // String equality against what both sides emit, the way the flag box's flash is pinned in
    // flagBox.test.ts. A shared helper name would pass with the two reading different properties.
    const [car, shift] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    if (car?.kind !== 'layer' || shift?.kind !== 'layer') throw new Error('layers');

    // The bar's top band is `Rpms >= X`. X is the number a readout beside it has to print.
    const band = String(segOf(shift, 14).bindings?.BackgroundColor?.formula ?? '');
    const parts = /^if\(\((.*)\) >= \((.*)\), '#[0-9A-F]{6}', '#[0-9A-F]{6}'\)$/.exec(band);
    expect(parts?.[1]).toBe(RPMS);
    const threshold = parts?.[2] ?? '';
    expect(threshold).toBe(SL('Last'));

    // Three deep since #353, in the precedence the bar draws in. Under the car's own ladder the
    // printed number IS that threshold, character for character. Under SimHub's there is no RPM to
    // print -- ADR 0004's fallback is two band progress values and a `RedLineReached` flag -- so the
    // fallback is SimHub's own redline, which is the single seam and is a property of what SimHub
    // exposes rather than a second model.
    const simhub = 'isnull([DataCorePlugin.GameData.CarSettings_CurrentGearRedLineRPM], 0)';
    expect(redlineRpm()).toBe(`if(${CAR}, ${TOP_RPM}, if(${MIRROR}, ${threshold}, ${simhub}))`);
    expect(formulaOf(speedoField('redline'), 'Text')).toBe(`format(${redlineRpm()}, '#,0')`);

    // And on the measured bar the printed number is the threshold of the segment that reddens, which
    // is the same promise one rung up: the plugin's `TopRpm` is the RPM its tenth of fifteen segments
    // lights at, and the digit on a flag box changes band on the same frame (CarLightsTests pins the
    // other half of that in C#).
    expect(String(segOf(car, 10).bindings?.BackgroundColor?.formula ?? '')).toBe(`if(((${LIT}) * (15)) > ((10) * (${LAMPS})), '#FF2D46', '#33383F')`);

    // And the choice is the bar's own gate, evaluated the same frame, not a second test that could
    // drift: the same strings the bar picks its visible layer with.
    expect(formulaOf(speedoField('redline'), 'Text')).toContain(MIRROR);
    expect(formulaOf(speedoField('redline'), 'Text')).toContain(CAR);
    expect(String(car.bindings?.Visible?.formula ?? '')).toContain(CAR);
    expect(String(shift.bindings?.Visible?.formula ?? '')).toContain(MIRROR);

    // The RPM readout above it is still engine speed and nothing else, so the page has not simply
    // printed the same number twice.
    expect(formulaOf(speedoField('rpm'), 'Text')).toBe(`format(${RPMS}, '#,0')`);
  });

  test('the four shift RPM property names appear in exactly one module', () => {
    expect(Object.values(SHIFT_RPM_PROPERTIES)).toEqual([
      'DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarSLFirstRPM',
      'DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarSLShiftRPM',
      'DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarSLLastRPM',
      'DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarSLBlinkRPM',
    ]);
    const src = new Bun.Glob('**/*.ts');
    const offenders: string[] = [];
    for (const file of src.scanSync({ cwd: `${import.meta.dir}/../src`, absolute: true })) {
      if (file.endsWith('/shift.ts')) continue;
      if (/DriverCarSL[A-Za-z]*RPM/.test(readFileSync(file, 'utf8'))) offenders.push(file);
    }
    expect(offenders).toEqual([]);
  });

  test('flags are visible by priority', () => {
    // The ring and the pit wall header still read the six SimHub normalises, ranked in the
    // catalogue's order: the chequer is last of them now, where it used to be second. Each asks
    // first whether a flag may show where the car is, which is the pit lane switch (#791).
    const here = '(((isnull([OpenDash.FlagsInPitLane], true)) = (true)) or (!((isnull([DataCorePlugin.GameData.IsInPitLane], 0)) > (0))))';
    expect(flagsAllowedHere()).toBe(here.slice(1, -1));
    expect(flagVisible('Flag_Black')).toBe(`${here} and (([DataCorePlugin.GameData.Flag_Black]) = (1))`);
    expect(flagVisible('Flag_Yellow')).toBe(`${here} and (([DataCorePlugin.GameData.Flag_Black]) = (0)) and (([DataCorePlugin.GameData.Flag_Yellow]) = (1))`);
    expect(flagVisible('Flag_Checkered').split(' and ')).toHaveLength(7);
    expect(flagVisible('Flag_Green').split(' and ')).toHaveLength(6);
  });

  test('band D ranks the whole catalogue off the bits, not the six summaries', () => {
    // One layer per condition, each gated on its own bits and on every higher condition being
    // absent, read null-safely so that a sim publishing no SessionFlagsDetails leaves the band dark,
    // and each asking whether a flag may show where the car is (#791).
    const red = conditionVisible(flagCondition('red'), false, FLAG_CATALOGUE, bandRaised);
    expect(red).toBe(`((${flagsAllowedHere()}) and (((isnull([DataCorePlugin.GameRawData.Telemetry.SessionFlagsDetails.Isred], 0)) = (1))))`);
    const yellow = conditionVisible(flagCondition('yellow'), false, FLAG_CATALOGUE, bandRaised);
    expect(yellow).toContain('SessionFlagsDetails.IsyellowWaving');
    expect(yellow).toContain('SessionFlagsDetails.Isred');
    expect(yellow).not.toContain('GameData.Flag_');
    // The green is the exception, and the only one: SimHub limits Flag_Green where the bit is held
    // all race, so the band reads the limited property and a green flag is an event again.
    const green = conditionVisible(flagCondition('green'), false, FLAG_CATALOGUE, bandRaised);
    expect(green).toInclude('(isnull([DataCorePlugin.GameData.Flag_Green], 0)) = (1)');
    expect(green).not.toContain('SessionFlagsDetails.Isgreen');
  });
});

describe('module expressions', () => {
  /** A text item of a module, at the zone density, where a module keeps every field it declares. */
  const moduleItem = (id: string, name: string): TextItem => {
    const module = MODULES.find((m) => m.id === id);
    if (!module) throw new Error(`no ${id} module`);
    const box = rect(0, 0, SHAPE_ARCHETYPES.wide.width, SHAPE_ARCHETYPES.wide.height);
    const item = [...walkItems(module.build({ frame: box, density: 'zone', prefix: `${id}.` }))].find((i) => i.name === `${id}.${name}`);
    if (!item || item.kind !== 'text') throw new Error(`${id}.${name} is not a text item`);
    return item;
  };

  test('the fuel page paints nothing low until a lap has said what one costs (#382)', () => {
    // `Fuel_RemainingLaps` is zero before the first crossing and zero is under a lap, so the level
    // and the estimate beside it were red on a full tank at an idle screen, next to the `--` the
    // estimate draws for itself. The gate the estimate reads is the gate the colour reads.
    for (const name of ['level.value', 'lapsLeft.value']) {
      const colour = formulaOf(moduleItem('fuel', name), 'TextColor');
      expect(colour).toContain('Fuel_LitersPerLap');
      expect(colour).toContain('Fuel_RemainingLaps');
    }
  });

  test('the delta draws its sign as U+2212, in the value and at the left end of the scale', () => {
    const value = moduleItem('delta', 'delta.value');
    // To thousandths or to hundredths, hundredths at either setting from a hundred seconds on, and the
    // minus is replaced in both.
    expect(formulaOf(value, 'Text')).toMatch(
      /^if\(\(\(isnull\(\[OpenDash\.DeltaPrecision\], 'hundredths'\)\) = \('thousandths'\)\) and \(\(abs\(.*\)\) < \(99\.9995\)\), replace\(format\(.*, '0\.000', true\), '-', '\u2212'\), replace\(format\(.*, '0\.00', true\), '-', '\u2212'\)\)$/,
    );
    expect(value.text).toBe('\u22120.21');
    expect(moduleItem('delta', 'scale.0').text).toBe('\u22122.0');
    expect(moduleItem('delta', 'scale.4').text).toBe('+2.0');
  });

  test('the lap times delta names the best it is against and is signed the same way', () => {
    const label = moduleItem('lapTimes', 'delta.label');
    expect(label.text).toBe('Delta to your best');
    // "your best" is true of the session best and the all-time best, and not of the last lap (#322).
    expect(formulaOf(label, 'Text')).toBe("if((isnull([OpenDash.DeltaReference], 'session')) = ('lastlap'), 'Delta to last lap', 'Delta to your best')");
    const value = moduleItem('lapTimes', 'delta.value');
    expect(formulaOf(value, 'Text')).toContain("'-', '\u2212'");
    expect(value.text).toBe('\u22120.21');
  });

  /**
   * The four outcomes of a sector's colour, and the boundary between two of them.
   *
   * The lap on which a driver improves a sector leaves `Sector<n>BestTime` equal to
   * `Sector<n>LastLapTime`, so the delta is exactly 0.000 on the one lap the improvement is worth
   * showing. A strict `< 0` therefore drew every new personal best in the slower red, which is why
   * the boundary rather than the colours is what this pins.
   */
  test('a sector is dim, purple, green or red, and a new personal best is green rather than red', () => {
    const colour = formulaOf(moduleItem('sectors', 's1.value'), 'TextColor');
    const delta = values.sectorDelta(1);
    expect(colour).toContain(`if(!(${values.hasTime(values.sectorLast(1))}), '#33383F'`);
    expect(colour).toContain("'#B14BFF'");
    expect(colour).toContain(`if(${ncalc.le(delta, ncalc.num(0))}, '#00D96A', '#FF2D46')`);
    expect(colour).not.toContain(`if(${ncalc.lt(delta, ncalc.num(0))}, '#00D96A'`);
    expect(sectorIsZero(1)).toBe(ncalc.eq(delta, ncalc.num(0)));
    expect(sectorIsSlower(1)).toBe(ncalc.gt(delta, ncalc.num(0)));
  });

  /**
   * The steering mark, which is a position rather than a rotation because a rotation does not bind.
   *
   * The two formulas are the whole of the dial's movement, so what they are worth is where they put
   * the mark: at the top of the rim on a wheel that is straight, at the right of it a quarter turn
   * clockwise, and at the left a quarter turn the other way. Evaluating them is the only way to
   * catch a sine and a cosine that have been exchanged, which reads as a dial that is right at the
   * two locks and wrong everywhere between them.
   */
  test('the steering mark rides the rim at the wheel angle, top dead centre when the wheel is straight', () => {
    const module = MODULES.find((m) => m.id === 'inputs')!;
    const box = rect(0, 0, SHAPE_ARCHETYPES.wide.width, SHAPE_ARCHETYPES.wide.height);
    const mark = [...walkItems(module.build({ frame: box, density: 'zone', prefix: 'inputs.' }))].find((i) => i.name === 'inputs.steer.mark');
    if (!mark || mark.kind !== 'rect') throw new Error('inputs.steer.mark is not a rect');
    // Rounded, because a quarter turn's cosine is 6e-17 rather than nought in either language.
    const place = (n: number): number => Math.round(n * 1e6) / 1e6 + 0;
    const at = (angle: number): { left: number; top: number } => ({
      left: place(evaluateNumber(mark.bindings!.Left!.formula as string, angle)),
      top: place(evaluateNumber(mark.bindings!.Top!.formula as string, angle)),
    });
    expect(at(0)).toEqual({ left: mark.rect.left, top: mark.rect.top });
    // A quarter turn clockwise puts it at the right of the rim, level with the centre, and the same
    // turn the other way puts it at the left, the two being the radius either side of top dead
    // centre. The radius is what the drop from the top gives.
    const right = at(Math.PI / 2);
    const left = at(-Math.PI / 2);
    const radius = right.top - mark.rect.top;
    expect(radius).toBeGreaterThan(0);
    expect(right).toEqual({ left: mark.rect.left + radius, top: mark.rect.top + radius });
    expect(left).toEqual({ left: mark.rect.left - radius, top: mark.rect.top + radius });
    expect(at(Math.PI)).toEqual({ left: mark.rect.left, top: mark.rect.top + 2 * radius });
    // Past full lock the mark stops rather than coming round again, which would read as a smaller
    // angle than the wheel is actually at.
    expect(at(10)).toEqual(at(values.STEERING_RANGE));
  });
});

/**
 * The identity the measured ladder rests on, at every segment count rather than at fifteen.
 *
 * The flag box's digit reads a band number the plugin worked out, and a screen's rev bar reads the
 * count the same walk produced. They are one answer only if the bar's band boundaries are the digit's
 * band boundaries, and the first cut of #353 made that true by arithmetic accident: it asked for
 * segment `k` of the whole bar, which coincides with `Stage` only where the top band begins at exactly
 * two thirds. Fifteen segments does; fourteen does not, and both tests spelled the fifteen out, so the
 * token could have been changed and left the digit reddening a frame before the bar with every test
 * green. This walks the arithmetic instead, at four counts, so there is nothing to keep in step.
 */
describe("the measured ladder's bands are the digit's bands", () => {
  /** The plugin's `CarLightMirror.CarLadder.Stage`, in TypeScript. Rounded up, capped at three. */
  const stage = (lit: number, lamps: number): number => (lamps <= 0 ? -1 : lit <= 0 ? 0 : Math.min(3, Math.ceil((lit * 3) / lamps)));

  /** Whether the expression for one segment holds, read off the integers it compares. */
  const lit = (formula: string, litNow: number, lamps: number): boolean => {
    const entry = /^\(isnull\(\[OpenDash\.CarLadderLit\], 0\)\) > \(0\)$/.exec(formula);
    if (entry) return litNow > 0;
    const parts = /^\(\(isnull\(\[OpenDash\.CarLadderLit\], 0\)\) \* \((\d+)\)\) > \(\((\d+)\) \* \(isnull\(\[OpenDash\.CarLadderLamps\], 0\)\)\)$/.exec(formula);
    if (!parts) throw new Error(`not a segment comparison: ${formula}`);
    return litNow * Number(parts[1]) > Number(parts[2]) * lamps;
  };

  for (const count of [15, 14, 12, 9]) {
    test(`${count} segments: the first segment of a band lights where the digit reaches it`, () => {
      const indexes = Array.from({ length: count }, (_, i) => i);
      for (let k = 0; k < count; k++) {
        const band = stageOf(k, count);
        const start = indexes.findIndex((i) => stageOf(i, count) === band);
        const size = indexes.filter((i) => stageOf(i, count) === band).length;
        const formula = String(carLadderSegmentLit(band, k - start, size));
        // Every bar a car has been measured with, and every count of lit lamps on it.
        for (const lamps of [1, 4, 6, 8, 9, 10, 15, 16]) {
          for (let on = 0; on <= lamps; on++) {
            const expected = k === start ? stage(on, lamps) >= band + 1 : undefined;
            if (expected === undefined) continue;
            expect({ count, k, lamps, on, band: stage(on, lamps), segment: lit(formula, on, lamps) }).toEqual({ count, k, lamps, on, band: stage(on, lamps), segment: expected });
          }
        }
      }
    });

    test(`${count} segments: a segment lights at the fraction of the bar it stands for, and never unlights`, () => {
      const formulas = (() => {
        const indexes = Array.from({ length: count }, (_, i) => i);
        return indexes.map((k) => {
          const band = stageOf(k, count);
          const start = indexes.findIndex((i) => stageOf(i, count) === band);
          const size = indexes.filter((i) => stageOf(i, count) === band).length;
          return String(carLadderSegmentLit(band, k - start, size));
        });
      })();
      for (const lamps of [1, 6, 9, 16]) {
        for (let on = 0; on <= lamps; on++) {
          const drawn = formulas.map((f) => lit(f, on, lamps));
          // A prefix and nothing else: no gap can appear in the middle of the bar.
          const litCount = drawn.filter((x) => x).length;
          expect({ lamps, on, drawn }).toEqual({ lamps, on, drawn: formulas.map((_, k) => k < litCount) });
          // And a full bar is a full bar, an empty one empty.
          if (on === lamps) expect(litCount).toBe(count);
          if (on === 0) expect(litCount).toBe(0);
        }
      }
    });
  }
});
