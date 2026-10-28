/**
 * The AiM theme's readings stand on their ghosts (#886): the ordinary reading of every figure, which
 * is its sample, lights segments of the ghost behind it cell for cell, its point over the ghost's
 * point and its digits over the ghost's eights.
 *
 * A ghost is the widest string the binding can draw, so a figure that declares a widest of another
 * shape than its sample has a ghost of another shape too, and where both are left-aligned the
 * ordinary reading starts at the ghost's first cell with its point a cell short of the ghost's. That
 * is what `widest` declared on the lap review's deltas did on every AiM face while every other test
 * passed: nothing clips and no box moves, and the figure simply sits off its ghost.
 *
 * The AiM's colours are its own, so a process drawing another theme starts this file again in one
 * drawing them, as `conformance.test.ts` does. It runs whatever the branch touched, because what
 * moves a ghost is as often a `widest` in the house's code as anything under `themes/aim/`.
 */
import { describe, expect, test } from 'bun:test';
import type { TextItem } from '../src/generator.ts';
import { DELTA_PRECISIONS } from '../src/contract.ts';
import { measureText } from '../src/design/advances.ts';
import { buildThemeFace } from '../src/themes/faces.ts';
import { THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc } from './ncalcEval.ts';

const AIM = 'aim';
const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
/** What SimHub takes for the narrow cells when an item names none, as `serialize.ts` writes it. */
const DEFAULT_SPECIAL_CHARS = '.,:';

/** Where each character of `text` starts in `item`'s box, and whether its cell is a narrow one. */
function cells(item: TextItem, text: string): string[] {
  const mono = item.monospace!;
  const specials = mono.specialChars ?? DEFAULT_SPECIAL_CHARS;
  const widthOf = (c: string): number => (specials.includes(c) ? mono.specialCharsWidth : mono.charWidth);
  const chars = [...text];
  const width = chars.reduce((sum, c) => sum + widthOf(c), 0);
  let x = item.rect.left + (item.hAlign === 'right' ? item.rect.width - width : item.hAlign === 'center' ? (item.rect.width - width) / 2 : 0);
  return chars.map((c) => {
    const at = `${x.toFixed(1)} ${specials.includes(c) ? 'narrow' : 'cell'}`;
    x += widthOf(c);
    return at;
  });
}

/** Every reading of the face that does not stand on its ghost, as a sentence naming it. */
function offTheirGhosts(size: { width: number; height: number }): string[] {
  const face = buildThemeFace(AIM, size, OPTS);
  const problems: string[] = [];
  for (const dashboard of [face.built.main, ...face.built.zones]) {
    for (const screen of dashboard.screens) {
      const texts = [...walkItems(screen.items)].filter((item): item is TextItem => item.kind === 'text');
      const byName = new Map(texts.map((item) => [item.name, item]));
      for (const ghost of texts.filter((item) => item.name.endsWith('.ghost'))) {
        const reading = byName.get(ghost.name.slice(0, -'.ghost'.length));
        if (!reading?.monospace || !ghost.monospace) continue;
        const under = new Set(cells(ghost, ghost.text));
        const off = cells(reading, reading.text).filter((at) => !under.has(at));
        if (off.length > 0) {
          problems.push(`${size.width}x${size.height} ${dashboard.name} ${screen.name}: ${reading.name} draws ${JSON.stringify(reading.text)} ${reading.hAlign} over ${JSON.stringify(ghost.text)} ${ghost.hAlign}`);
        }
      }
    }
  }
  return problems;
}

const PRECISION = 'OpenDash.DeltaPrecision';
const SESSION_DELTA = 'PersistantTrackerPlugin.SessionBestLiveDeltaSeconds';
/** The short end, a slower lap, a level one, the widest at three places, and a long stop's. */
const DELTAS = [-0.21, 1.03, 0.003, -12.345, 123.4] as const;

