/**
 * Band D's page D8: the twelve telltale lamps, in the order the 1280 x 480 artboard draws them.
 *
 * A lamp is a 38 by 32 box with a 1 px border and a 20 px pictogram centred in it, and the border
 * and the pictogram carry one colour between them, exactly as the DRS, push to pass and spotter
 * chips in the band's right corner do: an outline left bright around a dark pictogram would read as
 * a lamp half on. The rank is `when: 'dim'` rather than `when: 'close'`, which is the rule
 * docs/design/zones.md §11 states: a lamp coming on is a change of colour and not of layout,
 * because one that vanished and returned would move every lamp beside it at the moment the driver
 * most needs to read them.
 *
 * Two things the artboard draws are not drawn here, and both are absences rather than refusals.
 *
 * **The pictograms are not in the repository.** An `ImageItem` carries no tint, which is verified in
 * docs/research/simhub-dash-format.md, so a lamp that is amber lit and grey dark is two files and
 * not one file recoloured, hidden by complementary `Visible` expressions. The files are Material
 * Design Icons, which `design/assets.ts` records as owed and which arrive with their Apache 2.0
 * notice in the commit that brings them. {@link iconItems} therefore asks the registry for each
 * file it wants and draws whichever of them the registry already holds, so the lamps gain their
 * pictograms the moment the artwork lands and nothing here has to change.
 *
 * **Nine of the twelve have no source.** Which property lights a wiper, a stability lamp, a tyre
 * pressure warning or a door is a decision the canvas has not taken, and iRacing publishes nothing
 * for any of them; the lamps are built in their places and stay dark, which asserts nothing, rather
 * than being bound to a property that means something else. §10 of zones.md carries the list.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { flagBox } from '../contract.ts';
import { assetBox, assetNamed, imageOf } from '../design/assets.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { boxSlack, cells, monoWidth, textBox, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { numeral } from '../elements/numeral.ts';
import { rank, type RankMember } from '../second/rank.ts';
import { ds, TRANSPARENT } from '../tokens.ts';
import { tankIsLow } from '../second/values.ts';

const { computed, div, eq, game, gt, iff, isnull, lt, mod, num, or, raw, str, truncate } = ncalc;

/** The page of band D this rank is, which is the id the contract gives it. */
export const TELLTALE_PAGE = 'car';

/**
 * The colour a lamp lights in, as `purpose.telltale` names it.
 *
 * `off` is not one of them. It is what every lamp is drawn in before anything lights it, and it is
 * the ink rather than the edge: the box around a dark lamp is `surface.raised`, which is a shade
 * further back again, so that a rank of twelve dark lamps reads as a row of empty boxes and not as
 * a row of grey ones.
 */
export type TelltaleColour = Exclude<keyof typeof ds.purpose.telltale, 'off'>;

/** One lamp of the rank. */
export interface Telltale {
  id: string;
  /**
   * The colour the artboard draws this lamp lit in, where it draws it lit at all.
   *
   * A lamp can carry a colour and no source, which is the state of the first four: the drawing says
   * what blue or amber would mean and nothing yet says when. The colour is inert until a source
   * arrives, and recording it here is what keeps the answer in one place when one does.
   */
  lit?: TelltaleColour;
  /** True while the lamp is lit. Undefined where nothing the sim publishes says so. */
  on?: Expr;
  /**
   * A count in the lamp's bottom-right corner, drawn only while the lamp is lit.
   *
   * The artboard draws one on the wiper, which is the lamp nothing publishes a state for, so
   * nothing is drawn there today: a count under a dark lamp would be a number nobody measured, and
   * the band already leaves the relative page's country flag undrawn for the same reason. What is
   * recorded here is the artboard's own figure, so that the count arrives with the source rather
   * than after it.
   */
  count?: string;
}

/**
 * iRacing's `EngineWarnings` bit field: 1 water temperature, 2 fuel pressure, 4 oil pressure,
 * 8 stalled, 16 pit limiter, 32 rev limiter, recorded in docs/research/simhub-led-sources.md.
 *
 * NCalc has no bitwise operators, so a bit is read by dividing and taking the remainder, which is
 * how `second/values.ts` already reads `PitSvFlags`.
 */
