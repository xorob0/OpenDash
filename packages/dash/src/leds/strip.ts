/**
 * The shapes of RGB strip people actually own, expressed as side / centre / side.
 *
 * A strip is a run of addressable LEDs with a group at each end that the maker intends for
 * something other than revs. openDash describes one effect tree and fits it to each shape, and a
 * strip whose wiring presents that shape in another order is a `Groups.RemapGroup` rather than a
 * second profile — which is what makes a new device a row of numbers instead of a rebuild.
 *
 * The shapes are a **grid** rather than a catalogue of devices: sides of nought to four around a
 * centre of four to twelve, every combination generated. What a driver knows about their strip is
 * how many LEDs it has and how they are grouped, and those two numbers are the whole of what a
 * profile needs; a table of product names asked them to find themselves in somebody else's list and
 * left anyone whose wheel was not in it with nothing. The effects, the colours and the thresholds
 * are the same for every shape, which is the "one language of light" the wave is for.
 */


export interface StripShape {
  /** Stable id, used in the file name and as the settings value. Never reordered or reused. */
  id: string;
  /** How it is written in the profile's name and in the panel. */
  label: string;
  /** The hardware this matches, for the panel and the guide. Not read by the build. */
  devices?: readonly string[];
  left: number;
  centre: number;
  right: number;
  /**
   * Where each logical LED physically sits: `positions[i]` is the physical LED that logical LED `i`
   * paints. Emitted as a `Groups.RemapGroup` around the whole tree rather than by reversing every
   * effect, and absent on a shape wired in the order the effects are written in, which is most of
   * them.
   *
   * A list rather than a "reversed" flag because the far end is not the only way a device differs:
   * a Fanatec wheel read through Fanalab presents its runs in an order that is no reversal of
   * anything, and a boolean cannot say it.
   */
  positions?: readonly number[];
  /**
   * Further independent runs of the same length, wired after the main one. The 3/10/3 device
   * carries two more runs of nine, which the profile fills with the same centre content.
   */
  extraRuns?: { count: number; length: number };
}

/** How many LEDs a shape has in its main run. */
export const stripLength = (s: StripShape): number => s.left + s.centre + s.right;

/** Every LED the device has, extra runs included. */
export const deviceLength = (s: StripShape): number => stripLength(s) + (s.extraRuns ? s.extraRuns.count * s.extraRuns.length : 0);

/** 1-based position of the first centre LED. */
export const centreStart = (s: StripShape): number => s.left + 1;

/** 1-based position of the first right-hand side LED. */
export const rightStart = (s: StripShape): number => s.left + s.centre + 1;

/**
 * Logical-to-physical positions for a shape whose wiring runs backwards: `n, n-1, ... 1`.
 * `Groups.RemapGroup` stores these as `LedPosition` objects and reads them as "logical 1 is
 * physical n".
 */
export const reversedPositions = (length: number): readonly number[] => Array.from({ length }, (_, i) => length - i);

/**
 * Logical-to-physical positions for a wheel read through Fanalab, which presents the centre's rev
 * LEDs first, then the right-hand flag LEDs from the outside in, then the left-hand ones.
 *
 * Read off DNR's own `RemapGroup` rather than measured here: nobody on this project owns the wheel,
 * so the order is the best evidence available and not a confirmed fact, which is why it ships as a
 * shape of its own and not as a correction to one people have installed.
 *
 * Two things a rig would settle and this cannot. The direction is the one `LedRemapGroup` states,
 * `positions[i]` being the physical LED that logical `i` paints; were SimHub to mean the inverse,
 * every lamp would land on the far side of the wheel and the correction would be to invert this one
 * function. And the left group is read as running outside in, as the right one does, which is what
 * the review's wording carries rather than something measured.
 */
export const fanalabPositions = (s: StripShape): readonly number[] => [
  ...Array.from({ length: s.left }, (_, i) => s.centre + s.right + 1 + i),
  ...Array.from({ length: s.centre }, (_, i) => i + 1),
  // Counted from the innermost, because logical position climbs inwards to outwards on the right.
  ...Array.from({ length: s.right }, (_, i) => s.centre + s.right - i),
];

/** How a row spells a wiring that is not the plain one, before it becomes {@link StripShape.positions}. */
interface WheelOptions extends Omit<Partial<StripShape>, 'positions'> {
  /** The data line enters at the far end, so physical LED 1 is the logical last one. */
  reversed?: boolean;
}

/**
 * A side/centre/side shape, spelled once so a row is a row rather than an object literal.
 *
 * The far-end case stays the word `reversed: true` on the row rather than a call producing a list,
 * because the plugin's Install tab reads these rows back out of this file to caption them and
 * matches on that spelling (plugin/OpenDash.Tests/PanelLightRowsTests.cs).
 */
const wheel = (left: number, centre: number, right: number, { reversed, ...extra }: WheelOptions = {}): StripShape => {
  const shape: StripShape = {
    id: `${left}-${centre}-${right}${reversed ? '-reversed' : ''}`,
    label: `${left}/${centre}/${right}${reversed ? ' reversed' : ''}`,
    left,
    centre,
    right,
    ...extra,
  };
  return reversed ? { ...shape, positions: reversedPositions(deviceLength(shape)) } : shape;
};

