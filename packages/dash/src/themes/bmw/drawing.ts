/**
 * The BMW's own drawing of the parts the house draws differently (#203): the ten shift lights, the
 * tiles, the temperatures and the extra columns around the zones, and the black glass every zone
 * stands on. What each part is and where it comes from is said in its own file.
 */
import { rect } from '../../design/geometry.ts';
import type { ThemeDrawing } from '../drawing.ts';
import { bmwChrome } from './chrome.ts';
import { bmwDots } from './dots.ts';
import { BMW_FOOT } from './foot.ts';
import { bmwFace, ledRow, MARGIN } from './geometry.ts';
import { bmwModules } from './modules.ts';
import { bmwColour } from './register.ts';

/** The widest a settled flag's rectangle is drawn, which holds every condition's longest name at the row's height. */
const FLAG_WIDTH = 176;

/** Where zone C's title starts in from the zone's edge, which the flag keeps at the right end too. */
const TITLE_INSET = 12;

export const bmwDrawing: ThemeDrawing = {
  revBar: bmwDots,
  chrome: bmwChrome,
  zoneGround: () => bmwColour('face'),
  // The foot runs from margin to margin, so the band keeps no corner blocks.
  bandCorners: false,
  bandPages: [BMW_FOOT],
  modules: bmwModules,
  // The car shows one gear.
  gearGhosts: false,
  // The car counts no pages: a zone's page is its title.
  zoneCounters: false,
  // Where a flag settles, #731: in the row of the lights, at its right end, where nothing else is drawn
  // on any page; the lights are centred, so the room after them is a third of the face or more. With
  // the rev bar off the zones rise into that row; over a column of lap times, which stays where it is,
  // the flag does not move, and elsewhere it keeps to the right half of zone C's title line, which no
  // title reaches. Its height is even, so that the chequer's two rows fill it.
  flagSettled: ({ layout, withRevBar: lit }) => {
    const face = bmwFace(layout);
    const withRevBar = lit || face.laps !== undefined;
    const height = (face.leds.height - 2) & ~1;
    const right = withRevBar ? face.width - MARGIN : face.zoneC.left + face.zoneC.width - TITLE_INSET;
    const room = withRevBar ? right - (ledRow(face).left + ledRow(face).width) - MARGIN : Math.floor(face.zoneC.width / 2) - TITLE_INSET;
    const width = Math.min(FLAG_WIDTH, room);
    return rect(right - width, face.leds.top + 2, width, height);
  },
};
