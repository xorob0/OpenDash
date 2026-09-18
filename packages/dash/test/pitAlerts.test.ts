/**
 * The pit family, which is five states sharing one rectangle.
 *
 * The thing worth proving is that two of them can never draw at once, because they are drawn on top
 * of one another: the exclusion chain is the only thing standing between "ENGAGE LIMITER" and
 * "IGNITION OFF" being one illegible band. So the conditions are evaluated against every
 * combination of the four telemetry facts they read, in the manner flagBox.test.ts evaluates the
 * catalogue against several bits at once.
 *
 * The evaluator covers exactly what `pitAlerts.ts` emits -- property reads, `isnull` in both
 * arities, `truncate`, the arithmetic and the comparisons -- and throws on anything else rather
 * than guessing, so a condition that grows a function nobody here knows fails this file instead of
 * passing wrongly.
 */
import { describe, expect, test } from 'bun:test';
import { PIT_ALERTS, pitAlertVisible, pitAlerts } from '../src/components/pitAlerts.ts';
import { ALERT_BAND_BORDER } from '../src/components/alertBand.ts';
import { PIT_LIMITER_BLINK_MS } from '../src/components/pitLimiter.ts';
import { rect } from '../src/design/geometry.ts';
import { measureText } from '../src/design/advances.ts';
import { ds } from '../src/tokens.ts';
import type { RectangleItem, TextItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES } from '../src/zones/index.ts';

/** What a car is doing, as the four properties the family reads. */
interface Car {
  limiter: boolean;
  lane: boolean;
  ignition: boolean;
  stalled: boolean;
  /** Whether the car publishes an in-car limiter toggle at all. */
  hasLimiter: boolean;
}

const telemetry = (car: Car): Record<string, number | null> => ({
  'DataCorePlugin.GameData.PitLimiterOn': car.limiter ? 1 : 0,
  'DataCorePlugin.GameData.IsInPitLane': car.lane ? 1 : 0,
  'DataCorePlugin.GameData.EngineIgnitionOn': car.ignition ? 1 : 0,
  'DataCorePlugin.GameRawData.Telemetry.dcPitSpeedLimiterToggle': car.hasLimiter ? 1 : null,
  'DataCorePlugin.GameRawData.Telemetry.EngineWarnings': car.stalled ? 8 : 0,
});