export const ENGINE_WARNING_BITS = { waterTemperature: 1, fuelPressure: 2, oilPressure: 4, stalled: 8, pitLimiter: 16, revLimiter: 32 } as const;

const engineWarning = (bit: number): Expr => gt(mod(truncate(div(isnull(raw('EngineWarnings'), num(0)), num(bit))), num(2)), num(0));

/**
 * The twelve lamps, in the order the 1280 x 480 artboard draws them.
 *
 * The order is the artboard's and is not an importance order, which matters when a band too narrow
 * for twelve sheds from the tail: what goes first is the car door and the tyre pressure, which is
 * also what a driver would give up first, so the two agree and no shedding order is declared.
 */
export const TELLTALES: readonly Telltale[] = [
  // The two tyre lamps and the two beside them are drawn lit on the artboard and bound to nothing:
  // see the file comment, and §10 of zones.md.
  { id: 'tyreLines', lit: 'info' },
  { id: 'tyreSlant', lit: 'good' },
  { id: 'wiper', lit: 'caution', count: '2' },
  { id: 'surface', lit: 'caution' },
  { id: 'abs' },
  { id: 'esp' },
  // Water temperature and oil pressure are the two engine faults iRacing publishes as bits. Both
  // are failures rather than advisories, which is why the lamp is danger red and not the amber a
  // road car gives a check-engine light; the artboard draws this lamp dark and settles neither.
  { id: 'engine', lit: 'danger', on: or(engineWarning(ENGINE_WARNING_BITS.waterTemperature), engineWarning(ENGINE_WARNING_BITS.oilPressure)) },
  // The same threshold every other light openDash drives reads, per the contract's own note on
  // LightsLowFuelLaps: one number answers "am I low" for the strip, the rev bar, the box and now
  // the band. The remaining laps default high rather than to zero, so a sim that computes none
  // leaves the lamp dark instead of lighting it on every car that has no such reading.
  { id: 'fuel', lit: 'danger', on: tankIsLow() },
  { id: 'battery' },
  { id: 'limiter', lit: 'neutral', on: eq(isnull(game('PitLimiterOn'), num(0)), num(1)) },
  { id: 'pressure' },
  { id: 'door' },
];

/** The lamp box the artboard draws, its 1 px edge included. */
const LAMP = { width: 38, height: 32 };
const LAMP_BORDER = 1;
/** The pictogram inside it, which is a square whatever the file's own proportions turn out to be. */
const ICON = 20;
/** Gap between two lamps, which is the artboard's and wider than the 6 px the corner chips use. */
export const TELLTALE_GAP = 10;
/**
 * The count in a lamp's corner: 11 px, its right edge 2 px inside the box and its box flush with
 * the bottom of it, which is what the artboard draws.
 *
 * 11 px is smaller than `size.labelSm` and `design/tokens.json` carries no token for it, so the
 * number is here rather than read from one; a token would be the better home if the author wants
 * one, and nothing else on the face is set at this size.
 */
const COUNT_SIZE = 11;
const COUNT_RIGHT = 2;
const COUNT_CHARS: Chars = { digits: 1, specials: 0 };

/** The base every pictogram file is named from, so that a lamp's two states sort together. */
const ART = 'telltale-';

/** The file a lamp draws in one of its two states, whether or not the registry holds it yet. */
export const telltaleArt = (lamp: Telltale, state: TelltaleColour | 'off'): string => `${ART}${lamp.id}-${state}`;

/** Every pictogram the rank would draw if the registry held it, which is what a report of what is
 *  missing is read from. */
export const telltaleArtwork = (): string[] =>
  TELLTALES.flatMap((lamp) => (lamp.lit ? [telltaleArt(lamp, lamp.lit), telltaleArt(lamp, 'off')] : [telltaleArt(lamp, 'off')]));

