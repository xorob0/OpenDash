/**
 * The licences a package has to carry because of what it redistributes.
 *
 * A `.simhubdash` is a redistribution, not a build artefact that stays on this machine. The SIL
 * Open Font Licence requires its notice to accompany the fonts wherever they go, and every
 * package OpenDash ships carries five Barlow faces in `_SHFonts/`, so every package owes the
 * notice. It was not carrying one, which is the gap this closes.
 *
 * The notices are derived from what the package actually holds rather than listed by hand, so a
 * package that gains an asset gains its licence in the same commit. An asset class with no
 * registered notice is refused outright: a missing licence is a licence breach that no test would
 * otherwise catch, and it is better to fail the build than to publish one.
 */

import path from 'node:path';
import { assetNamed, type AssetSourceId } from './assets.ts';
import type { DashPackage, NoticeFile } from '../generator.ts';
import { VENDORED_FONTS_DIR } from './fontFiles.ts';

/** Barlow, under the SIL Open Font Licence 1.1. */
export const FONT_LICENCE: NoticeFile = { name: 'OFL.txt', path: path.join(VENDORED_FONTS_DIR, 'OFL.txt') };

/**
 * What each source of artwork owes, one entry per source of `design/assets.ts`.
 *
 * The record is exhaustive over the sources, so artwork from somewhere new does not compile until
 * somebody has answered what travels with it. An empty list is that answer and not an oversight:
 * OpenDash's own drawings are MIT with the repository they are published from, and nothing
 * separate has to accompany them. Material Design Icons, which the twelve telltale pictograms are
 * still waiting on, are Apache 2.0 and will owe both the licence text and the NOTICE naming
 * Pictogrammers, in the commit that brings the files.
 */
export const NOTICES_BY_SOURCE: Record<AssetSourceId, readonly NoticeFile[]> = {
  OpenDash: [],
};

/** Every licence `pkg` owes, in the order they are written into the folder. */
export const noticesForPackage = (pkg: DashPackage): NoticeFile[] => {
  const notices: NoticeFile[] = [];
  if (pkg.fonts.length > 0) notices.push(FONT_LICENCE);
  for (const image of pkg.dashboards.flatMap((dashboard) => dashboard.images ?? [])) {
    const asset = assetNamed(image.name);
    if (asset === undefined) {
      throw new Error(
        `${pkg.folderName} carries the image ${JSON.stringify(image.name)}, which no asset claims, so no licence is registered for it; add it to ASSETS in design/assets.ts before shipping the artwork`,
      );
    }
    for (const notice of NOTICES_BY_SOURCE[asset.source]) if (!notices.some((carried) => carried.name === notice.name)) notices.push(notice);
  }
  return notices;
};

/** The same list for the plugin, which redistributes the panel's fonts and nothing else. */
export const PANEL_NOTICES: readonly NoticeFile[] = [FONT_LICENCE];
