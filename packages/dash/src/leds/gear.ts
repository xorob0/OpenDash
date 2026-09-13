/**
 * The gear on the matrix: what the box shows when nothing is happening.
 *
 * It is the resting state rather than a feature of its own. Every flag outranks it and takes the
 * panel; when they let go, the gear is what comes back. `OpenDash.FlagBoxGear` turns it off, and
 * off means dark rather than something else.
 *
 * **These are glyphs in source, not text rendered small.** The bundled Barlow Condensed does not
 * exist at eight pixels, and a thin face leaves one or two pixels between a 6 and an 8 on a box
 * read at a glance in peripheral vision. The font below is 5 by 7, centred in the panel, with
 * every stroke a full pixel wide.
 *
 * **The colour is the shift model**, the same three bands the rev bar climbs: `shiftBands()` in
 * components/revSegments.ts is where the thresholds live, and the gear reads that rather than
 * restating them, so a driver with both learns one relationship and reads it in two places. When
 * XOR-230 replaces SimHub's per-car bands with the sim's own `DriverCarSL*` values, it changes
 * that one function.
 */
import { shiftBands, type ShiftBand } from '../components/revSegments.ts';
import { flagBox } from '../contract.ts';
import { ncalc, type Hex, type MatrixContainer } from '../generator.ts';
import { ds } from '../tokens.ts';
import { blinkFrames, pixelsOf, still, type Grid, type Palette } from './glyph.ts';

const { and, eq, not, str } = ncalc;

/** The character every gear glyph is drawn with; the palette gives it the band's colour. */
const INK = 'G';

/** The dark half of the redline blink. */
const DARK_PANEL: Grid = Array.from({ length: 8 }, () => '........');

/**
 * 5 by 7, one pixel per stroke. `1` carries a foot and a flag so that it is not a bare bar, and
 * `6` and `8` differ in two places rather than one, which is the pair the community warns about.
 */
export const GEAR_FONT: Readonly<Record<string, Grid>> = {
  R: ['GGGG.', 'G...G', 'G...G', 'GGGG.', 'G.G..', 'G..G.', 'G...G'],
  N: ['G...G', 'GG..G', 'GG..G', 'G.G.G', 'G.GGG', 'G..GG', 'G...G'],
  '1': ['..G..', '.GG..', 'G.G..', '..G..', '..G..', '..G..', 'GGGGG'],
  '2': ['.GGG.', 'G...G', '....G', '...G.', '..G..', '.G...', 'GGGGG'],
  '3': ['GGGGG', '...G.', '..G..', '...G.', '....G', 'G...G', '.GGG.'],
  '4': ['...G.', '..GG.', '.G.G.', 'G..G.', 'GGGGG', '...G.', '...G.'],
  '5': ['GGGGG', 'G....', 'GGGG.', '....G', '....G', 'G...G', '.GGG.'],
  '6': ['..GG.', '.G...', 'G....', 'GGGG.', 'G...G', 'G...G', '.GGG.'],
  '7': ['GGGGG', '....G', '...G.', '..G..', '.G...', '.G...', '.G...'],
  '8': ['.GGG.', 'G...G', 'G...G', '.GGG.', 'G...G', 'G...G', '.GGG.'],
  '9': ['.GGG.', 'G...G', 'G...G', '.GGGG', '....G', '...G.', '.GG..'],
};

/** The gears iRacing reports, as SimHub's `Gear` string gives them. */
export const GEARS: readonly string[] = ['R', 'N', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

/**
 * A 5 by 7 glyph centred in the 8 by 8 panel: one column of margin on the left, two on the right,
 * and the bottom row left dark. Off-centre by half a pixel in both directions, which a grid of
 * even width and odd glyph cannot avoid; the choice is recorded here rather than rediscovered.
 */
export function centred(glyph: Grid): Grid {
  const rows = glyph.map((row) => `.${row}..`);
  return [...rows, '........'];
}

/** The palette a gear is drawn in at one shift band. */
const paletteFor = (colour: Hex): Palette => ({ [INK]: colour });

/** The 8x8 grid of one gear. Throws for a gear the font does not carry, so a typo fails the build. */
export function gearGrid(gear: string): Grid {
  const glyph = GEAR_FONT[gear];
  if (glyph === undefined) throw new RangeError(`no gear glyph for ${JSON.stringify(gear)}`);
  return centred(glyph);
}

/** Proves a glyph is 8 by 8 and every lit pixel is in the palette. */
export const gearPixels = (gear: string, colour: Hex): ReturnType<typeof pixelsOf> => pixelsOf(gearGrid(gear), paletteFor(colour), `gear ${gear}`);

/** `[Gear] = 'R'`. SimHub reports the gear as a string, `R` and `N` included. */
export const gearIs = (gear: string): string => eq(ncalc.game('Gear'), str(gear));

/** The eleven gear glyphs at one shift band, each shown when the car is in that gear. */
function gearsAtBand(band: ShiftBand): MatrixContainer[] {
  return GEARS.map((gear) => ({
    kind: 'when' as const,
    description: `Gear ${gear} ${band.id}`,
    formula: gearIs(gear),
    children: [
      {
        // Named with the band as well as the gear: the same digit exists in four colours, and the
        // contact sheet and the fit test both address them by this name.
        kind: 'animation' as const,
        description: `Gear ${gear} ${band.id} glyph`,
        // The redline band blinks the digit rather than filling the panel behind it: a filled
        // panel is a flag's vocabulary, and the box has to keep those two apart.
        frames: band.blink
          ? blinkFrames(gearGrid(gear), DARK_PANEL, paletteFor(band.colour), ds.shiftLights.flashHz, `gear ${gear}`)
          : still(gearGrid(gear), paletteFor(band.colour), `gear ${gear}`),
      },
    ],
  }));
}

/**
 * The gear, banded by the shift model. The bands are ranked highest first so that exactly one
 * paints: redline, then the second band, then the first, then the resting colour.
 */
export function gearGroup(): MatrixContainer {
  return {
    kind: 'when',
    description: 'Gear',
    formula: eq(flagBox.gear(), 'true'),
    children: shiftBands().map((band, i, bands) => ({
      kind: 'when' as const,
      description: `Gear ${band.id}`,
      formula: and(...bands.slice(0, i).map((higher) => not(higher.raised)), band.raised),
      children: gearsAtBand(band),
    })),
  };
}

