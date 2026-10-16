/**
 * A text set in a segment face draws only the glyphs that face carries (#1184).
 *
 * A character the face does not carry is not refused: WPF draws it in a fallback face of its own,
 * with none of the segment face's metrics. That is how the AiM's engine speed came to read `1,350`
 * with a typographic comma hanging under the segment cells, below the one em `inkOf` takes a segment
 * figure to mark, so that the ground #1165 lays under the pit banner blanked the figure and left the
 * comma's tail. The fit tests passed, since a fallback glyph is measured at `FALLBACK_ADVANCE` and the
 * box held it.
 *
 * So every text of every face a theme draws in a segment face is read for what it can draw: its
 * sample, its declared widest, every literal its binding can put on the screen as written, and what
 * the patterns it formats a number or a time in write besides the digits. Every one of those has to
 * be in the face's table in `advances.ts`, which is read from the face itself.
 *
 * A theme's faces are drawn in its own process, so the default process starts this file again for
 * each theme it does not draw, as `aimRowMarks.test.ts` does for the AiM.
 */
import { describe, expect, test } from 'bun:test';
import { carries, type SegmentFace } from '../src/design/advances.ts';
import type { TextItem } from '../src/generator.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { bindingExpression, drawableLiterals, faceOf } from './monoGlyphs.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const SEGMENT_FACES: ReadonlySet<string> = new Set<SegmentFace>(['DSEG7Regular', 'DSEG7Bold', 'DSEG14Regular']);

/** The pattern of every `format(value, 'pattern')` in an expression, its second argument when it is one literal. */
function formatPatterns(expression: string): string[] {
  const out: string[] = [];
  const at = /\bformat\s*\(/gi;
  for (let m = at.exec(expression); m; m = at.exec(expression)) {
    let depth = 0;
    let comma = -1;
    let i = m.index + m[0].length;
    for (; i < expression.length; i++) {
      const ch = expression[i]!;
      if (ch === "'") {
        i += 1;
        while (i < expression.length && expression[i] !== "'") i += expression[i] === '\\' ? 2 : 1;
      } else if (ch === '[') {
        i = Math.max(i, expression.indexOf(']', i));
      } else if (ch === '(') depth += 1;
      else if (ch === ')') {
        if (depth === 0) break;
        depth -= 1;
      } else if (ch === ',' && depth === 0 && comma < 0) comma = i;
    }
    const literal = comma < 0 ? undefined : /^\s*'((?:[^'\\]|\\.)*)'\s*$/.exec(expression.slice(comma + 1, i));
    if (literal) out.push(literal[1]!.replace(/\\(.)/g, '$1'));
  }
  return out;
}

/**
 * What a pattern writes besides the digits. A number's pattern writes a comma where it groups
 * thousands, a comma followed by a digit's place, and a point where it has decimals; `#` and `0` are
 * the digits themselves. A time's pattern writes whatever is not one of its letters, such as the
 * colon of `HH:mm`.
 */
