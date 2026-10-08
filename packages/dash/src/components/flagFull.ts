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
 * condition of `ALERT_CATALOGUE`, ranked by the band's own `bandVisible`, so that the format a
 * driver chose cannot change which condition is out. The two neutral alerts, push to pass and the
 * headlight flash, are ranked and not drawn: the block is the flag that cannot be missed, and what the
 * driver's own hand has just done is not worth the gear for the length of a push to pass.
 * `CarAlert.neutral` says why their colour cannot stand without a name either. When the format first landed it drew the six
 * properties SimHub normalises, which meant that under a red flag or a full course yellow the band
 * named the condition and the block, having no state for it, drew nothing at all: the driver who had
 * asked for the flag that cannot be missed saw the least of it.
 *
 * The five shapes are `alertBand`'s, and four of them are drawn here rather than called: a filled
 * block named in `purpose.flag.onFlag`, an outlined block named in the alert's colour over an opaque
 * ground, the chequer as a board with no name, and the debris flag's stripes with its name on a plate
 * of its yellow. The fifth, the meatball's disc on the near-black, is called, because nothing about it
 * differs at this scale: it has no name and no flash, and its disc is already a fraction of the
 * rectangle's shorter side, which is the rule a block taller than it is wide needs. Three things
 * differ at this scale for the other four and are why the block is not a style of the band. The
 * flash covers the whole frame rather than an inset one, since a block that is the face has no 3 px
 * edge to keep through the dark phase; the type is measured down rather than set, because a name one
 * label row high is measured against sixty pixels and a name nearly half the face high is measured
 * against the face; and the two patterns are counted rather than sized, because a block can be taller
 * than it is wide and a band never is. The stripes are the one shape whose rectangles are the band's
 * own, `stripes`, since what makes them the flag is the count rather than the width either passes.
 *
 * The frame is handed in rather than computed here, because a component is a function of a
 * rectangle: `bodyRect` in `zones/layout.ts` is what every face passes, and the same component
 * therefore serves the portrait face, which stacks its zones, and the nano, which has no bar.
 */
