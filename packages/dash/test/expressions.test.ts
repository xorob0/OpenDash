/** The NCalc that reaches the file: digit counts, h:mm:ss, the shift lights and the card rules. */
import { describe, expect, test } from 'bun:test';
import { ncalc } from '../src/generator.ts';
import { revBar, REDLINE_BLINK_MS } from '../src/components/revBar.ts';
import { GEAR_COUNT_PROPERTY, SHIFT_RPM_PROPERTIES, lastGear, redlineRpm } from '../src/shift.ts';
import { MODULES } from '../src/modules/index.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { readFileSync } from 'node:fs';
import { flagVisible } from '../src/components/flagStrip.ts';
import { flagBox, setting } from '../src/contract.ts';
import { CARDS, cardByNumber } from '../src/cards/index.ts';
import { rect } from '../src/design/geometry.ts';
import { expressionsOf, walkItems } from '../src/walk.ts';
import { sectorIsSlower, sectorIsZero } from '../src/second/sectors.ts';
import { temperatureColour } from '../src/second/wheel.ts';
import * as values from '../src/second/values.ts';
import type { TextItem } from '../src/generator.ts';

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
 * are the whole of its movement. `isnull` is NCalc's and the rest are the maths library, which is
 * .NET's and therefore JavaScript's to six decimals.
 */
