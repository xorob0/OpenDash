/**
 * The licences a package has to carry because of what it redistributes.
 *
 * A `.simhubdash` is a redistribution, not a build artefact that stays on this machine. The SIL
 * Open Font Licence requires its notice to accompany the fonts wherever they go, and every
 * package openDash ships carries five Barlow faces in `_SHFonts/`, so every package owes the
 * notice. It was not carrying one, which is the gap this closes.
 *
 * The notices are derived from what the package actually holds rather than listed by hand, so a
 * package that gains an asset gains its licence in the same commit. An asset class with no
 * registered notice is refused outright: a missing licence is a licence breach that no test would
 * otherwise catch, and it is better to fail the build than to publish one.
 */

import path from 'node:path';
import type { DashPackage, NoticeFile } from '../generator.ts';
import { VENDORED_FONTS_DIR } from './fontFiles.ts';

/** Barlow, under the SIL Open Font Licence 1.1. */
export const FONT_LICENCE: NoticeFile = { name: 'OFL.txt', path: path.join(VENDORED_FONTS_DIR, 'OFL.txt') };

/**
 * The notices owed for the images a package carries.
 *
 * Empty until something ships an image. Material Design Icons are Apache 2.0, which requires both
 * the licence text and the NOTICE naming Pictogrammers, and both belong here beside the artwork
 * when XOR-97 brings it.
 */
export const IMAGE_LICENCES: readonly NoticeFile[] = [];

/** Every licence `pkg` owes, in the order they are written into the folder. */
export const noticesForPackage = (pkg: DashPackage): NoticeFile[] => {
  const notices: NoticeFile[] = [];
  if (pkg.fonts.length > 0) notices.push(FONT_LICENCE);
  if (pkg.dashboards.some((dashboard) => dashboard.images?.length)) {
    if (IMAGE_LICENCES.length === 0) {
      throw new Error(`${pkg.folderName} carries images but no licence is registered for them; add it to IMAGE_LICENCES before shipping the artwork`);
    }
    notices.push(...IMAGE_LICENCES);
  }
  return notices;
};

/** The same list for the plugin, which redistributes the panel's fonts and nothing else. */
export const PANEL_NOTICES: readonly NoticeFile[] = [FONT_LICENCE];
