/**
 * Module 8, Pit view: what the next stop is set to do. The corner toggles, the fast repair and the
 * tear-off come from iRacing's pit service bit field, read arithmetically because NCalc has no
 * bitwise operators.
 *
 * Pit time counts up while the car is in the lane and shows the last stop's duration otherwise, so
 * the field is useful both during a stop and after it.
 *
 * The two numbers are cut from the box rather than read off the ramp, which is the one page on the
 * catalogue artboard that is drawn that way: refuel at 116, 96, 64, 61 and 58 px and pit time at
 * 81, 67, 44, 42 and 40, none of them a ramp size and every pair within a pixel of 1.45 to 1. So
 * the quantity takes what the options row and the progress bar leave it, and the time follows it
 * down at that ratio.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { WHEEL_CHANGE_TICK, assetBox, imageOf } from '../design/assets.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { measureText } from '../design/advances.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { densityOf } from '../second/density.ts';
import { levelGauge } from '../second/gauge.ts';
import { fieldBlockHeight, fieldWidth, planLines, type FieldSpec } from '../second/field.ts';
import { ROW_TAIL, stack, type StackRow } from '../second/layout.ts';
import { columnsAt, promotesLead } from '../second/shape.ts';
import {
  CHARS,
  NO_VALUE,
  PIT_SERVICE_BITS,
  TYRE_SELECTIONS,
  fuelUnit,
  inPitSeconds,
  isInPitLane,
  lastPitDuration,
  pitRefuelLitres,
  pitServiceFlag,
  pitServiceProgress,
  pitTyreSelection,
} from '../second/values.ts';
import { ds } from '../tokens.ts';
import { blockRow, defineModule, fieldsRow, fld, pageKeeps, shapeIn } from './module.ts';

const { fmt, iff, isNull, str, not } = ncalc;

/**
 * The catalogue's ratio between the refuel quantity and the pit time. Every drawing holds it: 116
 * over 81, 96 over 67, 64 over 44, 61 over 42 and 58 over 40.
 */
const TIME_RATIO = 1.45;

/** How far past the ramp's own top a tall box takes the quantity: the catalogue's 96 over a 64 hero. */
const TALL_PROMOTION = 1.5;

/** The catalogue's grid of two: 10 px between the lines and 16 between the columns. */
const GRID_LINE_GAP = 10;
const GRID_GAP = ds.space[4];

/** The options are 16 apart, and each label is 6 from the state after it. Both are the canvas's. */
const OPTION_GAP = ds.space[4];
const STATE_GAP = 6;

/** The off state: a 10 by 2 dash, which is what the canvas draws for an option not selected. */
const DASH_WIDTH = 10;
const DASH_HEIGHT = 2;

/** The on state: the 14 px badge the wheel-change tick is cut to fill. */
const BADGE = 14;

/**
 * The progress bar's track. Six pixels at every shape the catalogue draws and on the companion
 * too, where `d.bar` is four in a zone, so the page carries its own rather than the density's.
 */
const BAR_HEIGHT = 6;

/** Padding round a label's measured advances, so that WPF has a pixel in hand at either end. */
const LABEL_SLACK = 2;

interface Toggle {
  id: string;
  text: string;
  bit: number;
}

/** The two the catalogue's option row names, in the order it lists them. */
export const SERVICE_TOGGLES: readonly Toggle[] = [
  { id: 'fastRepair', text: 'Fast repair', bit: PIT_SERVICE_BITS.fastRepair },
  { id: 'tearOff', text: 'Tear-off', bit: PIT_SERVICE_BITS.tearOff },
];

/**
 * The four the catalogue draws as badges on the car and writes in the row as one summary word.
 *
 * They stay beside that summary wherever the shape keeps the `tyres` part, because `RIGHTS` says
 * which pair and these say which corner, and a crew that has set three of them is a case the six
 * words of the summary do not name.
 */
export const CORNER_TOGGLES: readonly Toggle[] = [
  { id: 'FrontLeft', text: 'FL', bit: PIT_SERVICE_BITS.FrontLeft },
  { id: 'FrontRight', text: 'FR', bit: PIT_SERVICE_BITS.FrontRight },
  { id: 'RearLeft', text: 'RL', bit: PIT_SERVICE_BITS.RearLeft },
  { id: 'RearRight', text: 'RR', bit: PIT_SERVICE_BITS.RearRight },
];

/** One entry of the option row: a label, then the state it is in. */
interface Option {
  id: string;
  text: string;
  /** Width the state after the label takes. */
  stateWidth: number;
  /** The state, drawn from `x` on the line whose middle is `middle`. */
  state(name: string, x: number, middle: number): Item[];
}

