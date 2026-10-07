/**
 * The car's foot, as one more page of band D's catalogue (ADR 0015, #205): the badge's place, the TC
 * and ABS boxes, the tyre box with its yellow tab, and `Brake Bias` on a grey cell.
 *
 * Laid at the ticket's rectangles, which are the face's, less the band's top: the band runs the
 * whole width from x 0, so the page's x are the face's.
 *
 * **The badge is a place and not a mark.** The car shows the Porsche crest, a registered trade mark
 * that cannot ship in an MIT package (#194), so the page draws the canvas's placeholder: a shield
 * 54 by 62 at x 14, square at the top and round at the foot, outlined in the label grey with `your`
 * over `badge` in it at 10 px. The canvas dashes the outline and a SimHub border has no dash, so it
 * is solid; and it sits six pixels lower than the canvas's 404, because the face draws band D's `D`
 * at the band's left padding on every page and the shield's top would cross its foot.
 *
 * The two TC boxes read the two traction dials a GT3 car exposes, `TC` and `TC cut` in the house's
 * list of watched settings, where the car shows the same value in both; each is hidden on a car
 * that does not publish its dial, as the house's settings strip hides a cell. The tyre box is drawn
 * in `color.caution.primary` rather than the flag's yellow, because the box is never a flag and the
 * canvas draws it in the caution amber.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { hasSetting, trackedValue } from '../../second/tracked.ts';
import { brakeBias, CORNERS, tyrePressure, tyreTemperature, type Corner } from '../../second/values.ts';
import { ds, TRANSPARENT } from '../../tokens.ts';
import type { ThemeBandPage } from '../drawing.ts';
import { BORDER, carColour, centredY, INSET_RADIUS, LABEL_SIZE, namedCell, RADIUS, runWidth, settingBox } from './register.ts';

const { fmt } = ncalc;

/** The tyre box's figures: the tab, the corner temperatures and the pressures, from the ticket. */
const TYRE = { tab: { height: 26, pad: 14 }, corner: { x: 8, y: 2 }, grid: { x: 52, top: 26, bottom: 6 }, pressure: 32 };

/** The badge's placeholder, from the canvas: x 14, 54 by 62, radii 5 and 27, its words 10 px on a 12 px line. */
const BADGE = { left: 14, top: 80, width: 54, height: 62, word: 10, line: 12 };

/** `Brake Bias`'s cell and the value inside it. */
const BIAS_VALUE = { width: 120, size: 30 };

const settingReading = (id: string) => {
  const value = trackedValue(id);
  return { value, reading: { sample: value.sample, bind: fmt(value.read, value.pattern), widest: value.pattern === '0' ? '88' : '88.8' } };
};

