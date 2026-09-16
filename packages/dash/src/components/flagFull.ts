/**
 * flagFull: the flag drawn over the whole body, which is the second of the two formats
 * `OpenDash.<Face>FlagFormat` chooses between.
 *
 * The band format gives a flag the sixty pixels of band D and leaves the rest of the face alone.
 * This one gives it zones B, A and C together, for the driver who wants a flag that cannot be
 * missed and accepts the price, which is the gear: the block is opaque and is drawn over the zone
 * widgets, so for as long as a flag is out zone A is not readable. The rev bar, the bar of settled
 * values and band D are outside the block and keep drawing.
 *
 * It is a sibling of `flagStrip` rather than a mode of it, and it copies that file's six states
 * rather than sharing them: the same priority, the same colours, the black light on dark, the
 * chequer as a half-height board with no name, and the yellow flashing an opaque block over its
 * fill rather than blinking the layer away. Where the two differ is the rectangle and the type
 * size, and the type size is the reason this is not a parameter on the other one: a name that is
 * one label row high is measured against a sixty pixel band, and a name that is nearly half the
 * face high has to be measured down until it fits the face it is on.
 *
 * The frame is handed in rather than computed here, because a component is a function of a
 * rectangle: `bodyRect` in `zones/layout.ts` is what every face passes, and the same component
 * therefore serves the portrait face, which stacks its zones, and the nano, which has no bar.
 */
import type { Hex, Item, LayerItem, Rect, RectangleItem } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { numeral } from '../elements/numeral.ts';
import { ds } from '../tokens.ts';
import { BLACK_FLAG_BORDER, FLAG_BLINK_MS, flagVisible, type FlagProperty } from './flagStrip.ts';

/**
 * The name's size as a fraction of the block's height.
 *
 * Read off the FaceVariants sheets rather than chosen, and not tabulated per face: they draw 143 px
 * in a 320 px block, 115 in 258, 248 in 554, 142 in 314 and 148 in 328, which is this one figure
 * five times over. `design/tokens.json` carries no entry for it, in the way it carries none for the
 * pop-up's width, so it is written here with the sheets as its citation.
 */
export const FLAG_FULL_NAME_RATIO = 0.447;

/** Room left either side of the name, which is what the sheets' size is measured down to fit. */
export const FLAG_FULL_NAME_PAD = ds.space[6];

/** The name the black flag is written under, drawn in its own ink rather than on the fill. */
const BLACK_NAME = 'BLACK';

interface SolidFull {
  id: string;
  flag: FlagProperty;
  name: string;
  color: Hex;
  flash: boolean;
}

/**
 * The four states filled with one colour and named, in the order `flagStrip` lists them. The names
 * are the one word the sheets write across the block, not the band's "YELLOW FLAG": at this size
 * the block is the flag and the second word says nothing the first does not.
 */
const SOLID: readonly SolidFull[] = [
  { id: 'yellow', flag: 'Flag_Yellow', name: 'YELLOW', color: ds.purpose.flag.yellow, flash: true },
  { id: 'blue', flag: 'Flag_Blue', name: 'BLUE', color: ds.purpose.flag.blue, flash: false },
  { id: 'white', flag: 'Flag_White', name: 'WHITE', color: ds.purpose.flag.white, flash: false },
  { id: 'green', flag: 'Flag_Green', name: 'GREEN', color: ds.purpose.flag.green, flash: false },
];

/** Every name the format can draw. The chequer is absent because it carries none. */
export const FLAG_FULL_NAMES: readonly string[] = [BLACK_NAME, ...SOLID.map((s) => s.name)];

/**
 * One size for every name on a face: the sheets' fraction of the block, shrunk until the widest
 * name fits the block less its padding.
 *
 * The fraction alone does not fit every face. At 600 x 686 the block is 546 px tall, 0.447 of which
 * is 244 px, and YELLOW at 244 px in Barlow Condensed Bold measures 714 px against 600 px of face;
 * SimHub hands the box to WPF as `MaxTextWidth` and clips the rest without a word, so the size is
 * measured down rather than quoted. One size rather than one per state, because the sheets quote one
 * figure per face and a block whose type changed size between two flags would read as two formats.
 */
export function flagFullNameSize(frame: Rect): number {
  const room = frame.width - 2 * FLAG_FULL_NAME_PAD;
  const widestEm = Math.max(...FLAG_FULL_NAMES.map((name) => measureText('BarlowCondensedBold', name, 1)));
  return Math.max(1, Math.min(Math.floor(FLAG_FULL_NAME_RATIO * frame.height), Math.floor(room / widestEm)));
}

