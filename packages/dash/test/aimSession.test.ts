/**
 * The AiM writes every sim's session name whole, inside its cell and on its ghost (#1127).
 *
 * The LCD cuts the session's cell to the widest name it can write, and the names it knew were
 * iRacing's, so the widest was `Time Trial`, ten cells. Assetto Corsa writes its time attack as
 * `TIME_ATTACK`, eleven, and the face drew it a cell wider than its box, which WPF clips: the driver
 * read `TIME_ATTAC`. Nothing failed, because every test that measured the cell measured it against
 * the names it had been cut to. So this one takes the names from what SimHub's readers write
 * (`sessionNames.ts`), puts each through the binding the face draws, as SimHub would, and measures
 * what comes out against the box and against the ghost behind it. iRacing's names are pinned to what
 * the unit wrote before, since a sim added to the list must not move a word of iRacing's.
 *
 * The AiM's fonts are its own, so a process drawing another theme starts this file again in one
 * drawing them, as `aimGhosts.test.ts` does.
 */
import { describe, expect, test } from 'bun:test';
import type { TextItem } from '../src/generator.ts';
import { buildThemeFace } from '../src/themes/faces.ts';
import { THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { widthAsDrawn } from './drawnStrings.ts';
import { bindingExpression } from './monoGlyphs.ts';
import { evalNcalc } from './ncalcEval.ts';
import { WRITTEN_SESSION_NAMES } from './sessionNames.ts';

const AIM = 'aim';
const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const SESSION = 'DataCorePlugin.GameData.SessionTypeName';
const FIELD = 'session.type.value';

/** What the unit wrote for each of iRacing's names before a second sim's were listed. */
const IRACING_WRITTEN: Readonly<Record<string, string>> = {
  'Offline Testing': 'Testing',
  'Practice': 'Practice',
  'Open Practice': 'Practice',
  'Lone Practice': 'Practice',
  'Open Qualify': 'Qualify',
  'Lone Qualify': 'Qualify',
  'Qualify': 'Qualify',
  'Warmup': 'Warmup',
  'Heat': 'Heat',
  'Consolation': 'Consi',
  'Race': 'Race',
  'Test': 'Test',
  'Time Trial': 'Time!Trial',
};

/**
 * Every reading of the face that draws the session's name, which is the session page's Session field,
 * beside the ghost it stands on. Other readings read the name too, the fuel to the end to ask whether
 * the session is a race, and draw a figure.
 */
function sessionReadings(size: { width: number; height: number }): { where: string; value: TextItem; ghost: TextItem }[] {
  const face = buildThemeFace(AIM, size, OPTS);
  const found: { where: string; value: TextItem; ghost: TextItem }[] = [];
  for (const dashboard of [face.built.main, ...face.built.zones]) {
    for (const screen of dashboard.screens) {
      const texts = [...walkItems(screen.items)].filter((item): item is TextItem => item.kind === 'text');
      const byName = new Map(texts.map((item) => [item.name, item]));
      for (const value of texts.filter((item) => item.name.endsWith(FIELD) && bindingExpression(item, 'Text').includes(`[${SESSION}]`))) {
        const ghost = byName.get(`${value.name.slice(0, -'.value'.length)}.ghost`);
        if (ghost) found.push({ where: `${size.width}x${size.height} ${dashboard.name} ${screen.name} ${value.name}`, value, ghost });
      }
    }
  }
  return found;
}

const written = (value: TextItem, name: string): string => String(evalNcalc(bindingExpression(value, 'Text'), { [SESSION]: name }));

if (THEME_ID === AIM) {
  describe('the AiM theme at every size it is built at', () => {
    // The faces whose zones keep the Session field when the session page sheds its readings: at the
    // time of writing 1920 x 480 alone, whose columns are wide enough for it.
    const found = THEMES[AIM]!.anatomy.sizes.flatMap(sessionReadings);

    test('draws the session somewhere', () => {
      expect(found.length).toBeGreaterThan(0);
    });

    test("writes every sim's session name inside its cell and on its ghost", () => {
      for (const { where, value, ghost } of found) {
        for (const [sim, names] of Object.entries(WRITTEN_SESSION_NAMES)) {
          for (const name of names) {
            const text = written(value, name);
            const width = widthAsDrawn(value, text);
            expect({ where, sim, name, text, inBox: width <= value.rect.width, onGhost: width <= widthAsDrawn(ghost, ghost.text) }).toMatchObject({ inBox: true, onGhost: true });
          }
        }
      }
    });

    test("writes iRacing's names as it always has, and Assetto Corsa's time attack as its last word", () => {
      for (const { where, value } of found) {
        for (const [name, expected] of Object.entries(IRACING_WRITTEN)) expect({ where, name, text: written(value, name) }).toEqual({ where, name, text: expected });
        expect({ where, text: written(value, 'TIME_ATTACK') }).toEqual({ where, text: 'ATTACK' });
      }
    });
  });
} else {
  test(
    'the AiM theme, in a process drawing its fonts',
    () => {
      const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: AIM }, stdout: 'pipe', stderr: 'pipe' });
      const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
      expect({ exit: run.exitCode, output }).toEqual({ exit: 0, output: '' });
    },
    300_000,
  );
}
