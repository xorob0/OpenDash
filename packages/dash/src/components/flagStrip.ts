/**
 * flagStrip: one Layer per flag, sharing the band anatomy and differing in colour, label and
 * behaviour. One shows at a time in priority order black, chequered, yellow, blue, white, green;
 * nothing is drawn when no flag is out. Black is outlined, since a black band on this face is
 * invisible; chequered is a hard-edged check pattern (half the band high) with no label; yellow
 * flashes at 2 Hz. The nano's 12 px strip is too thin for a label, so its style drops the labels
 * and thins the black outline to 2 px.
 */
import type { FontWeight, Hex, Item, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { FACE_FLAG_PRIORITY, type FaceFlag } from '../flags.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

const { game, eq, and, num } = ncalc;

/**
 * The band's border, which every artboard draws on every state as `border: 3px solid`, counted
 * inside the box. The black flag draws it in the flag colour, because a dark band on a dark dash
 * needs an edge to read as a band; a coloured state draws it in its own fill, where the artboards
 * leave it transparent over the same fill, which is the same three pixels of that colour.
 */
export const BLACK_FLAG_BORDER = 3;

/** The weight the artboards set the flag name in, against the 500 of every other label. */
export const FLAG_NAME_WEIGHT: FontWeight = 'Bold';

/** How a strip is dressed: whether the flag name is drawn on it, and how thick the black outline is. */
export interface FlagStripStyle {
  /** Draw "YELLOW FLAG" and the like centred on the band. */
  labels: boolean;
  /** Border of the black flag's outline. */
  blackBorder: number;
}

export const FLAG_STRIP_STYLES = {
  standard: { labels: true, blackBorder: BLACK_FLAG_BORDER },
  /** The nano's 12 px strip: colour only, 2 px outline (from the canvas). */
  nano: { labels: false, blackBorder: 2 },
} as const satisfies Record<string, FlagStripStyle>;

/** Half period of the yellow flag's flash in ms (250 at 2 Hz). */
export const FLAG_BLINK_MS = Math.round(1000 / ds.indicator.flagBand.flashHz / 2);

/**
 * SimHub flag properties in priority order, taken from `FLAG_CATALOGUE` rather than restated: the
 * face and the flag box rank the same conditions, and two lists would eventually disagree about
 * which of two live flags wins.
 *
 * The face draws the six SimHub normalises. The box draws the whole catalogue, because
 * `Flag_Yellow` folds four iRacing bits together and `Flag_Black` hides a furled black; see
 * flags.ts.
 */
export const FLAG_PRIORITY: readonly FaceFlag[] = FACE_FLAG_PRIORITY;
export type FlagProperty = FaceFlag;

/** `[Flag_X] = 1` and every higher-priority flag `= 0`. */
export function flagVisible(flag: FlagProperty): Expr {
  const index = FLAG_PRIORITY.indexOf(flag);
  const higher = FLAG_PRIORITY.slice(0, index).map((f) => eq(game(f), num(0)));
  return and(...higher, eq(game(flag), num(1)));
}

const labelY = (frame: Rect): number => frame.top + (frame.height - ds.size.label) / 2;

/**
 * The centred flag name, when the style draws one. It is the one label on a face drawn in Bold
 * rather than Medium, which is what every FaceVariants sheet sets it in: the band is read at a
 * glance and from further away than a field label is.
 */
const flagLabel = (frame: Rect, style: FlagStripStyle, name: string, text: string, color: Hex): Item[] =>
  style.labels ? [label(name, text, frame.left, labelY(frame), frame.width, { color, hAlign: 'center', weight: FLAG_NAME_WEIGHT })] : [];

function solidFlag(frame: Rect, style: FlagStripStyle, prefix: string, id: string, flag: FlagProperty, color: Hex, text: string, blink: boolean): LayerItem {
  return {
    kind: 'layer',
    name: `${prefix}.${id}`,
    children: [
      band(`${prefix}.${id}.band`, frame, color, { border: { color, width: BLACK_FLAG_BORDER } }),
      ...flagLabel(frame, style, `${prefix}.${id}.label`, text, ds.purpose.flag.onFlag),
    ],
    ...(blink ? { blink: { enabled: true, delayMs: FLAG_BLINK_MS } } : {}),
    ...withBindings({ Visible: flagVisible(flag) }),
  };
}

/**
 * The black flag, filled like the other five.
 *
 * It was transparent with only a border, which left band D's page fully readable underneath the
 * most serious thing the band can say. A flag takes the band over, so it has to leave nothing of
 * the page showing.
 *
 * The ground is `surface.base` and not `purpose.flag.black`, which is `#F5F7FA` and is the ink
 * rather than the ground: a band filled with it would be indistinguishable from the white flag at
 * `#FFFFFF`, and two states a driver cannot tell apart is a bug whoever chose the colours. So this
 * flag is light on dark where the others are dark on light, which is also what a black flag looks
 * like. The border stays, because a dark band on a dark dash needs an edge to read as a band.
 */
function blackFlag(frame: Rect, style: FlagStripStyle, prefix: string): LayerItem {
  return {
    kind: 'layer',
    name: `${prefix}.black`,
    children: [
      band(`${prefix}.black.band`, frame, ds.color.surface.base, { border: { color: ds.purpose.flag.black, width: style.blackBorder } }),
      ...flagLabel(frame, style, `${prefix}.black.label`, 'BLACK FLAG', ds.purpose.flag.black),
    ],
    ...withBindings({ Visible: flagVisible('Flag_Black') }),
  };
}

function chequeredFlag(frame: Rect, prefix: string): LayerItem {
  const check = frame.height / 2;
  const columns = Math.ceil(frame.width / check);
  const children: Item[] = [band(`${prefix}.chequered.band`, frame, ds.color.surface.base)];
  for (let row = 0; row < 2; row++) {
    for (let col = row % 2; col < columns; col += 2) {
      const width = Math.min(check, frame.left + frame.width - (frame.left + col * check));
      children.push(
        band(`${prefix}.chequered.r${row}c${String(col).padStart(2, '0')}`, rect(frame.left + col * check, frame.top + row * check, width, check), ds.purpose.flag.chequer),
      );
    }
  }
  return { kind: 'layer', name: `${prefix}.chequered`, children, ...withBindings({ Visible: flagVisible('Flag_Checkered') }) };
}

export function flagStrip(frame: Rect, style: FlagStripStyle = FLAG_STRIP_STYLES.standard, prefix = 'flag'): Item[] {
  return [
    blackFlag(frame, style, prefix),
    chequeredFlag(frame, prefix),
    solidFlag(frame, style, prefix, 'yellow', 'Flag_Yellow', ds.purpose.flag.yellow, 'YELLOW FLAG', true),
    solidFlag(frame, style, prefix, 'blue', 'Flag_Blue', ds.purpose.flag.blue, 'BLUE FLAG', false),
    solidFlag(frame, style, prefix, 'white', 'Flag_White', ds.purpose.flag.white, 'WHITE FLAG', false),
    solidFlag(frame, style, prefix, 'green', 'Flag_Green', ds.purpose.flag.green, 'GREEN FLAG', false),
  ];
}
