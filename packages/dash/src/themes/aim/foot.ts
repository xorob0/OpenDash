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
 * The car page, the house's twelve lamps, is pictograms the LCD cannot draw, so each lamp is its word
 * in the 14-segment face instead: a ghost while it is dark, and inverse video, an ink block with the
 * word in the ground, while it is lit. A lamp nothing the sim publishes lights stays a ghost.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { gameRunning } from '../../second/values.ts';
import { walkItems } from '../../walk.ts';
import { BAND_PAGES, bandFlagBlocks, bandPageItems, relativeFields, type BandField } from '../../zones/bandPages.ts';
import { TELLTALE_PAGE, TELLTALES } from '../../zones/telltales.ts';
import { ghostOf, lcdColour, reading, segment, segmentWidth } from './register.ts';

const { concat, iff, str, ucase } = ncalc;


/** The canvas's foot at 60 px: the status 20 px in 200, cells 32 over 12 with 4 between, 56 apart. */
const FOOT = { height: 60, status: { size: 20, width: 200 }, value: 32, caption: 12, between: 4, gap: 56, rowGap: 10 } as const;

/** The steps a band too narrow for the canvas's sizes takes, in order. */
const VALUE_STEPS: readonly number[] = [32, 28, 24, 20];
const GAP_STEPS: readonly number[] = [56, 32, 20];

const STATUS = { good: 'GPS: GOOD', lost: 'GPS: LOST' } as const;

/** Between a flag's end block and the page. */
const FLAG_GAP = 6;

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

/** The word each of the house's twelve lamps is written as, in the house's order. */
const LAMP_WORDS: Readonly<Record<string, string>> = {
  tyreLines: 'TYRE',
  tyreSlant: 'GRIP',
  wiper: 'WIPER',
  surface: 'WET',
  abs: 'ABS',
  esp: 'ESP',
  engine: 'ENGINE',
  fuel: 'FUEL',
  battery: 'BATT',
  limiter: 'PIT',
  pressure: 'PRESS',
  door: 'DOOR',
};

/** The car page: the lamps as words across the band, as many as fit, the tail shed as the house's rank sheds it. */
function lampWords(frame: Rect, prefix: string): Item[] {
  // The largest size at which all twelve fit, never over the status line's nor under 10 px, and as many as fit at that.
  const geometry = (size: number) => {
    const pad = Math.round(size / 3);
    const cell = Math.max(...Object.values(LAMP_WORDS).map((word) => segmentWidth('DSEG14Regular', word, size))) + 2 * pad;
    const gap = Math.round(size / 2);
    return { size, pad, cell, gap, fits: Math.max(1, Math.min(TELLTALES.length, Math.floor((frame.width + gap) / (cell + gap)))) };
  };
  let g = geometry(Math.min(FOOT.status.size, Math.floor(frame.height / 2.4)));
  while (g.fits < TELLTALES.length && g.size > 10) g = geometry(g.size - 1);
  const { size, pad, cell, gap, fits } = g;
  const top = Math.round(frame.top + (frame.height - size) / 2);
  return TELLTALES.slice(0, fits).flatMap((lamp, i) => {
    const word = LAMP_WORDS[lamp.id] ?? lamp.id.toUpperCase();
    const left = frame.left + i * (cell + gap);
    const name = `${prefix}${lamp.id}`;
    const width = segmentWidth('DSEG14Regular', word, size);
    const at = left + Math.round((cell - width) / 2);
    const items: Item[] = [segment(`${name}.dark`, 'DSEG14Regular', word, at, top, width, { size, ghost: true, visibleBind: lamp.on === undefined ? undefined : ncalc.not(lamp.on) })];
    if (lamp.on !== undefined) {
      items.push(
        withMoreBindings<'rect'>({ kind: 'rect', name: `${name}.block`, rect: { left, top: top - pad, width: cell, height: size + 2 * pad }, backgroundColor: lcdColour('ink') }, { Visible: lamp.on }),
        segment(`${name}.lit`, 'DSEG14Regular', word, at, top, width, { size, color: lcdColour('ground'), visibleBind: lamp.on }),
      );
    }
    return items;
  });
}

/**
 * The room a page has: the band less the blocks at its two ends, which a flag settles into after it
 * has taken the whole band, so that a flag never covers the status line or the last cell.
 */
function pageRoom(frame: Rect): Rect {
  const { left, right } = bandFlagBlocks(frame, false);
  return { left: frame.left + left.width + FLAG_GAP, top: frame.top, width: frame.width - left.width - right.width - 2 * FLAG_GAP, height: frame.height };
}

/** One of band D's pages on the LCD; see the file comment. */
export function aimBandPage(page: string, band: Rect, prefix: string, classOnly?: Expr): Item[] {
  const frame = pageRoom(band);
  if (page === TELLTALE_PAGE) return lampWords(frame, prefix);
  const fields = page === 'relative' && classOnly !== undefined ? relativeFields(classOnly) : BAND_PAGES[page];
  if (!fields) throw new RangeError(`band D has no page "${page}"`);
  // The fields the house page draws at this width, which is the page's own shedding: on a narrow band
  // the house keeps the first few and drops the tail, and the LCD keeps the same ones.
  const house = bandPageItems(page, band, prefix, false, classOnly);
  const names = house.flatMap((item) => [...walkItems([item])]).map((item) => item.name);
  const kept = fields.filter((f) => names.some((name) => name === `${prefix}${f.id}` || name.startsWith(`${prefix}${f.id}.`)));
  const plan = arrangementFor(kept, frame);
  if (!plan) return house;
  const items: Item[] = plan.withStatus ? [status(prefix, frame, plan.value)] : [];
  // The cells from the band's right edge leftwards, so that the last ends against it as the canvas's does.
  let right = frame.left + frame.width;
  const placed: Item[] = [];
  for (const f of [...kept].reverse()) {
    placed.unshift(cell(f, prefix, right, frame, plan.value));
    right -= cellWidth(f, plan.value) + plan.gap;
  }
  return [...items, ...placed];
}
