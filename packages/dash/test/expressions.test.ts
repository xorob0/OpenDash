/** The NCalc that reaches the file: digit counts, h:mm:ss, the shift lights and the card rules. */
import { describe, expect, test } from 'bun:test';
import { ncalc } from '../src/generator.ts';
import { revBar, REDLINE_BLINK_MS } from '../src/components/revBar.ts';
import { flagVisible } from '../src/components/flagStrip.ts';
import { CARDS, cardByNumber } from '../src/cards/index.ts';
import { rect } from '../src/design/geometry.ts';
import { expressionsOf, walkItems } from '../src/walk.ts';
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

describe('hero expressions', () => {
  test('shift lights light per band and flash the last band at redline', () => {
    const [shift, rpm] = revBar({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    if (shift?.kind !== 'layer' || rpm?.kind !== 'layer') throw new Error('revBar returns two layers');
    expect(shift.bindings?.Visible).toEqual({ mode: 'formula', formula: 'isnull([OpenDash.ShiftLights], true)' });
    expect(rpm.bindings?.Visible).toEqual({ mode: 'formula', formula: '!(isnull([OpenDash.ShiftLights], true))' });
    const seg = (layer: typeof shift, k: number) => {
      const s = layer.children[k];
      if (!s || s.kind !== 'rect') throw new Error('segment');
      return s;
    };
    expect(expressionsOf(seg(shift, 0))).toEqual(["if((([DataCorePlugin.GameData.CarSettings_RPMShiftLight1]) * (5)) > (0), '#00D96A', '#33383F')"]);
    expect(expressionsOf(seg(shift, 4))).toEqual(["if((([DataCorePlugin.GameData.CarSettings_RPMShiftLight1]) * (5)) > (4), '#00D96A', '#33383F')"]);
    expect(expressionsOf(seg(shift, 5))).toEqual(["if((([DataCorePlugin.GameData.CarSettings_RPMShiftLight2]) * (5)) > (0), '#FFB300', '#33383F')"]);
    expect(seg(shift, 10).bindings?.BackgroundColor).toEqual({ mode: 'formula', formula: "if(([DataCorePlugin.GameData.CarSettings_RPMRedLineReached]) = (1), '#FF2D46', '#33383F')" });
    expect(seg(shift, 14).bindings?.BlinkEnabled).toEqual({ mode: 'formula', formula: '([DataCorePlugin.GameData.CarSettings_RPMRedLineReached]) = (1)' });
    expect(seg(shift, 14).blink).toEqual({ delayMs: 62 });
    expect(seg(shift, 9).blink).toBeUndefined();
    expect(REDLINE_BLINK_MS).toBe(62);
    expect(expressionsOf(seg(rpm, 3))).toEqual(["if(([DataCorePlugin.GameData.CarSettings_CurrentDisplayedRPMPercent]) > (20), '#8A9099', '#33383F')"]);
    expect(seg(rpm, 14).blink).toBeUndefined();
    for (const s of [...walkItems([shift, rpm])]) if (s.kind === 'rect') expect(s.border).toEqual({ radius: 2 });
  });

  test('flags are visible by priority', () => {
    expect(flagVisible('Flag_Black')).toBe('(([DataCorePlugin.GameData.Flag_Black]) = (1))');
    expect(flagVisible('Flag_Yellow')).toBe(
      '(([DataCorePlugin.GameData.Flag_Black]) = (0)) and (([DataCorePlugin.GameData.Flag_Checkered]) = (0)) and (([DataCorePlugin.GameData.Flag_Yellow]) = (1))',
    );
    expect(flagVisible('Flag_Green').split(' and ')).toHaveLength(6);
  });
});
