/**
 * The shapes of RPM strip people actually own, expressed as side / centre / side.
 *
 * A strip is a run of addressable LEDs with a group at each end that the maker intends for
 * something other than revs. openDash describes one effect tree and fits it to each shape, and a
 * strip whose wiring runs the other way is a `Groups.RemapGroup` rather than a second profile —
 * which is what makes a new device a row of numbers instead of a rebuild.
 */

export interface StripShape {
  /** Stable id, used in the profile name and as the settings value. */
  id: string;
  /** How it is written in the profile's name, and in the panel. */
  label: string;
  left: number;
  centre: number;
  right: number;
  /**
   * The data line enters at the far end, so physical LED 1 is the logical last one. Emitted as a
   * `Groups.RemapGroup` around the whole tree rather than by reversing every effect.
   */
  reversed?: boolean;
  /**
   * Further independent strips of the same length, wired after the main one. The 3/10/3 device
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
 * The shapes openDash generates for. Each is a real device family: 3/9/3 and 3/10/3 are the common
 * wheel strips, 4/14/4 is the wide one and is also sold wired backwards, and 0/9/0 is a bare run
 * with nothing at the ends.
 */
export const STRIP_SHAPES: readonly StripShape[] = [
  { id: '3-9-3', label: '3/9/3', left: 3, centre: 9, right: 3 },
  { id: '3-10-3', label: '3/10/3', left: 3, centre: 10, right: 3, extraRuns: { count: 2, length: 9 } },
  { id: '4-14-4', label: '4/14/4', left: 4, centre: 14, right: 4 },
  { id: '4-14-4-reversed', label: '4/14/4 reversed', left: 4, centre: 14, right: 4, reversed: true },
  { id: '0-9-0', label: '0/9/0', left: 0, centre: 9, right: 0 },
];

export const shapeById = (id: string): StripShape | undefined => STRIP_SHAPES.find((s) => s.id === id);

/**
 * Logical-to-physical positions for a shape whose wiring runs backwards: `n, n-1, ... 1`, which
 * `Groups.RemapGroup` stores as `Positions` and reads as "logical 1 is physical n".
 */
export const reversedPositions = (length: number): readonly number[] => Array.from({ length }, (_, i) => length - i);
