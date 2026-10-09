/**
 * The LCD's two takeovers, both over zone C while the zones stay built beneath (#204): the pit
 * limiter, `Pit Speed` over the lane's limit, and the engine warning, a box ruled in the ink saying
 * what is wrong. The gear and the speed stay in zone A under either. The full-screen flag is the
 * house's, given the LCD below the tacho as its body by the anatomy.
 *
 * `Pit Speed` is in inverse video, an ink block with the word in the ground, being the pit family's
 * one-line banner drawn the LCD's way. The warning is a box over the content and is therefore an
 * outline with its two lines in the ink, as the canvas draws it and as the pop-ups are (#751).
 *
 * Each is one of the house's states drawn the LCD's way rather than a state of its own. The limiter
 * is the house pit family's `limiter` state, shown exactly when that state's banner is and drawn over
 * it, so that the four states of the family that are mistakes keep the house's banner at the top of
 * zone C. The warning is iRacing's `EngineWarnings` bits for water temperature, oil pressure and fuel
 * pressure, which band D's car page lights its engine lamp on; a stalled engine is left out, since
 * a car sitting in its pit box is stalled and is not in trouble. When both hold, the limiter is
 * drawn over the warning, being the one that ends in seconds.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { pitAlertVisible } from '../../components/pitAlerts.ts';
import { rect } from '../../design/geometry.ts';
import { inTheCar } from '../../second/values.ts';
import { ENGINE_WARNING_BITS } from '../../zones/telltales.ts';
import { zoneRect } from '../anatomy.ts';
import type { FaceContext } from '../drawing.ts';
import { lcdColour, reading, segment, segmentWidth } from './register.ts';

const { and, div, fmt, game, gt, iff, isnull, lt, mod, num, or, raw, str, truncate } = ncalc;

/** The canvas's takeovers at 440 x 284: `Pit Speed` 30 over the limit 64, 24 apart; the warning box 6 in, 3 px of ink, two lines of 26, 16 apart. */
const LIMITER = { label: 30, value: 64, gap: 24 } as const;
const WARNING = { inset: 6, border: 3, line: 26, gap: 16, pad: 16 } as const;

/** One of iRacing's `EngineWarnings` bits, read by dividing and taking the remainder, as the band's telltales read it. */
const engineWarning = (bit: number): Expr => gt(mod(truncate(div(isnull(raw('EngineWarnings'), num(0)), num(bit))), num(2)), num(0));

const WARNINGS = [
  { bit: ENGINE_WARNING_BITS.oilPressure, text: 'OIL PRESS LOW' },
  { bit: ENGINE_WARNING_BITS.waterTemperature, text: 'WATER TEMP HIGH' },
  { bit: ENGINE_WARNING_BITS.fuelPressure, text: 'FUEL PRESS LOW' },
] as const;

const WARNING_WIDEST = WARNINGS.map((w) => w.text).reduce((a, b) => (b.length > a.length ? b : a));

/** The lane's limit in the driver's unit; an unpublished one reads as the house's no-data dashes. */
const pitLimit = (): Expr => isnull(game('PitLimiterSpeed'), num(999));

/** A word in inverse video: the ink block, `pad` round it, and the word in the ground. */
function inverse(name: string, text: string, centreX: number, top: number, size: number): Item[] {
  const width = segmentWidth('DSEG14Regular', text, size);
  const pad = Math.round(size / 3);
  const left = Math.round(centreX - width / 2);
  return [
    { kind: 'rect', name: `${name}.block`, rect: rect(left - pad, top - pad, width + 2 * pad, size + 2 * pad), backgroundColor: lcdColour('ink') },
    segment(name, 'DSEG14Regular', text, left, top, width, { size, hAlign: 'center', color: lcdColour('ground') }),
  ];
}

/** How large the canvas's sizes can be in `frame`: scaled to its height and held to its width. */
const sizeIn = (frame: Rect, wanted: number, text: string): number => {
  let size = Math.round(wanted * Math.min(1, frame.height / 284));
  while (size > 8 && segmentWidth('DSEG14Regular', text, size) > frame.width - 2 * WARNING.pad) size--;
  return size;
};

function limiter(frame: Rect): Item {
  const label = sizeIn(frame, LIMITER.label, 'Pit Speed');
  const value = Math.round(LIMITER.value * Math.min(1, frame.height / 284));
  const gap = Math.round(LIMITER.gap * Math.min(1, frame.height / 284));
  const top = Math.round(frame.top + (frame.height - label - gap - value) / 2);
  const valueWidth = segmentWidth('DSEG7Bold', '888', value);
  const children: Item[] = [
    { kind: 'rect', name: 'limiter.ground', rect: frame, backgroundColor: lcdColour('ground') },
    ...inverse('limiter.label', 'Pit Speed', frame.left + frame.width / 2, top, label),
    ...reading('limiter.limit', '60', Math.round(frame.left + (frame.width + valueWidth) / 2), top + label + gap, value, {
      bind: iff(lt(pitLimit(), num(999)), fmt(pitLimit(), '0'), str('--')),
      widest: '888',
    }),
  ];
  return withMoreBindings({ kind: 'layer', name: 'limiter', children }, { Visible: pitAlertVisible('limiter') });
}

function warning(frame: Rect): Item {
  const box = rect(frame.left, frame.top + WARNING.inset, frame.width, frame.height - 2 * WARNING.inset);
  const size = sizeIn(box, WARNING.line, WARNING_WIDEST);
  const gap = Math.round(WARNING.gap * Math.min(1, box.height / 272));
  const top = Math.round(box.top + (box.height - 2 * size - gap) / 2);
  const line = (name: string, text: string, y: number, bind?: Expr, widest?: string): Item => {
    const width = segmentWidth('DSEG14Regular', widest ?? text, size);
    return segment(name, 'DSEG14Regular', text, Math.round(box.left + (box.width - width) / 2), y, width, { size, hAlign: 'center', bind, widest });
  };
  const which = WARNINGS.reduceRight<Expr>((rest, w) => iff(engineWarning(w.bit), str(w.text), rest), str(WARNINGS[0].text));
  const children: Item[] = [
    { kind: 'rect', name: 'warning.box', rect: box, backgroundColor: lcdColour('ground'), border: { color: lcdColour('ink'), top: WARNING.border, bottom: WARNING.border, left: WARNING.border, right: WARNING.border } },
    line('warning.title', 'ENGINE', top),
    line('warning.reading', WARNINGS[0].text, top + size + gap, which, WARNING_WIDEST),
  ];
  const on = and(inTheCar(), or(...WARNINGS.map((w) => engineWarning(w.bit))));
  return withMoreBindings({ kind: 'layer', name: 'warning', children }, { Visible: on });
}

export const aimTakeovers = (ctx: FaceContext): Item[] => {
  const c = zoneRect(ctx.regions, 'C');
  return [warning(c), limiter(c)];
};
