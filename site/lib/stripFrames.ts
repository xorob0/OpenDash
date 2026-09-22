/**
 * The states an emulated strip can hold: what the site draws to show what a strip shows. A plain
 * module rather than part of the client component, because a server page reads FRAMES too, and a
 * value imported from a client module reaches a server component as a reference, not a value.
 */
export type Lamp = 'off' | 'blue' | 'yellow' | 'white' | 'amber' | 'red' | 'green';

export interface StripFrame {
  /** How much of the centre is lit, 0 to 1. */
  revs: number;
  /** The limiter flash: every lit centre LED blinks. */
  blink?: boolean;
  left: Lamp;
  right: Lamp;
  sidesBlink?: boolean;
  /** What the strip is showing, for the caption. */
  label: string;
}

export const FRAMES: Record<string, StripFrame> = {
  revs: { revs: 0.72, left: 'off', right: 'off', label: 'Revs, in the car’s own colours' },
  shift: { revs: 1, blink: true, left: 'off', right: 'off', label: 'Shift now' },
  blue: { revs: 0.45, left: 'blue', right: 'blue', label: 'Blue flag' },
  yellow: { revs: 0.45, left: 'yellow', right: 'yellow', sidesBlink: true, label: 'Waved yellow' },
  spotterLeft: { revs: 0.6, left: 'white', right: 'off', label: 'A car on your left' },
  limiter: { revs: 0.2, left: 'amber', right: 'amber', sidesBlink: true, label: 'Pit limiter' },
  abs: { revs: 0.1, left: 'white', right: 'white', sidesBlink: true, label: 'ABS' },
  fuel: { revs: 0.5, left: 'red', right: 'red', label: 'Low fuel' },
};

