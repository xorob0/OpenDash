/**
 * The shapes of RGB strip people actually own, expressed as side / centre / side.
 *
 * A strip is a run of addressable LEDs with a group at each end that the maker intends for
 * something other than revs. OpenDash describes one effect tree and fits it to each shape, and a
 * strip whose wiring presents that shape in another order is that tree inside a `Groups.RemapGroup`
 * -- a second profile of the same tree, not a second tree -- which is what makes a new device a row
 * of numbers instead of a rebuild. The far-end wiring of every plain shape is generated
 * ({@link withReversedTwins}); an order of a product's own, the Fanatec, is spelled as a row.
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
   * a Fanatec wheel presents its runs in an order that is no reversal of anything, and a boolean
   * cannot say it.
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
 * Logical-to-physical positions for a shape whose data line enters at the far end of its main run.
 *
 * The main run is reversed and nothing else: logical 1 is physical {@link stripLength}. Extra runs
 * (the 3/10/3's two further runs of nine) keep their own order after it, because they carry only
 * repeats of the centre and are wired after the main run whichever end that run is fed from.
 * Reversing the whole device instead would put the main run -- every lamp, the flags, the spotter --
 * on physical LEDs a strip of the main run's length does not have, and the repeats on the ones it
 * does: a strip that revs and never shows a flag. On a shape with no extra runs the two readings are
 * the same list, which is why the spelled 4/14/4 is unaffected.
 */
export const farEndPositions = (s: StripShape): readonly number[] => [
  ...reversedPositions(stripLength(s)),
  ...Array.from({ length: deviceLength(s) - stripLength(s) }, (_, i) => stripLength(s) + 1 + i),
];

/**
 * Logical-to-physical positions for a Fanatec wheel as SimHub's own Fanatec device presents it: the
 * centre's rev LEDs first, then the right-hand flag LEDs from the inside out, then the left-hand
 * ones.
 *
 * **Measured, not inferred.** A working `Any Game - Daniel Newman Racing - Fanatec 3-9-3.ledsprofile`
 * from a rig with the wheel opens with exactly this `Groups.RemapGroup`:
 *
 *     13, 14, 15,  1, 2, 3, 4, 5, 6, 7, 8, 9,  12, 11, 10
 *
 * which is what this function returns for 3/9/3, position for position. So the device is not a
 * fifteen-LED run at all: physical 1-9 are the nine RevLEDs and physical 10-15 are the six FlagLEDs,
 * three per side with the right-hand group wired inwards. A profile that ignores this paints the revs
 * across physical 4-12 -- half of the rev cluster and half of the flag LEDs -- which is why the plain
 * 3/9/3 lights "only some of them" and starts the bar from the middle of the wheel.
 *
 * It is SimHub's own Fanatec LED device and not Fanalab, which is the correction this replaces: the
 * profile above drives the wheel through SimHub with Fanalab nowhere in it, and captioning the shape
 * for Fanalab sent the one person with the hardware past the only profile that would have worked.
 */
export const fanatecPositions = (s: StripShape): readonly number[] => [
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
 * The far-end case is the word `reversed: true` rather than a call producing a list. The one row
 * that spells it is the 4/14/4, kept because it shipped as a row and because the plugin's Install
 * tab reads the rows back out of this file to caption them and matches on that spelling
 * (plugin/OpenDash.Tests/PanelLightRowsTests.cs); every other far-end shape is generated by
 * {@link withReversedTwins}, which calls this with the same word.
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
  return reversed ? { ...shape, positions: farEndPositions(shape) } : shape;
};

/**
 * The same geometry as its plain sibling, wired the way a Fanatec wheel presents it.
 *
 * A second row rather than a correction to the first. A profile's identity reaches the panel through
 * its file name, so changing the existing 3/9/3's physical order would silently relight every wheel
 * that has it installed -- including the Simucube, Cammus and Moza wheels of that shape, which are
 * wired in order and would break. The reversed 4/14/4 beside the plain one is the precedent.
 */
