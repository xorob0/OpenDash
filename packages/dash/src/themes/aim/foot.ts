/**
 * Band D on the LCD (#204): the status line at the left and then the page's cells, each a value in
 * the 7-segment face over its caption in the 14-segment one, right-aligned, as the canvas's foot draws
 * `GPS: GOOD` and then `12 LAP`, `31.2 FUEL  L`, `18 LAPS LEFT`.
 *
 * Every field the house page draws at this width is drawn, under the same id, which the conformance
 * harness checks against the house page built alone. Where the band is too narrow for all of them at
 * the canvas's sizes the status line gives way first, the value then comes down in steps, and a band
 * that still cannot hold the page draws the house's own, in the ink on the ground; nothing is ever
 * left out to make a line fit.
 *
 * **The status line** is a cue both units write as it is, `GPS: GOOD`, bound to the telemetry link: it
 * reads `GPS: LOST` while the game is not running, which on a face is the second before the idle
 * screen takes it.
 *
 * The car page, the house's twelve lamps, is pictograms and not readings, and keeps the house's.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { gameRunning } from '../../second/values.ts';
import { BAND_PAGES, bandPageItems, relativeFields, type BandField } from '../../zones/bandPages.ts';
import { TELLTALE_PAGE } from '../../zones/telltales.ts';
import { ghostOf, reading, segment, segmentWidth } from './register.ts';

const { concat, iff, str, ucase } = ncalc;

/** The canvas's foot at 60 px: the status 20 px in 200, cells 32 over 12 with 4 between, 56 apart. */
const FOOT = { height: 60, status: { size: 20, width: 200 }, value: 32, caption: 12, between: 4, gap: 56, rowGap: 10 } as const;

/** The steps a band too narrow for the canvas's sizes takes, in order. */
const VALUE_STEPS: readonly number[] = [32, 28, 24, 20];
const GAP_STEPS: readonly number[] = [56, 32, 20];

const STATUS = { good: 'GPS: GOOD', lost: 'GPS: LOST' } as const;

/** What a field's caption says: its label in capitals and its unit after it, as `FUEL  L`. */
function captionOf(f: BandField): { text: string; widest: string; bind?: Expr } {
  const label = f.label.toUpperCase();
  const labelWidest = (f.labelWidest ?? f.label).toUpperCase();
  const after = f.after === undefined ? '' : `  ${f.after}`;
  const afterWidest = f.after === undefined ? '' : `  ${f.afterWidest ?? f.after}`;
  const text = `${label}${after}`;
  const widest = `${labelWidest}${afterWidest.length > after.length ? afterWidest : after}`;
  if (f.labelBind === undefined && f.afterBind === undefined) return { text, widest };
  const parts: Expr[] = [f.labelBind === undefined ? str(label) : ucase(f.labelBind)];
  if (f.after !== undefined) parts.push(str('  '), f.afterBind ?? str(f.after));
  return { text, widest, bind: concat(...parts) };
}

/** The widest a reading of the field can be, in as many cells as its budget declares. */
function widestValue(f: BandField): string {
  const base = f.numeralWidest ?? f.sample;
  const cellsIn = [...base].filter((ch) => ch !== '.' && ch !== ':').length;
  return '8'.repeat(Math.max(0, f.chars.digits - cellsIn)) + base;
}

const readingsOf = (f: BandField): { sample: string; bind: string }[] => [{ sample: f.sample, bind: f.bind }, ...(f.row ?? [])];

const captionSize = (value: number): number => Math.max(9, Math.round((value * FOOT.caption) / FOOT.value));

/** The width of one of a field's readings, a word in the 14-segment face and a figure in the 7. */
const readingWidth = (f: BandField, value: number): number =>
  f.widest !== undefined ? segmentWidth('DSEG14Regular', f.widest.toUpperCase(), value) : segmentWidth('DSEG7Regular', ghostOf(widestValue(f)), value);

function cellWidth(f: BandField, value: number): number {
  const readings = readingsOf(f).length;
  const values = readings * readingWidth(f, value) + (readings - 1) * FOOT.rowGap;
  return Math.max(values, segmentWidth('DSEG14Regular', captionOf(f).widest, captionSize(value)));
}

