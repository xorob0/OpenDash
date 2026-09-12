/**
 * The zone faces the build produces, and everything needed to build one.
 *
 * They are a separate list from `LAYOUTS` rather than entries in it, and that is the whole reason
 * the zone face can be built without disturbing the ten that ship. `build.ts` walks both; XOR-95
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
export { rectOf, zoneLayoutDescription } from './layout.ts';
export { buildZoneFace, faceItems, zonesOf, sizeOf, FACE_SCREEN_NAME, type BuiltFace, type FaceBuildOptions } from './face.ts';
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
 * The fonts a zone face ships. The same three the card face uses: the zones draw the same
 * numerals, and the label face is Barlow Medium.
 */
export { FACE_FONT_FILES, fontsForPackage } from '../dashboard.ts';
