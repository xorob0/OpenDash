/**
 * The AiM LCD's own drawing of the parts the house draws differently (#204): the tacho as the rev
 * bar, no bar of settled values, the foot rule, the two takeovers over zone C, the rows of zones B and
 * C, zone A's gear and speed, and band D's cells. What each part is and where it comes from is said
 * in its own file.
 */
import type { ThemeDrawing } from '../drawing.ts';
import { regionRect } from '../anatomy.ts';
import { footRuleOf } from './anatomy.ts';
import { aimBandPage } from './foot.ts';
import { aimModuleFrame, aimModules } from './modules.ts';
import { inkRect, lcdColour } from './register.ts';
import { aimTacho } from './tacho.ts';
import { aimTakeovers } from './takeovers.ts';
import { aimZoneAPage } from './zoneA.ts';

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
};