/** One field: its readings right-aligned over its caption, the pair centred on the band's height. */
function cell(f: BandField, prefix: string, right: number, frame: Rect, value: number): Item {
  const name = `${prefix}${f.id}`;
  const caption = captionSize(value);
  const top = Math.round(frame.top + (frame.height - value - FOOT.between - caption) / 2);
  const one = readingWidth(f, value);
  const readings = readingsOf(f);
  const children: Item[] = readings.flatMap((r, i) => {
    const id = i === 0 ? `${name}` : `${name}.row${i}`;
    const at = right - (readings.length - 1 - i) * (one + FOOT.rowGap);
    if (f.widest !== undefined) {
      const word = f.widest.toUpperCase();
      return [segment(`${id}.value`, 'DSEG14Regular', r.sample.toUpperCase(), at - one, top, one, { size: value, hAlign: 'right', bind: ucase(r.bind), widest: word })];
    }
    return reading(id, r.sample, at, top, value, { bind: r.bind, widest: widestValue(f), face: 'DSEG7Regular' });
  });
  const c = captionOf(f);
  const captionWidth = segmentWidth('DSEG14Regular', c.widest, caption);
  children.push(segment(`${name}.caption`, 'DSEG14Regular', c.text, right - captionWidth, top + value + FOOT.between, captionWidth, { size: caption, hAlign: 'right', bind: c.bind, widest: c.bind === undefined ? undefined : c.widest }));
  return withMoreBindings({ kind: 'layer', name, children }, { Visible: f.present });
}

function status(prefix: string, frame: Rect, value: number): Item {
  const size = Math.round((value * FOOT.status.size) / FOOT.value);
  const width = segmentWidth('DSEG14Regular', STATUS.good, size);
  return segment(`${prefix}status`, 'DSEG14Regular', STATUS.good, frame.left, Math.round(frame.top + (frame.height - size) / 2), width, {
    size,
    bind: iff(gameRunning(), str(STATUS.good), str(STATUS.lost)),
    widest: STATUS.good,
  });
}

/** The largest value the band's height holds beside its caption, and never over the canvas's 32. */
const valueMax = (frame: Rect): number => Math.min(FOOT.value, Math.floor(((frame.height - 8 - FOOT.between) * FOOT.value) / (FOOT.value + FOOT.caption)));

/** The first arrangement in which every field fits the band, or undefined where none does. */
function arrangementFor(fields: readonly BandField[], frame: Rect): { withStatus: boolean; value: number; gap: number } | undefined {
  const most = valueMax(frame);
  for (const withStatus of [true, false]) {
    for (const value of VALUE_STEPS.filter((v) => v <= most).concat(VALUE_STEPS.every((v) => v > most) ? [most] : [])) {
      for (const gap of GAP_STEPS) {
        const statusRoom = withStatus ? Math.max(Math.round((FOOT.status.width * value) / FOOT.value), segmentWidth('DSEG14Regular', STATUS.good, Math.round((value * FOOT.status.size) / FOOT.value))) + gap : 0;
        const cells = fields.reduce((sum, f) => sum + cellWidth(f, value), 0) + (fields.length - 1) * gap;
        if (statusRoom + cells <= frame.width) return { withStatus, value, gap };
      }
    }
  }
  return undefined;
}

/** One of band D's pages on the LCD; see the file comment. */
export function aimBandPage(page: string, frame: Rect, prefix: string, classOnly?: Expr): Item[] {
  if (page === TELLTALE_PAGE) return bandPageItems(page, frame, prefix, false, classOnly);
  const fields = page === 'relative' && classOnly !== undefined ? relativeFields(classOnly) : BAND_PAGES[page];
  if (!fields) throw new RangeError(`band D has no page "${page}"`);
  const plan = arrangementFor(fields, frame);
  if (!plan) return bandPageItems(page, frame, prefix, false, classOnly);
  const items: Item[] = plan.withStatus ? [status(prefix, frame, plan.value)] : [];
  // The cells from the band's right edge leftwards, so that the last ends against it as the canvas's does.
  let right = frame.left + frame.width;
  const placed: Item[] = [];
  for (const f of [...fields].reverse()) {
    placed.unshift(cell(f, prefix, right, frame, plan.value));
    right -= cellWidth(f, plan.value) + plan.gap;
  }
  return [...items, ...placed];
}