function evaluate(expression: string, car: Car): boolean {
  const values = telemetry(car);
  const js = expression
    .replace(/\[([A-Za-z0-9_.]+)\]/g, (_, name: string) => `P(${JSON.stringify(name)})`)
    .replace(/\btruncate\(/g, 'Math.trunc(')
    .replace(/\band\b/g, '&&')
    .replace(/\bor\b/g, '||')
    .replace(/([^!<>=])=([^=])/g, '$1===$2');
  const words = js.replace(/P\("[^"]*"\)/g, '0').match(/[A-Za-z_][A-Za-z_.]*/g) ?? [];
  const unknown = words.filter((w) => w !== 'isnull' && w !== 'Math.trunc');
  if (unknown.length > 0) throw new Error(`the evaluator does not cover ${unknown.join(', ')} in ${expression}`);
  const read = (name: string): number | null => {
    if (!(name in values)) throw new Error(`the evaluator has no value for ${name}`);
    return values[name] ?? null;
  };
  // One function for both arities, which is what NCalc's isnull is: with a fallback it defaults the
  // reading, without one it asks whether the property is there at all.
  const isnull = (value: number | null, fallback?: number): number | boolean => (fallback === undefined ? value === null : (value ?? fallback));
  const result: unknown = new Function('P', 'isnull', `return (${js});`)(read, isnull);
  if (typeof result !== 'boolean') throw new Error(`not a condition: ${expression}`);
  return result;
}

/** Every combination of the five facts: thirty-two cars, which is the whole state space. */
const CARS: Car[] = [];
for (let mask = 0; mask < 32; mask++) {
  CARS.push({
    limiter: (mask & 1) > 0,
    lane: (mask & 2) > 0,
    ignition: (mask & 4) > 0,
    stalled: (mask & 8) > 0,
    hasLimiter: (mask & 16) > 0,
  });
}

/** Which alert is out on this car, or undefined where the rectangle stays empty. */
const shown = (car: Car): string | undefined => {
  const out = PIT_ALERTS.filter((alert) => evaluate(pitAlertVisible(alert.id), car));
  expect({ car, out: out.map((a) => a.id) }).toMatchObject({ out: out.slice(0, 1).map((a) => a.id) });
  return out[0]?.id;
};

describe('one pit alert at a time', () => {
  test('no two of the five are ever visible together', () => {
    for (const car of CARS) {
      const out = PIT_ALERTS.filter((alert) => evaluate(pitAlertVisible(alert.id), car)).map((a) => a.id);
      expect({ car, out }).toMatchObject({ out: out.slice(0, 1) });
    }
  });

  test('and the one that is out is the highest-ranked condition that holds', () => {
    for (const car of CARS) {
      const raised = PIT_ALERTS.filter((alert) => evaluate(alert.when, car)).map((a) => a.id);
      expect({ car, shown: shown(car) ?? null }).toMatchObject({ shown: raised[0] ?? null });
    }
  });
});

describe('what each state says', () => {
  const on = { limiter: true, lane: true, ignition: true, stalled: false, hasLimiter: true };

  test('the limiter engaged in the lane keeps the filled blinking band', () => {
    expect(shown(on)).toBe('limiter');
    const spec = PIT_ALERTS.find((a) => a.id === 'limiter')!;
    expect({ shape: spec.shape, colour: spec.colour, blink: spec.blinkMs }).toEqual({
      shape: 'filled',
      colour: ds.purpose.pitLimiter,
      blink: PIT_LIMITER_BLINK_MS,
    });
  });

  test('the limiter left on outside the lane is the outlined band, which is the mistake', () => {
    expect(shown({ ...on, lane: false })).toBe('disengage');
    // The same colour as the correct state, on purpose: purpose.pitLimiter and purpose.flag.white
    // are both #FFFFFF, so the two have to differ in shape. They do.
    const wrong = PIT_ALERTS.find((a) => a.id === 'disengage')!;
    const right = PIT_ALERTS.find((a) => a.id === 'limiter')!;
    expect({ colour: wrong.colour, shape: wrong.shape }).toEqual({ colour: right.colour, shape: 'outlined' });
  });

  test('entering the lane without the limiter asks for it, and only on a car that has one', () => {
    expect(shown({ ...on, limiter: false })).toBe('engage');
    // iRacing reports PitLimiterOn 0 for a car with no limiter, so without the presence test this
    // is the band a Mazda would carry down every pit lane it ever enters.
    expect(shown({ ...on, limiter: false, hasLimiter: false })).toBeUndefined();
  });

  test('the ignition and the engine are read only in the lane, and the ignition outranks the engine', () => {
    expect(shown({ ...on, ignition: false })).toBe('ignition');
    expect(shown({ ...on, stalled: true })).toBe('engine');
    expect(shown({ ...on, ignition: false, stalled: true })).toBe('ignition');
    // On the road neither is a pit alert: a car with its ignition off on track is a car being
    // restarted, and the band belongs to the lane.
    expect(shown({ ...on, lane: false, ignition: false, limiter: false })).toBeUndefined();
  });

  test('a sim publishing none of it draws nothing, which is what the isnull defaults are for', () => {
    // The ignition defaults to on and the limiter to off, so silence is not an alarm.
    expect(shown({ limiter: false, lane: false, ignition: true, stalled: false, hasLimiter: false })).toBeUndefined();
  });
});

describe('the drawing', () => {
  const frame = rect(824, 111, 272, 30);
  const items = [...walkItems(pitAlerts(frame))];

  test('every band is the rectangle the limiter banner already had', () => {
    const bands = items.filter((i): i is RectangleItem => i.kind === 'rect');
    expect(bands.length).toBe(PIT_ALERTS.length);
    for (const item of bands) expect({ item: item.name, rect: item.rect }).toEqual({ item: item.name, rect: frame });
  });

  test('the four that are mistakes are outlined and the one that is right is filled', () => {
    for (const spec of PIT_ALERTS) {
      const band = items.find((i): i is RectangleItem => i.kind === 'rect' && i.name === `pitAlert.${spec.id}.band`)!;
      const filled = spec.shape === 'filled';
      expect({ id: spec.id, ground: band.backgroundColor, border: band.border?.color, width: band.border?.top }).toEqual({
        id: spec.id,
        ground: filled ? spec.colour : ds.color.surface.base,
        border: filled ? undefined : spec.colour,
        width: filled ? undefined : ALERT_BAND_BORDER,
      });
    }
  });

  test('every name fits the narrowest banner of the eight faces without being shortened', () => {
    // 200 px at 600 x 686, where "DISENGAGE LIMITER" is the longest of the five at 135 px. It is
    // measured here as well as by the face's own fit suite because this is where the copy is
    // chosen: a sixth state with a longer name would fail on the sentence rather than on a face.
    const narrowest = Math.min(...ZONE_FACES.map((f) => f.zones.pitLimiter.width));
    for (const spec of PIT_ALERTS) {
      const width = measureText('BarlowMedium', spec.label.toUpperCase(), ds.size.label);
      expect({ id: spec.id, width, box: narrowest, fits: width <= narrowest }).toMatchObject({ fits: true });
    }
  });

  test('the names are drawn upper-cased, as every band on the face is', () => {
    const labels = items.filter((i): i is TextItem => i.kind === 'text');
    expect(labels.map((l) => l.text)).toEqual(PIT_ALERTS.map((a) => a.label.toUpperCase()));
  });
});
