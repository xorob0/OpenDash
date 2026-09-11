/**
 * Character budgets of the card values: how many digit cells and how many `.,:` cells each
 * monospaced numeral is sized for. `-` and letters use the digit cell.
 */
import type { Chars } from '../design/metrics.ts';

/** `m:ss.f` with room for a two-digit minute. */
export const CURRENT_LAP_CHARS: Chars = { digits: 5, specials: 2 };
/** `m:ss.fff`; laps over ten minutes overflow the box to the right, which SimHub allows. */
export const LAP_TIME_CHARS: Chars = { digits: 6, specials: 2 };
/** `+12.34` */
export const DELTA_CHARS: Chars = { digits: 5, specials: 1 };
/** `24` */
export const POSITION_CHARS: Chars = { digits: 2, specials: 0 };
/** `12` laps or `h:mm:ss` with a two-digit hour. */
export const SESSION_CHARS: Chars = { digits: 6, specials: 2 };
/** Digits of the lap count the session denominator follows. */
export const SESSION_LAP_DIGITS = 3;
/** `999.9` */
export const FUEL_CHARS: Chars = { digits: 4, specials: 1 };
/** Digits before the point the fuel unit follows. */
export const FUEL_INT_DIGITS = 3;
/** `123.4` */
export const FUEL_LAPS_CHARS: Chars = { digits: 4, specials: 1 };
/** `OFF`, `--`, `12` */
export const ASSIST_CHARS: Chars = { digits: 3, specials: 0 };
/** `299` in any unit. */
export const SPEED_CHARS: Chars = { digits: 3, specials: 0 };
/** Digits the speed unit follows. */
export const SPEED_DIGITS = 3;
/** `104` in any unit (373 K). */
export const TEMP_CHARS: Chars = { digits: 3, specials: 0 };
/** `186.2` kPa; psi and bar are shorter. */
export const PRESSURE_CHARS: Chars = { digits: 4, specials: 1 };
