/**
 * A gap or an interval that rounds to nought reads a bare `0.0` (#1078).
 *
 * The Gap and Int columns wrote their difference with `signed(x, '0.0')`, which keeps the sign of
 * the unrounded value, so two cars within a twentieth of a second of each other read `+0.0` or
 * `−0.0`. The VM's `quali` scenario showed both on 2026-10-14: P4's and P10's Int at `−0.0` and car
 * 3's Gap at `+0.0`. The lap readings draw a level delta as a bare zero (#614), and the columns now
 * do the same.
 *
 * The race frame is four GT3s: P2 0.04 s behind the leader, P3 12 s back and P4 0.04 s *ahead* of
 * P3 by gap, which is the inverted pair a distance ordering throws up (#1056). The practice frame is
 * the same field by best lap, P2 0.04 s off P1 and P3 0.04 s quicker than P2.
 */
import { describe, expect, test } from 'bun:test';
import type { Expr } from '../src/bind.ts';
import { rect } from '../src/design/geometry.ts';
import { MINUS } from '../src/design/metrics.ts';
import type { Item } from '../src/generator.ts';
import { ncalc } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { racePage } from '../src/screens/pitwall.ts';
import { carClassInterval, carClassRaceGap, carInterval, carRaceGap } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const { num } = ncalc;

/** Each car's gap to the leader, null on the leader's own row as SimHub leaves it. */
const GAPS: readonly (number | null)[] = [null, 0.04, 12.0, 11.96];
/** Each car's best lap, for the sessions ordered by it. */
const BESTS: readonly number[] = [90.0, 90.04, 90.0, 90.06];

const frame = (session: string, mode: 'class' | 'overall', gaps: readonly (number | null)[] = GAPS, row = 1): Props => {
  const props: Props = {
    'DataCorePlugin.GameData.SessionTypeName': session,
    'OpenDash.PositionMode': mode,
    'DataCorePlugin.GameData.OpponentsCount': gaps.length,
    'DataCorePlugin.GameData.PlayerClassOpponentsCount': gaps.length,
    'getplayerleaderboardposition()': 2,
    'repeatindex()': row,
  };
  gaps.forEach((gap, i) => {
    const n = i + 1;
    props[`driveravailable(${n})`] = true;
    props[`drivercarclass(${n})`] = 'GT3';
    props[`driverposition(${n})`] = n;
    props[`driverclassposition(${n})`] = n;
    props[`getopponentleaderboardposition_playerclassonly(${n})`] = n;
    props[`driverbestlap(${n})`] = BESTS[i];
    props[`drivergaptoleader(${n})`] = gap;
    props[`driverlapstoleader(${n})`] = n === 1 ? null : 0;
    props[`driverlapstoclassleader(${n})`] = n === 1 ? null : 0;
  });
  return props;
};

/** Rows 2 to 4 of one column on the frame. */
const column = (read: (idx: Expr) => Expr, session: string, mode: 'class' | 'overall', gaps?: readonly (number | null)[]): unknown[] =>
  [2, 3, 4].map((idx) => evalNcalc(read(num(idx)), frame(session, mode, gaps)));

describe('in a race', () => {
  for (const mode of ['overall', 'class'] as const) {
    test(`a car 0.04 s behind reads 0.0 in both columns, counting ${mode}`, () => {
      const gap = mode === 'class' ? carClassRaceGap : carRaceGap;
      const int = mode === 'class' ? carClassInterval : carInterval;
      expect(column(gap, 'Race', mode)).toEqual(['0.0', '+12.0', '+12.0']);
      // P4 is 0.04 s ahead of P3 by gap, which drew `−0.0`.
      expect(column(int, 'Race', mode)).toEqual(['0.0', '+12.0', '0.0']);
    });
  }

  test('a gap 0.04 s the other side of the leader reads 0.0 too', () => {
    expect(column(carRaceGap, 'Race', 'overall', [null, -0.04, 12.0, 11.96])).toEqual(['0.0', '+12.0', '+12.0']);
  });

  test('a difference that rounds to a tenth keeps its sign', () => {
    expect(column(carRaceGap, 'Race', 'overall', [null, -0.06, 12.0, 11.94])).toEqual([`${MINUS}0.1`, '+12.0', '+11.9']);
    expect(column(carInterval, 'Race', 'overall', [null, 0.06, 12.0, 11.94])).toEqual(['+0.1', '+11.9', `${MINUS}0.1`]);
  });
});

describe('outside a race, by best lap', () => {
  test('a best lap 0.04 s either side of the car in front reads 0.0', () => {
    expect(column(carRaceGap, 'Lone Qualify', 'overall')).toEqual(['0.0', '0.0', '+0.1']);
    expect(column(carInterval, 'Lone Qualify', 'overall')).toEqual(['0.0', '0.0', '+0.1']);
    expect(column(carClassInterval, 'Lone Qualify', 'class')).toEqual(['0.0', '0.0', '+0.1']);
  });
});

/** The boards the columns are drawn on, as they are shipped: the pit wall's race board and a face's leaderboard. */
describe('the boards that draw the two columns', () => {
  const formula = (items: Item[], suffix: string): string => {
    const found = items.find((i) => i.name.endsWith(suffix));
    const bound = found?.bindings?.Text;
    if (!bound || typeof bound !== 'object' || !('formula' in bound)) throw new Error(`no ${suffix} formula`);
    return String(bound.formula);
  };
  const PIT_WALL = [...walkItems(racePage(1920, 1080).items)];
  const ZONE = [...walkItems(MODULES.find((m) => m.id === 'leaderboard')!.build({ frame: rect(0, 0, 769, 358), density: 'zone', prefix: '' }))];

  test('the pit wall draws an Int column', () => {
    expect(PIT_WALL.some((i) => i.name.endsWith('row.int'))).toBe(true);
  });

  for (const [board, items] of [["the pit wall's race board", PIT_WALL], ["a face's leaderboard", ZONE]] as const) {
    for (const mode of ['overall', 'class'] as const) {
      test(`${board}, counting ${mode}`, () => {
        const rows = (suffix: string): unknown[] => [1, 2, 3, 4].map((row) => evalNcalc(formula(items, suffix), frame('Race', mode, GAPS, row)));
        expect(rows('row.gap')).toEqual(['Lead', '0.0', '+12.0', '+12.0']);
        // A face's zone is too narrow for an Int column and draws only the Gap.
        if (items.some((i) => i.name.endsWith('row.int'))) expect(rows('row.int')).toEqual(['', '0.0', '+12.0', '0.0']);
      });
    }
  }
});
