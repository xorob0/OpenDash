/**
 * The Porsche strip's page name, `RACE`, `QUAL` or `PRACTICE`, on every sim 1.0 claims (#1125).
 *
 * The strip names the session it reads from SimHub's `SessionTypeName`, and the sims write that name
 * in different cases. iRacing's words arrive as iRacing writes them, `Race` and `Lone Qualify`, and
 * Assetto Corsa's and ACC's as SimHub's `ACManager.GD_SessionTypeName` writes them, the enum's name
 * with its `AC_` taken off, so `RACE` and `QUALIFY` (decompiled `ACSharedMemory.dll`). NCalc's `=`
 * and `in` compare strings with `Comparer.Default`, which minds the case, so the strip compared the
 * name as iRacing writes it and read `PRACTICE` in an AC race. These evaluate the binding the
 * package carries against both spellings.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages, themesToBuild } from '../src/build.ts';
import type { Formula, TextItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc } from './ncalcEval.ts';

const themes = themesToBuild({ themes: ['porsche'], allThemes: false, touchedThemes: false }, () => {});
const PACKAGES = composePackages({ version: '0.0.0-test', themes, log: () => {} }).filter(({ pkg }) => pkg.folderName.includes('Porsche'));

const formulaText = (f: string | Formula): string => (typeof f === 'string' ? f : f.expression);

/** Every distinct binding the strip's page name is drawn with, in every Porsche package. */
const BINDINGS: string[] = [
  ...new Set(
    PACKAGES.flatMap(({ pkg }) =>
      pkg.dashboards.flatMap((dashboard) =>
        dashboard.screens.flatMap((screen) =>
          [...walkItems(screen.items)]
            .filter((i): i is TextItem => i.kind === 'text' && i.name === 'strip.page')
            .flatMap((i) => (i.bindings?.Text === undefined ? [] : [formulaText(i.bindings.Text.formula)])),
        ),
      ),
    ),
  ),
];

/** Each session name a sim writes, and the page the strip names for it. */
const SESSIONS: readonly [sim: string, name: string, page: string][] = [
  ['Assetto Corsa', 'RACE', 'RACE'],
  ['Assetto Corsa', 'QUALIFY', 'QUAL'],
  ['Assetto Corsa', 'PRACTICE', 'PRACTICE'],
  ['Assetto Corsa', 'HOTLAP', 'PRACTICE'],
  ['iRacing', 'Race', 'RACE'],
  ['iRacing', 'Qualify', 'QUAL'],
  ['iRacing', 'Lone Qualify', 'QUAL'],
  ['iRacing', 'Open Qualify', 'QUAL'],
  ['iRacing', 'Practice', 'PRACTICE'],
];

describe("the Porsche strip names the session whatever case the sim writes it in", () => {
  test('the strip is found in every Porsche package, with its page name bound', () => {
    expect(PACKAGES.length).toBeGreaterThan(0);
    expect(BINDINGS.length).toBeGreaterThan(0);
  });

  for (const [sim, name, page] of SESSIONS) {
    test(`${sim}'s ${name} reads ${page}`, () => {
      for (const binding of BINDINGS) expect(evalNcalc(binding, { 'DataCorePlugin.GameData.SessionTypeName': name })).toBe(page);
    });
  }
});
