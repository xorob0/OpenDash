/**
 * Trace version 2 end to end, without the VM (#257, #395).
 *
 * The committed traces are re-recorded on the VM, which this repository cannot do from a test. So a
 * small version 2 trace is written by hand in `scripts/fixtures/relative.v2.ndjson`: seven cars, the
 * player fourth, the on-track order different from the leaderboard's, and the calls the relative
 * makes recorded under their canonical text as the recorder writes them. This test reads it with the
 * trace reader, builds the relative page the packages draw, and evaluates its bindings against a
 * frame with the browser evaluator, repeated rows and all. What comes out is the driver names and
 * the gaps, which is the page every version 1 trace drew empty.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { MODULES } from '../packages/dash/src/modules/index.ts';
import { ncalcEvaluator as E, type Formula, type Item } from '../packages/dash/src/generator.ts';
import { callsOf, frame, parseTrace, propertiesOf } from './traceFormat.ts';

const fixture = parseTrace(readFileSync(path.join(import.meta.dir, 'fixtures', 'relative.v2.ndjson'), 'utf8'));

/** The companion's page, 802 by 356 at companion density: seven rows, the player in the middle. */
const COMPANION_PAGE = { left: 0, top: 0, width: 802, height: 356 };

const formulaText =(f: string | Formula): string => (typeof f === 'string' ? f : f.expression);

/** A drawn text: the item's name, the repeat copies around it, and what it says. */
interface Drawn {
  name: string;
  repeat: readonly number[];
  text: string;
}

/**
 * The texts a list of items draws against one frame, the way the demo's engine walks them: `Visible`
 * first, a repeated layer once per copy (1..Repetitions + 1) with its index pushed onto the scope, and
 * a text's bound `Text` in place of its literal. A binding that fails as it would in SimHub draws
 * nothing; one the evaluator cannot compute throws, and fails the test.
 */
function texts(items: readonly Item[], values: Readonly<Record<string, unknown>>, repeat: readonly number[] = []): Drawn[] {
  const out: Drawn[] = [];
  const scope: E.Scope = { properties: values, repeat };
  const bound = (item: Item, target: string): E.Value | undefined => {
    const binding = item.bindings?.[target as keyof typeof item.bindings];
    if (!binding) return undefined;
    const r = E.evaluateBinding(formulaText(binding.formula), scope);
    return r.ok ? r.value : null;
  };
  for (const item of items) {
    const visible = bound(item, 'Visible');
    if (visible !== undefined && visible !== true) continue;
    if (item.kind === 'layer') {
      const copies = item.repetitions ?? 0;
      if (copies === 0) out.push(...texts(item.children, values, repeat));
      else for (let copy = 1; copy <= copies + 1; copy++) out.push(...texts(item.children, values, [...repeat, copy]));
      continue;
    }
    if (item.kind !== 'text') continue;
    const text = bound(item, 'Text');
    out.push({ name: item.name, repeat, text: text === undefined ? item.text : text === null ? '' : E.valueToString(text) });
  }
  return out;
}

/** The relative's rows on one frame, as pos, number, name and gap. */
function rows(index: number): string[][] {
  const relative = MODULES.find((m) => m.id === 'relative');
  if (!relative) throw new Error('no relative module');
  const items = relative.build({ frame: COMPANION_PAGE, density: 'companion', prefix: '' });
  const drawn = texts(items, frame(fixture, index)).filter((d) => d.repeat.length === 1);
  const byRow = new Map<number, Map<string, string>>();
  for (const d of drawn) {
    const row = byRow.get(d.repeat[0]!) ?? new Map<string, string>();
    row.set(d.name.replace(/^table\.row\./, ''), d.text);
    byRow.set(d.repeat[0]!, row);
  }
  return [...byRow.keys()].sort((a, b) => a - b).map((k) => ['pos', 'num', 'name', 'gap'].map((c) => byRow.get(k)!.get(c) ?? '?'));
}

describe('a version 2 trace, read and replayed', () => {
  test('the reader separates the properties from the calls and hands both over in one frame', () => {
    expect(fixture.header).toMatchObject({ trace: 2, cars: 24, frames: 2 });
    expect(propertiesOf(fixture)).toContain('OpenDash.PositionMode');
    expect(callsOf(fixture)).toContain('drivername(3)');
    expect(callsOf(fixture)).toContain('getopponentleaderboardposition_aheadbehind(-3)');
    expect(frame(fixture, 0)).toMatchObject({ 'OpenDash.PositionMode': 'overall', 'drivername(3)': 'Liam Byrne', 'driverrelativegaptoplayer(6)': -4.512 });
    expect(frame(fixture, 1)['driverrelativegaptoplayer(6)']).toBe(-4.5);
  });

  test('the evaluator answers an opponent call from the frame, its argument computed first', () => {
    const scope = { properties: frame(fixture, 0) };
    expect(E.evaluate('drivername(getopponentleaderboardposition_aheadbehind(-1))', scope)).toBe('Liam Byrne');
    expect(E.evaluate('drivername(getplayerleaderboardposition())', scope)).toBe('Ana Silva');
    expect(E.evaluate('drivername(getopponentleaderboardposition_aheadbehind((repeatindex()) - (4)))', { ...scope, repeat: [7] })).toBe('Marco Rossi');
    // A position past the field has no column, and answers null as SimHub does.
    expect(E.evaluate('drivername(9)', scope)).toBeNull();
  });

  test('the relative page draws the cars around the player in road order, with their names and gaps', () => {
    // On the road a lapped P6 is three places ahead and the leader three behind, so the rows are not
    // the leaderboard: this is the order `getopponentleaderboardposition_aheadbehind` gave.
    expect(rows(0)).toEqual([
      ['P6', '64', 'SOFIA ROSSI', '−4.512'],
      ['P2', '11', 'CHLOE MARTIN', '−2.100'],
      ['P3', '22', 'LIAM BYRNE', '−0.874'],
      ['P4', '31', 'YOU', '0.000'],
      ['P5', '5', 'JONAS WEBER', '+1.203'],
      ['P7', '18', 'TOM HALE', '+3.950'],
      ['P1', '7', 'MARCO ROSSI', '+6.500'],
    ]);
  });

  test('the next frame moves the gaps and nothing else', () => {
    expect(rows(1).map((r) => r[3])).toEqual(['−4.500', '−2.050', '−0.900', '0.000', '+1.250', '+4.000', '+6.250']);
    expect(rows(1).map((r) => r.slice(0, 3))).toEqual(rows(0).map((r) => r.slice(0, 3)));
  });

  test('without the call columns, as in a version 1 trace, the same page draws no car', () => {
    const properties = Object.fromEntries(propertiesOf(fixture).map((p) => [p, frame(fixture, 0)[p]]));
    const relative = MODULES.find((m) => m.id === 'relative')!;
    const items = relative.build({ frame: COMPANION_PAGE, density: 'companion', prefix: '' });
    expect(texts(items, properties).filter((d) => d.repeat.length === 1)).toEqual([]);
  });
});