function patternGlyphs(pattern: string): string[] {
  if (/^[#0,.;]*$/.test(pattern)) {
    return [...(/,[#0]/.test(pattern) ? [','] : []), ...(/\.[#0]/.test(pattern) ? ['.'] : [])];
  }
  return [...pattern].filter((ch) => !/[A-Za-z#0\s]/.test(ch));
}

/** Every character a text item can draw, read from what it declares and what it is bound to. */
function drawable(item: TextItem): Set<string> {
  const expression = bindingExpression(item, 'Text');
  const glyphs = new Set<string>();
  for (const source of [item.text, item.widest ?? '', ...drawableLiterals(expression), ...formatPatterns(expression).flatMap(patternGlyphs)]) {
    for (const ch of source) glyphs.add(ch);
  }
  glyphs.delete(' ');
  return glyphs;
}

describe('the reading of a format', () => {
  test("a grouped pattern writes a comma, and the AiM's ungrouped one none", () => {
    expect(formatPatterns("format([DataCorePlugin.GameData.Rpms], '#,0')")).toEqual(['#,0']);
    expect(patternGlyphs('#,0')).toEqual([',']);
    expect(patternGlyphs('0')).toEqual([]);
    expect(patternGlyphs('0.0')).toEqual(['.']);
    expect(patternGlyphs('HH:mm')).toEqual([':']);
  });

  test('only the pattern argument is read, past a nested call and a bracketed property', () => {
    expect(formatPatterns("if([A], format(max(0, [B, C]), '0.00'), '--')")).toEqual(['0.00']);
    expect(formatPatterns("format([A], 'h\\:mm')")).toEqual(['h:mm']);
  });
});

/**
 * What a theme already draws in a fallback face, item by item, which this file holds closed: a new
 * item or a new glyph fails, and so does an entry here that nothing draws any more, so that the list
 * only shrinks. The comma of the engine speed is not on it (#1184).
 *
 * The AiM's are the signs and separators the house writes in its own glyphs: U+2212 and `+` for a
 * signed figure, which DSEG7 lacks and DSEG14 has only as a hyphen, the middle dot between a sector's
 * name and its delta, the hash of a car number, Δ, and the ∞ of an untimed session. The stint lap's
 * slash and the class's middle dot are #1044.
 */
const KNOWN: Readonly<Record<string, readonly string[]>> = {
  aim: [
    'delta.delta.value: "−" in DSEG7Bold',
    'delta.scale.0: "−" in DSEG14Regular',
    'fuel.toEnd.value: "+" in DSEG7Bold',
    'fuel.toEnd.value: "+" in DSEG7Regular',
    'lapHistory.head.delta: "Δ" in DSEG14Regular',
    'lapHistory.row.delta: "+" in DSEG7Regular',
    'lapHistory.row.delta: "−" in DSEG7Regular',
    'lapHistory.row.time: "−" in DSEG7Regular',
    'lapReview.driver: "#" in DSEG14Regular',
    'lapReview.lap.value: "−" in DSEG7Regular',
    'lapReview.vsBest.value: "+" in DSEG7Regular',
    'lapReview.vsBest.value: "−" in DSEG7Regular',
    'lapReview.vsPrevious.value: "−" in DSEG7Regular',
    'lapTimes.delta.value: "−" in DSEG7Bold',
    'leaderboard.table.row.gap: "+" in DSEG7Regular',
    'leaderboard.table.row.gap: "−" in DSEG7Regular',
    'leaderboard.table.row.last: "−" in DSEG7Regular',
    'opponents.ahead.gap.value: "−" in DSEG7Regular',
    'opponents.behind.gap.value: "+" in DSEG7Regular',
    'opponents.behind.gap.value: "−" in DSEG7Regular',
    'popUp.lap.value: "−" in DSEG7Regular',
    'relative.behind.value: "+" in DSEG7Regular',
    'relative.table.row.gap: "−" in DSEG7Regular',
    'sectors.s1.caption: "·" in DSEG14Regular',
    'sectors.s2.caption: "·" in DSEG14Regular',
    'sectors.s3.caption: "·" in DSEG14Regular',
    'session.class.value: "·" in DSEG7Bold',
    'session.timeLeft.mark: "∞" in DSEG14Regular',
    'stint.lap.value: "/" in DSEG7Bold',
  ],
};

/** Every glyph some text of the face sets in a segment face that does not carry it, as `item: "glyph" in face`. */
function fallbackGlyphs(themeId: string, size: { width: number; height: number }): Set<string> {
  const face = buildThemeFace(themeId, size, OPTS);
  const missing = new Set<string>();
  for (const dashboard of [face.built.main, ...face.built.zones]) {
    for (const screen of dashboard.screens) {
      for (const item of walkItems(screen.items)) {
        if (item.kind !== 'text') continue;
        const set = faceOf(item);
        if (!SEGMENT_FACES.has(set)) continue;
        for (const glyph of drawable(item)) if (!carries(set, glyph)) missing.add(`${item.name}: ${JSON.stringify(glyph)} in ${set}`);
      }
    }
  }
  return missing;
}

for (const themeId of Object.keys(THEMES)) {
  if (drawsInThisProcess(themeId)) {
    const known = new Set(KNOWN[themeId] ?? []);
    const seen = new Set<string>();
    for (const size of THEMES[themeId]!.anatomy.sizes) {
      test(`the ${themeId} theme at ${size.width}x${size.height} draws no glyph its segment faces lack`, () => {
        const missing = fallbackGlyphs(themeId, size);
        for (const m of missing) seen.add(m);
        expect([...missing].filter((m) => !known.has(m))).toEqual([]);
      });
    }
    test(`every glyph the ${themeId} theme is known to draw in a fallback face is still drawn`, () => {
      expect([...known].filter((k) => !seen.has(k))).toEqual([]);
    });
  } else if (THEME_ID === DEFAULT_THEME_ID) {
    test(
      `the ${themeId} theme, in a process drawing its fonts`,
      () => {
        const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: themeId }, stdout: 'pipe', stderr: 'pipe' });
        const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
        expect({ exit: run.exitCode, output }).toEqual({ exit: 0, output: '' });
      },
      300_000,
    );
  }
}
