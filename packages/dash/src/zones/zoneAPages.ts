/**
 * Zone A's four pages: the one a driver reads by reflex.
 *
 * This is why zone A is a narrow column rather than a third of the screen. The gear wants height,
 * not width — a gear cut from a 314 px column is 258 px and needs about 135 px of cell — so a
 * column 380 wide and 314 tall is exactly the shape it is for, and the width left over goes to the
 * two zones that show tables.
 *
 * Rule 18 is what sizes every page here: the content is cut from the height of the box, the way a
 * drawing is cut from its box, rather than drawn at a fixed size with room around it. Nothing on
 * these pages is on the density ramp and nothing is capped at `ds.size.gear`, which is the card and
 * round faces' size and was what left 241 px of the 1280x720 column empty. What bounds a run here
 * is the box: its line box down, its cells across.
 *
 * None of these draws a header. The other three zones carry the zone letter and the page name in a
 * 22 px line, and spending that here would cost the gear its size for the sake of saying "gear".
 * What zone A does when its page changes is the question XOR-103 owns.
 */
import type { Hex, Item, Monospace, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { gear as gearComponent, gearGhosts, ghostedGearWidth, GEAR_BOX_SLACK, GEAR_CHARS, type GearGhosts } from '../components/gear.ts';
import { measureText } from '../design/advances.ts';
import { boxSlack, canvasBaseline, canvasYForBaseline, cells, gearCells, monoWidth, type Chars, type DataWeight } from '../design/metrics.ts';
import { GEAR_CELL, LINE_SPACING } from '../design/metrics.ts';
import { inset, rect } from '../design/geometry.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { densityOf } from '../second/density.ts';
import { CHARS, speed, speedUnit } from '../second/values.ts';
import { pageBuilder } from '../modules/index.ts';
import { shapeOf } from '../second/shape.ts';
import { ds } from '../tokens.ts';

const { game, fmt, isnull, num } = ncalc;

/**
 * The weight the speed page draws its one big value in, and the weight it is measured in.
 *
 * Every sheet says the same thing: a numeral is Barlow Condensed 600, and the current gear alone is
 * 700. The speed was Bold as well, and zone A's catalogue is on every face, so the second Bold was
 * on every screen the build emits rather than on one artboard. The weight is also what the size is
 * solved in, Bold cells being wider than SemiBold ones, so the page settles on a larger speed now.
 */
const SPEED_WEIGHT = 'SemiBold' as const;

/** The smallest a run may be shrunk to when the column is too narrow for its share. */
const MIN_SIZE = 24;

/**
 * How zone A's pages share their column, as fractions of its height.
 *
 * Every number here is read off the face variants sheets, which draw each page at the real zone
 * rectangle: A1's gear at 180 px in a 328 px column and at 107 in a 194 px one is 55 per cent of
 * both, and its speed and revs are 19 and 11. The gaps are shares too, 4 px at 194, 7 at 328 and
 * 12 at 554 between A1's rows and half again as much between A3's two, rather than the density
 * ramp's fixed 12 and 4 — a gap that does not scale with the column is empty height on the tall
 * faces and a collision on the short ones.
 */
const SHARE = {
  a1: { gear: 0.55, speed: 0.19, revs: 0.11, gap: 0.021 },
  a3: { speed: 0.44, gear: 0.24, gap: 0.03 },
} as const;

/**
 * Zone A's runs are set at `line-height: 0.9` on the canvas, so a row of one of its stacks is nine
 * tenths of the font size rather than the em `textBox` converts from.
 *
 * The rows are therefore tighter than the WPF line boxes they hold, which overlap in their leading.
 * That is deliberate and it is what makes the canvas's shares drawable: at 55, 19 and 11 per cent
 * the three em boxes sum past the column and the gear's would start above it. The boxes are
 * transparent and a digit's ink is the 0.7 em cap height, so no row reaches the ink of the next.
 */
const LINE_HEIGHT = 0.9;

/** The height a run of `fs` takes in a stack. */
const rowHeight = (fs: number): number => Math.round(LINE_HEIGHT * fs);

/** The canvas line box of a run of `fs`, centred in its stack row: the half-leading, which is negative. */
const rowLineBox = (top: number, fs: number): number => top + (rowHeight(fs) - fs) / 2;

/** The gap between a stack's rows, never less than two pixels however short the column. */
const rowGap = (height: number, share: number): number => Math.max(2, Math.round(height * share));

/** How zone A draws the ghosted neighbours: the sheets' share of the gear, off its cell edge. */
const GHOSTS: GearGhosts = { gap: ds.space[3] };

/**
 * Gap between a zone A value and the small label after it, which is not `second/field.ts`'s.
 *
 * `UNIT_GAP` is six, and six is what the catalogue draws for a module's field: the fuel, the tyres
 * and the speedo all set their unit six pixels off the number. Zone A is drawn wider. Every one of
 * the seven per-size Dash artboards sets this row at eight, at every size it draws them — 58, 47,
 * 36 and 23 px values alike — and the catalogue draws the A3 gear's label at eight as well, so it
 * is a literal of the zone rather than a share of the value. A follower two pixels closer than the
 * drawing is not a clip, but it is the pair reading as one word.
 */
const UNIT_GAP = 8;

/**
 * Gap between the speed and the rpm value beside it on A3. Ten pixels on the catalogue, which is
 * off the `space` scale (it goes 8 then 12), so the literal stays here with the canvas as its
 * citation. Wider than `UNIT_GAP`, because these are two values rather than a value and its unit.
 */
const RPM_GAP = 10;

/**
 * The largest gear a box can hold: its line box down, its letter-wide cell across.
 *
 * Rule 18 rather than a ceiling. `ds.size.gear` stays the card and round faces' size in
 * `components/gear.ts`; here it was what switched the rule off in any box taller than 333 px, which
 * is 241 px of a 554 px column left empty. What bounds the gear instead is WPF's 1.2 em line box,
 * which a box has to hold whole: that is about 83 per cent of the column against the 86 the canvas
 * draws, and the difference is the leading under the baseline that a digit does not use but an "N"
 * and an "R" do.
 */
export const gearSizeIn = (frame: Rect): number => {
  const byHeight = Math.floor((frame.height - GEAR_BOX_SLACK) / LINE_SPACING);
  const byWidth = Math.floor((frame.width - GEAR_BOX_SLACK) / GEAR_CELL);
  return Math.max(72, Math.min(byHeight, byWidth));
};

/**
 * One run of a row: a value, or the small label that follows it.
 *
 * `gap` is what goes *before* the run, so a row states its own spacing rather than the caller
 * counting gaps between parts.
 */
type Run =
  | { kind: 'value'; name: string; sample: string; fs: number; chars: Chars; weight?: DataWeight; mono?: Monospace; color?: Hex; bind?: Expr; gap?: number }
  | { kind: 'label'; name: string; text: string; fs: number; widest: string; color?: Hex; bind?: Expr; gap?: number };

/**
 * What a run draws: its cells, or its advances. Not its box, which is a little wider.
 *
 * A follower is positioned from the cells, as `numeral` says it is: the slack a box takes beyond
 * its text is there so that WPF clips no glyph, and counting it as width would put it between the
 * value and its unit, where the canvas draws a gap of exactly eight pixels.
 */
function runWidth(run: Run): number {
  if (run.kind === 'label') return Math.ceil(measureText('BarlowMedium', run.widest, run.fs));
  return monoWidth(run.mono ?? cells(run.weight ?? 'SemiBold', run.fs), run.chars);
}

/** The slack a run's box takes beyond what it draws. */
const runSlack = (run: Run): number => (run.kind === 'label' ? 1 : boxSlack(run.fs));

/** What a whole row draws across, its gaps included; what the row is centred by. */
const rowWidth = (runs: readonly Run[]): number =>
  runs.reduce((sum, run, i) => sum + runWidth(run) + (i === 0 ? 0 : (run.gap ?? UNIT_GAP)), 0);

/**
 * What a row's boxes need of the column: what it draws, and the slack its last box takes past that.
 * Twice, because a centred row splits what is left over between its two ends.
 */
const rowExtent = (runs: readonly Run[]): number => rowWidth(runs) + 2 * runSlack(runs[runs.length - 1]!);

/**
 * A row of runs on one baseline, centred across `frame` in the stack row starting at `top`.
 *
 * The unit goes beside the value rather than under it, which is what the canvas draws on every zone
 * A page, so the pair is measured and centred as one group: centring the value alone would put the
 * group's centre half a unit's width left of the column's, which is what a 380 px column showed.
 */
function row(frame: Rect, top: number, runs: readonly Run[]): Item[] {
  const lead = runs[0];
  if (!lead) return [];
  const baseline = canvasBaseline(rowLineBox(top, lead.fs), lead.fs);
  let x = frame.left + (frame.width - rowWidth(runs)) / 2;
  return runs.map((run, i) => {
    if (i > 0) x += run.gap ?? UNIT_GAP;
    const drawn = runWidth(run);
    // The box takes its slack past the cells, and no further than the column: the last run of a
    // centred row has only half the row's leftover beside it.
    const box = Math.min(drawn + runSlack(run), frame.left + frame.width - x);
    const y = canvasYForBaseline(baseline, run.fs);
    const item =
      run.kind === 'label'
        ? unit(run.name, run.text, x, y, box, { size: run.fs, color: run.color, bind: run.bind, widest: run.widest })
        : numeral(run.name, run.sample, x, y, run.fs, run.chars, {
            bind: run.bind,
            color: run.color,
            weight: run.weight,
            mono: run.mono,
            maxWidth: box,
          });
    x += drawn;
    return item;
  });
}

/** Rows centred as one stack in `frame`, `gap` apart: equal slack above the first and below the last. */
function stack(frame: Rect, gap: number, rows: readonly { fs: number; draw: (top: number) => Item[] }[]): Item[] {
  const height = rows.reduce((sum, r) => sum + rowHeight(r.fs), 0) + gap * Math.max(0, rows.length - 1);
  let top = frame.top + (frame.height - height) / 2;
  const items: Item[] = [];
  for (const r of rows) {
    items.push(...r.draw(top));
    top += rowHeight(r.fs) + gap;
  }
  return items;
}

/**
 * The largest size at or under `wanted` whose row fits `width`.
 *
 * Solved by trying rather than by algebra: a cell is rounded up per character and a box takes a
 * slack beyond its cells, so a size derived from the em fraction alone lands a few pixels over. A
 * share of the column that the column is too narrow for is shrunk, never drawn outside.
 */
function fitWidth(wanted: number, width: number, extent: (fs: number) => number): number {
  let fs = wanted;
  while (fs > MIN_SIZE && extent(fs) > width) fs -= 1;
  return fs;
}

/** A run's share of the column, rounded rather than floored: the canvas draws 107 in 194, not 106. */
const shareOf = (height: number, share: number): number => Math.round(height * share);

/** The speed and its unit, as A1 and A3 both draw them. */
const speedRuns = (prefix: string, fs: number, unitFs: number): Run[] => [
  {
    kind: 'value',
    name: `${prefix}speed`,
    sample: '187',
    fs,
    chars: CHARS.speed,
    weight: SPEED_WEIGHT,
    // `SpeedLocal` and not `SpeedKmh`: the label under this number follows the driver's own unit,
    // so a value that is always kilometres per hour would read 180 under the word "mph" on an
    // imperial rig, where the companion's speedo and the pit wall's trace both showed 112.
    bind: fmt(speed(), '0'),
  },
  // The written unit rather than the enum. `SpeedLocalUnit` reads `KMH` or `MPH`, so binding it
  // raw drew `KMH` where every sheet writes `KM/H`; `speedUnit` is the one place that mapping lives.
  { kind: 'label', name: `${prefix}speed.unit`, text: 'KM/H', fs: unitFs, widest: 'KM/H', bind: speedUnit() },
];

/** The revs and their unit, in the secondary ink the canvas draws them in. */
const revsRuns = (prefix: string, fs: number, unitFs: number, gap?: number): Run[] => [
  {
    kind: 'value',
    name: `${prefix}revs`,
    sample: '7,420',
    fs,
    chars: CHARS.rpm,
    color: ds.color.text.secondary,
    bind: fmt(isnull(game('Rpms'), num(0)), '#,##0'),
    gap,
  },
  { kind: 'label', name: `${prefix}revs.unit`, text: 'RPM', fs: unitFs, widest: 'RPM' },
];

/** A1: the gear with its neighbours ghosted either side, the speed under it, the revs under that. */
function gearSpeedRevs(frame: Rect, prefix: string): Item[] {
  const d = densityOf('zone');
  const share = SHARE.a1;
  const gearSize = fitWidth(shareOf(frame.height, share.gear), frame.width, (fs) => ghostedGearWidth(fs, GHOSTS));
  const speedSize = fitWidth(shareOf(frame.height, share.speed), frame.width, (fs) => rowExtent(speedRuns(prefix, fs, d.labelSm)));
  const revsSize = fitWidth(shareOf(frame.height, share.revs), frame.width, (fs) => rowExtent(revsRuns(prefix, fs, d.labelSm)));
  return stack(frame, rowGap(frame.height, share.gap), [
    { fs: gearSize, draw: (top) => gearRow(frame, top, gearSize, prefix) },
    { fs: speedSize, draw: (top) => row(frame, top, speedRuns(prefix, speedSize, d.labelSm)) },
    { fs: revsSize, draw: (top) => row(frame, top, revsRuns(prefix, revsSize, d.labelSm)) },
  ]);
}

/** The gear and its ghosts, in a box exactly the gear's own canvas line box. */
function gearRow(frame: Rect, top: number, size: number, prefix: string): Item[] {
  const box = rect(frame.left, rowLineBox(top, size), frame.width, size);
  return [...gearGhosts(box, size, GHOSTS, `${prefix}gear`), ...gearComponent(box, size, `${prefix}main`)];
}

/** A2: the gear alone, as large as the column allows. */
function gearAlone(frame: Rect, prefix: string): Item[] {
  return gearComponent(frame, gearSizeIn(frame), `${prefix}main`);
}

/**
 * A3: the speed as the largest value, with the gear readable under it in the secondary ink.
 *
 * Both are cut from the column, 44 and 24 per cent of it, rather than taken from the density ramp
 * that drew a 46 px gear under a 195 px speed. The catalogue draws the rpm beside the speed as
 * well; a tall narrow column has no room for it once the speed has had its share, so it is drawn
 * where the group fits and dropped where it does not, rather than shrinking the page's own value.
 */
function speedPage(frame: Rect, prefix: string): Item[] {
  const d = densityOf('zone');
  const share = SHARE.a3;
  const gearSize = fitWidth(shareOf(frame.height, share.gear), frame.width, (fs) => rowExtent(gearRuns(prefix, fs, d.labelSm)));
  const lead = (speedFs: number, withRevs: boolean): Run[] => [
    ...speedRuns(prefix, speedFs, d.labelSm),
    ...(withRevs ? revsRuns(prefix, gearSize, d.labelSm, RPM_GAP) : []),
  ];
  const speedSize = fitWidth(shareOf(frame.height, share.speed), frame.width, (fs) => rowExtent(lead(fs, false)));
  const withRevs = rowExtent(lead(speedSize, true)) <= frame.width;
  return stack(frame, rowGap(frame.height, share.gap), [
    { fs: speedSize, draw: (top) => row(frame, top, lead(speedSize, withRevs)) },
    { fs: gearSize, draw: (top) => row(frame, top, gearRuns(prefix, gearSize, d.labelSm)) },
  ]);
}

/** The gear as A3 draws it: a quarter of the column, in the secondary ink, its label beside it. */
const gearRuns = (prefix: string, fs: number, labelFs: number): Run[] => [
  { kind: 'value', name: `${prefix}gear`, sample: '4', fs, chars: GEAR_CHARS, mono: gearCells(fs), color: ds.color.text.secondary, bind: game('Gear') },
  { kind: 'label', name: `${prefix}gear.label`, text: 'GEAR', fs: labelFs, widest: 'GEAR', color: ds.color.text.label },
];

/**
 * A4: the track map with every car on it.
 *
 * Titleless, and inset by the column's own padding. The module's title line is what the zone's page
 * setting already says, and drawing it here cost the map a line and anchored the page to the top of
 * a column every other page centres in.
 */
function trackPage(frame: Rect, prefix: string): Item[] {
  const d = densityOf('zone');
  const inner = inset(frame, d.padY, d.padX);
  return pageBuilder('track')({ frame: inner, density: 'zone', prefix, shape: shapeOf(inner), title: false });
}

const PAGES: Record<string, (frame: Rect, prefix: string) => Item[]> = {
  gearSpeedRevs,
  gearAlone,
  speed: speedPage,
  track: trackPage,
};

/** One of zone A's four pages, drawn in `frame`. */
export function zoneAPage(id: string, frame: Rect, prefix: string): Item[] {
  const draw = PAGES[id];
  if (!draw) throw new RangeError(`zone A has no page "${id}"`);
  return draw(frame, prefix);
}
