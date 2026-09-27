/**
 * Composes a layout into the two dashboards of a package: the main face (rules, hero, slots)
 * and the cards widget (one screen per card). Serialising, validating and zipping are the
 * generator's job; build.ts drives that.
 */
import path from 'node:path';
import type { Dashboard, DashboardMetadata, DashPackage } from './generator.ts';
import { hero } from './hero/hero.ts';
import { GENERATED_FONTS_DIR, prepareFont } from './design/fontFiles.ts';
import { rect } from './design/geometry.ts';
import { idleScreen } from './idle.ts';
import type { Layout } from './layouts/layout.ts';
import { faceOf, innerDiameter } from './layouts/round.ts';
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
        // Not the idle screen any more: showing this face with no game behind it is the bug #763 is
        // about, and the screen after it is what SimHub shows instead.
        idle: false,
        pit: true,
        backgroundColor: layout.background,
        items: [...rules, ...hero(layout.hero), ...slots],
      },
      // Last, so that screen 0 is still the racing face: SimHub's own `MainPreviewIndex` is the first
      // in-game screen and every test that reaches for `screens[0]` means the same thing it meant.
      idleScreen({
        frame: rect(0, 0, layout.width, layout.height),
        background: layout.background,
        // A round face is a disc inside its bounding square, and nothing may lie outside the inner
        // disc the flag ring leaves; `layouts/round.ts` is where both come from.
        ...(layout.shape === 'round' ? { disc: { ...faceOf(layout.width), r: innerDiameter(layout.width) / 2 } } : {}),
      }),
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

/**
 * The TTFs the face uses; the others in fonts/ stay for the plugin and future surfaces. This is
 * the set of faces the face draws in rather than the set it could ask for, and `validateOrThrow`
 * in build.ts refuses a package that draws a weight missing from it, so a weight is added here,
 * and measured into design/advances.ts, before anything is drawn in it.
 */
export const FACE_FONT_FILES = [
  'BarlowCondensed-SemiBold.ttf',
  'BarlowCondensed-Bold.ttf',
  // The idle screen's wordmark, whose "open" is Light: the second screens have always carried this
  // file for the pit wall header, and #763 put the same two words on every face at rest.
  'BarlowCondensed-Light.ttf',
  'Barlow-Medium.ttf',
  'Barlow-Bold.ttf',
] as const;

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
