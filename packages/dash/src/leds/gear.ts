/**
 * The gear on the matrix: what the box shows when nothing is happening.
 *
 * It is the resting state rather than a feature of its own. Every flag outranks it and takes the
 * panel; when they let go, the gear is what comes back. `OpenDash.FlagBoxMatrix<N>Gear` turns it off
 * for that panel, and off means dark rather than something else.
 *
 * **These are glyphs in source, not text rendered small.** The bundled Barlow Condensed does not
 * exist at eight pixels, and a thin face leaves one or two pixels between a 6 and an 8 on a box
 * read at a glance in peripheral vision. The font below is drawn on the whole 8 by 8 panel, with
 * two-pixel strokes: it was 5 by 7 dropped into the corner of the grid, which left the last column
 * and the last row permanently dark and made a digit read as small and off-centre beside a flag
 * that filled the panel. Sixty-four pixels is not enough to spend eight of them on a margin.
 *
 * **The colour is the shift model**, the same three bands the rev bar climbs: `shiftBands()` in
 * components/revSegments.ts is where the thresholds live, and the gear reads that rather than
 * restating them, so a driver with both learns one relationship and reads it in two places.
 *
 * #281 replaced SimHub's per-car bands with the sim's own `DriverCarSL*` values, and it changed
 * that one function: a band is now entered on the car's own ladder where the car publishes one and
 * on SimHub's bands where it does not (ADR 0014). The gear did not have to know, which is the
 * property the single function was for. What it does mean is that a digit and a rev segment and an
 * LED on a strip all change colour on the same frame for the same reason.
 *
 * **The flash is a threshold of its own, not a property of the top band**, and reading it as one was
 * the defect #284's review found here. `ShiftBand.blink` now carries the over-rev expression the
 * rev bar and the strip flash on — `max(Blink, Last)`, and never in the last gear — and the digit
 * reads that rather than flashing the moment the top band is entered.
 */
import { shiftBands, type ShiftBand } from '../components/revSegments.ts';
import { flagBoxMatrix, type FlagBoxMatrix } from '../contract.ts';
import { ncalc, type Hex, type MatrixContainer } from '../generator.ts';
import { ds } from '../tokens.ts';
import { blinkFrames, pixelsOf, still, type Grid, type Palette } from './glyph.ts';

const { and, eq, not, str } = ncalc;

/** The character every gear glyph is drawn with; the palette gives it the band's colour. */
const INK = 'G';

/** The dark half of the redline blink. */
const DARK_PANEL: Grid = Array.from({ length: 8 }, () => '........');

/**
 * The eleven gears, each drawn on the whole 8 by 8 grid with strokes two LEDs thick **in both
 * directions**.
 *
 * Two pixels rather than one because a single-pixel stroke on a diffused panel read at the edge of
 * vision is a smudge. The first cut of this font only half kept that promise: the uprights were two
 * columns wide and every crossbar was one row tall, so a 3 was three hairlines between two solid
 * stems and the digits read as though they were falling apart. Reported from a rig as lines drawn a
 * single LED wide.
 *
 * Eight rows hold exactly three two-row bars and the two single rows between them, which is what
 * makes the ten-digit set a seven-segment one: top, middle and bottom bars, and a left or right stem
 * in each gap. That is not a stylistic choice so much as the only arrangement the panel has room for
 * once every stroke has to be two thick, and it has the happy property that the digits differ by a
 * whole missing stem rather than by a curve -- which is the reliable way to tell a pair apart on
 * sixty-four pixels.
 *
 * `leds.test.ts` holds every glyph to the rule: no maximal run of lit LEDs, across or down, is
 * shorter than two.
 */
export const GEAR_FONT: Readonly<Record<string, Grid>> = {
  // The bowl over a leg that steps inboard: a diagonal two thick cannot be drawn in the rows left
  // under the bowl, so the leg is two uprights offset by one stem instead.
  R: ['.GGGGG..', '.GGGGG..', '.GG..GG.', '.GG..GG.', '.GGGGG..', '.GGGGG..', '.GG.GG..', '.GG.GG..'],
  // The diagonal steps two rows at a time for the same reason, which keeps every column of it two
  // deep and every row of it two wide.
  N: ['.GG..GG.', '.GG..GG.', '.GGG.GG.', '.GGG.GG.', '.GG.GGG.', '.GG.GGG.', '.GG..GG.', '.GG..GG.'],
  '1': ['..GGG...', '..GGG...', '...GG...', '...GG...', '...GG...', '...GG...', '.GGGGGG.', '.GGGGGG.'],
  '2': ['.GGGGGG.', '.GGGGGG.', '.....GG.', '.GGGGGG.', '.GGGGGG.', '.GG.....', '.GGGGGG.', '.GGGGGG.'],
  '3': ['.GGGGGG.', '.GGGGGG.', '.....GG.', '.GGGGGG.', '.GGGGGG.', '.....GG.', '.GGGGGG.', '.GGGGGG.'],
  // Two stems joined by the middle bar, the right one carrying on to the floor. The old 4 hung its
  // crossbar off a diagonal and was the one glyph a rig picked out as not looking like its digit.
  '4': ['.GG..GG.', '.GG..GG.', '.GG..GG.', '.GGGGGG.', '.GGGGGG.', '.....GG.', '.....GG.', '.....GG.'],
  '5': ['.GGGGGG.', '.GGGGGG.', '.GG.....', '.GGGGGG.', '.GGGGGG.', '.....GG.', '.GGGGGG.', '.GGGGGG.'],
  '6': ['.GGGGGG.', '.GGGGGG.', '.GG.....', '.GGGGGG.', '.GGGGGG.', '.GG..GG.', '.GGGGGG.', '.GGGGGG.'],
  // The tail steps rather than slopes, two rows per step, so no column of it is a single LED deep.
  '7': ['.GGGGGG.', '.GGGGGG.', '....GG..', '....GG..', '...GG...', '...GG...', '..GG....', '..GG....'],
  '8': ['.GGGGGG.', '.GGGGGG.', '.GG..GG.', '.GGGGGG.', '.GGGGGG.', '.GG..GG.', '.GGGGGG.', '.GGGGGG.'],
  '9': ['.GGGGGG.', '.GGGGGG.', '.GG..GG.', '.GGGGGG.', '.GGGGGG.', '.....GG.', '.GGGGGG.', '.GGGGGG.'],
};

