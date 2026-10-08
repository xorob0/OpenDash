/**
 * The car's foot, as one more page of band D's catalogue (ADR 0015, #205): the badge's place, the TC
 * and ABS boxes, the tyre box with its yellow tab, and `Brake Bias` on a grey cell.
 *
 * Laid at `geometry.ts`'s rectangles for the face whose foot this band is, which on the reference
 * are the ticket's, less the band's top: the band runs the whole width from x 0, so the page's x are
 * the face's. A band is handed its own frame and nothing else, and its width and height name the
 * face, the two 1280 faces of 480 and 720 sharing one foot. The faces without a settings column, 850
 * and 800 wide, shed the badge; the 800 x 286 sheds the TC and ABS boxes too and keeps the tyre box,
 * its four pressures in one row under the tab, and the bias; the portrait face puts the tyre box
 * beside the bias over the TC pair and ABS (#713).
 *
 * **The badge draws the crest the plugin fetched, and the package carries none.** The car shows the
 * Porsche crest, a registered trade mark that cannot ship in an MIT package (#194), so the page holds
 * the canvas's placeholder: a shield 54 by 62 at x 14, square at the top and round at the foot,
 * outlined in the label grey with `your` over `badge` in it at 10 px. The canvas dashes the outline and
 * a SimHub border has no dash, so it is solid; and it stands on the band's foot, six pixels lower than
 * the canvas's 404. On the 1280 x 400 face it is the board's nine tenths of that size, at the foot of
 * the shorter band.
 *
 * Over it, in the same rectangle, an `ImageFromFileItem` draws the file `OpenDash.PorscheCrest` names:
 * the crest the plugin downloads once into the user's own SimHub folder, the way the car light tables
 * are fetched (#714, the trade dress paragraph of `docs/scope.md`). SimHub fits the picture to the
 * rectangle keeping its proportions, so the crest stands 62 high and about 47 wide, centred. While the
 * property is empty -- no plugin, the address cleared on the panel, or a fetch that failed -- the item
 * draws nothing, and the placeholder, bound to the same emptiness, is what shows. A face that sheds the
 * badge draws neither, and fetches nothing (#713).
 *
 * The two TC boxes read the two traction dials a GT3 car exposes, `TC` and `TC cut` in the house's
 * list of watched settings, where the car shows the same value in both; each is hidden on a car
 * that does not publish its dial, as the house's settings strip hides a cell. The tyre box is drawn
 * in `color.caution.primary` rather than the flag's yellow, because the box is never a flag and the
 * canvas draws it in the caution amber.
 */
import type { Hex, Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings } from '../../bind.ts';
import { setting } from '../../contract.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { hasSetting, trackedValue } from '../../second/tracked.ts';
import { brakeBias, CORNERS, tyrePressure, tyreTemperature, type Corner } from '../../second/values.ts';
import { ds, TRANSPARENT } from '../../tokens.ts';
import type { ThemeBandPage } from '../drawing.ts';
import { porscheFaceForFoot, type FootBox } from './geometry.ts';
import { BORDER, carColour, centredY, INSET_RADIUS, LABEL_SIZE, namedCell, RADIUS, runWidth, settingBox } from './register.ts';

const { eq, fmt, str } = ncalc;

/** The tyre box's figures on the reference: the tab, the corner temperatures and the pressures, from the ticket. */
const TYRE = { tab: { height: 26, pad: 14 }, corner: { x: 8, y: 2 }, grid: { x: 52, top: 26, bottom: 6 }, pressure: 32 };

/** The reference tyre box, which the others are scaled from: 142 tall, and 291 wide where it carries one row. */
const TYRE_BOX = { height: 142, oneRow: { height: 60, width: 291 } };

/** The badge's words, from the canvas: 10 px on a 12 px line. */
const BADGE = { word: 10, line: 12 };

/** The widest pressure and temperature a corner can draw. */
const PRESSURE_WIDEST = '188.8';
const TEMPERATURE_WIDEST = '188';

const settingReading = (id: string) => {
  const value = trackedValue(id);
  return { value, reading: { sample: value.sample, bind: fmt(value.read, value.pattern), widest: value.pattern === '0' ? '88' : '88.8' } };
};

/** The largest size, from `size` down, at which `text` fits `width`; a font follows its box. */
function fitting(text: string, size: number, width: number, bold = false): number {
  let at = size;
  while (at > 8 && runWidth(text, at, bold) > width) at--;
  return at;
}

