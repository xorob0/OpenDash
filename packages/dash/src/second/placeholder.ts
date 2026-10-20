/**
 * What a module draws when the sim cannot answer it. Three modules ship this way on iRacing:
 * virtual energy is a Le Mans Ultimate feature, iRacing publishes no damage values at all where
 * Assetto Corsa and ACC do, and per-segment rival timing is not a SimHub property.
 *
 * They say so rather than showing zeros. A dashboard that invents a number is worse than one that
 * admits it has none, and the modules stay in the catalogue so another sim can switch them on.
 */
import { ncalc, type Item, type Rect } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { LINE_SPACING } from '../design/metrics.ts';
import { label } from '../elements/label.ts';
import { ds } from '../tokens.ts';
import { densityOf, type Density } from './density.ts';

/** The words of `text` gathered into lines no wider than `width`; a word wider than that keeps a line. */
function greedyLines(text: string, width: number, size: number): string[] {
  const lines: string[] = [];
  for (const word of text.split(' ')) {
    const last = lines[lines.length - 1];
    const joined = last === undefined ? word : `${last} ${word}`;
    if (last === undefined || measureText('BarlowMedium', joined, size) > width) lines.push(word);
    else lines[lines.length - 1] = joined;
  }
  return lines;
}

/**
 * The prose set to `width`: one line where it fits, else a line per half of "<module> · <why>",
 * else those halves broken at their spaces.
 *
 * The separator goes with the break it caused. It is there to join the two halves on one line, and
 * a middot left hanging at the end of a line, or worse opening the next one, reads as a fault in
 * the renderer rather than as punctuation.
 */
function wrapText(text: string, width: number, size: number): string[] {
  if (measureText('BarlowMedium', text, size) <= width) return [text];
  return text.split(' · ').flatMap((half) => greedyLines(half, width, size));
}

/**
 * What the box gets to say: the whole message where its wrapping fits, else the reason alone, and
 * at worst the lines the height holds.
 *
 * The module's name is drawn in the header above the box, on the companion page and on a pit wall
 * zone alike, so of the two halves of "<module> · <why>" it is the one the prose gives up.
 */
function linesThatFit(text: string, frame: Rect, size: number, step: number): string[] {
  const rows = Math.max(1, Math.floor((frame.height - size) / step) + 1);
  const [, ...reason] = text.split(' · ');
  const forms = reason.length > 0 ? [text, reason.join(' · ')] : [text];
  const wrapped = forms.map((form) => wrapText(form, frame.width, size));
  return (wrapped.find((lines) => lines.length <= rows) ?? wrapped[wrapped.length - 1]!).slice(0, rows);
}

/**
 * How tall {@link placeholder} draws `text` in a box `width` wide and at most `height` tall: the
 * lines it keeps, from the top of the first to the foot of the last. A page that says part of
 * itself is missing sets the notice as a row among its readings and needs this for the row.
 */
export function placeholderHeight(text: string, width: number, height: number, density: Density): number {
  const size = densityOf(density).label;
  const step = Math.ceil(LINE_SPACING * size);
  const lines = linesThatFit(text, { left: 0, top: 0, width, height }, size, step);
  return (lines.length - 1) * step + size;
}

/**
 * A centred block in text.dim, the module's name first and then why there is nothing to show.
 *
 * The prose is cut from the box rather than sized to it (rule 18): it is set at the density's label
 * size, wraps where the box is narrow and sheds where it is short. It used to shrink instead, a
 * pixel at a time down to 8, which put the sentence under the readable floor the density ramp
 * exists to hold -- and a sentence explaining that a reading is missing, itself too small to read,
 * is the worst of both.
 *
 * `prefix` is the caller's item-name prefix, dot included, the same one `fld` takes: every caller
 * passes `ctx.prefix`, so it used to have a dot appended to it here and the items came out named
 * `damage..placeholder`. Harmless and wrong, and worth spelling right now that twelve modules draw
 * one of these rather than three disabled ones (#406).
 */
export function placeholder(prefix: string, text: string, frame: Rect, density: Density): Item[] {
  const size = densityOf(density).label;
  const step = Math.ceil(LINE_SPACING * size);
  const lines = linesThatFit(text, frame, size, step);
  const top = frame.top + (frame.height - ((lines.length - 1) * step + size)) / 2;
  return lines.map((line, i) =>
    label(i === 0 ? `${prefix}placeholder` : `${prefix}placeholder${i + 1}`, line, frame.left, top + i * step, frame.width, {
      size,
      color: ds.color.text.dim,
      hAlign: 'center',
    }),
  );
}

/**
 * A module's drawing while `reported` is true and {@link placeholder}'s notice while it is false: the
 * not-available state of a module whose reading the sim may or may not give, decided frame by frame.
 * The drawing goes into one layer named `<prefix>reported`, so that one binding hides all of it.
 * `prefix` is the gate's own, dot included (`leaderboard.field.`), so that a module already behind
 * the session gate of `modules/module.ts` names its notice apart from that one.
 */
export function unlessReported(prefix: string, reported: Expr, drawing: Item[], text: string, frame: Rect, density: Density): Item[] {
  return [
    withMoreBindings({ kind: 'layer', name: `${prefix}reported`, children: drawing }, { Visible: reported }),
    ...placeholder(prefix, text, frame, density).map((item) => withMoreBindings(item, { Visible: ncalc.not(reported) })),
  ];
}
