/**
 * The Porsche's own drawing of the parts the house draws differently: the dots, the top strip, the
 * panels and the two columns around the body, the three overlay states, and the foot as one more
 * page of band D. What each part is and where it comes from is said in its own file.
 */
import { ds } from '../../tokens.ts';
import type { ThemeDrawing } from '../drawing.ts';
import { porscheBody } from './body.ts';
import { porscheDots } from './dots.ts';
import { PORSCHE_FOOT } from './foot.ts';
import { porscheBandPage } from './bandPages.ts';
import { porscheModuleFrame, porscheModules, ZONE_TYPE } from './modules.ts';
import { porscheChangeNotifications, porscheTakeovers } from './overlays.ts';
import { carColour } from './register.ts';
import { porscheStrip } from './strip.ts';

export const porscheDrawing: ThemeDrawing = {
  revBar: porscheDots,
  bar: porscheStrip,
  chrome: porscheBody,
  takeovers: porscheTakeovers,
  changeNotifications: porscheChangeNotifications,
  // The readings stand on the black ground inside the zone's grey outline and the gear on its tile, as
  // the car draws them, and the foot sits on the ground where the house recesses its band into a well.
  zoneGround: (zone) => (zone === 'A' ? carColour('tile') : ds.color.surface.base),
  // The car's foot fills the band from end to end, so the band keeps no corner blocks, as the house
  // face does at 850 and 800 wide; the strip and the telltale column carry what they held.
  bandCorners: false,
  // The car shows one gear, with no neighbour ghosted beside it.
  gearGhosts: false,
  bandPages: [PORSCHE_FOOT],
  // Every module, band page and the foot in the one register of the car's panels.
  modules: porscheModules,
  moduleFrame: porscheModuleFrame,
  // The car's titles are set at the zone's label size.
  zoneHeaderSize: ZONE_TYPE.label,
  bandPage: porscheBandPage,
};