/**
 * Where each character of a right-aligned run ends, and whether it is the point: a run's right
 * edges, in DSEG7Bold advances. A cell is held by where it ends rather than where it starts because
 * the `+` is not a glyph of the face and is drawn narrower than a cell, so it starts inside the
 * cell it stands on and ends where that cell ends.
 */
function rightEdges(item: TextItem, text: string): string[] {
  let x = item.rect.left + item.rect.width;
  return [...text].reverse().map((c) => {
    const at = `${x.toFixed(2)} ${c === '.' ? 'point' : 'cell'}`;
    x -= measureText('DSEG7Bold', c, item.fontSize!);
    return at;
  });
}

const textFormula = (item: TextItem): string | undefined => {
  const b = item.bindings?.Text;
  return b && typeof b.formula === 'string' ? b.formula : undefined;
};

/**
 * Every live delta that does not stand on its ghost at some precision and some delta, as a sentence
 * naming it, and the names of the readings looked at. The live delta is drawn to two places or to
 * three, and its point has to land on the ghost's point at both (#988). `offTheirGhosts` looks at the
 * monospaced readings, which these are not.
 */
function deltasOffTheirGhosts(size: { width: number; height: number }): { problems: string[]; seen: Set<string> } {
  const face = buildThemeFace(AIM, size, OPTS);
  const problems: string[] = [];
  const seen = new Set<string>();
  for (const dashboard of [face.built.main, ...face.built.zones]) {
    for (const screen of dashboard.screens) {
      const texts = [...walkItems(screen.items)].filter((item): item is TextItem => item.kind === 'text');
      const byName = new Map(texts.map((item) => [item.name, item]));
      for (const value of texts.filter((item) => item.name.endsWith('.value') && textFormula(item)?.includes(`[${PRECISION}]`))) {
        const ghost = byName.get(`${value.name.slice(0, -'.value'.length)}.ghost`);
        if (ghost === undefined) {
          problems.push(`${size.width}x${size.height} ${screen.name}: ${value.name} has no ghost`);
          continue;
        }
        seen.add(value.name.replace(/^.*?((delta|lapTimes)\.delta\.value)$/, '$1'));
        for (const precision of DELTA_PRECISIONS) {
          for (const delta of DELTAS) {
            const props = { [PRECISION]: precision, [SESSION_DELTA]: delta };
            const lit = String(evalNcalc(textFormula(value)!, props));
            const ghostFormula = textFormula(ghost);
            const unlit = ghostFormula === undefined ? ghost.text : String(evalNcalc(ghostFormula, props));
            const under = new Set(rightEdges(ghost, unlit));
            if (rightEdges(value, lit).some((at) => !under.has(at))) {
              problems.push(`${size.width}x${size.height} ${screen.name}: ${value.name} draws ${JSON.stringify(lit)} over ${JSON.stringify(unlit)} at ${precision}`);
            }
          }
        }
      }
    }
  }
  return { problems, seen };
}

if (THEME_ID === AIM) {
  for (const size of THEMES[AIM]!.anatomy.sizes) {
    describe(`the AiM theme at ${size.width}x${size.height}`, () => {
      test('every reading stands on its ghost cell for cell', () => {
        expect(offTheirGhosts(size)).toEqual([]);
      });

      test('the live delta stands on its ghost at both precisions, its point on the unlit point (#988)', () => {
        const { problems, seen } = deltasOffTheirGhosts(size);
        expect(problems).toEqual([]);
        // The delta page is on every size; a scan that found nothing would pass for the wrong reason.
        expect(seen.has('delta.delta.value')).toBe(true);
      });
    });
  }
} else {
  test(
    'the AiM theme, in a process drawing its colours',
    () => {
      const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: AIM }, stdout: 'pipe', stderr: 'pipe' });
      const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
      expect({ exit: run.exitCode, output }).toEqual({ exit: 0, output: '' });
    },
    300_000,
  );
}
