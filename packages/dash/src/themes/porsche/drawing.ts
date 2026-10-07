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
import { porscheChangeNotifications, porscheTakeovers } from './overlays.ts';
import { carColour } from './register.ts';
import { porscheStrip } from './strip.ts';

export const porscheDrawing: ThemeDrawing = {
  revBar: porscheDots,
  bar: porscheStrip,
  chrome: porscheBody,
  takeovers: porscheTakeovers,
  changeNotifications: porscheChangeNotifications,
  // The readings are drawn on the panel's grey and the gear on its tile, as the car draws them. The
  // foot sits on the ground with nothing behind it, where the house recesses its band into a well.
  zoneGround: (zone) => (zone === 'A' ? carColour('tile') : zone === 'D' ? ds.color.surface.base : carColour('panel')),
  // The car's foot fills the band from end to end, so the band keeps no corner blocks, as the house
  // face does at 850 and 800 wide; the strip and the telltale column carry what they held.
  bandCorners: false,
  // The car shows one gear, with no neighbour ghosted beside it.
  gearGhosts: false,
  bandPages: [PORSCHE_FOOT],
};
