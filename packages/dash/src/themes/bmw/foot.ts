/**
 * The car's foot, as one more page of band D's catalogue (ADR 0015, #203): the row of pills at the
 * left, PSL, ABS, TC, LIGHTS and RAIN, and the LAP and MIN boxes at the right.
 *
 * A pill is dim until what it names is on: PSL lights magenta with the pit limiter, LIGHTS green for
 * the alert window after the driver flashes the headlights, which is all iRacing publishes of them.
 * ABS, TC and RAIN have no source the core reads, so they stay dim, as band D's telltales without one
 * do; the car lights them on the system working and the wipers running.
 *
 * MIN is the session's whole minutes left, the canvas's figure, and `--` in a session of laps.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import type { Expr } from '../../bind.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { currentLap, headlightFlashed, LAP_WIDEST, NO_VALUE, pitLimiterOn, sessionTimeLeft } from '../../second/values.ts';
import type { ThemeBandPage } from '../drawing.ts';
import { bmwFaceForFoot } from './geometry.ts';
import { bmwColour, centredY, runWidth, stackedBox, stackedTypeFor, type BmwColour } from './register.ts';

const { div, fmt, gt, iff, num, str, truncate } = ncalc;

const PILLS: readonly { id: string; title: string; lit: BmwColour; on?: () => Expr }[] = [
  { id: 'psl', title: 'PSL', lit: 'map', on: pitLimiterOn },
  { id: 'abs', title: 'ABS', lit: 'lit' },
  { id: 'tc', title: 'TC', lit: 'lit' },
  { id: 'lights', title: 'LIGHTS', lit: 'lit', on: headlightFlashed },
  { id: 'rain', title: 'RAIN', lit: 'lit' },
];

const PILL_GAP = 6;

/** The session's whole minutes left, `--` while it counts none. */
const minutesLeft = (): Expr => iff(gt(sessionTimeLeft(), num(0)), fmt(truncate(div(sessionTimeLeft(), num(60))), '0'), str(NO_VALUE));

function pill(prefix: string, spec: (typeof PILLS)[number], r: Rect, size: number): Item[] {
  const name = `${prefix}pill.${spec.id}`;
  const text = (id: string, colour: BmwColour, visible?: Expr): Item =>
    label(`${name}.${id}`, spec.title, r.left + 2, centredY(r, size), r.width - 4, { size, weight: 'Bold', color: bmwColour(colour), hAlign: 'center', visibleBind: visible });
  const on = spec.on?.();
  return [
    band(`${name}.dim`, r, bmwColour('pillDim'), { radius: 3 }),
    text('name', 'pillInk'),
    ...(on ? [band(`${name}.lit`, r, bmwColour(spec.lit), { radius: 3, visibleBind: on }), text('litName', 'tileInk', on)] : []),
  ];
}

export const BMW_FOOT: ThemeBandPage = {
  id: 'bmwFoot',
  name: 'BMW',
  items: (frame, prefix) => {
    const face = bmwFaceForFoot(frame.width, frame.height);
    const k = Math.min(face.height / 480, 1.25);
    const boxWidth = Math.round(96 * Math.min(Math.max(k, 0.8), 1.15));
    const boxes = [
      { id: 'lap', title: 'LAP', reading: { sample: '12', bind: fmt(currentLap(), '0'), widest: LAP_WIDEST } },
      { id: 'min', title: 'MIN', reading: { sample: '24', bind: minutesLeft(), widest: LAP_WIDEST } },
    ];
    const roomOf = (i: number): Rect => rect(frame.left + frame.width - (boxes.length - i) * (boxWidth + PILL_GAP) + PILL_GAP, frame.top, boxWidth, frame.height);
    const type = stackedTypeFor(roomOf(0), boxes.map((b) => ({ title: b.title, widest: b.reading.widest })), { label: Math.round(16 * face.s), value: Math.round(36 * face.s) });
    if (!type) throw new Error(`bmw: the foot's boxes do not fit ${frame.width} x ${frame.height}`);

    const scale = face.short ? 0.78 : k;
    const pillHeight = Math.min(Math.round(34 * scale), frame.height);
    const room = frame.width - boxes.length * (boxWidth + PILL_GAP) - PILL_GAP;
    const pillWidth = Math.min(Math.round(78 * (face.short ? 0.78 : Math.min(k, 1.1))), Math.floor((room - (PILLS.length - 1) * PILL_GAP) / PILLS.length));
    const pillSize = Math.min(Math.round(14 * face.s), ...PILLS.map(() => 99));
    const fitSize = (size: number): number => (PILLS.every((p) => runWidth(p.title, size, true) <= pillWidth - 4) ? size : fitSize(size - 1));
    const top = Math.round(frame.top + (frame.height - pillHeight) / 2);
    return [
      ...PILLS.flatMap((spec, i) => pill(prefix, spec, rect(frame.left + i * (pillWidth + PILL_GAP), top, pillWidth, pillHeight), fitSize(pillSize))),
      ...boxes.map((b, i) => stackedBox(`${prefix}${b.id}`, roomOf(i), b.title, b.reading, type, {}, 'center')),
    ];
  },
};
