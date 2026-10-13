/**
 * What a zone face draws that a driver reads, as rectangles on the face: the test helper behind
 * `flagSettled.test.ts`, which holds the settled flag to a rectangle that covers none of them (#731).
 *
 * "Draws a reading" is decided item by item, and the rule is meant to be the obvious one written out:
 *
 * - A text draws the run it can be bound to, which is its `widest` where it declares one and its own
 *   text otherwise, measured in the face it is drawn in and placed by its alignment inside its box.
 *   A text whose `Text` is bound and which declares no `widest` could draw anything, so it is its box.
 *   The height is the box's, whatever the glyphs: SimHub clips to the box and nothing tighter.
 * - An image, an ellipse, a chart, a gauge, a map, a radar or a web page is its rectangle.
 * - A rectangle is a reading where it or a layer above it is bound, which is a lamp, a segment or a
 *   bar that moves with the car, and a ground where nothing binds it: a well, a box, a rule. One that
 *   covers the whole of its dashboard is a ground even when bound, which is the AiM's backlight.
 *
 * A zone widget is replaced by every screen of the dashboard it embeds, so a rectangle that is clear
 * of a zone is clear of every page the driver can put there rather than of the page the build opens on.
 *
 * What the face draws after its zone D widget is not read: the flag in both its phases, the
 * full-screen flag, the pit alerts, the pop-ups, the change notifications, the lap review and a
 * theme's takeovers. Each of those is drawn over the face for as long as it is out, and the question
 * is what the settled flag leaves of the face, not of another overlay.
 */
import { measureText, type MeasuredFace } from '../src/design/advances.ts';
import type { Dashboard, Item, Rect, Screen, TextItem } from '../src/generator.ts';

/** One thing a driver reads, on the face's own canvas. */
export interface Ink {
  name: string;
  rect: Rect;
}

/** Which measured face an item draws in: the family it names, at the weight it asks for. */
const faceOf = (item: TextItem): MeasuredFace => {
  if (item.font === 'Barlow') return item.fontWeight === 'Bold' ? 'BarlowBold' : 'BarlowMedium';
  if (item.font.startsWith('DSEG14')) return 'DSEG14Regular';
  if (item.font.startsWith('DSEG7')) return item.fontWeight === 'Bold' ? 'DSEG7Bold' : 'DSEG7Regular';
  if (item.fontWeight === 'Bold') return 'BarlowCondensedBold';
  if (item.fontWeight === 'Light') return 'BarlowCondensedLight';
  return 'BarlowCondensedSemiBold';
};

const bound = (item: Item): boolean => Object.keys(item.bindings ?? {}).length > 0;

/** The part of a text's box its run can reach, in the box's own coordinates. */
function textExtent(item: TextItem): Rect {
  const box = item.rect;
  if (item.bindings?.Text !== undefined && item.widest === undefined) return box;
  const text = item.widest ?? item.text;
  const mono = item.monospace;
  const measured = mono ? [...text].length * Math.max(mono.charWidth, mono.specialCharsWidth) : measureText(faceOf(item), text, item.fontSize);
  const width = Math.min(box.width, Math.ceil(measured));
  const left = item.hAlign === 'left' ? box.left : item.hAlign === 'right' ? box.left + box.width - width : box.left + (box.width - width) / 2;
  return { left, top: box.top, width, height: box.height };
}

/** The screens' items of the dashboard a widget embeds, found by its file name. */
const embedded = (dashboards: readonly Dashboard[], fileName: string): Dashboard => {
  const name = fileName.replace(/\.djson$/, '');
  const found = dashboards.find((d) => d.name === name);
  if (!found) throw new Error(`no dashboard ${name} in the package`);
  return found;
};

/** Every reading a list of items draws, mapped onto the face through the widget it sits in. */
function inkOf(items: readonly Item[], dashboards: readonly Dashboard[], frame: { dx: number; dy: number; sx: number; sy: number; width: number; height: number }, underBinding: boolean, out: Ink[]): void {
  const place = (r: Rect): Rect => ({ left: frame.dx + r.left * frame.sx, top: frame.dy + r.top * frame.sy, width: r.width * frame.sx, height: r.height * frame.sy });
  for (const item of items) {
    const isBound = underBinding || bound(item);
    if (item.kind === 'layer') {
      inkOf(item.children, dashboards, frame, isBound, out);
      continue;
    }
    if (item.kind === 'widget') {
      const dashboard = embedded(dashboards, item.fileName);
      const at = place(item.rect);
      const inner = { dx: at.left, dy: at.top, sx: at.width / dashboard.width, sy: at.height / dashboard.height, width: dashboard.width, height: dashboard.height };
      for (const screen of dashboard.screens) inkOf(screen.items, dashboards, inner, false, out);
      continue;
    }
    if (item.kind === 'text') {
      out.push({ name: item.name, rect: place(textExtent(item)) });
      continue;
    }
    if (item.kind === 'rect') {
      const r = item.rect;
      const whole = r.left <= 0 && r.top <= 0 && r.left + r.width >= frame.width && r.top + r.height >= frame.height;
      if (isBound && !whole) out.push({ name: item.name, rect: place(r) });
      continue;
    }
    out.push({ name: item.name, rect: place(item.rect) });
  }
}

/** The name of the widget a face draws band D in, after which everything is an overlay. */
export const ZONE_D_WIDGET = 'zoneD';

/**
 * Every reading one screen of a face draws, zones included at every page their dashboards hold, and
 * the overlays after the zone D widget left out.
 */
export function faceInk(screen: Screen, dashboards: readonly Dashboard[], face: { width: number; height: number }): Ink[] {
  const last = screen.items.findIndex((item) => item.name === ZONE_D_WIDGET);
  if (last < 0) throw new Error(`${screen.name} draws no ${ZONE_D_WIDGET}`);
  const out: Ink[] = [];
  inkOf(screen.items.slice(0, last + 1), dashboards, { dx: 0, dy: 0, sx: 1, sy: 1, width: face.width, height: face.height }, false, out);
  return out;
}

/** Whether two rectangles share any area; touching along an edge is not overlapping. */
export const overlapping = (a: Rect, b: Rect): boolean =>
  a.left < b.left + b.width && b.left < a.left + a.width && a.top < b.top + b.height && b.top < a.top + a.height;
