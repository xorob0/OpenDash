/**
 * The Porsche's own drawing of the parts the house draws differently: the dots, the top strip, the
 * panels and the two columns around the body, the three overlay states, and the foot as one more
 * page of band D. What each part is and where it comes from is said in its own file.
 */
import { ds } from '../../tokens.ts';
import { rect } from '../../design/geometry.ts';
import { flagSettledAt } from '../../components/flagStrip.ts';
import type { ThemeDrawing } from '../drawing.ts';
import { porscheBody } from './body.ts';
import { porscheDots } from './dots.ts';
import { PORSCHE_FOOT } from './foot.ts';
import { porscheBandPage } from './bandPages.ts';
import { porscheModuleFrame, porscheModules, ZONE_TYPE } from './modules.ts';
import { porscheChangeNotifications, porscheTakeovers } from './overlays.ts';
import { carColour } from './register.ts';
import { porscheStrip } from './strip.ts';

/**
 * Where a flag settles on the Porsche's faces, #731, measured on the built faces: the bottom right
 * corner, under the foot's last row, where no page of the foot reaches on any of its pages.
 *
 * The foot is 142 px on most faces and its pages stand in its top two thirds, so its bottom right is
 * empty ground on seven faces; the rectangle sits there against the foot's right padding, and as wide as
 * the longest name asks where the corner has that much room. The nano's 60 px foot is full from end to
 * end, so its flag sits at the foot of the telltale column on the right instead, under the column's two
 * lamps, which is the one corner of that face with room for a name. `flagSettled.test.ts` holds every
 * rectangle to the face it is on.
 */
const PORSCHE_FLAG: Readonly<Record<string, ReturnType<typeof rect>>> = {
  '1920x480': rect(1728, 436, 176, 30),
  '1280x480': rect(1088, 436, 176, 30),
  '1280x400': rect(1088, 362, 176, 30),
  '1280x720': rect(1088, 676, 176, 30),
  '850x480': rect(678, 436, 156, 30),
  '800x480': rect(659, 436, 125, 30),
  '800x286': rect(716, 154, 80, 30),
  '600x686': rect(522, 657, 74, 26),
};

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
  flagSettled: ({ layout }) => flagSettledAt(PORSCHE_FLAG, layout, 'porsche'),
};
