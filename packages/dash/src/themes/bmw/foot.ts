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
import { label } from '../../elements/label.ts';
import { currentLap, headlightFlashed, LAP_WIDEST, NO_VALUE, pitLimiterOn, sessionTimeLeft } from '../../second/values.ts';
import type { ThemeBandPage } from '../drawing.ts';
import { bmwFaceForFoot } from './geometry.ts';
import { bmwColour, box, caps, centredY, FILLED_SHADOW, fitSize, readingBox, type BmwColour } from './register.ts';

const { div, fmt, gt, iff, num, str, truncate } = ncalc;

const PILLS: readonly { id: string; title: string; lit: BmwColour; on?: () => Expr }[] = [
  { id: 'psl', title: 'PSL', lit: 'map', on: pitLimiterOn },
  { id: 'abs', title: 'ABS', lit: 'lit' },
  { id: 'tc', title: 'TC', lit: 'lit' },
  { id: 'lights', title: 'LIGHTS', lit: 'lit', on: headlightFlashed },
  { id: 'rain', title: 'RAIN', lit: 'lit' },
];

/** The canvas's foot: the pills 6 px apart, and 8 px between the row of them and each box. */
const PILL_GAP = 6;
const BOX_GAP = 8;

/** The session's whole minutes left, `--` while it counts none. */
const minutesLeft = (): Expr => iff(gt(sessionTimeLeft(), num(0)), fmt(truncate(div(sessionTimeLeft(), num(60))), '0'), str(NO_VALUE));

/** A pill: dim, and over it the lit one while what it names is on; each lit from above and casting its shadow. */
function pill(prefix: string, spec: (typeof PILLS)[number], r: Rect, size: number): Item[] {
  const name = `${prefix}pill.${spec.id}`;
  const face = (id: string, fill: BmwColour, ink: BmwColour, visible?: Expr): Item[] => {
    const { items, inner } = box(`${name}.${id}`, r, { fill: bmwColour(fill), within: rect(r.left, r.top, r.width + FILLED_SHADOW.x, r.height + FILLED_SHADOW.y), visible });
    return [
      ...items,
      label(`${name}.${id}.name`, spec.title, inner.left + 2, centredY(inner, size), inner.width - 4, { size, weight: 'Bold', color: bmwColour(ink), hAlign: 'center', visibleBind: visible }),
    ];
  };
  const on = spec.on?.();
  return [...face('dim', 'pillDim', 'pillInk'), ...(on ? face('lit', spec.lit, 'tileInk', on) : [])];
}

export const BMW_FOOT: ThemeBandPage = {
  id: 'bmwFoot',
  name: 'BMW',
  items: (frame, prefix) => {
    const face = bmwFaceForFoot(frame.width, frame.height);
    const k = Math.min(face.height / 480, 1.25);
    const lab = Math.round(16 * face.s);
    const boxes = [
      { id: 'lap', title: 'LAP', reading: { sample: '16', bind: fmt(currentLap(), '0'), widest: LAP_WIDEST } },
      { id: 'min', title: 'MIN', reading: { sample: '24', bind: minutesLeft(), widest: LAP_WIDEST } },
    ].slice(0, face.portrait ? 1 : 2);
    const boxWidth = face.portrait ? 96 : Math.round(96 * Math.min(Math.max(k, 0.8), 1.15));
    const value = face.portrait ? 32 : Math.round(36 * face.s);
    const writing = caps(lab, value, { top: 0, x: 10, bottom: 2 }, 1);
    const roomOf = (i: number): Rect => rect(frame.left + frame.width - (boxes.length - i) * (boxWidth + BOX_GAP) + BOX_GAP, frame.top, boxWidth, frame.height);

    const gap = face.portrait ? 5 : PILL_GAP;
    const room = frame.width - boxes.length * (boxWidth + BOX_GAP) - FILLED_SHADOW.x;
    const wanted = face.portrait ? { w: 66, h: 32 } : { w: Math.round(78 * (face.short ? 0.78 : Math.min(k, 1.1))), h: Math.round(34 * (face.short ? 0.78 : k)) };
    const pillWidth = Math.min(wanted.w, Math.floor((room - (PILLS.length - 1) * gap) / PILLS.length));
    const pillHeight = Math.min(wanted.h, frame.height - FILLED_SHADOW.y);
    const size = fitSize(Math.round(14 * face.s), pillWidth - 4, PILLS.map((p) => p.title), true);
    const top = Math.round(frame.top + (frame.height - pillHeight) / 2);
    return [
      ...PILLS.flatMap((spec, i) => pill(prefix, spec, rect(frame.left + i * (pillWidth + gap), top, pillWidth, pillHeight), size)),
      ...boxes.map((b, i) => readingBox(`${prefix}${b.id}`, roomOf(i), b.title, b.reading, writing)),
    ];
  },
};