/**
 * The same geometry as its plain sibling, wired the way Fanalab presents it.
 *
 * A second row rather than a correction to the first. A profile's identity reaches the panel through
 * its file name, so changing the existing 3/9/3's physical order would silently relight every wheel
 * that has it installed — including the Simucube, Cammus and Moza wheels of that shape, which are
 * wired in order and would break. The reversed 4/14/4 beside the plain one is the precedent.
 */
const fanalab = (left: number, centre: number, right: number, extra: WheelOptions = {}): StripShape => {
  const base = wheel(left, centre, right, extra);
  return { ...base, id: `${base.id}-fanalab`, label: `${base.label} Fanalab`, positions: fanalabPositions(base) };
};

/**
 * How wide the grid is: every side length a shape may have, and every centre.
 *
 * The table used to be a list of devices, one row per product somebody had asked for, and adding a
 * wheel meant adding a row. It is a range now, and the shapes are its product: **A / B / A**, sides
 * of nought to four around a centre of four to twelve. That covers the wheels people own without
 * naming any of them, and it covers the brows for free, because a brow is a strip with no sides.
 *
 * Both sides are the same length on purpose. A strip with three LEDs at one end and four at the
 * other is a strip whose lamps do not line up with each other, and no maker sells one; a device
 * whose *wiring* presents the runs in another order is {@link StripShape.positions} and not a second
 * geometry.
 */
export const SIDE_LENGTHS: readonly number[] = [0, 1, 2, 3, 4];

export const CENTRE_LENGTHS: readonly number[] = [4, 5, 6, 7, 8, 9, 10, 11, 12];

/**
 * Bare runs longer than the grid's centre, which is what the long brows are.
 *
 * A brow of twenty-five is one run of twenty-five and nothing at its ends, so it is 0/25/0 and needs
 * no idea of its own; the only reason these are a second range rather than a wider centre is that a
 * *wheel* with twenty-five LEDs in the middle and four at each end does not exist, and generating
 * the fifty-two shapes that would cover costs a release fifteen megabytes of files nobody can use.
 */
export const BARE_RUN_LENGTHS: readonly number[] = [13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25];

/**
 * The shapes the grid generates: every side against every centre, and the long bare runs after them.
 *
 * Ordered widest centre first within each side, and sides outward from none, so the list reads as a
 * grid rather than as a sort. Nothing here names a device: what a driver knows about their strip is
 * how many LEDs it has and how they are grouped, which is exactly what the two numbers are.
 */
export const GRID_SHAPES: readonly StripShape[] = [
  ...SIDE_LENGTHS.flatMap((side) => CENTRE_LENGTHS.map((centre) => wheel(side, centre, side))),
  ...BARE_RUN_LENGTHS.map((centre) => wheel(0, centre, 0)),
];

/**
 * The shapes that shipped before the grid and fall outside it.
 *
 * Kept, not carried forward as a principle. Each one is a device somebody already has a profile for,
 * and a shape that disappears is a driver whose wheel goes dark on an update; the grid is the rule
 * from here and this list only ever shrinks. Three of them are geometries the ranges do not reach --
 * a centre of fourteen, a side of five -- and two are wirings rather than geometries, which is why
 * they sit beside their plain siblings rather than replacing them.
 */
export const LEGACY_SHAPES: readonly StripShape[] = [
  wheel(4, 14, 4, { devices: ['SimRep Engineering MLD', 'Ascher Racing'] }),
  wheel(4, 14, 4, { reversed: true, devices: ['SimRep Engineering MLD, wired from the far end'] }),
  wheel(3, 10, 3, { devices: ['GridSim Lab GTSL Pro'], extraRuns: { count: 2, length: 9 } }),
  fanalab(3, 9, 3, { devices: ['Fanatec ClubSport / Podium wheels driven through Fanalab'] }),
  wheel(5, 10, 5, { devices: ['generic WS2812b runs'] }),
];

/**
 * Every shape the build emits a profile for: the grid, less anything a legacy shape already spells.
 *
 * The one collision today is 3/10/3. The grid would generate a plain one and the GridSim device is
 * the same geometry with two further runs of nine wired after it, and both want the id `3-10-3`. The
 * legacy row wins, because it is the file people already have installed and the id is what the
 * installer recognises its own by; a plain 3/10/3 then gets a profile carrying two runs its device
 * does not have, which is exactly what it got before the grid, since that was the only 3/10/3 there
 * was. Renaming it instead would orphan every profile already in somebody's SimHub.
 */
export const ALL_SHAPES: readonly StripShape[] = (() => {
  const spelled = new Set(LEGACY_SHAPES.map((shape) => shape.id));
  return [...GRID_SHAPES.filter((shape) => !spelled.has(shape.id)), ...LEGACY_SHAPES];
})();

export const shapeById = (id: string): StripShape | undefined => ALL_SHAPES.find((s) => s.id === id);
