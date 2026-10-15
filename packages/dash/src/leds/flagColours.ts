/**
 * The flags as a lamp draws them: the one table the strips' rows (`FLAG_ROWS` in effects.ts) and the
 * flag box's glyphs (`FLAG_PALETTE` in glyphs.ts) both read, so a flag is the same colour on every
 * LED of a rig (#733). `leds.flags.test.ts` holds the two to it.
 *
 * A flag on a lamp has a universal convention, the FIA's flags, and none of the face's reasons for
 * its own greens and blues apply there: no text beside it, no dark surface to contrast against, no
 * delta to be confused with. So the table is the place for the flags' own hues, and every entry is a
 * token, since `design/tokens.json` is the only place a colour is decided.
 *
 * The green, red, blue and white are `purpose.light.flag.*`, LED-only tokens in the flags' own hues
 * (`palette.led`). The face's `purpose.flag.*` are aliases of its state colours, and
 * `purpose.flag.green` is `color.good.primary`, a green chosen to read beside white text, which an
 * RGB LED renders as a teal. The yellow is `palette.yellow.200` rather than the caution amber, and the
 * meatball's disc is `purpose.light.meatball`, the orange a third of the way to red that an RGB LED
 * does not draw as the yellow flag; the box drew it in `purpose.flag.orange`, the caution amber, while
 * the strip drew the orange. The debris flag's stripe, the black and the chequer are still the face's.
 */
import { ds } from '../tokens.ts';

export const LAMP_FLAG = {
  red: ds.purpose.light.flag.red,
  yellow: ds.purpose.flag.yellow,
  debris: ds.purpose.flag.debris,
  debrisStripe: ds.purpose.flag.debrisStripe,
  blue: ds.purpose.light.flag.blue,
  white: ds.purpose.light.flag.white,
  green: ds.purpose.light.flag.green,
  chequer: ds.purpose.flag.chequer,
  black: ds.purpose.flag.black,
  meatball: ds.purpose.light.meatball,
} as const;
