/**
 * The companion: a phone or a tablet beside the wheel showing one module at a time.
 *
 * Every module is a screen of one dashboard, in catalogue order, and **the plugin decides which one
 * is up**: a screen is enabled when the rotation leaves its module on and `OpenDash.CompanionPage`
 * names it, so exactly one of the twenty-one is enabled at any moment and SimHub shows that one.
 * `EditorModel.CheckGameModeScreen` re-evaluates every screen's expression each frame and moves off
 * a screen that has stopped being enabled, which is the same mechanism the two arrangements of a
 * zone face are chosen by, and it is what gives the companion the two things a face already had: a
 * module it opens on, and a module a held button shows.
 *
 * **What it costs is SimHub's own Next and Previous ring.** With one screen enabled the ring has
 * nowhere to move to, so `CompanionNextModule` replaces it -- a plugin action bound to a wheel
 * button, which advances past whatever the driver has turned off. A package installed without the
 * plugin therefore opens on the first module and stays there, where before it could be paged; that
 * is the price of a start page and a glance, and it is the same price the paged form would have
 * charged.
 *
 * Every screen carries the same roles (in game and idle, not pit). SimHub only filters screens by
 * role when the roles differ between them, so keeping them identical means the page shows whether or
 * not a game is running, which is what a companion is for.
 */
import type { Dashboard, DashboardMetadata, Item, Rect, Screen } from '../generator.ts';
import { MODULE_CATALOGUE, MODULE_COUNT, secondScreen } from '../contract.ts';
import { rect } from '../design/geometry.ts';
import { flagStrip, FLAG_STRIP_STYLES } from '../components/flagStrip.ts';
import { MODULES } from '../modules/index.ts';
import { COMPANION_HEADER, companionHeader, pageDots } from '../second/header.ts';
import { contentRect } from '../second/layout.ts';
import { ds } from '../tokens.ts';

/** Height of the page-dot row and of the flag band at the bottom edge. */
export const DOTS_HEIGHT = 24;
/**
 * The canvas draws a 12 px strip here, as on the nano face, too thin for a label; no token holds
 * that value, so it is written locally as `layouts/800x286.ts` already does. The heightSm token is
 * 32, which stole twenty pixels from the module body on all forty-two screens.
 */
export const FLAG_HEIGHT = 12;

export interface CompanionSize {
  folder: string;
  width: number;
  height: number;
  description: string;
}

/** The two companion packages: the tablet in landscape and the phone stood on end. */
export const COMPANION_SIZES: readonly CompanionSize[] = [
  { folder: 'openDash Companion', width: 850, height: 480, description: '850 x 480, 21 modules' },
  { folder: 'openDash Companion portrait', width: 480, height: 850, description: '480 x 850, 21 modules' },
];

/** Where each part of a companion screen goes. */
export function companionGeometry(size: CompanionSize): { header: Rect; module: Rect; dots: Rect; flags: Rect } {
  const header = rect(0, 0, size.width, COMPANION_HEADER.height);
  const flags = rect(0, size.height - FLAG_HEIGHT, size.width, FLAG_HEIGHT);
  const dots = rect(0, flags.top - DOTS_HEIGHT, size.width, DOTS_HEIGHT);
  return { header, module: rect(0, header.height, size.width, dots.top - header.height), dots, flags };
}

/** One screen: the header, the module, the dots and the flag band. */
export function companionScreen(size: CompanionSize, page: number): Screen {
  const meta = MODULE_CATALOGUE[page - 1];
  const module = MODULES[page - 1];
  if (!meta || !module) throw new RangeError(`companion: no module on page ${page}`);
  const g = companionGeometry(size);
  const prefix = `${meta.id}.`;
  const items: Item[] = [
    ...companionHeader(`${prefix}header`, { frame: g.header, moduleName: meta.name, page, pages: MODULE_COUNT }),
    ...module.build({ frame: contentRect(g.module, 'companion'), density: 'companion', prefix }),
    ...pageDots(`${prefix}dots`, g.dots, MODULE_COUNT, page),
    ...flagStrip(g.flags, FLAG_STRIP_STYLES.nano, `${prefix}flag`),
  ];
  return {
    name: meta.id,
    inGame: true,
    idle: true,
    pit: false,
    backgroundColor: ds.color.surface.base,
    enabledExpression: secondScreen.moduleShown(page),
    items,
  };
}

/** The companion dashboard: 21 screens, one per module, in page order, one of them enabled. */
export function companionDashboard(size: CompanionSize, metadata: DashboardMetadata): Dashboard {
  return {
    name: size.folder,
    width: size.width,
    height: size.height,
    backgroundColor: ds.color.surface.base,
    screens: Array.from({ length: MODULE_COUNT }, (_, i) => companionScreen(size, i + 1)),
    metadata,
  };
}
