/**
 * The shapes of RGB strip people actually own, expressed as side / centre / side.
 *
 * A strip is a run of addressable LEDs with a group at each end that the maker intends for
 * something other than revs. openDash describes one effect tree and fits it to each shape, and a
 * strip whose wiring presents that shape in another order is a `Groups.RemapGroup` rather than a
 * second profile — which is what makes a new device a row of numbers instead of a rebuild.
 *
 * Adding a device is adding a row to {@link STRIP_SHAPES}. That is the whole of it: the effects,
 * the colours and the thresholds are the same for every shape, which is the "one language of
 * light" the wave is for.
 */

/** What a strip is fitted to: a wheel rim or base, a brow above a monitor, or a button box. */
export type StripPlacement = 'wheel' | 'brow' | 'buttons';

export interface StripShape {
  /** Stable id, used in the file name and as the settings value. Never reordered or reused. */
  id: string;
  /** How it is written in the profile's name and in the panel. */
  label: string;
  /** The hardware this matches, for the panel and the guide. Not read by the build. */
  devices?: readonly string[];
  placement: StripPlacement;
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
    placement: 'wheel',
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
 * The shapes openDash generates for.
 *
 * The named ones are real device families. The rest are the side/centre/side configurations that
 * make sense on hardware people actually buy: sides of none, two, three, four or five, around a
 * centre of eight to sixteen. A shape not here is one row away.
 *
 * `0/n/0` is a bare run with nothing at the ends, which is also what a brow is — see
 * {@link BROW_SHAPES}.
 */
export const STRIP_SHAPES: readonly StripShape[] = [
  wheel(3, 9, 3, { devices: ['Fanatec ClubSport / Podium wheels', 'Simucube wireless wheels', 'Cammus', 'Moza'] }),
  fanalab(3, 9, 3, { devices: ['Fanatec ClubSport / Podium wheels driven through Fanalab'] }),
  wheel(3, 10, 3, { devices: ['GridSim Lab GTSL Pro'], extraRuns: { count: 2, length: 9 } }),
  wheel(4, 14, 4, { devices: ['SimRep Engineering MLD', 'Ascher Racing'] }),
  wheel(4, 14, 4, { reversed: true, devices: ['SimRep Engineering MLD, wired from the far end'] }),
  wheel(2, 10, 2, { devices: ['generic WS2812b runs'] }),
  wheel(4, 9, 4, { devices: ['generic WS2812b runs'] }),
  wheel(5, 10, 5, { devices: ['generic WS2812b runs'] }),
  wheel(0, 8, 0, { devices: ['generic WS2812b runs'] }),
  wheel(0, 9, 0, { devices: ['generic WS2812b runs'] }),
  wheel(0, 10, 0, { devices: ['generic WS2812b runs'] }),
  wheel(0, 12, 0, { devices: ['generic WS2812b runs'] }),
  wheel(0, 16, 0, { devices: ['generic WS2812b runs'] }),
];

/**
 * Brows: a bare run mounted above a monitor rather than on a wheel. Nine to twenty-five LEDs is
 * what the makers sell, and the effect tree is the same one — a brow is a strip with no sides.
 * XOR-241.
 */
export const BROW_SHAPES: readonly StripShape[] = [9, 12, 15, 16, 18, 20, 25].map((n) => ({
  id: `brow-${n}`,
  label: `brow ${n}`,
  devices: ['LED Brows', 'generic WS2812b brow strips'],
  placement: 'brow' as const,
  left: 0,
  centre: n,
  right: 0,
}));

/** Every shape the build emits a profile for. */
export const ALL_SHAPES: readonly StripShape[] = [...STRIP_SHAPES, ...BROW_SHAPES];

export const shapeById = (id: string): StripShape | undefined => ALL_SHAPES.find((s) => s.id === id);
