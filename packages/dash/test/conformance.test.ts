/**
 * Every theme still shows everything (#200): at every size a theme claims, nothing it draws clips,
 * nothing escapes its frame, and nothing disappears. `conformance.ts` holds the three properties;
 * this file asks them of every theme the run selects, which is how a theme added to the registry is
 * held to them without a line written here.
 *
 * A theme with colours of its own cannot be built in a process drawing another theme's, so the
 * default process starts this file again in one drawing them, once per such theme. The colours move
 * no rectangle, but a process that drew a theme in someone else's is the very thing `faces.ts`
 * refuses, and a test is not the place to make the exception.
 *
 * The test theme of #196 is checked on every run, since it is what proves that the harness reads a
 * theme's own anatomy rather than the house face's.
 */
import { afterAll, beforeAll, describe, expect, test } from 'bun:test';
import { buildThemeFace, drawsInThisProcess, type ThemeFace } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { clipped, disappeared, escaped, themesToCheck } from './conformance.ts';
import { GEAR_LEFT_THEME_ID, gearLeftTheme } from './fixtures/gearLeftTheme.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

// Registered as the file is read rather than in a hook, because the tests below are named after the
// themes; and taken out again once they have run, so that no other file iterates it.
THEMES[GEAR_LEFT_THEME_ID] = gearLeftTheme;
afterAll(() => {
  delete THEMES[GEAR_LEFT_THEME_ID];
});

/**
 * What a theme is known to get wrong, by property, pinned rather than tolerated: the property's
 * other problems still fail, and a pin whose fault has been put right fails too, so that putting it
 * right is a line taken out here.
 *
 * Empty since #1047. The test theme puts the hero in the gear's place at the left edge, and the
 * pop-ups and the change notifications were centred on the hero and wider than it, so they started
 * left of the face; they are no larger than the hero now, which answers the question this pin left
 * to the machinery, and `popUpRegions.test.ts` holds every theme's family to its region.
 */
const KNOWN: Record<string, Partial<Record<'clipped' | 'escaped' | 'disappeared', RegExp>>> = {};

/** The problems a property finds, less the ones pinned for this theme, of which there must still be some. */
function unknown(id: string, property: 'clipped' | 'escaped' | 'disappeared', problems: string[]): string[] {
  const pin = KNOWN[id]?.[property];
  if (!pin) return problems;
  expect({ theme: id, property, stillPinned: problems.some((p) => pin.test(p)) }).toEqual({ theme: id, property, stillPinned: true });
  return problems.filter((p) => !pin.test(p));
}

const selection = themesToCheck();
const ids = [...new Set([...selection.ids, GEAR_LEFT_THEME_ID])];
const here = ids.filter(drawsInThisProcess);
console.log(`theme conformance in ${THEME_ID}'s colours: ${here.join(', ')}; ${selection.why}`);

for (const id of here) {
  for (const size of THEMES[id]!.anatomy.sizes) {
    describe(`the ${id} theme at ${size.width}x${size.height}`, () => {
      let face: ThemeFace;
      beforeAll(() => {
        face = buildThemeFace(id, size, OPTS);
      });

      test('nothing clips', () => {
        expect(unknown(id, 'clipped', clipped(id, face))).toEqual([]);
      });

      test('nothing escapes its frame', () => {
        expect(unknown(id, 'escaped', escaped(id, face))).toEqual([]);
      });

      test('nothing disappears', () => {
        expect(unknown(id, 'disappeared', disappeared(id, face))).toEqual([]);
      });
    });
  }
}

if (THEME_ID === DEFAULT_THEME_ID) {
  for (const id of ids.filter((each) => !drawsInThisProcess(each))) {
    test(
      `the ${id} theme, in a process drawing its colours`,
      () => {
        const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: id }, stdout: 'pipe', stderr: 'pipe' });
        const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
        expect({ theme: id, exit: run.exitCode, output }).toEqual({ theme: id, exit: 0, output: '' });
      },
      300_000,
    );
  }
}