const PRESSURE_SAMPLES = ['23.9', '23.9', '23.9', '24.0'];

function pressureLabel(prefix: string, corner: Corner, i: number, cell: Rect, size: number): Item {
  return label(`${prefix}pressure.${corner}`, PRESSURE_SAMPLES[i]!, cell.left, centredY(cell, size), cell.width, {
    size,
    color: ds.color.text.primary,
    hAlign: 'center',
    bind: fmt(tyrePressure(corner), '0.0'),
    widest: PRESSURE_WIDEST,
  });
}

/**
 * The tyre box: the yellow outline, the `TYRE` tab hanging from its top border, the temperatures in
 * the corners and the pressures in a 2 by 2 grid; or, where the box is a single row's height, the four
 * pressures in one row under the tab and no temperatures, as the 800 x 286 board draws it. Its type
 * follows the box's height, and a pressure that would not fit its cell is set smaller rather than cut.
 */
function tyreBox(prefix: string, box: Rect, oneRow: boolean): Item[] {
  const amber = ds.color.caution.primary;
  const fs = oneRow ? Math.min(1, box.height / TYRE_BOX.oneRow.height, box.width / TYRE_BOX.oneRow.width) : Math.min(1, box.height / TYRE_BOX.height);
  const s = (n: number): number => Math.round(n * fs);
  const labelSize = s(LABEL_SIZE);
  const inner = rect(box.left + BORDER, box.top + BORDER, box.width - 2 * BORDER, box.height - 2 * BORDER);
  const tabWidth = runWidth('TYRE', labelSize, true) + 2 * s(TYRE.tab.pad);
  const tab = rect(box.left + (box.width - tabWidth) / 2, box.top, tabWidth, s(TYRE.tab.height));
  const items: Item[] = [
    band(`${prefix}box`, box, TRANSPARENT, { border: { color: amber, width: BORDER }, radius: RADIUS }),
    // The tab hangs from the top border, so only its foot is rounded.
    { kind: 'rect', name: `${prefix}tab`, rect: tab, backgroundColor: amber, border: { radius: { topLeft: 0, topRight: 0, bottomLeft: INSET_RADIUS, bottomRight: INSET_RADIUS } } },
    // Centred on the tab, less the pixel its line box would otherwise take above the band's top edge.
    label(`${prefix}tab.name`, 'TYRE', tab.left, Math.max(centredY(tab, labelSize), tab.top + Math.ceil(0.1 * labelSize)), tab.width, { size: labelSize, weight: 'Bold', color: ds.color.text.primary, hAlign: 'center' }),
  ];

  if (oneRow) {
    const row = rect(inner.left + s(10), inner.top + s(24), inner.width - 2 * s(10), inner.height - s(24) - s(4));
    const cellWidth = Math.floor(row.width / 4);
    const size = fitting(PRESSURE_WIDEST, s(TYRE.pressure), cellWidth);
    CORNERS.forEach((corner, i) => {
      const cell = rect(row.left + i * cellWidth, row.top, cellWidth, row.height);
      if (i > 0) items.push(band(`${prefix}grid.down${i}`, rect(cell.left, cell.top, 1, cell.height), carColour('divider')));
      items.push(pressureLabel(prefix, corner, i, cell, size));
    });
    return items;
  }

  const tempWidth = runWidth(TEMPERATURE_WIDEST, labelSize);
  const tempBottom = inner.top + inner.height - labelSize - 3;
  const cornerX = s(TYRE.corner.x);
  const corners: Record<Corner, { x: number; y: number; align: 'left' | 'right' }> = {
    FrontLeft: { x: inner.left + cornerX, y: inner.top + s(TYRE.corner.y), align: 'left' },
    FrontRight: { x: inner.left + inner.width - cornerX - tempWidth, y: inner.top + s(TYRE.corner.y), align: 'right' },
    RearLeft: { x: inner.left + cornerX, y: tempBottom, align: 'left' },
    RearRight: { x: inner.left + inner.width - cornerX - tempWidth, y: tempBottom, align: 'right' },
  };
  // The grid is 52 px in on the reference and less in a narrower box, by `gen_sizes.py`'s rule, so
  // that its two columns keep 76 px each.
  const gridX = Math.max(10, Math.min(s(TYRE.grid.x), Math.floor((box.width - 2 * s(76)) / 2) - 4));
  const grid = rect(inner.left + gridX, inner.top + s(TYRE.grid.top), inner.width - 2 * gridX, inner.height - s(TYRE.grid.top) - s(TYRE.grid.bottom));
  const cellWidth = Math.floor(grid.width / 2);
  const cellHeight = Math.floor(grid.height / 2);
  const size = fitting(PRESSURE_WIDEST, s(TYRE.pressure), cellWidth);
  items.push(
    band(`${prefix}grid.across`, rect(grid.left, grid.top + cellHeight, grid.width, 1), carColour('divider')),
    band(`${prefix}grid.down`, rect(grid.left + cellWidth, grid.top, 1, grid.height), carColour('divider')),
  );
  CORNERS.forEach((corner, i) => {
    const at = corners[corner];
    items.push(
      label(`${prefix}temp.${corner}`, ['78', '81', '84', '86'][i]!, at.x, at.y, tempWidth, {
        size: labelSize,
        color: ds.color.text.primary,
        hAlign: at.align,
        bind: fmt(tyreTemperature(corner), '0'),
        widest: TEMPERATURE_WIDEST,
      }),
    );
    items.push(pressureLabel(prefix, corner, i, rect(grid.left + (i % 2) * cellWidth, grid.top + Math.floor(i / 2) * cellHeight, cellWidth, cellHeight), size));
  });
  return items;
}

