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
 * and ABS from the foot, then the brake bias the 1280 x 720 board puts next in the column, and then,
 * for the eight that board's column holds, TC-LO from the foot and the two anti-roll bars the board
 * names after the bias. The foot is a page the plugin cannot reach yet, which is the second reason
 * its boxes belong here. As many as fit are drawn, stacked from the top at the column's fixed gap
 * as the ticket's size rule says, which is four on the 480 and 400 tall faces and eight at 720;
 * a box whose setting the car does not publish is hidden, and the ones under it move up into its
 * place, so that a car with two settings shows two boxes at the top and not two boxes with holes.
 *
 * **The telltale column** is the car's four pictograms, dim until lit: the lights, the warning, the
 * hazard and the tyre. Two have a reading: the warning lights on the engine faults iRacing publishes
 * as bits, which band D's car page lights its engine lamp on, and the hazard on the fuel alarm. The
 * lights and the tyre have none, iRacing publishing neither a headlight state nor a tyre warning, and
 * are drawn dim, as the band's telltales without a source are. The 1280 x 720 face's column holds
 * eight, and the four after the car's are the fuel pump on the fuel pressure bit, the charge lamp on
 * the stalled engine, the oil can on the oil pressure bit and the limiter's dial while it is on. Like
 * the settings column it stacks from the top at a fixed gap and shows as many as its height holds,
 * up to the face's own number: two on the 800 x 286, none on the portrait face, which has no column.
 */
