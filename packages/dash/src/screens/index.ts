/**
 * The second-screen packages: the two companions and the two pit walls.
 *
 * A package is the main dashboard plus one zone dashboard per distinct zone rectangle the pages
 * embed. The zone dashboards are derived from the widgets the pages actually placed rather than
 * listed by hand, so a page that moves a zone cannot leave a dangling file behind.
 */
import path from 'node:path';
import type { Dashboard, DashboardMetadata, DashPackage, WidgetItem } from '../generator.ts';
import { DEFAULT_AUTHOR, DEFAULT_SIMHUB_VERSION, type BuildOptions } from '../dashboard.ts';
import { itemsOf } from '../walk.ts';
import { COMPANION_SIZES, companionDashboard, type CompanionSize } from './companion.ts';
import { PIT_WALL_SIZES, pitWallDashboard, type PitWallSize } from './pitwall.ts';
import { GENERATED_FONTS_DIR, prepareFont } from '../design/fontFiles.ts';
import { zoneDashboard, zoneDashboardName, type ZoneKind } from './zones.ts';

export type ScreenKind = 'companion' | 'pitwall';

export interface ScreenPackageDef {
  folder: string;
  kind: ScreenKind;
  width: number;
  height: number;
  description: string;
}

/** Every second-screen package, in the order the manifest lists them. */
export const SCREEN_PACKAGES: readonly ScreenPackageDef[] = [
  ...COMPANION_SIZES.map((s) => ({ folder: s.folder, kind: 'companion' as const, width: s.width, height: s.height, description: s.description })),
  ...PIT_WALL_SIZES.map((s) => ({ folder: s.folder, kind: 'pitwall' as const, width: s.width, height: s.height, description: s.description })),
];

/**
 * The fonts a second screen needs: the face's four, plus Barlow Condensed Light, which the pit
 * wall wordmark's "open" is set in. One list serves both screens, so a companion carries Light
 * without drawing it; the build only refuses the other direction, a weight drawn with no file.
 *
 * Barlow Bold is here because the pit wall's flag band names its flag, and `alertBandName` sets
 * that one label in Bold on every surface that draws it. The companion's band is the nano style and
 * draws no name, which is why the list did without it until the wall had a band of its own.
 */
export const SCREEN_FONT_FILES = ['BarlowCondensed-SemiBold.ttf', 'BarlowCondensed-Bold.ttf', 'BarlowCondensed-Light.ttf', 'Barlow-Medium.ttf', 'Barlow-Bold.ttf'] as const;

export function fontsForScreens(): string[] {
  const dir = path.resolve(import.meta.dir, '..', '..', 'fonts');
  return SCREEN_FONT_FILES.map((f) => prepareFont(path.join(dir, f), path.join(dir, GENERATED_FONTS_DIR)));
}

/** The zone widgets a dashboard placed, keyed by the file they point at. */
export function zoneWidgetsOf(dashboard: Dashboard): Map<string, WidgetItem> {
  const widgets = new Map<string, WidgetItem>();
  for (const item of itemsOf(dashboard)) {
    if (item.kind === 'widget' && !widgets.has(item.fileName)) widgets.set(item.fileName, item);
  }
  return widgets;
}

/** One zone dashboard per file the pages point at, sized to the widget that placed it. */
export function zoneDashboardsFor(main: Dashboard, metadata: DashboardMetadata): Dashboard[] {
  return [...zoneWidgetsOf(main).values()].map((widget) => {
    const kind: ZoneKind = widget.fileName.startsWith('zones-wide') ? 'wide' : 'standard';
    const size = { width: widget.rect.width, height: widget.rect.height };
    const dashboard = zoneDashboard(kind, size, metadata);
    if (`${dashboard.name}.djson` !== widget.fileName) {
      throw new Error(`screens: widget ${widget.name} points at ${widget.fileName} but its zone dashboard is ${dashboard.name}.djson`);
    }
    return dashboard;
  });
}

const companionOf = (def: ScreenPackageDef): CompanionSize => {
  const size = COMPANION_SIZES.find((s) => s.folder === def.folder);
  if (!size) throw new Error(`screens: no companion size for ${def.folder}`);
  return size;
};

const pitWallOf = (def: ScreenPackageDef): PitWallSize => {
  const size = PIT_WALL_SIZES.find((s) => s.folder === def.folder);
  if (!size) throw new Error(`screens: no pit wall size for ${def.folder}`);
  return size;
};

/** The package for a second screen: the main dashboard, its zone dashboards and the fonts. */
export function buildScreenPackage(def: ScreenPackageDef, opts: BuildOptions): DashPackage {
  const metadata: DashboardMetadata = {
    title: opts.title ?? def.folder,
    author: opts.author ?? DEFAULT_AUTHOR,
    description: opts.description ?? def.description,
    version: opts.version,
    simHubVersion: opts.simHubVersion ?? DEFAULT_SIMHUB_VERSION,
  };
  const main = def.kind === 'companion' ? companionDashboard(companionOf(def), metadata) : pitWallDashboard(pitWallOf(def), metadata);
  return { folderName: def.folder, dashboards: [main, ...zoneDashboardsFor(main, metadata)], fonts: fontsForScreens() };
}

export { COMPANION_SIZES, companionDashboard, companionGeometry, companionScreen } from './companion.ts';
export { PIT_WALL_SIZES, pitWallDashboard, racePage, towerPage, telemetryPage, portraitPage } from './pitwall.ts';
export { zoneDashboard, zoneDashboardName, zoneScreen, zoneWidget, pagesOf } from './zones.ts';
export { pitWallHeader, PIT_WALL_HEADER } from './pitwallHeader.ts';