/** The gears iRacing reports, as SimHub's `Gear` string gives them. */
export const GEARS: readonly string[] = ['R', 'N', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

/** The palette a gear is drawn in at one shift band. */
const paletteFor = (colour: Hex): Palette => ({ [INK]: colour });

/** The 8x8 grid of one gear. Throws for a gear the font does not carry, so a typo fails the build. */
export function gearGrid(gear: string): Grid {
  const glyph = GEAR_FONT[gear];
  if (glyph === undefined) throw new RangeError(`no gear glyph for ${JSON.stringify(gear)}`);
  return glyph;
}

/** Proves a glyph is 8 by 8 and every lit pixel is in the palette. */
export const gearPixels = (gear: string, colour: Hex): ReturnType<typeof pixelsOf> => pixelsOf(gearGrid(gear), paletteFor(colour), `gear ${gear}`);

/** `[Gear] = 'R'`. SimHub reports the gear as a string, `R` and `N` included. */
export const gearIs = (gear: string): string => eq(ncalc.game('Gear'), str(gear));

/**
 * The eleven gear glyphs at one shift band, each shown when the car is in that gear, drawn either
 * flashing or steady.
 *
 * A matrix animation has no blink formula of its own — an `AnimationContainer` simply loops its
 * frames — so "flashes only while over-revving" is two sets of glyphs under two conditions rather
 * than one set with a binding. {@link gearGroup} is where that condition is applied.
 */
function gearsAtBand(band: ShiftBand, flashing: boolean): MatrixContainer[] {
  return GEARS.map((gear) => ({
    kind: 'when' as const,
    description: `Gear ${gear} ${band.id}`,
    formula: gearIs(gear),
    children: [
      {
        // Named with the band as well as the gear: the same digit exists in four colours, and the
        // contact sheet and the fit test both address them by this name. The flashing and steady
        // copies share it, because they are one picture drawn at two rates.
        kind: 'animation' as const,
        description: `Gear ${gear} ${band.id} glyph`,
        // The redline band blinks the digit rather than filling the panel behind it: a filled
        // panel is a flag's vocabulary, and the box has to keep those two apart.
        frames: flashing
          ? blinkFrames(gearGrid(gear), DARK_PANEL, paletteFor(band.colour), ds.shiftLights.flashHz, `gear ${gear}`)
          : still(gearGrid(gear), paletteFor(band.colour), `gear ${gear}`),
      },
    ],
  }));
}

/**
 * One band's glyphs, split by its flash if it has one.
 *
 * The band says *when* it flashes rather than merely *that* it does, and the digit has to honour
 * the difference: the redline band is entered at `Last` and flashes at `max(Blink, Last)`, and it
 * stops flashing in the last gear. Before #284's review the digit flashed on the band, so on a
 * rig with a box and a screen the digit strobed while the bar's top band sat solid, and went on
 * strobing in top gear where the bar deliberately does not. Two conditional groups, over-rev first,
 * is the matrix's way of saying what `blinkBind` says on a screen segment.
 */
function bandChildren(band: ShiftBand, matrix: FlagBoxMatrix): MatrixContainer[] {
  if (band.blink === null) return gearsAtBand(band, false);
  // The panel's own switch is folded into the condition rather than made a group above it: the two
  // branches have to stay exhaustive, or a driver who turns the flash off gets a dark panel at the
  // moment the engine is screaming, which is worse than either answer.
  const flashing = and(eq(flagBoxMatrix(matrix).gearBlink(), 'true'), band.blink);
  return [
    { kind: 'when', description: `Gear ${band.id} over-rev`, formula: flashing, children: gearsAtBand(band, true) },
    { kind: 'when', description: `Gear ${band.id} steady`, formula: not(flashing), children: gearsAtBand(band, false) },
  ];
}

/**
 * The gear, banded by the shift model. The bands are ranked highest first so that exactly one
 * paints: redline, then the second band, then the first, then the resting colour.
 *
 * The gate is the panel's resting state, and it is the only gate the gear has. It used to be a
 * switch of its own with the resting state above it, which made two settings out of one decision:
 * dark ignored the switch and `gear` left the switch deciding alone. `flagBoxMatrix().rest()` is
 * where the retired switch now resolves.
 */
export function gearGroup(matrix: FlagBoxMatrix): MatrixContainer {
  return {
    kind: 'when',
    description: 'Gear',
    formula: eq(flagBoxMatrix(matrix).rest(), "'gear'"),
    children: shiftBands().map((band, i, bands) => ({
      kind: 'when' as const,
      description: `Gear ${band.id}`,
      formula: and(...bands.slice(0, i).map((higher) => not(higher.raised)), band.raised),
      children: bandChildren(band, matrix),
    })),
  };
}