import type { Hex, Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { hasSetting, trackedValue } from '../../second/tracked.ts';
import { ds, TRANSPARENT } from '../../tokens.ts';
import { limiterOn } from '../../components/pitAlerts.ts';
import { antiRollFront } from '../../second/values.ts';
import type { TrackedValue } from '../../second/tracked.ts';
import { ENGINE_WARNING_BITS } from '../../zones/telltales.ts';
import { zoneRect } from '../anatomy.ts';
import type { FaceContext } from '../drawing.ts';
import { porscheFace, type SettingsColumn, type TelltaleColumn } from './geometry.ts';
import { pictogram, type Lit, type Pictogram } from './pictograms.ts';
import { BORDER, carColour, lowFuelAlarm, RADIUS, settingBox } from './register.ts';

const { add, div, fmt, gt, iff, isnull, lt, mod, mul, num, raw, truncate, and, or } = ncalc;

/** A zone fills the inside of its 3 px border, so its cells stand flush against it. */
const PANEL_PAD = BORDER;

/** The column's list, in the order it fills, with the colours the car gives each. */
const COLUMN_SETTINGS: readonly { id: string; title: string; colour: () => Hex; value?: () => TrackedValue }[] = [
  { id: 'map', title: 'MAP', colour: () => ds.color.good.primary },
  { id: 'slip', title: 'THR', colour: () => ds.color.caution.primary },
  { id: 'tc', title: 'TC-LA', colour: () => ds.color.danger.primary },
  { id: 'abs', title: 'ABS', colour: () => ds.color.info.primary },
  { id: 'bias', title: 'BIAS', colour: () => ds.color.danger.primary },
  { id: 'cut', title: 'TC-LO', colour: () => ds.color.good.primary },
  // The front bar is no tracked value, having no change notification of its own, so its reading is
  // written here in the shape of one; the rear bar is the house's `diff`, which reads it.
  { id: 'arbFront', title: 'ARB F', colour: () => ds.color.good.primary, value: () => ({ id: 'arbFront', strip: 'ARB F', notice: 'ARB front', sample: '3', read: antiRollFront(), pattern: '0' }) },
  { id: 'diff', title: 'ARB R', colour: () => ds.color.info.primary },
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

/**
 * Every item of a box moved to slot `slots`, the static `rect` being the box at slot `at`: a box past
 * the column's end is drawn at its last slot, so that no box stands outside the face in the editor.
 */
const atSlot = (item: Item, slots: Expr, at: number, pitch: number): Item => {
  if (item.kind === 'layer') return { ...item, children: item.children.map((child) => atSlot(child, slots, at, pitch)) };
  return withMoreBindings(item, { Top: add(num(item.rect.top - at * pitch), mul(slots, num(pitch))) });
};

function settingsColumn(column: SettingsColumn, top: number, height: number): Item[] {
  const pitch = column.box + column.gap;
  const fits = Math.min(COLUMN_SETTINGS.length, Math.floor((height + column.gap) / pitch));
  if (fits < 1) return [];
  const settings = COLUMN_SETTINGS.map((setting) => ({ ...setting, value: setting.value?.() ?? trackedValue(setting.id) }));
  const present = settings.map(({ value }) => iff(hasSetting(value), num(1), num(0)));
  return settings.map((setting, place) => {
    // How many of the boxes before this one are drawn, which is the slot this one takes.
    const before = place === 0 ? num(0) : add(...present.slice(0, place));
    const at = Math.min(place, fits - 1);
    const frame = rect(column.left, top + at * pitch, column.width, column.box);
    const widest = setting.value.pattern === '0' ? '88' : '88.8';
    const box = settingBox(`settings.${setting.id}`, frame, setting.colour(), setting.title, { sample: setting.value.sample, bind: fmt(setting.value.read, setting.value.pattern), widest }, undefined, column);
    return withMoreBindings(atSlot(box, before, at, pitch), { Visible: and(hasSetting(setting.value), lt(before, num(fits))) });
  });
}

/** The column's pictograms in the order it fills, each with what lights it. */
const TELLTALE_LIST = (): readonly { id: string; pictogram: Pictogram; lit?: Lit }[] => [
  { id: 'lights', pictogram: 'lights' },
  { id: 'warning', pictogram: 'warning', lit: { on: or(engineWarning(ENGINE_WARNING_BITS.waterTemperature), engineWarning(ENGINE_WARNING_BITS.oilPressure)), state: 'lit' } },
  { id: 'hazard', pictogram: 'hazard', lit: { on: lowFuelAlarm(), state: 'lit' } },
  { id: 'tyre', pictogram: 'tyre' },
  { id: 'fuel', pictogram: 'fuel', lit: { on: engineWarning(ENGINE_WARNING_BITS.fuelPressure), state: 'lit' } },
  { id: 'battery', pictogram: 'battery', lit: { on: engineWarning(ENGINE_WARNING_BITS.stalled), state: 'lit' } },
  { id: 'oil', pictogram: 'oil', lit: { on: engineWarning(ENGINE_WARNING_BITS.oilPressure), state: 'lit' } },
  { id: 'limiter', pictogram: 'limiter', lit: { on: limiterOn(), state: 'lit' } },
];

function telltaleColumn(column: TelltaleColumn, top: number, height: number): Item[] {
  const list = TELLTALE_LIST();
  const room = height - 2 * column.inset;
  const count = Math.min(column.most, list.length, Math.floor((room + column.gap) / (column.icon + column.gap)));
  const box = (i: number): Rect => rect(column.left, top + column.inset + i * (column.icon + column.gap), column.width, column.icon);
  return list.slice(0, count).flatMap((telltale, i) => pictogram(`telltales.${telltale.id}`, telltale.pictogram, box(i), telltale.lit));
}

export function porscheBody(ctx: FaceContext): Item[] {
  const tile = tileOf(ctx);
  const face = porscheFace(ctx.layout);
  return [
    band('body.panelB', panelOf(ctx, 'B'), TRANSPARENT, { border: { color: carColour('panel'), width: BORDER }, radius: RADIUS }),
    band('body.tileA', tile, carColour('tile'), { border: { color: carColour('edge'), width: BORDER }, radius: RADIUS }),
    band('body.panelC', panelOf(ctx, 'C'), TRANSPARENT, { border: { color: carColour('panel'), width: BORDER }, radius: RADIUS }),
    ...(face.settings ? settingsColumn(face.settings, tile.top, tile.height) : []),
    ...(face.telltales ? telltaleColumn(face.telltales, tile.top, tile.height) : []),
  ];
}
