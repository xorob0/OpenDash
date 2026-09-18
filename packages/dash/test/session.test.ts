/**
 * The session's three modes, exercised by evaluating the NCalc the card emits, and the module
 * beside it reading the same setting. The
 * evaluator covers only the subset those expressions use: [Property] reads, if, isnull, format,
 * timespantoseconds (seconds are passed as numbers), max, truncate, comparison, and/or/!, +, %, /.
 */
import { describe, expect, test } from 'bun:test';
import { session, showTime, timedSession, TIME_PLACEHOLDER, UNTIMED_SECONDS } from '../src/cards/session.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { walkItems } from '../src/walk.ts';
import type { TextItem } from '../src/generator.ts';

type Props = Record<string, unknown>;

function evalNcalc(expression: string, props: Props): unknown {
  // Split on string literals so that operator rewriting never touches their contents.
  const js = expression
    .split(/('(?:[^'\\]|\\.)*')/)
    .map((part, i) =>
      i % 2 === 1
        ? part
        : part
            .replace(/\[([A-Za-z0-9_.]+)\]/g, (_, name: string) => `P(${JSON.stringify(name)})`)
            .replace(/\bif\(/g, 'IF(')
            .replace(/\band\b/g, '&&')
            .replace(/\bor\b/g, '||')
            .replace(/ = /g, ' === ')
            .replace(/ != /g, ' !== '),
    )
    .join('');
  const fns = {
    P: (name: string): unknown => (name in props ? props[name] : null),
    IF: (c: unknown, a: unknown, b: unknown): unknown => (c ? a : b),
    isnull: (v: unknown, d?: unknown): unknown => (d === undefined ? v === null || v === undefined : (v ?? d)),
    format: (v: number, pattern: string): string => {
      const [int = '0', frac = ''] = pattern.split('.');
      const [i = '0', f] = Math.abs(v).toFixed(frac.length).split('.');
      return `${v < 0 ? '-' : ''}${i.padStart(int.length, '0')}${f ? `.${f}` : ''}`;
    },
    timespantoseconds: (v: unknown): number => Number(v),
    max: Math.max,
    truncate: Math.trunc,
  };
  return new Function(...Object.keys(fns), `return (${js});`)(...Object.values(fns));
}

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

/** What the card reads, as a driver would see it. */
const reading = (props: Props) => ({
  label: bound('label', 'Text', props),
  value: bound('value', 'Text', props),
  dim: bound('value', 'TextColor', props) === '#33383F',
  denominator: bound('denominator', 'Visible', props) ? bound('denominator', 'Text', props) : null,
});

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

  test('timed means 0 < time left < a day: iRacing reports a week when there is no limit', () => {
    expect(UNTIMED_SECONDS).toBe(86400);
    for (const [secs, timed] of [[1, true], [1800, true], [86399, true], [0, false], [-1, false], [86400, false], [A_WEEK, false]] as const) {
      expect({ secs, timed: evalNcalc(timedSession(), game(secs, 0)) }).toEqual({ secs, timed });
    }
    expect(evalNcalc(showTime(), game(1800, 0))).toBe(true);
    expect(evalNcalc(showTime(), game(A_WEEK, 0))).toBe(false);
  });

  test('auto (and no plugin) shows time left in a timed session, laps otherwise', () => {
    expect(reading(game(1800, 0))).toEqual({ label: 'TIME LEFT', value: '0:30:00', dim: false, denominator: null });
    expect(reading(game(5025, 20, 3, 'auto'))).toEqual({ label: 'TIME LEFT', value: '1:23:45', dim: false, denominator: null });
    expect(reading(game(A_WEEK, 20, 3, 'auto'))).toEqual({ label: 'LAP', value: '3', dim: false, denominator: '/ 20' });
    expect(reading(game(A_WEEK, 0, 3, 'auto'))).toEqual({ label: 'LAP', value: '3', dim: false, denominator: null });
    expect(reading(game(0, 20, 3))).toEqual({ label: 'LAP', value: '3', dim: false, denominator: '/ 20' });
  });

  test('time shows time left, or the dim placeholder when the session has no limit', () => {
    expect(reading(game(86399, 20, 3, 'time'))).toEqual({ label: 'TIME LEFT', value: '23:59:59', dim: false, denominator: null });
    expect(reading(game(A_WEEK, 20, 3, 'time'))).toEqual({ label: 'TIME LEFT', value: TIME_PLACEHOLDER, dim: true, denominator: null });
    expect(reading(game(0, 0, 3, 'time'))).toEqual({ label: 'TIME LEFT', value: '-:--:--', dim: true, denominator: null });
  });

  test('laps shows the lap, with the total only when one is declared', () => {
    expect(reading(game(1800, 20, 3, 'laps'))).toEqual({ label: 'LAP', value: '3', dim: false, denominator: '/ 20' });
    expect(reading(game(1800, 0, 112, 'laps'))).toEqual({ label: 'LAP', value: '112', dim: false, denominator: null });
    expect(reading(game(A_WEEK, 5, 7, 'laps'))).toEqual({ label: 'LAP', value: '7', dim: false, denominator: '/ 5' });
  });
});

describe('the module reads the same setting as the card', () => {
  test('a zone shows the lap or the time left, never both', () => {
    // The setting was the card's alone, so every zone face and every companion drew Lap, Time left
    // and Laps left side by side whatever the driver had chosen. One of the first two answers the
    // question now and the rank closes over the other, which is what the setting is for.
    const module = MODULES.find((m) => m.id === 'session')!;
    const items = module.build({ frame: rect(0, 0, 445, 286), density: 'zone', prefix: '' });
    const visible = (name: string): string => {
      const item = [...walkItems(items)].find((i) => i.name === `${name}.value`);
      if (!item) throw new Error(`no ${name}`);
      const formula = item.bindings?.Visible?.formula;
      return String(typeof formula === 'string' ? formula : formula?.expression);
    };
    expect(visible('timeLeft')).toContain('SessionProgress');
    expect(visible('lap')).toContain('SessionProgress');
    // Complementary: one is the negation of the other, so exactly one of the two is ever drawn.
    expect(visible('lap')).toBe(`!(${visible('timeLeft')})`);
  });
});