/**
 * The tick that marks an option selected.
 *
 * The canvas strokes a bare 14 px check and the format draws no strokes at all, so what is drawn
 * is the badge the same tick sits on over on the car: a square in `text.primary` with the tick's
 * own `surface.base` ink baked into the picture. `assets.ts` records why an image cannot be tinted
 * and why the file covers the whole square.
 */
const tick = (name: string, box: Rect, on: Expr): Item[] => [
  band(`${name}.badge`, box, ds.color.text.primary, { visibleBind: on }),
  {
    kind: 'image',
    name: `${name}.tick`,
    image: WHEEL_CHANGE_TICK.name,
    rect: roundRect(assetBox(box, imageOf(WHEEL_CHANGE_TICK))),
    ...withBindings({ Visible: on }),
  },
];

/** An option whose state is a dash when off and the tick when on: the fast repair and the tear-off. */
const serviceOption = (toggle: Toggle): Option => {
  const on = pitServiceFlag(toggle.bit);
  return {
    id: toggle.id,
    text: toggle.text,
    stateWidth: BADGE,
    state: (name, x, middle) => [
      band(`${name}.off`, rect(x, middle - DASH_HEIGHT / 2, DASH_WIDTH, DASH_HEIGHT), ds.color.text.dim, { visibleBind: not(on) }),
      ...tick(name, rect(x, middle - BADGE / 2, BADGE, BADGE), on),
    ],
  };
};

/**
 * A corner, whose state is the block the catalogue paints on the car: `text.label` while that
 * wheel stays on and `text.primary` under the tick once it is down for a change.
 */
const cornerOption = (toggle: Toggle): Option => {
  const on = pitServiceFlag(toggle.bit);
  return {
    id: toggle.id,
    text: toggle.text,
    stateWidth: BADGE,
    state: (name, x, middle) => {
      const box = rect(x, middle - BADGE / 2, BADGE, BADGE);
      return [band(`${name}.off`, box, ds.color.text.label, { visibleBind: not(on) }), ...tick(name, box, on)];
    },
  };
};

/** The summary the catalogue writes beside `Tyres`, measured by the widest word it can produce. */
const tyresOption = (fs: number): Option => {
  const widest = TYRE_SELECTIONS.reduce((worst, word) => (measureText('BarlowMedium', word, fs) > measureText('BarlowMedium', worst, fs) ? word : worst));
  return {
    id: 'tyres',
    text: 'Tyres',
    stateWidth: Math.ceil(measureText('BarlowMedium', widest, fs)) + LABEL_SLACK,
    state: (name, x, middle) => [
      label(`${name}.value`, 'RIGHTS', x, middle - fs / 2, Math.ceil(measureText('BarlowMedium', widest, fs)) + LABEL_SLACK, {
        size: fs,
        color: ds.color.text.primary,
        bind: pitTyreSelection(),
        widest,
      }),
    ],
  };
};

