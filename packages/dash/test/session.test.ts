/**
 * The session's three modes, exercised by evaluating the NCalc the card emits, and the module
 * beside it reading the same setting.
 *
 * The evaluator is `ncalcEval.ts`, which covers the subset these expressions use and is shared with
 * the fuel margin's tests: two readings that follow one setting are better read off one evaluator
 * than off two.
 */
import { describe, expect, test } from 'bun:test';
import { session, showTime, timedSession, TIME_PLACEHOLDER, UNTIMED_SECONDS } from '../src/cards/session.ts';
import { UNTIMED_MARK } from '../src/second/values.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { walkItems } from '../src/walk.ts';
import { ncalc, type TextItem } from '../src/generator.ts';
import type { Density } from '../src/second/density.ts';
import { takingTurns, type FieldSpec } from '../src/second/field.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const items = session.build(rect(0, 0, 255, 187), 'session.');
const item = (name: string): TextItem => {
  const found = items.find((i) => i.name === `session.${name}`);
  if (found?.kind !== 'text') throw new Error(`session.${name} is not a text item`);
  return found;
};
const bound = (name: string, target: 'Text' | 'TextColor' | 'Visible', props: Props): unknown => {
  const b = item(name).bindings?.[target];
  if (!b || typeof b.formula !== 'string') throw new Error(`session.${name} has no ${target} formula`);
  return evalNcalc(b.formula, props);
};

/**
 * What the card reads, as a driver would see it: the value, or the mark that stands in for it.
 *
 * The mark is a second item and not a string the value is bound to, because `∞` cannot be laid in a
 * digit cell (#439). Which of the two is drawn is asserted here as well as read, so a state that
 * drew both or neither fails rather than being reported as whichever this helper looked at first.
 */
const reading = (props: Props) => {
  const marked = bound('mark', 'Visible', props) === true;
  expect({ props, marked, value: bound('value', 'Visible', props) }).toMatchObject({ value: !marked });
  return {
    label: bound('label', 'Text', props),
    value: marked ? item('mark').text : bound('value', 'Text', props),
    dim: !marked && bound('value', 'TextColor', props) === '#33383F',
    denominator: bound('denominator', 'Visible', props) ? bound('denominator', 'Text', props) : null,
  };
};

const game = (timeLeft: number, totalLaps: number, currentLap = 12, mode?: string): Props => ({
  'DataCorePlugin.GameData.SessionTimeLeft': timeLeft,
  'DataCorePlugin.GameData.TotalLaps': totalLaps,
  'DataCorePlugin.GameData.CurrentLap': currentLap,
  ...(mode === undefined ? {} : { 'OpenDash.SessionProgress': mode }),
});

const A_WEEK = 604800;

describe('session card modes', () => {
  test('the evaluator agrees with NCalc on the pieces it covers', () => {
    expect(evalNcalc("if((1) > (0), 'a', 'b')", {})).toBe('a');
    expect(evalNcalc("isnull([X], 'd')", {})).toBe('d');
    expect(evalNcalc("(isnull([X], 'd')) = ('x')", { X: 'x' })).toBe(true);
    expect(evalNcalc("format(truncate((125) / (60)), '00')", {})).toBe('02');
    expect(evalNcalc("(('/ ') + (format([N], '0')))", { N: 24 })).toBe('/ 24');
    expect(evalNcalc('!((1) > (0)) or ((2) > (1))', {})).toBe(true);
  });

  test('timed means 0 < time left <= a day, the day included: iRacing reports a week when there is no limit', () => {
    expect(UNTIMED_SECONDS).toBe(86400);
    // A day exactly is timed, and deliberately: Daytona, Le Mans and the Nurburgring are 86400 s,
    // and `SessionTimeRemain` sits on the total until the clock starts, so a race whose time left is
    // the whole point would otherwise open by saying it has none (#439).
    for (const [secs, timed] of [[1, true], [1800, true], [86399, true], [86400, true], [0, false], [-1, false], [86401, false], [A_WEEK, false]] as const) {
      expect({ secs, timed: evalNcalc(timedSession(), game(secs, 0)) }).toEqual({ secs, timed });
    }
    expect(reading(game(86400, 20, 3, 'auto'))).toEqual({ label: 'Time left', value: '24:00:00', dim: false, denominator: null });
    expect(evalNcalc(showTime(), game(1800, 0))).toBe(true);
    expect(evalNcalc(showTime(), game(A_WEEK, 0))).toBe(false);
  });

  test('auto (and no plugin) shows time left in a timed session, laps otherwise', () => {
    expect(reading(game(1800, 0))).toEqual({ label: 'Time left', value: '0:30:00', dim: false, denominator: null });
    expect(reading(game(5025, 20, 3, 'auto'))).toEqual({ label: 'Time left', value: '1:23:45', dim: false, denominator: null });
    expect(reading(game(A_WEEK, 20, 3, 'auto'))).toEqual({ label: 'Lap', value: '3', dim: false, denominator: '/ 20' });
    expect(reading(game(A_WEEK, 0, 3, 'auto'))).toEqual({ label: 'Lap', value: '3', dim: false, denominator: null });
    expect(reading(game(0, 20, 3))).toEqual({ label: 'Lap', value: '3', dim: false, denominator: '/ 20' });
  });

  test('time shows time left, the mark where the session has no clock, and the dim placeholder where there is no session', () => {
    expect(reading(game(86399, 20, 3, 'time'))).toEqual({ label: 'Time left', value: '23:59:59', dim: false, denominator: null });
    // Not the placeholder and not dim: a lap race has no clock, which is a reading rather than an
    // absence, and `∞` is what says so (#439). The placeholder is the session that has not started.
    expect(reading(game(A_WEEK, 20, 3, 'time'))).toEqual({ label: 'Time left', value: UNTIMED_MARK, dim: false, denominator: null });
    expect(reading(game(0, 0, 3, 'time'))).toEqual({ label: 'Time left', value: TIME_PLACEHOLDER, dim: true, denominator: null });
    expect(TIME_PLACEHOLDER).toBe('-:--:--');
  });

  test('laps shows the lap, with the total only when one is declared', () => {
    expect(reading(game(1800, 20, 3, 'laps'))).toEqual({ label: 'Lap', value: '3', dim: false, denominator: '/ 20' });
    expect(reading(game(1800, 0, 112, 'laps'))).toEqual({ label: 'Lap', value: '112', dim: false, denominator: null });
    expect(reading(game(A_WEEK, 5, 7, 'laps'))).toEqual({ label: 'Lap', value: '7', dim: false, denominator: '/ 5' });
  });
});

