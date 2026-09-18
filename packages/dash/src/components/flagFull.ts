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
 * It is a sibling of `flagStrip` rather than a mode of it, and the two now read one list: every
 * condition of `FLAG_CATALOGUE`, ranked by the band's own `conditionVisible`, so that the format a
 * driver chose cannot change which flag is out. It drew the six properties SimHub normalises until
 * this file's last revision, which meant that under a red flag or a full-course caution the band
 * named the condition and the block, having no state for it, drew nothing at all.
 *
 * The three shapes are `alertBand`'s and are drawn here rather than called: a filled block named in
 * `purpose.flag.onFlag`, an outlined block named in the alert's colour over an opaque ground, and
 * the chequer as a half-height board with no name. Two things differ at this scale and are why the
 * block is not a style of the band. The flash covers the whole frame rather than an inset one,
 * since a block that is the face has no 3 px edge to keep through the dark phase; and the type is
 * measured down rather than set, because a name one label row high is measured against sixty pixels
 * and a name nearly half the face high is measured against the face.
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
import { bandRaised, conditionVisible, FLAG_CATALOGUE, type FlagCondition } from '../flags.ts';
import { BLACK_FLAG_BORDER, FLAG_BLINK_MS } from './flagStrip.ts';

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

/**
 * What each condition reads as on the block: the band's label shortened to the one word the sheets
 * write across it.
 *
 * A table rather than the band's own label, because the block's name is a fact about the block's
 * width. One size serves every state, that size is the widest name divided into the block, and the
 * band writes "BLACK FLAG · FURLED": taking the labels as they are would set every name on the
 * portrait face at 69 px where the sheets draw 244, which is a block a driver reads the flag off
 * rather than the word. Shortened, the widest is MEATBALL, and the only face that pays anything at
 * all for the other nine conditions is the portrait one, at 143 px against the 183 the five names
 * before them allowed.
 *
 * The standing and the waved yellow read the same word and are told apart by the flash, which is
 * the rule the flag box keeps under "waving is blinking"; they cannot be out at once, so the block
 * never has to distinguish two things a driver can see side by side.
 */
const BLOCK_NAMES: Readonly<Record<string, string>> = {
  red: 'RED',
  disqualify: 'DSQ',
  furled: 'FURLED',
  black: 'BLACK',
  meatball: 'MEATBALL',
  caution: 'SAFETY',
  yellowWaving: 'YELLOW',
  yellow: 'YELLOW',
  debris: 'DEBRIS',
  blue: 'BLUE',
  white: 'WHITE',
  green: 'GREEN',
  startSet: 'SET',
  startReady: 'READY',
};

/**
 * The name, or the failure to build the face at all.
 *
 * Every face calls this for every condition, so a condition that reaches the catalogue without a
 * name on the block fails `bun run build` rather than drawing an unnamed colour. That is the same
 * gate `AlertBandSpec` is for the band, expressed as a throw because a `FlagCondition`'s id is a
 * string and no type can be made to refuse it.
 */
const blockName = (condition: FlagCondition): string => {
  const name = BLOCK_NAMES[condition.id];
  if (name === undefined) throw new RangeError(`${condition.id} has no name on the full-screen block`);
  return name;
};

/** Whether the condition carries a name at all. The chequer is the one that does not. */
const named = (condition: FlagCondition): boolean => condition.band.shape !== 'chequer';

/** Every name the format can draw, once each, which is what the one size is measured against. */
export const FLAG_FULL_NAMES: readonly string[] = [...new Set(FLAG_CATALOGUE.filter(named).map(blockName))];

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
 * The off phase of the waved yellow's flash: the face's own ground, opaque, over the whole block.
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

/** A block filled with the alert's colour, named on the fill, flashing where the condition flashes. */
const filledFull = (name: string, frame: Rect, colour: Hex, text: string, flash: boolean): Item[] => [
  band(`${name}.band`, frame, colour),
  flagFullName(`${name}.name`, frame, text, ds.purpose.flag.onFlag),
  ...(flash ? [flashFull(`${name}.flash`, frame)] : []),
];

/**
 * A block outlined and named in the alert's colour over the face's own ground, which is how the
 * black family and the start gantry are drawn.
 *
 * `purpose.flag.black` is `#F5F7FA` and is the ink rather than the ground, so a block filled with it
 * would be the white flag. The border stays for the reason it does on the band: a dark block on a
 * dark dash needs an edge to read as a block rather than as the face going out.
 */
const outlinedFull = (name: string, frame: Rect, colour: Hex, text: string): Item[] => [
  band(`${name}.band`, frame, ds.color.surface.base, { border: { color: colour, width: BLACK_FLAG_BORDER } }),
  flagFullName(`${name}.name`, frame, text, colour),
];

/**
 * The chequer: the band's board at the block's scale, half the block high, opening on the ground one
 * square in, so that the two formats are one flag at two sizes rather than two drawings of it.
 */
function chequeredFull(name: string, frame: Rect): Item[] {
  const check = frame.height / 2;
  const columns = Math.ceil(frame.width / check);
  const children: Item[] = [band(`${name}.band`, frame, ds.color.surface.base)];
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
      children.push(band(`${name}.r${row}c${String(col).padStart(2, '0')}`, rect(left, top, width, height), ds.purpose.flag.chequer));
    }
  }
  return children;
}

/** The shape the condition asks for, at the block's scale. */
const blockParts = (name: string, frame: Rect, condition: FlagCondition): Item[] => {
  const spec = condition.band;
  switch (spec.shape) {
    case 'filled':
      return filledFull(name, frame, spec.colour, blockName(condition), spec.flash ?? false);
    case 'outlined':
      return outlinedFull(name, frame, spec.colour, blockName(condition));
    case 'chequer':
      return chequeredFull(name, frame);
  }
};

/**
 * One Layer per condition over `frame`, each visible when its condition is raised and none above it
 * in the catalogue is.
 *
 * `bandRaised` and the same `false` for the critical-flags switch as band D, which is what makes the
 * two formats one reading of one list: null-safe, so a sim publishing no `SessionFlagsDetails`
 * leaves the block dark rather than covering the gear with the highest-ranked flag in it, and
 * limited where a bit outlives the flag it announces, so the green does not sit over zone A for a
 * whole stint.
 */
export function flagFull(frame: Rect, prefix = 'flagFull'): LayerItem[] {
  return FLAG_CATALOGUE.map((condition) => {
    const name = `${prefix}.${condition.id}`;
    return {
      kind: 'layer',
      name,
      children: blockParts(name, frame, condition),
      ...withBindings({ Visible: conditionVisible(condition, false, FLAG_CATALOGUE, bandRaised) }),
    };
  });
}
