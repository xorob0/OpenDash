/**
 * Composes a layout into the two dashboards of a package: the main face (rules, hero, slots)
 * and the cards widget (one screen per card). Serialising, validating and zipping are the
 * generator's job; build.ts drives that.
 */
import path from 'node:path';
import type { Dashboard, DashboardMetadata, DashPackage } from './generator.ts';
import { hero } from './hero/hero.ts';
import { GENERATED_FONTS_DIR, prepareFont } from './design/fontFiles.ts';
import type { Layout } from './layouts/layout.ts';
import { rule } from './elements/rule.ts';
import { CARDS_DASHBOARD_NAME, cardScreens, DEFAULT_STRATEGY, inlineSlotItems, widgetSlotItems, type SlotStrategy } from './slots.ts';

export const DEFAULT_SIMHUB_VERSION = '9.12.6';
export const DEFAULT_AUTHOR = 'OpenDash contributors';
export const MAIN_SCREEN_NAME = 'Main';

export interface BuildOptions {
  /** Contents of VERSION; the plugin compares it to decide whether to reinstall. */
  version: string;
  simHubVersion?: string;
  strategy?: SlotStrategy;
  /** Default: the layout's folder name. */
  title?: string;
  author?: string;
  /** Default: the layout's description. */
  description?: string;
}

export interface BuiltLayout {
  main: Dashboard;
  /** The cards widget. Unused by the inline strategy, which draws every card in the main dashboard. */
  cards: Dashboard;
}

export function buildLayout(layout: Layout, opts: BuildOptions): BuiltLayout {
  const strategy = opts.strategy ?? DEFAULT_STRATEGY;
  const title = opts.title ?? layout.folder;
  const metadata: DashboardMetadata = {
    title,
    author: opts.author ?? DEFAULT_AUTHOR,
    description: opts.description ?? layout.description,
    version: opts.version,
    simHubVersion: opts.simHubVersion ?? DEFAULT_SIMHUB_VERSION,
  };
  const rules = layout.rules.map((r) => rule(r.name, r.rect.left, r.rect.top, r.rect.width, r.rect.height));
  const slots = strategy === 'widget' ? widgetSlotItems(layout) : inlineSlotItems(layout);
  const main: Dashboard = {
    name: layout.folder,
    width: layout.width,
    height: layout.height,
    backgroundColor: layout.background,
    screens: [
      {
        name: MAIN_SCREEN_NAME,
        inGame: true,
        idle: true,
        pit: true,
        backgroundColor: layout.background,
        items: [...rules, ...hero(layout.hero), ...slots],
      },
    ],
    metadata,
  };
  const cards: Dashboard = {
    name: CARDS_DASHBOARD_NAME,
    width: layout.slotSize.width,
    height: layout.slotSize.height,
    backgroundColor: layout.background,
    screens: cardScreens(layout),
    metadata: { ...metadata, title: `${title} cards`, description: 'One screen per card; the main dashboard embeds this in every slot.' },
  };
  return { main, cards };
}

/** The TTFs the face uses; the others in fonts/ stay for the plugin and future surfaces. */
export const FACE_FONT_FILES = ['BarlowCondensed-SemiBold.ttf', 'BarlowCondensed-Bold.ttf', 'Barlow-Medium.ttf'] as const;

/**
 * Absolute paths of the fonts to copy into `_SHFonts/`, renamed on the way so that SimHub resolves
 * the condensed family at all; `design/fontFiles.ts` explains why that is necessary.
 */
export function fontsForPackage(): string[] {
  const dir = path.resolve(import.meta.dir, '..', 'fonts');
  return FACE_FONT_FILES.map((f) => prepareFont(path.join(dir, f), path.join(dir, GENERATED_FONTS_DIR)));
}

/** The package for a layout: folder, dashboards (cards.djson only with the widget strategy) and fonts. */
export function buildPackage(layout: Layout, opts: BuildOptions): DashPackage {
  const { main, cards } = buildLayout(layout, opts);
  const strategy = opts.strategy ?? DEFAULT_STRATEGY;
  return { folderName: layout.folder, dashboards: strategy === 'widget' ? [main, cards] : [main], fonts: fontsForPackage() };
}