describe('the module reads the same setting as the card', () => {
  const module = MODULES.find((m) => m.id === 'session')!;
  const moduleItems = module.build({ frame: rect(0, 0, 445, 286), density: 'zone', prefix: '' });
  const formulaOf = (name: string): string => {
    const found = [...walkItems(moduleItems)].find((i) => i.name === name);
    if (!found) throw new Error(`no ${name}`);
    const formula = found.bindings?.Visible?.formula;
    return String(typeof formula === 'string' ? formula : formula?.expression);
  };

  test('a zone shows the lap, the time left or the mark that stands in for it, never two of them', () => {
    // The setting was the card's alone, so every zone face and every companion drew Lap, Time left
    // and Laps left side by side whatever the driver had chosen. One of the first two answers the
    // question now and the rank closes over the other, which is what the setting is for.
    expect(formulaOf('timeLeft.value')).toContain('SessionProgress');
    expect(formulaOf('lap.value')).toContain('SessionProgress');
    // The three are read as one claim rather than as two formulas compared letter by letter, because
    // the clock's own Visible now carries the mark's state too: a session with no clock draws `∞`
    // where the clock would go, and the clock steps aside for it (#439). The lap's `/ 20` goes with
    // the lap: iRacing's TotalLaps in a timed race is the leader's laps, so a denominator shown on
    // that alone drew `/ 20` with no lap before it, on top of the time left.
    const drawn = (props: Props): string[] =>
      ['lap.value', 'lap.denominator', 'timeLeft.value', 'timeLeft.mark'].filter((name) => evalNcalc(formulaOf(name), props) === true);
    for (const [secs, mode, expected] of [
      [1800, 'auto', ['timeLeft.value']],
      [1800, 'time', ['timeLeft.value']],
      [1800, 'laps', ['lap.value', 'lap.denominator']],
      [0, 'time', ['timeLeft.value']],
      [0, 'auto', ['lap.value', 'lap.denominator']],
      [A_WEEK, 'auto', ['lap.value', 'lap.denominator']],
      [A_WEEK, 'time', ['timeLeft.mark']],
      [A_WEEK, 'laps', ['lap.value', 'lap.denominator']],
    ] as const) {
      expect({ secs, mode, drawn: drawn(game(secs, 20, 3, mode)) }).toEqual({ secs, mode, drawn: [...expected] });
    }
  });
});

describe('the lap and the time left take turns in one place', () => {
  const module = MODULES.find((m) => m.id === 'session')!;
  const textNamed = (items: ReturnType<typeof module.build>, name: string): TextItem | undefined => {
    const found = [...walkItems(items)].find((i) => i.name === name);
    return found?.kind === 'text' ? found : undefined;
  };

  test('they share a line in every box, so the page never draws a line that is always empty', () => {
    // A lap budgeted for three digits and a race of three no longer fits beside the time left in a
    // 249 or 274 px zone, and the rank wrapped the two onto lines of their own: Position, Class, a
    // blank line, then Time left, since only one of them is ever shown (#596).
    const densities: Density[] = ['zone', 'panel', 'companion', 'wide', 'compact'];
    let both = 0;
    for (const density of densities) {
      for (let width = 160; width <= 900; width += 3) {
        for (const height of [158, 226, 328, 366, 598]) {
          const items = module.build({ frame: rect(0, 0, width, height), density, prefix: '' });
          const lap = textNamed(items, 'lap.label');
          const time = textNamed(items, 'timeLeft.label');
          if (lap === undefined || time === undefined) continue;
          both++;
          expect({ density, width, height, lap: lap.rect.top }).toEqual({ density, width, height, lap: time.rect.top });
        }
      }
    }
    expect(both).toBeGreaterThan(1000);
  });

  test('a pair is refused unless each is shown exactly when the other is not', () => {
    const spec = (name: string, visibleBind?: string): FieldSpec => ({ name, label: name, value: { sample: '1', chars: { digits: 1, specials: 0 }, fs: 34 }, visibleBind });
    const shown = ncalc.gt('[A]', ncalc.num(0));
    expect(takingTurns(spec('a', ncalc.not(shown)), spec('b', shown)).alternate?.name).toBe('b');
    expect(takingTurns(spec('a', shown), spec('b', ncalc.not(shown))).alternate?.name).toBe('b');
    expect(() => takingTurns(spec('a', shown), spec('b', shown))).toThrow();
    expect(() => takingTurns(spec('a'), spec('b', shown))).toThrow();
  });
});