const fanatec = (left: number, centre: number, right: number, extra: WheelOptions = {}): StripShape => {
  const base = wheel(left, centre, right, extra);
  return { ...base, id: `${base.id}-fanatec`, label: `${base.label} Fanatec`, positions: fanatecPositions(base) };
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
 * the fifty-two shapes that would cover costs every build and every plugin files nobody can use.
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
 *
 * The reversed 4/14/4 is the one far-end wiring spelled as a row, because it shipped as one; every
 * other shape's far-end twin is generated by {@link withReversedTwins}. The Fanatec wiring stays a
 * named row for good, because it is not a reversal of anything: it is one product's order, measured
 * off a working profile, and a wiring of that kind is added when somebody produces that evidence.
 */
export const LEGACY_SHAPES: readonly StripShape[] = [
  wheel(4, 14, 4, { devices: ['SimRep Engineering MLD', 'Ascher Racing'] }),
  wheel(4, 14, 4, { reversed: true, devices: ['SimRep Engineering MLD, wired from the far end'] }),
  wheel(3, 10, 3, { devices: ['GridSim Lab GTSL Pro'], extraRuns: { count: 2, length: 9 } }),
  fanatec(3, 9, 3, { devices: ['Fanatec ClubSport / Podium wheels, on the Fanatec LED device in SimHub'] }),
  wheel(5, 10, 5, { devices: ['generic WS2812b runs'] }),
];

/**
 * The shapes, and after them the far-end twin of every plain one: the same geometry wired from the
 * other end, `<left>-<centre>-<right>-reversed`. #503.
 *
 * This overturns what the legacy list used to argue, which was that a remapped twin of every sided
 * shape would be profiles "no device on earth answers to". That was true while a profile was the
 * rig's: one strip, one profile, and a driver who picked a shape picked a wiring with it. Since the
 * bar model a profile is installed per bar, and which way a strip is wired is a switch on the bar
 * rather than a shape the driver goes looking for: a twin is not an entry in a list somebody has
 * to read. What it costs is bytes, about half a megabyte gzipped across the release, and work in the
 * panel: the Install tab inflates every embedded strip profile each time it builds its rows, and the
 * Lights tab does the same for every bar it looks up, so the twins double that from about 21 MB of
 * JSON a call to 43 MB, each profile a string on net48's large object heap. Caching the texts once a
 * session, or reading only the one resource a bar needs by name, is the plugin's follow-up. What it
 * buys is that a strip whose data line enters at the far end works on any geometry, rather than on
 * the one that happened to be spelled.
 *
 * A twin is made for a shape with no `positions` of its own and no `-reversed` or `-fanatec` suffix,
 * and not where a `-reversed` sibling is already spelled, which is the 4/14/4 that shipped as a row.
 * The Fanatec wiring gets no twin: it is not a plain order, and its reversal is no device's either.
 * A twin keeps its sibling's extra runs and reverses its main run only ({@link farEndPositions}), so
 * a plain 3/10/3 strip fed from the far end gets its lamps on its own sixteen LEDs. Its devices are
 * its sibling's, each said to be wired from the far end, because none of them is wired that way out
 * of the box and the caption is where a driver looks for their hardware.
 *
 * The twins go after every existing shape rather than beside their siblings, so that no id already
 * in the list moves.
 */
export function withReversedTwins(shapes: readonly StripShape[]): StripShape[] {
  const ids = new Set(shapes.map((shape) => shape.id));
  const plain = shapes.filter(
    (shape) => shape.positions === undefined && !shape.id.endsWith('-reversed') && !shape.id.endsWith('-fanatec') && !ids.has(`${shape.id}-reversed`),
  );
  const twins = plain.map(({ id: _id, label: _label, left, centre, right, positions: _positions, devices, ...extra }) =>
    wheel(left, centre, right, {
      ...extra,
      ...(devices ? { devices: devices.map((device) => `${device}, wired from the far end`) } : {}),
      reversed: true,
    }),
  );
  return [...shapes, ...twins];
}

/**
 * The shapes before their generated twins: the grid's geometries, less anything a legacy shape
 * already spells, and the legacy rows, two of which are wirings rather than geometries (4/14/4
 * reversed and the Fanatec 3/9/3). What a driver counts on their strip, before
 * {@link withReversedTwins} adds the far-end wiring of each plain one.
 *
 * The one collision today is 3/10/3. The grid would generate a plain one and the GridSim device is
 * the same geometry with two further runs of nine wired after it, and both want the id `3-10-3`. The
 * legacy row wins, because it is the file people already have installed and the id is what the
 * installer recognises its own by; a plain 3/10/3 then gets a profile carrying two runs its device
 * does not have, which is exactly what it got before the grid, since that was the only 3/10/3 there
 * was. Renaming it instead would orphan every profile already in somebody's SimHub.
 */
export const BASE_SHAPES: readonly StripShape[] = (() => {
  const spelled = new Set(LEGACY_SHAPES.map((shape) => shape.id));
  return [...GRID_SHAPES.filter((shape) => !spelled.has(shape.id)), ...LEGACY_SHAPES];
})();

/**
 * Every shape the build emits a profile for: {@link BASE_SHAPES} and then the far-end twin of every
 * plain one ({@link withReversedTwins}). A twin is a wiring rather than a geometry, so a list meant
 * for a reader -- the site's strip grid -- reads {@link BASE_SHAPES} instead.
 */
export const ALL_SHAPES: readonly StripShape[] = withReversedTwins(BASE_SHAPES);

export const shapeById = (id: string): StripShape | undefined => ALL_SHAPES.find((s) => s.id === id);