function footItems(frame: Rect, prefix: string): Item[] {
  const { footParts: parts } = porscheFaceForFoot(frame.width, frame.height);
  const at = (r: Rect): Rect => rect(frame.left + r.left, frame.top + r.top, r.width, r.height);
  const items: Item[] = [];

  if (parts.badge) {
    const badge = at(parts.badge);
    const words = badge.top + (badge.height - 2 * BADGE.line) / 2;
    const crest = setting.porscheCrest();
    const noCrest = eq(crest, str(''));
    items.push(
      withMoreBindings(
        {
          kind: 'rect',
          name: `${prefix}badge`,
          rect: badge,
          backgroundColor: TRANSPARENT,
          border: { color: carColour('badge'), top: 1, bottom: 1, left: 1, right: 1, radius: { topLeft: INSET_RADIUS, topRight: INSET_RADIUS, bottomLeft: badge.width / 2, bottomRight: badge.width / 2 } },
        },
        { Visible: noCrest },
      ),
      ...['your', 'badge'].map((word, i) =>
        label(`${prefix}badge.${word}`, word, badge.left, words + i * BADGE.line + (BADGE.line - BADGE.word) / 2, badge.width, { size: BADGE.word, color: carColour('badge'), hAlign: 'center', visibleBind: noCrest }),
      ),
      // The crest itself, from the user's own folder and never from the package (#714).
      withMoreBindings({ kind: 'imageFromFile', name: `${prefix}badge.crest`, rect: badge, backgroundColor: TRANSPARENT }, { ImagePath: crest }),
    );
  }

  const box = (name: string, part: FootBox | undefined, colour: Hex, title: string, id: string): Item[] => {
    if (!part) return [];
    const { value, reading } = settingReading(id);
    return [settingBox(`${prefix}${name}`, at(part.rect), colour, title, reading, hasSetting(value), part)];
  };
  items.push(
    ...box('tcLa', parts.tcLa, ds.color.danger.primary, 'TC-LA', 'tc'),
    ...box('tcLo', parts.tcLo, ds.color.good.primary, 'TC-LO', 'cut'),
    ...box('abs', parts.abs, ds.color.info.primary, 'ABS', 'abs'),
    ...tyreBox(`${prefix}tyre.`, at(parts.tyre.rect), parts.tyre.oneRow),
  );

  const { bias } = parts;
  const cell = at(bias.rect);
  const biasValue = rect(cell.left + cell.width - 4 - bias.valueWidth, cell.top + 4, bias.valueWidth, cell.height - 8);
  items.push(...namedCell(`${prefix}bias`, cell, 'Brake Bias', biasValue, bias.value, { sample: '54.5', bind: fmt(brakeBias(), '0.0'), widest: '88.8' }, bias.label));
  return items;
}

export const PORSCHE_FOOT: ThemeBandPage = { id: 'porscheFoot', name: 'Porsche', items: footItems };
