/**
 * What the car draws around its three body regions: the grey outlines zones B and C are inset in, the
 * outlined tile behind the gear, the column of coloured setting boxes down the left edge and the
 * column of telltale pictograms down the right. The zones themselves are the house's widgets cycling
 * the house's pages, drawn on the panel's grey and the tile's black by `zoneGround`.
 *
 * **The settings column.** The car's MAP, AC, THR and FC1 are the user's to choose on the panel in
 * #205, and the contract has no per-zone setting that picks a field, so the column's list is fixed
 * for now and the choice is a follow-up. The list is the 992 GT3 R's row of the per-car table, in its
 * order, less the two settings openDash has no reading for: MAP and THR from the column, then TC-LA
 * and ABS from the foot, then the brake bias the 1280 x 720 board puts next in the column. The foot
 * is a page the plugin cannot reach yet, which is the second reason its boxes belong here. As many
 * as fit are drawn, stacked from the top at the column's fixed gap as the ticket's size rule says;
 * a box whose setting the car does not publish is hidden, and the ones under it move up into its
 * place, so that a car with two settings shows two boxes at the top and not two boxes with holes.
 *
 * **The telltale column** is the car's four pictograms, dim until lit: the lights, the warning, the
 * hazard and the tyre. Two have a reading: the warning lights on the engine faults iRacing publishes
 * as bits, which band D's car page lights its engine lamp on, and the hazard on the fuel alarm. The
 * lights and the tyre have none, iRacing publishing neither a headlight state nor a tyre warning, and
 * are drawn dim, as the band's telltales without a source are.
 */
import type { Hex, Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { hasSetting, trackedValue } from '../../second/tracked.ts';
import { ds, TRANSPARENT } from '../../tokens.ts';
import { ENGINE_WARNING_BITS } from '../../zones/telltales.ts';
import { zoneRect } from '../anatomy.ts';
import type { FaceContext } from '../drawing.ts';
import { pictogram } from './pictograms.ts';
import { BORDER, carColour, lowFuelAlarm, RADIUS, settingBox } from './register.ts';

const { add, div, fmt, gt, iff, isnull, lt, mod, mul, num, raw, truncate, and, or } = ncalc;

/** A zone fills the inside of its 3 px border, so its cells stand flush against it. */
const PANEL_PAD = BORDER;

/** The settings column: 9 px from the edge, 146 wide, boxes 48 tall and 10 apart from the top of the body. */
const COLUMN = { left: 9, width: 146, box: 48, gap: 10 };

/** The telltale column: 46 wide at x 1147, its pictograms 32 tall and inset 8 from the body's ends. */
const TELLTALES = { left: 1147, width: 46, size: 32, inset: 8 };

/** The column's list, in the order it fills, with the colours the car gives each. */
const COLUMN_SETTINGS: readonly { id: string; title: string; colour: () => Hex }[] = [
  { id: 'map', title: 'MAP', colour: () => ds.color.good.primary },
  { id: 'slip', title: 'THR', colour: () => ds.color.caution.primary },
  { id: 'tc', title: 'TC-LA', colour: () => ds.color.danger.primary },
  { id: 'abs', title: 'ABS', colour: () => ds.color.info.primary },
  { id: 'bias', title: 'BIAS', colour: () => ds.color.danger.primary },
];

const outset = (r: Rect, by: number): Rect => rect(r.left - by, r.top - by, r.width + 2 * by, r.height + 2 * by);

/** The panel a zone is inset in, which the takeovers and the change notification fill. */
export const panelOf = (ctx: FaceContext, zone: 'B' | 'C'): Rect => outset(zoneRect(ctx.regions, zone), PANEL_PAD);

/** The tile behind the gear, its border three pixels outside zone A and its ends four. */
const tileOf = (ctx: FaceContext): Rect => {
  const gear = zoneRect(ctx.regions, 'A');
  return rect(gear.left - BORDER, gear.top - PANEL_PAD, gear.width + 2 * BORDER, gear.height + 2 * PANEL_PAD);
};

/** One of iRacing's `EngineWarnings` bits, read by dividing and taking the remainder, as the band's telltales read it. */
const engineWarning = (bit: number): Expr => gt(mod(truncate(div(isnull(raw('EngineWarnings'), num(0)), num(bit))), num(2)), num(0));

/** Every item of a box moved down by `slots` places, the static `rect` being the box's place in the list. */
const atSlot = (item: Item, slots: Expr, place: number, pitch: number): Item => {
  if (item.kind === 'layer') return { ...item, children: item.children.map((child) => atSlot(child, slots, place, pitch)) };
  return withMoreBindings(item, { Top: add(num(item.rect.top - place * pitch), mul(slots, num(pitch))) });
};

function settingsColumn(top: number, height: number): Item[] {
  const pitch = COLUMN.box + COLUMN.gap;
  const fits = Math.floor((height + COLUMN.gap) / pitch);
  const settings = COLUMN_SETTINGS.map((setting) => ({ ...setting, value: trackedValue(setting.id) }));
  const present = settings.map(({ value }) => iff(hasSetting(value), num(1), num(0)));
  return settings.map((setting, place) => {
    // How many of the boxes before this one are drawn, which is the slot this one takes.
    const before = place === 0 ? num(0) : add(...present.slice(0, place));
    const frame = rect(COLUMN.left, top + place * pitch, COLUMN.width, COLUMN.box);
    const widest = setting.value.pattern === '0' ? '88' : '88.8';
    const box = settingBox(`settings.${setting.id}`, frame, setting.colour(), setting.title, { sample: setting.value.sample, bind: fmt(setting.value.read, setting.value.pattern), widest });
    return withMoreBindings(atSlot(box, before, place, pitch), { Visible: and(hasSetting(setting.value), lt(before, num(fits))) });
  });
}

function telltaleColumn(top: number, height: number): Item[] {
  const columnTop = top + TELLTALES.inset;
  const pitch = (height - 2 * TELLTALES.inset - TELLTALES.size) / 3;
  const box = (i: number): Rect => rect(TELLTALES.left, Math.round(columnTop + i * pitch), TELLTALES.width, TELLTALES.size);
  const engine = or(engineWarning(ENGINE_WARNING_BITS.waterTemperature), engineWarning(ENGINE_WARNING_BITS.oilPressure));
  return [
    ...pictogram('telltales.lights', 'lights', box(0)),
    ...pictogram('telltales.warning', 'warning', box(1), { on: engine, state: 'lit' }),
    ...pictogram('telltales.hazard', 'hazard', box(2), { on: lowFuelAlarm(), state: 'lit' }),
    ...pictogram('telltales.tyre', 'tyre', box(3)),
  ];
}

export function porscheBody(ctx: FaceContext): Item[] {
  const tile = tileOf(ctx);
  return [
    band('body.panelB', panelOf(ctx, 'B'), TRANSPARENT, { border: { color: carColour('panel'), width: BORDER }, radius: RADIUS }),
    band('body.tileA', tile, carColour('tile'), { border: { color: carColour('edge'), width: BORDER }, radius: RADIUS }),
    band('body.panelC', panelOf(ctx, 'C'), TRANSPARENT, { border: { color: carColour('panel'), width: BORDER }, radius: RADIUS }),
    ...settingsColumn(tile.top, tile.height),
    ...telltaleColumn(tile.top, tile.height),
  ];
}