import type { Hex, Item, LayerItem, Rect, RectangleItem } from '../generator.ts';
import { withMoreBindings } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { DATA_FACE } from '../design/metrics.ts';
import { rect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { numeral } from '../elements/numeral.ts';
import { ds } from '../tokens.ts';
import { ALERT_CATALOGUE, bandNames, bandVisible, type AlertCondition } from '../flags.ts';
import { discBand, namePlate, nearestOdd, stripes } from './alertBand.ts';
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

/**
 * The least share of the one size at which the full course yellow's whole name is written, below
 * which the block writes FCY at the one size instead.
 *
 * The author's ruling on #497, "long wherever legible": FULL COURSE YELLOW is the condition's name,
 * so the block writes it at a size of its own wherever that size keeps it legible, and half the size
 * the other names are written at is the line the ruling draws. With it the form follows the block's
 * proportions rather than its size, the whole name on a block wide for its height and FCY on a tall
 * one. The rule it replaced wrote the whole name only where it fitted at the one size, which drew that
 * line so far towards the wide blocks that the pit wall and the 1280 x 720 face wrote FCY, and the
 * 800 x 286 face, with the rev bar on, the whole name.
 */
export const FLAG_FULL_LONG_NAME_MIN_RATIO = 0.5;

/** Room left either side of the name, which is what the sheets' size is measured down to fit. */
export const FLAG_FULL_NAME_PAD = ds.space[6];

/**
 * What each condition reads as on the block: the band's label cut to one word, in the manner of the
 * five the FaceVariants sheets drew.
 *
 * A table rather than the band's own label, because the block's name is a fact about the block's
 * width. One size serves every name here, that size is the widest name divided into the block, and
 * the band writes "WHITE · LAST LAP": taking the labels as they are, FCY for the full course yellow,
 * would set every name on the portrait face at 84 px where the sheets draw 244, which is neither the
 * block the sheets drew nor a word worth the body. Cut, the widest is INCIDENT, and the only face
 * that pays anything at all for the conditions the block did not use to have is the portrait one, at
 * 160 px against the 183 the five sheet names allowed. It was MEATBALL, at 143 px, until the author
 * ruled on #498 that the meatball is a disc with no name, which is why the table has no entry for it,
 * as it has none for the chequer.
 *
 * The standing and the waved yellow read the same word and are told apart by the flash, which is
 * the rule the flag box keeps under "waving is blinking"; they cannot be out at once, so the block
 * never has to distinguish two things a driver can see side by side.
 *
 * The furled black reads BLACK as the black flag does, #497, since FURLED alone did not say that it
 * was a black flag at all. Unlike the two yellows, nothing on the block tells those two apart, as
 * neither of them flashes, and band D is the same, since it writes BLACK for both: the box alone
 * draws the one as a bar that walks and the other as an outline that waves.
 *
 * The full course yellow is not in the table, because its band already carries the two forms the
 * block wants: FCY, which is a word like the others and is what the one size is measured against, and
 * FULL COURSE YELLOW, which the block writes instead at a size of its own wherever that size is at
 * least {@link FLAG_FULL_LONG_NAME_MIN_RATIO} of the one size, #497. That is the block of every
 * landscape screen, and on the portrait ones, whose blocks are narrow, it writes FCY. The long form
 * never sets the one size, since that would take every name on the block down to make room for one
 * condition's name, which is the bargain the blue flag's detail is refused below.
 *
 * **The blue flag's detail does not reach the block, and that is this table's own rule rather than
 * an omission.** Band D can name the car a blue flag is being waved for, because `BlueFlagDetail`
 * asks it to and sixty pixels of 15 px label have the room. Here one size serves every name in the
 * table and that size is the widest name divided into the block, so `BLUE · P24 LMP2` would not shrink
 * the blue alone: it would set RED, BLACK and FCY at a third of their height on every face, which is
 * every condition on the block paying for one. A block is the flag that cannot be missed, and a class
 * code half the face high is not what makes it one.
 */
const BLOCK_NAMES: Readonly<Record<string, string>> = {
  ignition: 'IGNITION',
  engine: 'ENGINE',
  red: 'RED',
  disqualify: 'DSQ',
  furled: 'BLACK',
  black: 'BLACK',
  yellowWaving: 'YELLOW',
  yellow: 'YELLOW',
  debris: 'DEBRIS',
  incident: 'INCIDENT',
  blue: 'BLUE',
  white: 'WHITE',
  green: 'GREEN',
  startSet: 'SET',
  startReady: 'READY',
};

/**
 * The names the block can write for the condition, longest first, or the failure to build the face
 * at all: the band's own two where the band has a short form, and the table's one word otherwise.
 *
 * Every face calls this for every condition, so a condition that reaches the catalogue without a
 * name on the block fails `bun run build` rather than drawing an unnamed colour. That is the same
 * gate `AlertBandSpec` is for the band, expressed as a throw because a `FlagCondition`'s id is a
 * string and no type can be made to refuse it.
 */
const blockNames = (condition: AlertCondition): readonly string[] => {
  const band = bandNames(condition.band);
  if (band.length > 1) return band;
  const name = BLOCK_NAMES[condition.id];
  if (name === undefined) throw new RangeError(`${condition.id} has no name on the full-screen block`);
  return [name];
};

/** Whether the block draws the condition at all: everything but the two neutral alerts. */
const onTheBlock = (condition: AlertCondition): boolean => !('neutral' in condition && condition.neutral);

/** The conditions the block draws, in the catalogue's order. */
const BLOCK_CONDITIONS: readonly AlertCondition[] = ALERT_CATALOGUE.filter(onTheBlock);

/** Whether the condition carries a name at all. The chequer and the meatball are the two that do not. */
const named = (condition: AlertCondition): boolean => condition.band.shape !== 'chequer' && condition.band.shape !== 'disc';

/**
 * The name every condition the format draws must be able to write, once each, which is what the one
 * size is measured against: the shortest of its names, so that a longer form is written at a size of
 * its own rather than shrinking every name on the face to make room for it.
 */
export const FLAG_FULL_NAMES: readonly string[] = [...new Set(BLOCK_CONDITIONS.filter(named).map((condition) => blockNames(condition).at(-1)!))];

/**
 * One size for every name on a face: the sheets' fraction of the block, shrunk until the widest
 * name fits the block less its padding.
 *
 * The fraction alone does not fit every face. At 600 x 686 the block is 546 px tall, 0.447 of which
 * is 244 px, and YELLOW at 244 px in Barlow Condensed Bold measures 714 px against 600 px of face;
 * SimHub hands the box to WPF as `MaxTextWidth` and clips the rest without a word, so the size is
 * measured down rather than quoted. One size rather than one per state, because the sheets quote one
 * figure per face and a block whose type changed size between two flags would read as two formats.
 * The full course yellow's whole name is the one exception, since the author ruled that it is worth
 * a smaller type wherever that type stays legible, #497, and {@link FLAG_FULL_LONG_NAME_MIN_RATIO}
 * says where that is.
 */
export function flagFullNameSize(frame: Rect): number {
  const room = frame.width - 2 * FLAG_FULL_NAME_PAD;
  const widestEm = Math.max(...FLAG_FULL_NAMES.map((name) => measureText(DATA_FACE.Bold, name, 1)));
  return Math.max(1, Math.min(Math.floor(FLAG_FULL_NAME_RATIO * frame.height), Math.floor(room / widestEm)));
}

/** A name as the block writes it: the text, and the size it is set at. */
interface WrittenName {
  text: string;
  size: number;
}

/**
 * The name the block writes for the condition, and its size, decided here, when the block's size is
 * known, rather than bound.
 *
 * A condition with one name writes it at the one size. The full course yellow writes FULL COURSE
 * YELLOW at the largest whole-pixel size, no larger than the one size, at which it fits the block less
 * its padding, measured in the face it is drawn in, wherever that size is at least
 * {@link FLAG_FULL_LONG_NAME_MIN_RATIO} of the one size, and FCY at the one size everywhere else,
 * which always fits, since the one size is measured against it.
 */
const blockName = (condition: AlertCondition, frame: Rect): WrittenName => {
  const size = flagFullNameSize(frame);
  const [long, short] = blockNames(condition);
  if (short === undefined) return { text: long!, size };
  const room = frame.width - 2 * FLAG_FULL_NAME_PAD;
  const own = Math.min(size, Math.floor(room / measureText(DATA_FACE.Bold, long!, 1)));
  return own >= FLAG_FULL_LONG_NAME_MIN_RATIO * size ? { text: long!, size: own } : { text: short, size };
};

/**
 * The name, centred on the block at its own size.
 *
 * `numeral` rather than `label`, because the sheets set it in Barlow Condensed Bold and `label`
 * fixes its family to Barlow; proportional rather than in cells, because a name is not a value and
 * nothing about it ticks.
 */
function flagFullName(name: string, frame: Rect, { text, size }: WrittenName, color: Hex): Item {
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
const filledFull = (name: string, frame: Rect, colour: Hex, written: WrittenName, flash: boolean): Item[] => [
  band(`${name}.band`, frame, colour),
  flagFullName(`${name}.name`, frame, written, ds.purpose.flag.onFlag),
  ...(flash ? [flashFull(`${name}.flash`, frame)] : []),
];

/**
 * A block outlined and named in the alert's colour over the face's own ground, which is how the
 * black family, the start gantry and the two power alerts are drawn.
 *
 * `purpose.flag.black` is `#F5F7FA` and is the ink rather than the ground, so a block filled with it
 * would be the white flag. The border stays for the reason it does on the band: a dark block on a
 * dark dash needs an edge to read as a block rather than as the face going out.
 */
const outlinedFull = (name: string, frame: Rect, colour: Hex, written: WrittenName): Item[] => [
  band(`${name}.band`, frame, ds.color.surface.base, { border: { color: colour, width: BLACK_FLAG_BORDER } }),
  flagFullName(`${name}.name`, frame, written, colour),
];

/**
 * How many checks the chequer lays across the block's shorter side.
 *
 * Three rather than the band's two. Two across a block that is nearly square is a quartered flag
 * rather than a chequer, which is what the 600 x 686 face would draw, and two across the portrait
 * companion is two stripes. The canvas draws the chequer only as a band, so this figure is the
 * code's own rather than a sheet's; #473.
 */
const FLAG_FULL_CHEQUER_ACROSS = 3;

/**
 * The chequer: the band's board at the block's scale, opening on the ground one square in, so that
 * the two formats are one flag in one phase rather than two drawings of it.
 *
 * The checks are counted and each is the block divided by the count. It used to be sized at half the
 * block high, as the band's is, and the columns cut wherever the block ended, which left every block
 * but one ending on part of a square and the portrait companion drawing 1.27 squares across: one
 * whole square and a strip. Now it is three across the shorter side and the nearest odd count along
 * the longer, so a check is never further from square than the rounding of that count, and the
 * board is the same at both ends and at the top and bottom, starting and ending on the ground at
 * every corner.
 */
function chequeredFull(name: string, frame: Rect): Item[] {
  const along = nearestOdd(Math.max(frame.width, frame.height) / (Math.min(frame.width, frame.height) / FLAG_FULL_CHEQUER_ACROSS));
  const [columns, rows] = frame.width >= frame.height ? [along, FLAG_FULL_CHEQUER_ACROSS] : [FLAG_FULL_CHEQUER_ACROSS, along];
  const children: Item[] = [band(`${name}.band`, frame, ds.color.surface.base)];
  // The edges are rounded and the squares cut between them, rather than each square being rounded
  // on its own, so that the squares meet without a gap or an overlap and the last one ends on the
  // block's own edge wherever the division leaves a fraction of a pixel.
  const edge = (k: number, count: number, start: number, extent: number): number => Math.round(start + (k * extent) / count);
  for (let row = 0; row < rows; row++) {
    const top = edge(row, rows, frame.top, frame.height);
    const height = edge(row + 1, rows, frame.top, frame.height) - top;
    for (let col = (row + 1) % 2; col < columns; col += 2) {
      const left = edge(col, columns, frame.left, frame.width);
      const width = edge(col + 1, columns, frame.left, frame.width) - left;
      children.push(band(`${name}.r${row}c${String(col).padStart(2, '0')}`, rect(left, top, width, height), ds.purpose.flag.chequer));
    }
  }
  return children;
}

/**
 * The debris flag at the block's scale: the yellow, red stripes a check of the block's own chequer
 * wide, and the name on a plate of the yellow.
 *
 * A check wide, so that the two patterns are one scale on the block as they are on the band, and
 * counted by the same rule as the band's stripes, so both ends are yellow. On the three portrait
 * blocks, the face's, the companion's and the pit wall's, that is three stripes: one red between two
 * yellow, with the plate laid over the middle of it, which leaves the red above and below the name.
 * The plate is what keeps a name half the face high off the edges between the two colours.
 */
function stripedFull(name: string, frame: Rect, colour: Hex, stripe: Hex, written: WrittenName): Item[] {
  const { text, size } = written;
  const lineTop = frame.top + (frame.height - size) / 2;
  return [
    band(`${name}.band`, frame, colour),
    ...stripes(name, frame, Math.min(frame.width, frame.height) / FLAG_FULL_CHEQUER_ACROSS, stripe),
    namePlate(`${name}.plate`, frame, measureText(DATA_FACE.Bold, text, size), lineTop, size, ds.space[4], colour),
    flagFullName(`${name}.name`, frame, written, ds.purpose.flag.onFlag),
  ];
}

/** The shape the condition asks for, at the block's scale. */
const blockParts = (name: string, frame: Rect, condition: AlertCondition): Item[] => {
  const spec = condition.band;
  switch (spec.shape) {
    case 'filled':
      return filledFull(name, frame, spec.colour, blockName(condition, frame), spec.flash ?? false);
    case 'outlined':
      return outlinedFull(name, frame, spec.colour, blockName(condition, frame));
    case 'chequer':
      return chequeredFull(name, frame);
    case 'striped':
      return stripedFull(name, frame, spec.colour, spec.stripe, blockName(condition, frame));
    case 'disc':
      return discBand(name, frame, spec.colour);
  }
};

/**
 * One Layer per condition over `frame`, each visible when its condition is raised and none above it
 * in the catalogue is.
 *
 * `bandVisible`, the band's own ranking, which is what makes the two formats one reading of one
 * list: null-safe, so a sim publishing no `SessionFlagsDetails` leaves the block dark rather than
 * covering the gear with the highest-ranked flag in it, and limited where a bit outlives the flag it
 * announces, so the green does not sit over zone A for a whole stint. The two neutral alerts have no
 * layer and still rank, so a push to pass leaves the block dark rather than letting the flash under it
 * through.
 */
export function flagFull(frame: Rect, prefix = 'flagFull'): LayerItem[] {
  return BLOCK_CONDITIONS.map((condition) => {
    const name = `${prefix}.${condition.id}`;
    return withMoreBindings({
      kind: 'layer',
      name,
      children: blockParts(name, frame, condition),
    }, { Visible: bandVisible(condition) });
  });
}
