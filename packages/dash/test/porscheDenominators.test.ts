/**
 * The car register aligns its values on the right, and a value with a denominator after it is a pair
 * the house sets the other way round: the value at the left of its cells and the denominator bound to
 * follow whatever it draws. Once a lap was cut for `999` (#596), that left the empty third cell at the
 * right of `12 / 30`, so the session page's lap stood a cell left of the position over it. The register
 * sets the value against the right of its cells instead and stands the denominator after them, which
 * these hold it to on every page of every face.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages, themesToBuild } from '../src/build.ts';
import type { TextItem } from '../src/generator.ts';
import { DENOMINATOR_GAP } from '../src/second/field.ts';
import { walkItems } from '../src/walk.ts';

const themes = themesToBuild({ themes: ['porsche'], allThemes: false, touchedThemes: false }, () => {});
const PACKAGES = composePackages({ version: '0.0.0-test', themes, log: () => {} }).filter(({ pkg }) => pkg.folderName.includes('Porsche'));

interface Pair {
  where: string;
  value: TextItem;
  denominator: TextItem;
}

const PAIRS: Pair[] = PACKAGES.flatMap(({ pkg }) =>
  pkg.dashboards.flatMap((dashboard) =>
    dashboard.screens.flatMap((screen) => {
      const texts = new Map([...walkItems(screen.items)].filter((i): i is TextItem => i.kind === 'text').map((i) => [i.name, i]));
      return [...texts.values()].flatMap((denominator) => {
        if (!denominator.name.endsWith('.denominator')) return [];
        const value = texts.get(denominator.name.replace(/\.denominator$/, '.value'));
        return value === undefined ? [] : [{ where: `${pkg.folderName} / ${dashboard.name} / ${screen.name}`, value, denominator }];
      });
    }),
  ),
);

describe('the car register sets a value and its denominator from the right', () => {
  test('the session pages draw the lap and the position over their denominators', () => {
    const names = new Set(PAIRS.map((p) => p.value.name));
    expect(names.has('session.lap.value')).toBe(true);
    expect(names.has('session.position.value')).toBe(true);
  });

  test('the value is right-aligned in its cells and the denominator stands after the last of them, whatever the value reads', () => {
    const wrong = PAIRS.flatMap(({ where, value, denominator }) => {
      const faults = [
        value.hAlign === 'right' ? undefined : `${value.name} is set ${value.hAlign}`,
        denominator.bindings?.Left === undefined ? undefined : `${denominator.name} moves with the value`,
        value.rect.left + value.rect.width + DENOMINATOR_GAP === denominator.rect.left
          ? undefined
          : `${denominator.name} at ${denominator.rect.left}, not ${DENOMINATOR_GAP} px after the value's box at ${value.rect.left + value.rect.width}`,
      ];
      return faults.filter((f) => f !== undefined).map((f) => `${where}: ${f}`);
    });
    expect(wrong).toEqual([]);
  });

  test("in a line, the lap's slash stands where the position's does, and both pairs end on the same edge", () => {
    const byPage = new Map<string, Pair[]>();
    for (const pair of PAIRS) byPage.set(pair.where, [...(byPage.get(pair.where) ?? []), pair]);
    let lines = 0;
    const wrong = [...byPage].flatMap(([where, pairs]) => {
      const lap = pairs.find((p) => p.value.name === 'session.lap.value');
      const position = pairs.find((p) => p.value.name === 'session.position.value');
      if (lap === undefined || position === undefined) return [];
      const end = (p: Pair): number => p.denominator.rect.left + p.denominator.rect.width;
      // A line sets its value against the zone's right edge; a value centred under its title cell does not.
      if (end(lap) !== end(position)) return [];
      lines++;
      return lap.denominator.rect.left === position.denominator.rect.left ? [] : [`${where}: the lap's slash at ${lap.denominator.rect.left}, the position's at ${position.denominator.rect.left}`];
    });
    expect(lines).toBeGreaterThan(0);
    expect(wrong).toEqual([]);
  });
});
