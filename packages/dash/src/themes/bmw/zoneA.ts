/**
 * Zone A on the BMW (#203), the canvas's middle column: `GEAR` over the gear, and `SPEED` over the
 * speed, set at the column's right with a twelfth of it to spare, red while the car is over the pit
 * limit. No box: the figures stand on the glass, in Barlow Medium.
 *
 * The canvas sizes the gear from the column, `(height - 2 × label line - 8) / 1.42` and never wider
 * than the column holds, and the speed at 0.46 of it. The house's pages keep their meaning: the first
 * is the gear and the speed; the second the gear alone, as large as the column holds it; the third the
 * speed alone over the gear at the speed's size. The track map is a drawing and stays the house's.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { label } from '../../elements/label.ts';
import { gear, pitSpeeding, speed } from '../../second/values.ts';
import { scaleOf, typeOf } from './pages.ts';
import { bmwColour, fitSize, lineY } from './register.ts';

const { fmt, iff, str } = ncalc;

const GEAR_WIDEST = 'N';
const SPEED_WIDEST = '444';

interface Figure {
  id: string;
  title: string;
  sample: string;
  size: number;
  lh: number;
  align: 'center' | 'right';
}

/** A titled figure: the title on its line of 1.2, the figure on its own line under it. */
function figure(prefix: string, frame: Rect, top: number, aLab: number, f: Figure): { items: Item[]; bottom: number } {
  const ink = bmwColour('ink');
  const pad = f.align === 'right' ? Math.round(frame.width * 0.08) : 0;
  const isGear = f.id === 'gear';
  const valueTop = top + aLab * 1.2;
  return {
    items: [
      label(`${prefix}${f.id}.name`, f.title, frame.left, lineY(top, aLab), frame.width, { size: aLab, color: ink, hAlign: 'center' }),
      label(`${prefix}${f.id}.value`, f.sample, frame.left, lineY(valueTop, f.size, f.lh), frame.width - pad, {
        size: f.size,
        color: ink,
        hAlign: f.align,
        bind: isGear ? gear() : fmt(speed(), '0'),
        widest: isGear ? GEAR_WIDEST : SPEED_WIDEST,
        ...(isGear ? {} : { colorBind: iff(pitSpeeding(), str(bmwColour('red')), str(ink)) }),
      }),
    ],
    bottom: valueTop + f.size * f.lh,
  };
}

/**
 * A page drawn at the largest size from `wanted` down whose text boxes all stay on the column: WPF's
 * box reaches above a line of under 1.2 and below every line, so the page is moved down by what its
 * highest box leaves over the top, and drawn a size smaller while its lowest box then leaves the foot.
 */
function onColumn(frame: Rect, wanted: number, draw: (size: number, top: number) => Item[]): Item[] {
  const bottom = frame.top + frame.height;
  for (let size = wanted; ; size--) {
    const first = draw(size, frame.top);
    const over = Math.max(0, frame.top - Math.min(...first.map((i) => (i.kind === 'text' ? i.rect.top : frame.top))));
    const items = over > 0 ? draw(size, frame.top + over) : first;
    if (size <= 8 || items.every((i) => i.kind !== 'text' || (i.rect.top >= frame.top && i.rect.top + i.rect.height <= bottom))) return items;
  }
}

/** A1: the canvas's column. */
function gearAndSpeed(frame: Rect, prefix: string): Item[] {
  const aLab = typeOf(scaleOf(frame).s).aLab;
  const wanted = fitSize(Math.min(Math.round((frame.height - 2 * aLab * 1.2 - 8) / 1.42), Math.round(frame.width / 0.62)), frame.width - 4, [GEAR_WIDEST]);
  return onColumn(frame, wanted, (gearSize, top) => {
    const speedSize = fitSize(Math.round(gearSize * 0.46), frame.width - 4 - Math.round(frame.width * 0.08), [SPEED_WIDEST]);
    const g = figure(prefix, frame, top, aLab, { id: 'gear', title: 'GEAR', sample: '3', size: gearSize, lh: 0.92, align: 'center' });
    const sp = figure(prefix, frame, g.bottom + 4, aLab, { id: 'speed', title: 'SPEED', sample: '148', size: speedSize, lh: 1, align: 'right' });
    return [...g.items, ...sp.items];
  });
}

/** A2: the gear alone, as large as the column holds it. */
function gearAlone(frame: Rect, prefix: string): Item[] {
  const aLab = typeOf(scaleOf(frame).s).aLab;
  const wanted = fitSize(Math.floor((frame.height - aLab * 1.2 - 2) / 1.1), frame.width - 4, [GEAR_WIDEST]);
  return onColumn(frame, wanted, (size, top) => {
    const height = aLab * 1.2 + size * 1.1;
    return figure(prefix, frame, top + Math.max(0, (frame.height - (top - frame.top) - height) / 2), aLab, { id: 'gear', title: 'GEAR', sample: '3', size, lh: 1, align: 'center' }).items;
  });
}

/** A3: the speed alone, as large as the column holds it, and the gear under it at the speed's size. */
function speedAlone(frame: Rect, prefix: string): Item[] {
  const aLab = typeOf(scaleOf(frame).s).aLab;
  const wanted = fitSize(Math.floor((frame.height - 2 * aLab * 1.2 - 8) / 2.2), frame.width - 4, [SPEED_WIDEST]);
  return onColumn(frame, wanted, (size, top) => {
    const height = 2 * aLab * 1.2 + 2 * size * 1.1 + 8;
    const sp = figure(prefix, frame, top + Math.max(0, (frame.height - (top - frame.top) - height) / 2), aLab, { id: 'speed', title: 'SPEED', sample: '148', size, lh: 1.1, align: 'center' });
    return [...sp.items, ...figure(prefix, frame, sp.bottom + 8, aLab, { id: 'gear', title: 'GEAR', sample: '3', size, lh: 1.1, align: 'center' }).items];
  });
}

const PAGES: Record<string, (frame: Rect, prefix: string) => Item[]> = { gearSpeedRevs: gearAndSpeed, gearAlone, speed: speedAlone };

/** One of zone A's pages on the BMW, or nothing for the track map, which the house draws. */
export const bmwZoneAPage = (page: string, frame: Rect, prefix: string): Item[] | undefined => PAGES[page]?.(frame, prefix);
