/** The NCalc that reaches the file: digit counts, h:mm:ss, the shift lights and the card rules. */
import { describe, expect, test } from 'bun:test';
import { ncalc } from '../src/generator.ts';
import { revBar, REDLINE_BLINK_MS } from '../src/components/revBar.ts';
import { GEAR_COUNT_PROPERTY, SHIFT_RPM_PROPERTIES, lastGear, redlineRpm } from '../src/shift.ts';
import { MODULES } from '../src/modules/index.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { readFileSync } from 'node:fs';
import { flagVisible } from '../src/components/flagStrip.ts';
import { setting } from '../src/contract.ts';
import { CARDS, cardByNumber } from '../src/cards/index.ts';
import { rect } from '../src/design/geometry.ts';
import { expressionsOf, walkItems } from '../src/walk.ts';
import * as values from '../src/second/values.ts';
import type { TextItem } from '../src/generator.ts';

const slot = rect(0, 0, 255, 187);
const textItem = (id: string, name: string): TextItem => {
  const item = cardByNumber(CARDS.findIndex((c) => c.id === id)).build(slot, `${id}.`).find((i) => i.name === `${id}.${name}`);
  if (!item || item.kind !== 'text') throw new Error(`${id}.${name} is not a text item`);
  return item;
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
    expect(formulaOf(textItem('fuel', 'unit'), 'Text')).toBe("if(([DataCorePlugin.GameData.FuelUnit]) = ('Gallons'), 'GAL', 'L')");
  });

  test('lap times use toshorttime with forced minutes and dim no-data glyphs', () => {
    expect(formulaOf(textItem('currentLap', 'value'), 'Text')).toBe(
      "if((timespantoseconds([DataCorePlugin.GameData.CurrentLapTime])) <= (0), '-:--.-', toshorttime([DataCorePlugin.GameData.CurrentLapTime], 1, false, true))",
    );
    expect(formulaOf(textItem('lastLap', 'value'), 'Text')).toContain('toshorttime([DataCorePlugin.GameData.LastLapTime], 3, false, true)');
    expect(formulaOf(textItem('lastLap', 'value'), 'TextColor')).toContain("< (0.0005), '#B14BFF'");
    expect(formulaOf(textItem('bestLap', 'value'), 'TextColor')).not.toContain('#B14BFF');
  });

  test('delta reads the reference setting and colours by sign with a 5 ms deadband', () => {
    const text = formulaOf(textItem('delta', 'value'), 'Text');
    expect(text).toContain("isnull([OpenDash.DeltaReference], 'session')");
    expect(text).toContain('[PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds]');
    expect(text).toContain("format(if(");
    expect(text).toContain("'0.00', true)");
    const colour = formulaOf(textItem('delta', 'value'), 'TextColor');
    expect(colour).toContain("< (-0.005), '#00D96A'");
    expect(colour).toContain("> (0.005), '#FF2D46'");
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

  test("SimHub's bands are unchanged for a car that publishes no ladder of its own, and still flash at redline", () => {
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
    expect(seg(14).bindings?.BlinkEnabled).toEqual({ mode: 'formula', formula: REDLINE });
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
    // SimHub's fallback is untouched by this: it never knew about gears either.
    expect(segOf(simhub, 14).bindings?.BlinkEnabled?.formula).not.toContain('Gear');
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
