/**
 * The AiM LCD's own drawing of the parts the house draws differently (#204): the tacho as the rev
 * bar, no bar of settled values, the foot rule, the two takeovers over zone C, the rows of zones B and
 * C, zone A's gear and speed, and band D's cells. What each part is and where it comes from is said
 * in its own file.
 */
import type { ThemeDrawing } from '../drawing.ts';
import { flagSettledAt } from '../../components/flagStrip.ts';
import { rect, type Rect } from '../../design/geometry.ts';
import { regionRect } from '../anatomy.ts';
import { footRuleOf } from './anatomy.ts';
import { backlit } from './backlight.ts';
import { aimBandPage } from './foot.ts';
import { aimModuleFrame, aimModules } from './modules.ts';
import { lcd } from './lcd.ts';
import { inkRect, lcdColour } from './register.ts';
import { aimTacho } from './tacho.ts';
import { aimTakeovers } from './takeovers.ts';
import { aimZoneAPage } from './zoneA.ts';

/**
 * Where a flag settles on the AiM's faces, #731, measured on the built faces: in the foot, between the
 * status line at its left and the first cell any page lays there, which no page uses.
 *
 * On five faces that room is there whatever page the foot shows, and the rectangle is centred on the
 * foot's height in it, as wide as the longest name asks where the room allows. At 800 x 480 it is
 * sixty-three pixels, too narrow for YELLOW, so the rectangle goes under the status line instead, along
 * the bottom of the face, where the foot's twenty-three pixels below the line are empty; the portrait
 * face does the same. The nano's foot has neither: its fuel page lays a cell where the status line
 * stands on the others, and nothing below or after the line is free, so the rectangle takes the status
 * line's own place and covers `GPS: GOOD`, which is the one thing on that face a driver does not read
 * from. `flagSettled.test.ts` holds every rectangle to its face, and names that one cover.
 */
const AIM_FLAG: Readonly<Record<string, Rect>> = {
  '1920x480': rect(197, 427, 176, 30),
  '1280x480': rect(195, 427, 146, 30),
  '1280x400': rect(163, 354, 176, 30),
  '1280x720': rect(195, 667, 146, 30),
  '850x480': rect(177, 427, 69, 30),
  '800x480': rect(30, 457, 176, 22),
  '800x286': rect(28, 245, 73, 26),
  '600x686': rect(30, 658, 176, 26),
};

export const aimDrawing: ThemeDrawing = {
  revBar: aimTacho,
  // The LCD's only lines are the tacho's rule, which the tacho draws, and the foot's.
  chrome: (ctx) => [inkRect('chrome.footRule', footRuleOf(regionRect(ctx.regions, 'band')))],
  takeovers: aimTakeovers,
  // Every zone stands on the LCD's ground, band D too, where the house recesses it into a well.
  zoneGround: () => lcdColour('ground'),
  // The foot runs from margin to margin, so the band keeps no corner blocks.
  bandCorners: false,
  modules: aimModules,
  moduleFrame: aimModuleFrame,
  // The LCD writes no page name and counts no pages.
  zoneCounters: false,
  zoneAPage: aimZoneAPage,
  bandPage: aimBandPage,
  // Everything the house draws on the LCD, in the LCD's register; the flag is never handed to it.
  restyle: (items, opts) => lcd(items, opts),
  // The backlight the driver chose, on every item the LCD draws; the white with no plugin (#715).
  settings: backlit,
  flagSettled: ({ layout }) => flagSettledAt(AIM_FLAG, layout, 'aim'),
};
