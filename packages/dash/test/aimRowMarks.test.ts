/**
 * On the AiM, a pitting car's PIT is set as the gap it replaces (#388).
 *
 * The house draws the mark as the pit wall's chip, standing in the gap's cell, and the LCD pass took
 * the chip's word for a caption: no fill, the 14-segment face, and the chip's 19 px box, which left
 * `PIT` at 7 px where the gaps around it are 24 and 34. The author asked for it in the gap's own
 * font. `themes/aim/lcd.ts` now sets a word that stands in a figure's place as the figure is set,
 * and what is pinned here is that it is: in the gap's face, weight and size, in the gap's box,
 * right-aligned where the gap's figures end, shown exactly while the gap is not, and measured inside
 * that box in the face it is drawn in, on both lists of every face the AiM is built at.
 *
 * The AiM's fonts are its own, so a process drawing another theme starts this file again in one
 * drawing them, as `aimGhosts.test.ts` does.
 */
import { describe, expect, test } from 'bun:test';
import { measureText } from '../src/design/advances.ts';
import type { TextItem } from '../src/generator.ts';
import { ncalc } from '../src/generator.ts';
import { buildThemeFace } from '../src/themes/faces.ts';
import { THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { faceOf } from './monoGlyphs.ts';

const AIM = 'aim';
const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const MARK = '.row.gap.pit.text';

const visibleOf = (item: TextItem): string | undefined => {
  const binding = item.bindings?.Visible;
  return binding && 'formula' in binding && typeof binding.formula === 'string' ? binding.formula : undefined;
};

/** Every PIT mark of the face beside the gap it stands in for, as what each is set in. */
function marks(size: { width: number; height: number }) {
  const face = buildThemeFace(AIM, size, OPTS);
  const found: { where: string; mark: TextItem; gap: TextItem | undefined }[] = [];
  for (const dashboard of [face.built.main, ...face.built.zones]) {
    for (const screen of dashboard.screens) {
      const texts = [...walkItems(screen.items)].filter((item): item is TextItem => item.kind === 'text');
      const byName = new Map(texts.map((item) => [item.name, item]));
      for (const mark of texts.filter((item) => item.name.endsWith(MARK))) {
        found.push({ where: `${size.width}x${size.height} ${dashboard.name} ${screen.name} ${mark.name}`, mark, gap: byName.get(mark.name.slice(0, -'.pit.text'.length)) });
      }
    }
  }
  return found;
}

const setIn = (item: TextItem) => ({ font: item.font, weight: item.fontWeight, size: item.fontSize, rect: item.rect, hAlign: item.hAlign, vAlign: item.vAlign });

if (THEME_ID === AIM) {
  for (const size of THEMES[AIM]!.anatomy.sizes) {
    describe(`the AiM theme at ${size.width}x${size.height}`, () => {
      const found = marks(size);

      test('marks a pitting car on both lists', () => {
        const pages = new Set(found.map(({ mark }) => (mark.name.includes('relative') ? 'relative' : mark.name.includes('leaderboard') ? 'leaderboard' : mark.name)));
        expect([...pages].sort()).toEqual(['leaderboard', 'relative']);
      });

      test("sets PIT in the gap's face, weight and size, in the gap's box, against the edge its figures keep", () => {
        for (const { where, mark, gap } of found) {
          expect({ where, gap: gap !== undefined }).toEqual({ where, gap: true });
          if (!gap) continue;
          expect({ where, text: mark.text, ...setIn(mark) }).toEqual({ where, text: 'PIT', ...setIn(gap) });
          expect({ where, hAlign: mark.hAlign }).toEqual({ where, hAlign: 'right' });
        }
      });

      test('shows it exactly while the gap is hidden', () => {
        for (const { where, mark, gap } of found) {
          const when = visibleOf(mark);
          expect({ where, bound: when !== undefined, gap: gap && visibleOf(gap) }).toEqual({ where, bound: true, gap: when === undefined ? undefined : ncalc.not(when) });
        }
      });

      test('fits it inside the box, measured in the face it is drawn in', () => {
        for (const { where, mark } of found) {
          const width = measureText(faceOf(mark), mark.widest ?? mark.text, mark.fontSize);
          expect({ where, face: faceOf(mark), fits: width < mark.rect.width }).toEqual({ where, face: 'DSEG7Regular', fits: true });
        }
      });
    });
  }
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