export const pitView = defineModule('pitView', (ctx) => {
  const d = densityOf(ctx.density);
  const shape = shapeIn(ctx);
  const refuel = pitRefuelLitres();

  // Which corners are being changed is the page's own summary, and it is what a box one column
  // wide drops: the fast repair and the tear-off stay at every shape, the tyres do not.
  const options: Option[] = [
    ...SERVICE_TOGGLES.map(serviceOption),
    ...(pageKeeps('tyres', ctx) ? [tyresOption(d.labelSm), ...CORNER_TOGGLES.map(cornerOption)] : []),
  ];
  /** Each option with the width its label really takes, so the wrap below can be measured. */
  const measured = options.map((option) => ({
    ...option,
    textWidth: Math.ceil(measureText('BarlowMedium', option.text.toUpperCase(), d.labelSm)) + LABEL_SLACK,
  }));
  type Measured = (typeof measured)[number];
  const widthOf = (option: Measured): number => option.textWidth + STATE_GAP + option.stateWidth;
  const lineHeight = Math.max(BADGE, d.labelSm);

  /** The options wrapped to the frame, in the order the catalogue lists them. */
  const wrapped = (at: readonly Measured[]): Measured[][] => {
    const rows: Measured[][] = [];
    let row: Measured[] = [];
    let used = 0;
    for (const option of at) {
      const w = widthOf(option);
      const needed = row.length === 0 ? w : used + OPTION_GAP + w;
      if (row.length > 0 && needed > ctx.frame.width) {
        rows.push(row);
        row = [option];
        used = w;
      } else {
        row.push(option);
        used = needed;
      }
    }
    if (row.length > 0) rows.push(row);
    return rows;
  };

  // The options wrap rather than running off the right edge. They used to be laid out on one line
  // whatever the width, which is fine at 600 and puts TEAR-OFF 8 px past the edge at 360 -- where
  // WPF clips it to "TEAR-OF" and the row reads as a rendering fault. What the row declares is what
  // a box too short sheds from it, corner by corner and least important last in the order, so that
  // the bar under it survives a zone the wrap alone would have cost it.
  const optionsRow = (at: readonly Measured[]): StackRow | undefined => {
    if (at.length === 0) return undefined;
    const rows = wrapped(at);
    const height = rows.length * lineHeight + Math.max(0, rows.length - 1) * OPTION_GAP;
    return {
      height,
      draw: (bottom) => {
        const top = bottom - height;
        const items: Item[] = [];
        rows.forEach((row, rowIndex) => {
          const rowTop = top + rowIndex * (lineHeight + OPTION_GAP);
          let x = ctx.frame.left;
          for (const option of row) {
            const name = `${ctx.prefix}${option.id}`;
            items.push(
              label(`${name}.label`, option.text, x, rowTop + (lineHeight - d.labelSm) / 2, option.textWidth, { size: d.labelSm }),
              ...option.state(name, x + option.textWidth + STATE_GAP, rowTop + lineHeight / 2),
            );
            x += widthOf(option) + OPTION_GAP;
          }
        });
        return items;
      },
      shed: {
        ids: at.map((option) => option.id),
        order: measured.map((option) => option.id),
        without: (ids) => optionsRow(at.filter((option) => !ids.includes(option.id))),
      },
    };
  };
  const optionsBlock = optionsRow(measured);
  const optionsHeight = optionsBlock?.height ?? 0;

  // The catalogue sets the two numbers side by side wherever the column count allows a pair and
  // stacks them in a box tall enough to make them large, which is its `tall` drawing.
  const columns = promotesLead(shape) ? 1 : Math.min(2, columnsAt(shape));
  const rowOptions = { lines: 'grid' as const, columns, gap: GRID_GAP, lineGap: GRID_LINE_GAP };
  const specsAt = (fs: number): FieldSpec[] => [
    fld(ctx, 'refuel', 'Refuel', {
      sample: '12.6',
      bind: iff(isNull(refuel), str(NO_VALUE), fmt(refuel, '0.0')),
      chars: CHARS.fuel,
      fs,
      color: ds.purpose.fuel.low,
      follower: { text: 'L', bind: fuelUnit(), widest: 'gal' },
    }),
    fld(ctx, 'pitTime', 'Pit time', {
      sample: '24.3',
      bind: fmt(iff(isInPitLane(), inPitSeconds(), lastPitDuration()), '0.0'),
      chars: CHARS.consumption,
      fs: Math.round(fs / TIME_RATIO),
      follower: { text: 's' },
    }),
  ];

  // What the options and the bar leave the numbers, which is the height they are cut from. The two
  // tails are the stack's own reservation at either end of a block it centres.
  const room = ctx.frame.height - 2 * ROW_TAIL - optionsHeight - BAR_HEIGHT - 2 * d.gapY;
  const ceiling = Math.round(d.hero * (promotesLead(shape) ? TALL_PROMOTION : 1));
  const linesOf = (at: readonly FieldSpec[]): FieldSpec[][] => planLines(at, ctx.frame.width, ctx.density, { plan: 'grid', columns, gap: GRID_GAP });
  let lead = d.tiny;
  for (let fs = ceiling; fs > d.tiny; fs--) {
    const at = specsAt(fs);
    const lines = linesOf(at);
    // A grid whose cell cannot hold a field falls back to the greedy wrap, which draws a value off
    // the right edge rather than smaller; the size that fits the cell is the size the page takes.
    if (lines.some((line) => line.some((spec) => fieldWidth(spec, ctx.density) > ctx.frame.width))) continue;
    if (fieldBlockHeight(lines, ctx.density, GRID_LINE_GAP) > room) continue;
    lead = fs;
    break;
  }

  const stackRows: StackRow[] = [fieldsRow(specsAt(lead), ctx, rowOptions)];
  if (optionsBlock) stackRows.push(optionsBlock);
  return stack(
    ctx.frame,
    [
      ...stackRows,
      blockRow(BAR_HEIGHT, (bottom) => [
        levelGauge(`${ctx.prefix}gauge`, rect(ctx.frame.left, bottom - BAR_HEIGHT, ctx.frame.width, BAR_HEIGHT), pitServiceProgress(), { value: 62 }),
      ]),
    ],
    ctx.density,
  );
});