const evaluateNumber = (formula: string, value: number): number => {
  const js = formula
    .replace(/\[[^\]]+\]/g, String(value))
    .replace(/\bisnull\(/g, 'nz(')
    .replace(/\b(sin|cos|min|max)\(/g, 'Math.$1(');
  return Number(new Function('nz', `return ${js};`)((v: number, fallback: number) => v ?? fallback));
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
    expect(e).toContain('max(0, [S])');
    expect(e).toContain('/ (3600)');
    expect(e).toContain("format(truncate(((max(0, [S])) % (3600)) / (60)), '00')");
    expect(e).toContain("format(truncate((max(0, [S])) % (60)), '00')");
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

  test('session resolves its mode from the setting and the 0 < time left < 86400 guard', () => {
    const mode = "isnull([OpenDash.SessionProgress], 'auto')";
    const secs = 'timespantoseconds([DataCorePlugin.GameData.SessionTimeLeft])';
    const timed = `((${secs}) > (0)) and ((${secs}) < (86400))`;
    const time = `((${mode}) = ('time')) or (((${mode}) = ('auto')) and (${timed}))`;
    expect(formulaOf(textItem('session', 'label'), 'Text')).toBe(`if(${time}, 'TIME LEFT', 'LAP')`);
    const value = formulaOf(textItem('session', 'value'), 'Text');
    expect(value).toBe(`if(${time}, if(${timed}, ${ncalc.hms(secs)}, '-:--:--'), format([DataCorePlugin.GameData.CurrentLap], '0'))`);
    expect(value).toContain('/ (3600)');
    expect(formulaOf(textItem('session', 'value'), 'TextColor')).toBe(`if((${time}) and (!(${timed})), '#33383F', '#F5F7FA')`);
    const denominator = textItem('session', 'denominator');
    expect(formulaOf(denominator, 'Text')).toBe("('/ ') + (format([DataCorePlugin.GameData.TotalLaps], '0'))");
    expect(formulaOf(denominator, 'Visible')).toBe(`(!(${time})) and (([DataCorePlugin.GameData.TotalLaps]) > (0))`);
    expect(formulaOf(denominator, 'Left')).toContain('>= (100), 3');
    for (const item of ['label', 'value', 'denominator'] as const) {
      const expressions = Object.values(textItem('session', item).bindings ?? {}).map((b) => (b && typeof b.formula === 'string' ? b.formula : ''));
      for (const e of expressions) expect(e.replace(/isnull\(\[OpenDash\.SessionProgress\], 'auto'\)/g, '')).not.toContain('OpenDash.');
    }
  });

  test('fuel laps show -.- in text.dim without an estimate and go red only under a lap', () => {
    const laps = 'isnull([DataCorePlugin.Computed.Fuel_RemainingLaps], 0)';
    expect(formulaOf(textItem('fuelLaps', 'value'), 'Text')).toBe(`if((${laps}) <= (0), '-.-', format(${laps}, '0.0'))`);
    expect(formulaOf(textItem('fuelLaps', 'value'), 'TextColor')).toBe(
      `if((${laps}) <= (0), '#33383F', if(((${laps}) > (0)) and ((${laps}) < (1)), '#FF2D46', '#F5F7FA'))`,
    );
  });

  test('fuel unit Left adds one digit cell for the decimal and one special for the point', () => {
    const left = formulaOf(textItem('fuel', 'unit'), 'Left');
    expect(left).toMatch(/\+ \(1\)\) \* \(31\)\) \+ \(17\) \+ \(8\)$/);
    // Through `ucase`, which is what makes a unit the sim spells match the style the canvas sets
    // on every small label. The element cannot know that this particular expression already
    // returns capitals, and a unit that arrives in the sim's own case is the reason it is there.
    expect(formulaOf(textItem('fuel', 'unit'), 'Text')).toBe("ucase(if(([DataCorePlugin.GameData.FuelUnit]) = ('Gallons'), 'GAL', 'L'))");
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

  test('delta reads the reference setting and colours by sign with a 5 ms deadband', () => {
    const text = formulaOf(textItem('delta', 'value'), 'Text');
    expect(text).toContain("isnull([OpenDash.DeltaReference], 'session')");
    expect(text).toContain('[PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds]');
    expect(text).toContain("'0.00', true)");
    expect(text).toContain("'-', '−'");
    const colour = formulaOf(textItem('delta', 'value'), 'TextColor');
    expect(colour).toContain("< (0), '#00D96A'");
    expect(colour).toContain("'#FF2D46'");
    // The deadband decides the text and the colour together, so a level delta cannot be drawn as
    // "+0.00" in the resting white: it is the bare "0.00" the canvas draws.
    const deadband = 'if((abs(isnull(';
    expect(text.startsWith(deadband)).toBe(true);
    expect(colour.startsWith(deadband)).toBe(true);
    expect(text).toContain("<= (0.005), '0.00'");
    expect(colour).toContain("<= (0.005), '#F5F7FA'");
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

  test('tyre temps convert thresholds per unit and tyre pressures upper-case the unit', () => {
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
      "('PRESSURES ') + (ucase(isnull([DataCorePlugin.GameData.TyrePressureUnit], 'Psi'))) + (' · LAST STOP')",
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

  test('a unit reaches the screen as the word the face draws, not the name of its enum', () => {
    expect(values.speedUnit()).toBe("if((isnull([DataCorePlugin.GameData.SpeedLocalUnit], 'KMH')) = ('MPH'), 'mph', 'km/h')");
    expect(values.fuelUnit()).toContain("'gal'");
    expect(values.fuelUnit()).toContain("'L'");
    expect(values.pressureUnit()).toContain("'kPa'");
  });

  test('the five-lap average reads the five history slots and waits for all five', () => {
    const average = values.average5();
    for (const slot of [1, 2, 3, 4, 5]) expect(average).toContain(`('PersistantTrackerPlugin.PreviousLap_') + (format(${slot}, '00'))`);
    expect(average).not.toContain("format(6, '00')");
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

  test('the grip status is upper-cased, and MODERATE is the word a box is cut for', () => {
    expect(values.trackGrip()).toBe("ucase(isnull([DataCorePlugin.GameData.TrackGripStatus], '--'))");
    expect(values.GRIP_WIDEST).toBe('MODERATE');
  });

  test('a low tank is one sentence, against the threshold every light reads', () => {
    expect(values.tankIsLow()).toBe(`(isnull([DataCorePlugin.Computed.Fuel_RemainingLaps], 999)) < (${flagBox.lowFuelLaps()})`);
    // The laps a warning is raised on default high where the laps a field draws default to zero, so
    // a sim that computes none leaves the warning away rather than raising it on every car.
    expect(values.fuelLapsLeft()).toContain(', 0)');
    expect(values.tankIsLow()).not.toContain(values.fuelLapsLeft());
  });
});

describe('hero expressions', () => {
  const RPMS = 'isnull([DataCorePlugin.GameData.Rpms], 0)';
  const SL = (n: string) => `isnull([DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarSL${n}RPM], 0)`;
  const MIRROR = `((${SL('First')}) > (0)) and ((${SL('Last')}) > (${SL('First')})) and ((${SL('Shift')}) >= (${SL('First')})) and ((${SL('Last')}) >= (${SL('Shift')}))`;
  const ON = setting.revBarIs('shift');
  const GEARS = 'isnull([DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarGearNumForward], 0)';
  const LAST_GEAR = `((${GEARS}) > (0)) and ((isnull([DataCorePlugin.GameRawData.Telemetry.Gear], 0)) >= (${GEARS}))`;

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

  test('the rev bar is three layers, and exactly one of them is visible at a time', () => {
    const layers = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    expect(layers.map((l) => l.kind)).toEqual(['layer', 'layer', 'layer']);
    expect(layers.map((l) => l.name)).toEqual(['revBar.shiftLights', 'revBar.shiftLightsSimHub', 'revBar.rpmBar']);
    // The gate is the tri-state RevBar (ADR 0004, XOR-138), not the deprecated ShiftLights boolean,
    // so `rpm` and `off` both land on the plain bar and the ladder split applies only within `shift`.
    expect(ON).toContain('[OpenDash.RevBar]');
    // Which ladder a car is on is which layer is visible, which is how it is seen in Dash Studio.
    expect(layers[0]!.bindings?.Visible).toEqual({ mode: 'formula', formula: `(${ON}) and (${MIRROR})` });
    expect(layers[1]!.bindings?.Visible).toEqual({ mode: 'formula', formula: `(${ON}) and (!(${MIRROR}))` });
    expect(layers[2]!.bindings?.Visible).toEqual({ mode: 'formula', formula: `!(${ON})` });
  });

  test("the car's own ladder: nothing below First, bands at First and Shift, the last band at Last, the flash at Blink", () => {
    const [shift] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
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
    const blink = `max(isnull([DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarSLBlinkRPM], 0), ${SL('Last')})`;
    expect(seg(10).bindings?.BackgroundColor).toEqual({ mode: 'formula', formula: `if((${RPMS}) >= (${SL('Last')}), '#FF2D46', '#33383F')` });
    expect(seg(14).bindings?.BlinkEnabled).toEqual({ mode: 'formula', formula: `((${RPMS}) >= (${blink})) and (!(${LAST_GEAR}))` });
    expect(seg(14).blink).toEqual({ delayMs: 62 });
    expect(seg(9).blink).toBeUndefined();
    expect(REDLINE_BLINK_MS).toBe(62);
  });

  test("SimHub's bands are unchanged for a car that publishes no ladder of its own, and flash at redline outside the last gear", () => {
    const [, simhub, rpm] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
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
    const [shift, simhub] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
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
    const [shift] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    if (shift?.kind !== 'layer') throw new Error('layer');

    // The bar's top band is `Rpms >= X`. X is the number a readout beside it has to print.
    const band = String(segOf(shift, 14).bindings?.BackgroundColor?.formula ?? '');
    const parts = /^if\(\((.*)\) >= \((.*)\), '#[0-9A-F]{6}', '#[0-9A-F]{6}'\)$/.exec(band);
    expect(parts?.[1]).toBe(RPMS);
    const threshold = parts?.[2] ?? '';
    expect(threshold).toBe(SL('Last'));

    // Under the car's own ladder the printed number IS that threshold, character for character.
    // Under SimHub's there is no RPM to print -- ADR 0004's fallback is two band progress values
    // and a `RedLineReached` flag -- so the fallback is SimHub's own redline, which is the single
    // seam and is a property of what SimHub exposes rather than a second model.
    const simhub = 'isnull([DataCorePlugin.GameData.CarSettings_CurrentGearRedLineRPM], 0)';
    expect(redlineRpm()).toBe(`if(${MIRROR}, ${threshold}, ${simhub})`);
    expect(formulaOf(speedoField('redline'), 'Text')).toBe(`format(${redlineRpm()}, '#,0')`);

    // And the choice is the bar's own gate, evaluated the same frame, not a second test that could
    // drift: the same string the bar picks its visible layer with.
    expect(formulaOf(speedoField('redline'), 'Text')).toContain(MIRROR);
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
    expect(flagVisible('Flag_Black')).toBe('(([DataCorePlugin.GameData.Flag_Black]) = (1))');
    expect(flagVisible('Flag_Yellow')).toBe(
      '(([DataCorePlugin.GameData.Flag_Black]) = (0)) and (([DataCorePlugin.GameData.Flag_Checkered]) = (0)) and (([DataCorePlugin.GameData.Flag_Yellow]) = (1))',
    );
    expect(flagVisible('Flag_Green').split(' and ')).toHaveLength(6);
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

  test('the delta draws its sign as U+2212, in the value and at the left end of the scale', () => {
    const value = moduleItem('delta', 'delta.value');
    expect(formulaOf(value, 'Text')).toMatch(/^replace\(format\(.*, '0\.00', true\), '-', '\u2212'\)$/);
    expect(value.text).toBe('\u22120.21');
    expect(moduleItem('delta', 'scale.0').text).toBe('\u22122.0');
    expect(moduleItem('delta', 'scale.4').text).toBe('+2.0');
  });

  test('the lap times delta names the best it is against and is signed the same way', () => {
    expect(moduleItem('lapTimes', 'delta.label').text).toBe('DELTA TO YOUR BEST');
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