/**
 * The name, centred on the block.
 *
 * `numeral` rather than `label`, because the sheets set it in Barlow Condensed Bold and `label`
 * fixes its family to Barlow; proportional rather than in cells, because a name is not a value and
 * nothing about it ticks.
 */
function flagFullName(name: string, frame: Rect, text: string, color: Hex): Item {
  const size = flagFullNameSize(frame);
  return numeral(name, text, frame.left, frame.top + (frame.height - size) / 2, size, { digits: 0, specials: 0 }, {
    proportional: true,
    weight: 'Bold',
    color,
    width: frame.width,
    hAlign: 'center',
  });
}

/**
 * The off phase of the yellow's flash: the face's own ground, opaque, over the whole block.
 *
 * `blink` on the layer would draw nothing at all for half of every cycle and the zones the flag
 * covers would read straight through it, which is the bug the band was fixed for. The block covers
 * the body precisely so that what is under it cannot be read, so what alternates is two opaque
 * things. It takes the whole frame rather than an inset one, unlike the band's flash: the band
 * insets its flash to keep its 3 px edge through the dark phase, and a block that is the face has
 * no edge to keep.
 */
const flashFull = (name: string, frame: Rect): RectangleItem => ({
  ...band(name, frame, ds.color.surface.base),
  blink: { enabled: true, delayMs: FLAG_BLINK_MS },
});

function solidFull(frame: Rect, prefix: string, spec: SolidFull): LayerItem {
  return {
    kind: 'layer',
    name: `${prefix}.${spec.id}`,
    children: [
      band(`${prefix}.${spec.id}.band`, frame, spec.color),
      flagFullName(`${prefix}.${spec.id}.name`, frame, spec.name, ds.purpose.flag.onFlag),
      ...(spec.flash ? [flashFull(`${prefix}.${spec.id}.flash`, frame)] : []),
    ],
    ...withBindings({ Visible: flagVisible(spec.flag) }),
  };
}

/**
 * The black flag, light on dark exactly as the band draws it: `purpose.flag.black` is `#F5F7FA` and
 * is the ink rather than the ground, so a block filled with it would be the white flag. The border
 * stays for the same reason it does on the band, a dark block on a dark dash needing an edge.
 */
function blackFull(frame: Rect, prefix: string): LayerItem {
  return {
    kind: 'layer',
    name: `${prefix}.black`,
    children: [
      band(`${prefix}.black.band`, frame, ds.color.surface.base, { border: { color: ds.purpose.flag.black, width: BLACK_FLAG_BORDER } }),
      flagFullName(`${prefix}.black.name`, frame, BLACK_NAME, ds.purpose.flag.black),
    ],
    ...withBindings({ Visible: flagVisible('Flag_Black') }),
  };
}

/**
 * The chequer: the band's board at the block's scale, half the block high, opening on the ground one
 * square in, so that the two formats are one flag at two sizes rather than two drawings of it.
 */
function chequeredFull(frame: Rect, prefix: string): LayerItem {
  const check = frame.height / 2;
  const columns = Math.ceil(frame.width / check);
  const children: Item[] = [band(`${prefix}.chequered.band`, frame, ds.color.surface.base)];
  // The edges are rounded and the squares cut between them, rather than each square being rounded
  // on its own. A block of odd height puts the check on a half pixel, and a square whose left and
  // width then round apart ends a pixel past the block: the arrangement without the rev bar is
  // where that happens, 361 px of body at 1280 x 480.
  const edge = (k: number, start: number, extent: number): number => Math.min(start + extent, Math.round(start + k * check));
  for (let row = 0; row < 2; row++) {
    const top = edge(row, frame.top, frame.height);
    const height = edge(row + 1, frame.top, frame.height) - top;
    for (let col = (row + 1) % 2; col < columns; col += 2) {
      const left = edge(col, frame.left, frame.width);
      const width = edge(col + 1, frame.left, frame.width) - left;
      children.push(band(`${prefix}.chequered.r${row}c${String(col).padStart(2, '0')}`, rect(left, top, width, height), ds.purpose.flag.chequer));
    }
  }
  return { kind: 'layer', name: `${prefix}.chequered`, children, ...withBindings({ Visible: flagVisible('Flag_Checkered') }) };
}

/** One Layer per flag over `frame`, each visible when its flag is out and no higher one is. */
export function flagFull(frame: Rect, prefix = 'flagFull'): Item[] {
  return [blackFull(frame, prefix), chequeredFull(frame, prefix), ...SOLID.map((spec) => solidFull(frame, prefix, spec))];
}
