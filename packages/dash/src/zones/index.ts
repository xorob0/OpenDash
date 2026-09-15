/**
 * The zone faces the build produces, and everything needed to build one.
 *
 * They are a separate list from `LAYOUTS` rather than entries in it, and that is the whole reason
 * the zone face can be built without disturbing the ten that ship. `build.ts` walks both; #146
 * deletes the other list and this becomes the only one.
 */
import { zoneFace1920x480 } from './faces/1920x480.ts';
import { zoneFace1280x480 } from './faces/1280x480.ts';
import { zoneFace1280x400 } from './faces/1280x400.ts';
import { zoneFace850x480 } from './faces/850x480.ts';
import { zoneFace800x480 } from './faces/800x480.ts';
import { zoneFace1280x720 } from './faces/1280x720.ts';
import { zoneFace800x286 } from './faces/800x286.ts';
import { zoneFace600x686 } from './faces/600x686.ts';
import type { ZoneLayout } from './layout.ts';

export type { ZoneLayout, ZoneRects } from './layout.ts';
export { layoutWithoutRevBar, rectOf, revBarReclaim, zoneLayoutDescription, zonesWithoutRevBar } from './layout.ts';
export { buildZoneFace, faceItems, zonesOf, sizeOf, FACE_SCREEN_NAME, FACE_SCREEN_NAME_NO_REV_BAR, type BuiltFace, type FaceBuildOptions } from './face.ts';
export { zoneDashboard, zoneDashboardName, zoneDashboardsFor, zonePageScreen, zoneWidget, kindOf, type ZoneKind } from './pages.ts';
export { bar, BAR_FIELD_SPECS, STRIP_CELLS } from './bar.ts';
export { zoneAPage, gearSizeIn } from './zoneAPages.ts';
export { bandPageItems, bandCorners, bandCornerWidths, BAND_PAGES, BAND_PAGE_IDS } from './bandPages.ts';
export { zoneFace1920x480, zoneFace1280x480, zoneFace1280x400, zoneFace850x480, zoneFace800x480, zoneFace1280x720, zoneFace800x286, zoneFace600x686 };

/**
 * Every zone face, in the same order `LAYOUTS` lists the card faces, so the two halves of the
 * manifest read alike while both exist.
 */
export const ZONE_FACES: readonly ZoneLayout[] = [
  zoneFace1920x480,
  zoneFace1280x480,
  zoneFace1280x400,
  zoneFace850x480,
  zoneFace800x480,
  zoneFace1280x720,
  zoneFace800x286,
  zoneFace600x686,
];

/**
 * The base face and the large one: the two sizes anything that has to pick a face picks.
 *
 * `bun run dev` opens the base when no package is named, `README.md` photographs both, and a
 * change to the face is looked at in the pair before it is looked at anywhere else. 850 x 480 is
 * the base because it is the tightest artboard the design drew that still carries all five parts,
 * so its 274 x 328 zones are where a page has to stack rather than tabulate; 1280 x 480 is the
 * large one, where the same page gets 469 x 320 and spreads back out. Between them they show that
 * a page is laid out for its box rather than scaled into it, which one capture cannot.
 *
 * Neither is the *reference* face. That is `zoneFace1920x480`, the widest artboard and the one the
 * plugin's pre-face settings migrate into; it is a drawing job rather than a size many people own.
 * docs/scope.md holds the distinction.
 */
export const BASE_FACE = zoneFace850x480;
export const LARGE_FACE = zoneFace1280x480;

/**
 * The fonts a zone face ships. The same three the card face uses: the zones draw the same
 * numerals, and the label face is Barlow Medium.
 */
export { FACE_FONT_FILES, fontsForPackage } from '../dashboard.ts';