/**
 * The rank drawn in `frame`, centred in the room `usable` leaves between the corner blocks.
 *
 * The lamp boxes are centred on the band rather than laid out from the block geometry the field
 * pages use: a lamp has no label over it, so there is no two-row block to ride up, and 32 px of box
 * in a 54 px band is centred by the same arithmetic at every size the faces draw.
 */
export function telltaleItems(frame: Rect, prefix: string, usable: { left: number; width: number }): Item[] {
  const top = frame.top + (frame.height - LAMP.height) / 2;
  const members = TELLTALES.map(
    (lamp): RankMember => ({
      id: lamp.id,
      width: LAMP.width,
      present: lamp.on,
      draw: (at) => {
        const box = rect(at.x, top, LAMP.width, LAMP.height);
        // A lamp is in its lit state only when the drawing gives it a colour and the sim gives it a
        // condition; either alone leaves it dark, and a dark lamp carries no binding at all.
        const state = lamp.lit !== undefined && at.litBind !== undefined ? { colour: ds.purpose.telltale[lamp.lit], on: at.litBind } : undefined;
        return [
          band(`${prefix}${lamp.id}.chip`, box, TRANSPARENT, {
            border: {
              color: ds.color.surface.raised,
              width: LAMP_BORDER,
              ...(state ? { colorBind: iff(state.on, str(state.colour), str(ds.color.surface.raised)) } : {}),
            },
          }),
          ...iconItems(lamp, `${prefix}${lamp.id}.icon`, box, state?.on),
          ...(lamp.count !== undefined && state
            ? [countItem(lamp.count, `${prefix}${lamp.id}.count`, box, iff(state.on, str(state.colour), str(ds.purpose.telltale.off)), state.on)]
            : []),
        ];
      },
    }),
  );
  return rank(members, { left: usable.left, width: usable.width, gap: TELLTALE_GAP, when: 'dim' }).items;
}

/**
 * The lamp's pictogram: one file per colour, each hidden by the other's condition.
 *
 * A lamp nothing lights draws its dark file alone and carries no binding at all, which is the same
 * economy the rank makes for a member that can never go missing. A file the registry does not hold
 * draws nothing, so the rank is the boxes until the artwork arrives.
 */
function iconItems(lamp: Telltale, name: string, box: Rect, lit: Expr | undefined): Item[] {
  const states: { state: TelltaleColour | 'off'; visible?: Expr }[] =
    lit === undefined || lamp.lit === undefined ? [{ state: 'off' }] : [{ state: lamp.lit, visible: lit }, { state: 'off', visible: ncalc.not(lit) }];
  return states.flatMap(({ state, visible }) => {
    const asset = assetNamed(telltaleArt(lamp, state));
    if (asset === undefined) return [];
    return [
      {
        kind: 'image' as const,
        name: `${name}.${state}`,
        image: asset.name,
        rect: roundRect(assetBox(box, imageOf(asset), { maxWidth: ICON, maxHeight: ICON })),
        ...withBindings({ Visible: visible }),
      },
    ];
  });
}

/** Width the count's box takes, computed the way `numeral` computes its own so that the right edge
 *  lands where the artboard puts it. */
const countWidth = (): number => monoWidth(cells('SemiBold', COUNT_SIZE), COUNT_CHARS) + boxSlack(COUNT_SIZE);

function countItem(sample: string, name: string, box: Rect, ink: Expr, lit: Expr): Item {
  const width = countWidth();
  // The box sits flush with the bottom of the lamp, so the line is placed from the box's own
  // height rather than from the size: a WPF line box opens a tenth of the size above its line.
  const y = box.top + LAMP.height - textBox(0, COUNT_SIZE).height + 0.1 * COUNT_SIZE;
  return numeral(name, sample, box.left + LAMP.width - COUNT_RIGHT - width, y, COUNT_SIZE, COUNT_CHARS, {
    color: ds.purpose.telltale.off,
    colorBind: ink,
    visibleBind: lit,
    width,
    hAlign: 'right',
  });
}