function tyreBox(prefix: string, box: Rect): Item[] {
  const amber = ds.color.caution.primary;
  const inner = rect(box.left + BORDER, box.top + BORDER, box.width - 2 * BORDER, box.height - 2 * BORDER);
  const tabWidth = runWidth('TYRE', LABEL_SIZE, true) + 2 * TYRE.tab.pad;
  const tab = rect(box.left + (box.width - tabWidth) / 2, box.top, tabWidth, TYRE.tab.height);
  const tempWidth = runWidth('188', LABEL_SIZE);
  const tempBottom = inner.top + inner.height - LABEL_SIZE - 3;
  const corners: Record<Corner, { x: number; y: number; align: 'left' | 'right' }> = {
    FrontLeft: { x: inner.left + TYRE.corner.x, y: inner.top + TYRE.corner.y, align: 'left' },
    FrontRight: { x: inner.left + inner.width - TYRE.corner.x - tempWidth, y: inner.top + TYRE.corner.y, align: 'right' },
    RearLeft: { x: inner.left + TYRE.corner.x, y: tempBottom, align: 'left' },
    RearRight: { x: inner.left + inner.width - TYRE.corner.x - tempWidth, y: tempBottom, align: 'right' },
  };
  const grid = rect(inner.left + TYRE.grid.x, inner.top + TYRE.grid.top, inner.width - 2 * TYRE.grid.x, inner.height - TYRE.grid.top - TYRE.grid.bottom);
  const cellWidth = Math.floor(grid.width / 2);
  const cellHeight = Math.floor(grid.height / 2);
  const items: Item[] = [
    band(`${prefix}box`, box, TRANSPARENT, { border: { color: amber, width: BORDER }, radius: RADIUS }),
    // The tab hangs from the top border, so only its foot is rounded.
    { kind: 'rect', name: `${prefix}tab`, rect: tab, backgroundColor: amber, border: { radius: { topLeft: 0, topRight: 0, bottomLeft: INSET_RADIUS, bottomRight: INSET_RADIUS } } },
    // Centred on the tab, less the pixel its line box would otherwise take above the band's top edge.
    label(`${prefix}tab.name`, 'TYRE', tab.left, Math.max(centredY(tab, LABEL_SIZE), tab.top + Math.ceil(0.1 * LABEL_SIZE)), tab.width, { size: LABEL_SIZE, weight: 'Bold', color: ds.color.text.primary, hAlign: 'center' }),
    band(`${prefix}grid.across`, rect(grid.left, grid.top + cellHeight, grid.width, 1), carColour('divider')),
    band(`${prefix}grid.down`, rect(grid.left + cellWidth, grid.top, 1, grid.height), carColour('divider')),
  ];
  CORNERS.forEach((corner, i) => {
    const at = corners[corner];
    items.push(
      label(`${prefix}temp.${corner}`, ['78', '81', '84', '86'][i]!, at.x, at.y, tempWidth, {
        size: LABEL_SIZE,
        color: ds.color.text.primary,
        hAlign: at.align,
        bind: fmt(tyreTemperature(corner), '0'),
        widest: '188',
      }),
    );
    const cell = rect(grid.left + (i % 2) * cellWidth, grid.top + Math.floor(i / 2) * cellHeight, cellWidth, cellHeight);
    items.push(
      label(`${prefix}pressure.${corner}`, ['23.9', '23.9', '23.9', '24.0'][i]!, cell.left, centredY(cell, TYRE.pressure), cell.width, {
        size: TYRE.pressure,
        color: ds.color.text.primary,
        hAlign: 'center',
        bind: fmt(tyrePressure(corner), '0.0'),
        widest: '188.8',
      }),
    );
  });
  return items;
}

function footItems(frame: Rect, prefix: string): Item[] {
  const at = (left: number, top: number, width: number, height: number): Rect => rect(frame.left + left, frame.top + top, width, height);
  const badge = rect(frame.left + BADGE.left, frame.top + BADGE.top, BADGE.width, BADGE.height);
  const words = badge.top + (badge.height - 2 * BADGE.line) / 2;
  const tc = settingReading('tc');
  const cut = settingReading('cut');
  const abs = settingReading('abs');
  const bias = at(788, 54, 309, 50);
  const biasValue = rect(bias.left + bias.width - 4 - BIAS_VALUE.width, bias.top + 4, BIAS_VALUE.width, bias.height - 8);
  return [
    {
      kind: 'rect',
      name: `${prefix}badge`,
      rect: badge,
      backgroundColor: TRANSPARENT,
      border: { color: carColour('badge'), top: 1, bottom: 1, left: 1, right: 1, radius: { topLeft: INSET_RADIUS, topRight: INSET_RADIUS, bottomLeft: BADGE.width / 2, bottomRight: BADGE.width / 2 } },
    },
    ...['your', 'badge'].map((word, i) =>
      label(`${prefix}badge.${word}`, word, badge.left, words + i * BADGE.line + (BADGE.line - BADGE.word) / 2, badge.width, { size: BADGE.word, color: carColour('badge'), hAlign: 'center' }),
    ),
    settingBox(`${prefix}tcLa`, at(160, 0, 151, 56), ds.color.danger.primary, 'TC-LA', tc.reading, hasSetting(tc.value)),
    settingBox(`${prefix}tcLo`, at(318, 0, 151, 56), ds.color.good.primary, 'TC-LO', cut.reading, hasSetting(cut.value)),
    settingBox(`${prefix}abs`, at(236, 66, 153, 56), ds.color.info.primary, 'ABS', abs.reading, hasSetting(abs.value)),
    ...tyreBox(`${prefix}tyre.`, at(475, 0, 291, 142)),
    ...namedCell(`${prefix}bias`, bias, 'Brake Bias', biasValue, BIAS_VALUE.size, { sample: '54.5', bind: fmt(brakeBias(), '0.0'), widest: '88.8' }),
  ];
}

export const PORSCHE_FOOT: ThemeBandPage = { id: 'porscheFoot', name: 'Porsche', items: footItems };

