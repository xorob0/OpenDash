/**
 * The companion's header and its page dots, and the pit wall's panel and zone frames.
 *
 * The companion shows one module at a time, so the header is what stays: which module this is,
 * where it sits in the cycle, and the two things a driver should never have to page for, position
 * and lap. The dots under the module are the same information as the counter, in a form the eye
 * reads without focusing.
 */
import type { HAlign, Hex, Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { withBindings } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { canvasBaseline, canvasYForBaseline, cells, monoWidth } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { dot, DOT_SIZE } from '../elements/dot.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { rule } from '../elements/rule.ts';
import { unit } from '../elements/unit.ts';
import { ds } from '../tokens.ts';
import { densityOf, type Density } from './density.ts';
import { CHARS, carPosition, currentLap, fieldSize, player, totalLaps } from './values.ts';

const { concat, str, fmt, iff, gt, num } = ncalc;

/** Height of the companion header, and the padding either side of it. */
export const COMPANION_HEADER = { height: 56, padX: 24, gap: 12, groupGap: 20 } as const;
/**
 * Page dots: a square per module, at the dot element's own size. The page-indicator sheet draws
 * them 8 where the element sheet draws 6, which is why the size is passed rather than assumed.
 */
export const PAGE_DOT = { size: DOT_SIZE, gap: 6 } as const;

/** A value with a small denominator after it, as the header draws "P4 / 24". */
function pair(
  name: string,
  valueSample: string,
  valueBind: Expr,
  denominator: string,
  denominatorWidest: string,
  denominatorBind: Expr,
  x: number,
  y: number,
  fs: number,
  density: Density,
  visibleBind?: Expr,
): { items: Item[]; width: number } {
  const d = densityOf(density);
  const mono = cells('SemiBold', fs);
  const chars = { digits: valueSample.length, specials: 0 };
  const valueWidth = monoWidth(mono, chars);
  const denominatorWidth = Math.ceil(measureText('BarlowMedium', denominatorWidest, d.labelSm));
  const denominatorX = x + valueWidth + ds.space[2];
  return {
    items: [
      numeral(`${name}.value`, valueSample, x, y, fs, chars, { bind: valueBind, maxWidth: valueWidth + 4 }),
      unit(`${name}.denominator`, denominator, denominatorX, canvasYForBaseline(canvasBaseline(y, fs), d.labelSm), denominatorWidth + 2, {
        bind: denominatorBind,
        widest: denominatorWidest,
        visibleBind,
      }),
    ],
    width: valueWidth + ds.space[2] + denominatorWidth,
  };
}

export interface CompanionHeaderSpec {
  frame: Rect;
  /** Module name, drawn as it is written. */
  moduleName: string;
  /** 1-based page number and how many pages there are. */
  page: number;
  pages: number;
}

/**
 * The header: module name and page counter on the left, position and lap on the right, a rule
 * along the bottom edge. Both right-hand values are static in layout and bound in content, so the
 * header is identical on all 21 pages bar its name and number.
 */
export function companionHeader(name: string, spec: CompanionHeaderSpec, density: Density = 'companion'): Item[] {
  const d = densityOf(density);
  const frame = spec.frame;
  const fs = d.tiny;
  const textY = frame.top + (frame.height - d.label) / 2;
  const valueY = canvasYForBaseline(canvasBaseline(textY, d.label), fs);
  const items: Item[] = [];
  const nameWidth = Math.ceil(measureText('BarlowMedium', spec.moduleName.toUpperCase(), d.label));
  items.push(label(`${name}.module`, spec.moduleName, frame.left + COMPANION_HEADER.padX, textY, nameWidth + 2, { size: d.label, color: ds.color.text.primary }));
  const counter = `${spec.page} / ${spec.pages}`;
  items.push(
    label(`${name}.counter`, counter, frame.left + COMPANION_HEADER.padX + nameWidth + COMPANION_HEADER.gap, textY, Math.ceil(measureText('BarlowMedium', counter, d.label)) + 2, {
      size: d.label,
    }),
  );

  // Right group, laid out from the right edge so the two pairs keep their gap whatever they read.
  const lap = pair(
    `${name}.lap`,
    'L12',
    concat(str('L'), fmt(currentLap(), '0')),
    '/ 30',
    '/ 999',
    concat(str('/ '), fmt(totalLaps(), '0')),
    0,
    valueY,
    fs,
    density,
    gt(totalLaps(), num(0)),
  );
  const position = pair(`${name}.position`, 'P24', concat(str('P'), fmt(carPosition(player()), '0')), '/ 24', '/ 999', concat(str('/ '), fmt(fieldSize(), '0')), 0, valueY, fs, density);
  const right = frame.left + frame.width - COMPANION_HEADER.padX;
  const lapX = right - lap.width;
  const positionX = lapX - COMPANION_HEADER.groupGap - position.width;
  items.push(...shift(position.items, positionX), ...shift(lap.items, lapX));
  items.push(rule(`${name}.rule`, frame.left, frame.top + frame.height - 1, frame.width, 1));
  return items;
}

/** Moves items right by `dx`; the pair helper lays out from zero and the header places the group. */
const shift = (items: Item[], dx: number): Item[] =>
  items.map((item) => (item.kind === 'layer' ? item : { ...item, rect: { ...item.rect, left: item.rect.left + dx } }));

/** The dot row: one square per module, the current one lit. */
export function pageDots(name: string, frame: Rect, count: number, active: number): Item[] {
  const width = count * PAGE_DOT.size + (count - 1) * PAGE_DOT.gap;
  const x = Math.round(frame.left + (frame.width - width) / 2);
  const y = Math.round(frame.top + (frame.height - PAGE_DOT.size) / 2);
  return Array.from({ length: count }, (_, i) =>
    dot(`${name}.dot${String(i + 1).padStart(2, '0')}`, x + i * (PAGE_DOT.size + PAGE_DOT.gap), y, i + 1 === active ? ds.color.text.primary : ds.color.text.dim, { size: PAGE_DOT.size }),
  );
}

export interface PanelSpec {
  frame: Rect;
  title: string;
  /** Padding inside the panel. */
  padX?: number;
  padY?: number;
  /**
   * Gap between the title's row and the body under it. Eight by default, which is what the panel
   * sheets draw; a trace panel asks for six, because its title row also carries the legend and the
   * two pixels are the difference between the plot the artboard draws and one two pixels shorter.
   */
  titleGap?: number;
}

/** Height a panel's title row takes, gap included. */
export const PANEL_TITLE_HEIGHT = ds.size.labelSm + ds.space[2];

/** A pit wall panel: a title in small caps and the body rect under it. */
export function panel(name: string, spec: PanelSpec, density: Density = 'zone'): { items: Item[]; body: Rect } {
  const d = densityOf(density);
  const padX = spec.padX ?? 20;
  const padY = spec.padY ?? 14;
  const titleY = spec.frame.top + padY;
  const items: Item[] = [
    // A panel title is the sheet's plain small label and takes its colour, where a zone title is
    // the one label the sheet overrides to the brighter secondary: on the pit wall zones every
    // title carries `color: #8A9099` inline and the counter beside it does not, which is the whole
    // difference between the two chromes.
    label(`${name}.title`, spec.title, spec.frame.left + padX, titleY, spec.frame.width - 2 * padX, { size: d.labelSm, color: ds.color.text.label }),
  ];
  const bodyTop = titleY + d.labelSm + (spec.titleGap ?? ds.space[2]);
  return {
    items,
    body: rect(spec.frame.left + padX, bodyTop, Math.max(0, spec.frame.width - 2 * padX), Math.max(0, spec.frame.top + spec.frame.height - padY - bodyTop)),
  };
}

/** Height of a zone's title bar. */
export const ZONE_TITLE_HEIGHT = 28;

/**
 * Whose chrome a zone frame draws.
 *
 * The face and the pit wall share `zoneFrame` and their artboards do not draw the same header.
 * Every Dash sheet pads a zone `6px 12px` and opens it with a 22 px row of 15 px labels in
 * `#5A6069`; `PitWallZones.dc.html` draws 28 px over 16. So the caller says which it is rather than
 * the frame reading it off the density, which cannot tell a face zone from a pit wall one.
 */
export type ZoneChrome = 'face' | 'pitwall';

export interface ZoneFrameMetrics {
  /** The header row the title sits in. */
  title: number;
  /** Padding at the sides. */
  padX: number;
  /** Padding over the header row. */
  padTop: number;
  /** Between the header row and the body. */
  gap: number;
  /** What is left under the body. */
  padBottom: number;
  /** The size the zone letter, the page name and the counter are drawn at. */
  size: number;
  /** Their ink. */
  color: Hex;
}

/**
 * The face's chrome, read off the Dash artboards.
 *
 * The same at every density, compact included, because this is the face's chrome and not the
 * page's ramp: the nano steps its *page* down to the compact ladder and still draws the 22 px row
 * of 15 px labels its artboard draws.
 */
const FACE_CHROME: ZoneFrameMetrics = { title: 22, padX: 12, padTop: 6, gap: 4, padBottom: 6, size: ds.size.label, color: ds.color.text.label };

/**
 * The title line and the padding a zone frame takes.
 *
 * The pit wall's follow the density: a 28 px title over a 16 px gutter is right on a 769 by 314
 * zone and is a fifth of a compact one's height, the frame being chrome that should cost the page
 * less where the page has less. `padBottom` is what is left under the *body*, which is the ten
 * pixels the pit wall has always drawn; it used to be written as sixteen and measured from the
 * title row instead, so the number moved and the rectangle did not.
 */
export const zoneFrameMetrics = (density: Density, chrome: ZoneChrome = 'pitwall'): ZoneFrameMetrics =>
  chrome === 'face'
    ? FACE_CHROME
    : {
        ...(density === 'compact' ? { title: 20, padX: 10 } : { title: ZONE_TITLE_HEIGHT, padX: 16 }),
        padTop: 0,
        gap: 6,
        padBottom: 10,
        size: densityOf(density).labelSm,
        color: ds.color.text.secondary,
      };

/**
 * The page counter in a zone's title bar.
 *
 * `static` is a catalogue with no mask behind it, which is the pit wall: the screen knows it is
 * page three of eleven and says so. `reserved` keeps the room and draws nothing, for a zone whose
 * counter somebody else binds -- the face zones, where the mask decides how long the cycle is and
 * only the face knows which zone a shared dashboard is serving.
 */
export type ZoneCounter = { kind: 'static'; page: number; pages: number } | { kind: 'reserved'; widest: string };

export interface ZoneSpec {
  frame: Rect;
  title: string;
  counter: ZoneCounter;
  /**
   * Space kept clear before the title, for a zone letter somebody else draws. Zones B and C are the
   * same rectangle on most faces and so share one dashboard file, which means the letter cannot be
   * baked into it: the face draws it, and this is the room it needs.
   */
  indent?: number;
}

/** The y a zone frame puts its title on, which the face needs to line the letter up with it. */
export const zoneTitleY = (frame: Rect, metrics: ZoneFrameMetrics): number => frame.top + metrics.padTop + (metrics.title - metrics.size) / 2;

/** The room a zone's title bar keeps at its right for the counter, drawn there or not. */
export function zoneCounterWidth(counter: ZoneCounter, metrics: ZoneFrameMetrics): number {
  const text = counter.kind === 'static' ? `${counter.page} / ${counter.pages}` : counter.widest;
  return Math.ceil(measureText('BarlowMedium', text, metrics.size)) + 2;
}

/** The x a zone's counter is drawn at, which a caller that draws its own needs. */
export const zoneCounterX = (frame: Rect, counter: ZoneCounter, metrics: ZoneFrameMetrics): number =>
  frame.left + frame.width - metrics.padX - zoneCounterWidth(counter, metrics);

/** A data zone: a title bar with the page name and counter, and the body rect under it. */
export function zoneFrame(name: string, spec: ZoneSpec, density: Density = 'zone', chrome: ZoneChrome = 'pitwall'): { items: Item[]; body: Rect } {
  const metrics = zoneFrameMetrics(density, chrome);
  const { title: titleHeight, padX, padTop, gap, padBottom, size, color } = metrics;
  const titleY = zoneTitleY(spec.frame, metrics);
  const indent = spec.indent ?? 0;
  const counterWidth = zoneCounterWidth(spec.counter, metrics);
  const items: Item[] = [
    label(`${name}.title`, spec.title, spec.frame.left + padX + indent, titleY, spec.frame.width - 2 * padX - indent - counterWidth, { size, color }),
  ];
  if (spec.counter.kind === 'static') {
    items.push(label(`${name}.counter`, `${spec.counter.page} / ${spec.counter.pages}`, zoneCounterX(spec.frame, spec.counter, metrics), titleY, counterWidth, { size, hAlign: 'right' }));
  }
  const bodyTop = spec.frame.top + padTop + titleHeight + gap;
  return {
    items,
    body: rect(spec.frame.left + padX, bodyTop, Math.max(0, spec.frame.width - 2 * padX), Math.max(0, spec.frame.top + spec.frame.height - padBottom - bodyTop)),
  };
}

/**
 * One part of an inline group: a small label, a value, or a coloured block.
 *
 * A label that is bound has to declare `widest`, the longest string the binding can draw, because
 * the group measures the box before the binding exists and WPF clips whatever does not fit. The
 * type requires it so that the declaration cannot be forgotten, which is how "NO FLAG" once came
 * out as "NO FLA".
 */
export type InlinePart =
  | { kind: 'label'; text: string; bind?: undefined; widest?: undefined; color?: `#${string}`; hAlign?: HAlign; visibleBind?: Expr }
  | { kind: 'label'; text: string; bind: Expr; widest: string; color?: `#${string}`; hAlign?: HAlign; visibleBind?: Expr }
  | { kind: 'value'; sample: string; bind?: Expr; chars: { digits: number; specials: number }; color?: `#${string}`; colorBind?: Expr; visibleBind?: Expr }
  | { kind: 'block'; width: number; height: number; color: `#${string}`; colorBind?: Expr; visibleBind?: Expr };

/**
 * A run of labels, values and blocks on one baseline, as the pit wall header draws "Left 0:42:15"
 * or a flag colour beside its name. The group measures itself so a caller can lay several of them
 * out from the right edge of a header.
 */
export function inlineGroup(name: string, parts: readonly InlinePart[], fs: number, density: Density, gap = ds.space[2]): { width: number; draw(x: number, top: number): Item[] } {
  const d = densityOf(density);
  const widths = parts.map((part) => {
    if (part.kind === 'label') return Math.ceil(measureText('BarlowMedium', (part.widest ?? part.text).toUpperCase(), d.labelSm)) + 2;
    if (part.kind === 'block') return part.width;
    return monoWidth(cells('SemiBold', fs), part.chars) + 4;
  });
  const width = widths.reduce((sum, w) => sum + w, 0) + gap * Math.max(0, parts.length - 1);
  return {
    width,
    draw(x: number, top: number): Item[] {
      const items: Item[] = [];
      const baseline = canvasBaseline(top, fs);
      let cursor = x;
      parts.forEach((part, i) => {
        const w = widths[i] ?? 0;
        if (part.kind === 'label') {
          items.push(
            label(`${name}.${i}`, part.text, cursor, canvasYForBaseline(baseline, d.labelSm), w, {
              size: d.labelSm,
              color: part.color,
              hAlign: part.hAlign,
              bind: part.bind,
              widest: part.widest,
              visibleBind: part.visibleBind,
            }),
          );
        } else if (part.kind === 'block') {
          items.push({
            ...band(`${name}.${i}`, rect(cursor, Math.round(top + (fs - part.height) / 2), part.width, part.height), part.color),
            ...withBindings({ BackgroundColor: part.colorBind, Visible: part.visibleBind }),
          });
        } else {
          items.push(
            numeral(`${name}.${i}`, part.sample, cursor, top, fs, part.chars, {
              bind: part.bind,
              color: part.color,
              colorBind: part.colorBind,
              visibleBind: part.visibleBind,
              maxWidth: w,
            }),
          );
        }
        cursor += w + gap;
      });
      return items;
    },
  };
}
