/**
 * What the car draws around its three body regions: the grey panels zones B and C are inset in, the
 * outlined tile behind the gear, the column of coloured setting boxes down the left edge and the
 * column of telltales down the right. The zones themselves are the house's widgets cycling the
 * house's pages, drawn on the panel's grey and the tile's black by `zoneGround`.
 *
 * **The settings column shows three boxes, not the car's four.** The car's MAP, AC, THR and FC1 are
 * the user's to choose on the panel in #205, and the contract has no per-zone setting that picks a
 * field, so the column is fixed for now and the choice is a follow-up. Of the settings openDash
 * reads, it carries the three the car puts in that column and the foot does not: the engine map, the
 * throttle map and the rear anti-roll bar, which the 1280 x 720 board lists among the column's eight.
 * AC and FC1 have no reading openDash can name, and a box bound to nothing would be a box that never
 * lights. Each box is hidden while the car does not publish its setting.
 *
 * **The telltale column carries band D's three corner lamps.** The car's pictograms have no artwork
 * in the repository, and the band at this size draws no corner blocks (`bandCorners` below), so DRS,
 * push to pass and the spotter move here, drawn as the band draws them: a word in a chip, dim until
 * lit. That keeps on the face what the corner blocks carried, the track state having moved into the
 * strip's teal box.
 */
import type { Hex, Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { hasSetting, trackedValue } from '../../second/tracked.ts';
import { ds, TRANSPARENT } from '../../tokens.ts';
import { cornerLamps } from '../../zones/bandPages.ts';
import { zoneRect } from '../anatomy.ts';
import type { FaceContext } from '../drawing.ts';
import { BORDER, carColour, centredY, RADIUS, settingBox } from './register.ts';

const { fmt, iff, str } = ncalc;

/** A grey panel holds its zone four pixels in; the gear's tile holds it inside its three-pixel border. */
const PANEL_PAD = 4;

/** The settings column: 9 px from the edge, 146 wide, boxes 48 tall and 10 apart from the top of the body. */
const COLUMN = { left: 9, width: 146, box: 48, gap: 10 };

/** The telltale column, centred on x 1170 between the body and the edge, and how its chips are drawn. */
const TELLTALES = { left: 1147, width: 46, chip: 32, inset: 8, size: 13 };

/** The three, in the colours the 1280 x 720 board gives them. */
const COLUMN_SETTINGS: readonly { id: string; title: string; colour: () => Hex }[] = [
  { id: 'map', title: 'MAP', colour: () => ds.color.good.primary },
  { id: 'slip', title: 'THR', colour: () => ds.color.caution.primary },
  { id: 'diff', title: 'ARB R', colour: () => ds.color.info.primary },
];

const outset = (r: Rect, by: number): Rect => rect(r.left - by, r.top - by, r.width + 2 * by, r.height + 2 * by);

/** The panel a zone is inset in, which the takeovers and the change notification fill. */
export const panelOf = (ctx: FaceContext, zone: 'B' | 'C'): Rect => outset(zoneRect(ctx.regions, zone), PANEL_PAD);

export function porscheBody(ctx: FaceContext): Item[] {
  const gear = zoneRect(ctx.regions, 'A');
  const tile = rect(gear.left - BORDER, gear.top - PANEL_PAD, gear.width + 2 * BORDER, gear.height + 2 * PANEL_PAD);
  const items: Item[] = [
    band('body.panelB', panelOf(ctx, 'B'), carColour('panel'), { radius: RADIUS }),
    band('body.tileA', tile, carColour('tile'), { border: { color: carColour('edge'), width: BORDER }, radius: RADIUS }),
    band('body.panelC', panelOf(ctx, 'C'), carColour('panel'), { radius: RADIUS }),
  ];

  const top = tile.top;
  const fits = Math.floor((tile.height + COLUMN.gap) / (COLUMN.box + COLUMN.gap));
  COLUMN_SETTINGS.slice(0, fits).forEach((setting, i) => {
    const value = trackedValue(setting.id);
    const frame = rect(COLUMN.left, top + i * (COLUMN.box + COLUMN.gap), COLUMN.width, COLUMN.box);
    items.push(settingBox(`settings.${setting.id}`, frame, setting.colour(), setting.title, { sample: value.sample, bind: fmt(value.read, value.pattern), widest: '88' }, hasSetting(value)));
  });

  // Spaced from the top of the column to its foot as the car spaces its pictograms.
  const lamps = cornerLamps();
  const columnTop = top + TELLTALES.inset;
  const columnHeight = tile.height - 2 * TELLTALES.inset;
  const pitch = (columnHeight - TELLTALES.chip) / Math.max(1, lamps.length - 1);
  lamps.forEach((lamp, i) => {
    const chip = rect(TELLTALES.left, Math.round(columnTop + i * pitch), TELLTALES.width, TELLTALES.chip);
    const ink = iff(lamp.on, str(lamp.colour), str(carColour('panel')));
    items.push(
      band(`telltales.${lamp.id}.chip`, chip, TRANSPARENT, { border: { color: carColour('panel'), width: 2, colorBind: ink }, radius: RADIUS }),
      label(`telltales.${lamp.id}`, lamp.text, chip.left, centredY(chip, TELLTALES.size), chip.width, {
        size: TELLTALES.size,
        hAlign: 'center',
        color: carColour('panel'),
        colorBind: ink,
      }),
    );
  });
  return items;
}
