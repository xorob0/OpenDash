/**
 * The comparison with Lovely Sim Racing and Daniel Newman Racing, row by row.
 *
 * Every competitor cell was read from that product's own public pages on the date in `asOf`, and
 * nothing is inferred: a feature their pages do not state is `unchecked`, not `no`. OpenDash's
 * column marks what is shipped, what is scheduled (with the issue it is scheduled under) and what
 * is not built, with the scope line where one exists. No competitor image appears anywhere; the
 * comparison is words and OpenDash's own captures.
 */
import { FREE_FOREVER, NOTHING_TO_UNLOCK } from './site';

export type ProductId = 'opendash' | 'lovely' | 'dnr';

export type Mark = 'yes' | 'partial' | 'no' | 'paid' | 'soon' | 'notBuilt' | 'unchecked';

export interface Cell {
  mark: Mark;
  /** The word in the cell: Free, Included, Compatible, Paid, Coming soon, Not built, Not checked. */
  word: string;
  text: string;
  /** The issues a coming feature is scheduled under. Required when the mark is `soon`. */
  issues?: number[];
  /** The line in docs/scope.md a refusal cites, when one exists. */
  scope?: string;
}

export interface Product {
  id: ProductId;
  name: string;
  short: string;
  /** ISO date the product's pages were read. Competitors only. */
  asOf?: string;
}

export interface CompareRow {
  id: string;
  label: string;
  cells: Record<ProductId, Cell>;
}

export const CHECKED_ON = '2026-09-22';

export const PRODUCTS: Product[] = [
  { id: 'opendash', name: 'OpenDash', short: 'openDash' },
  { id: 'lovely', name: 'Lovely Sim Racing', short: 'Lovely', asOf: CHECKED_ON },
  { id: 'dnr', name: 'Daniel Newman Racing', short: 'DNR', asOf: CHECKED_ON },
];

const WORD: Record<Mark, string> = { yes: 'Included', partial: 'Partly', no: 'No', paid: 'Paid', soon: 'Coming soon', notBuilt: 'Not built', unchecked: 'Not checked' };
const cell = (mark: Mark, text: string, extra: Partial<Cell> = {}): Cell => ({ mark, word: WORD[mark], text, ...extra });
const unchecked = cell('unchecked', '');

export const ROWS: CompareRow[] = [
  {
    id: 'sims',
    label: 'Sims',
    cells: {
      opendash: cell('partial', 'iRacing, tested. Other sims may work and are not claimed.', { word: 'iRacing' }),
      lovely: cell('yes', '12 sims natively, and any sim SimHub reads.', { word: 'Compatible' }),
      dnr: cell('yes', '7 sims with measured car data, and any sim SimHub reads.', { word: 'Compatible' }),
    },
  },
  {
    id: 'price',
    label: 'Price',
    cells: {
      opendash: cell('yes', `${FREE_FOREVER} ${NOTHING_TO_UNLOCK}`, { word: 'Free' }),
      lovely: cell('paid', 'Free tier limited to 3 modules per side and no pit wall. Starter €1 a month, Pro €3, Gold €9.'),
      dnr: cell('paid', 'Dashboards free as shipped. Changing anything starts at Pit Crew, £3 a month. Team Driver £6, Team Principal £9, all before VAT.'),
    },
  },
  {
    id: 'licence',
    label: 'Licence',
    cells: {
      opendash: cell('yes', 'MIT. Use it, change it, redistribute it.', { word: 'MIT' }),
      lovely: cell('partial', 'A licence key, on at most 2 PCs. No refunds. Source published, reuse of the design forbidden.', { word: 'Restricted' }),
      dnr: cell('partial', '1 PC per plan, 2 on Team Principal.', { word: 'Restricted' }),
    },
  },
  {
    id: 'accounts',
    label: 'Activation and accounts',
    cells: {
      opendash: cell('yes', '', { word: 'None' }),
      lovely: cell('partial', 'A licence key for the paid tiers.', { word: 'Licence key' }),
      dnr: cell('partial', 'A membership for anything beyond the shipped dashboards.', { word: 'Membership' }),
    },
  },
  {
    id: 'sizes',
    label: 'Screen sizes',
    cells: {
      opendash: cell('yes', '10 faces: 1920 × 480, 1280 × 480, 1280 × 400, 1280 × 720, 850 × 480, 800 × 480, 800 × 286, 600 × 686, 800 round, 480 round.'),
      lovely: cell('yes', 'Standard, curved, square, round, nano 800 × 286, 1280 × 400, 1280 × 480, 1920 × 480, 800 round, and the Heusinkveld DisplayDash.'),
      dnr: cell('yes', '7 dashboards for VoCore, DDUs, wheels, phones, tablets and monitors. Sizes not listed.'),
    },
  },
  {
    id: 'noPlugin',
    label: 'Works without the plugin',
    cells: {
      opendash: cell('yes', 'Yes. Every dashboard carries its own defaults.', { word: 'Yes' }),
      lovely: cell('no', 'The dashboards cannot run without the Lovely Plugin, in their own words.'),
      dnr: cell('no', 'The plugin is part of the install; running without it is not described.'),
    },
  },
  {
    id: 'companion',
    label: 'Second screen',
    cells: {
      opendash: cell('yes', 'A companion at 850 × 480 and 480 × 850, 21 pages.'),
      lovely: cell('yes', 'A companion, landscape and portrait.'),
      dnr: cell('yes', 'Co-Pilot.'),
    },
  },
  {
    id: 'pitWall',
    label: 'Pit wall',
    cells: {
      opendash: cell('yes', '1920 × 1080 with 3 pages, and 1080 × 1920.'),
      lovely: cell('paid', 'Lovely Pit Wall, on Pro at €3 a month.'),
      dnr: cell('yes', 'Race Control.'),
    },
  },
  {
    id: 'leds',
    label: 'LED profiles and per-car shift lights',
    cells: {
      opendash: cell('yes', '62 strip shapes. The car’s own lights from the open Lovely Car Data table, fetched by the plugin.', { word: 'Free' }),
      lovely: unchecked,
      dnr: cell('paid', 'Every LED profile needs a membership. Shift points measured for over 600 cars.'),
    },
  },
  {
    id: 'flagBox',
    label: 'Flag box, 8 × 8 matrix',
    cells: {
      opendash: cell('yes', '69 glyphs, generated. Flags, pit states, warnings, the gear and the spotter.', { word: 'Free' }),
      lovely: unchecked,
      dnr: cell('paid', 'Matrix profiles on Team Driver, £6 a month.'),
    },
  },
  {
    id: 'idle',
    label: 'Idle screen',
    cells: {
      opendash: cell('soon', '', { issues: [113] }),
      lovely: cell('yes', 'An animated screen, in the free tier.'),
      dnr: cell('yes', 'DNR, driver, car or your own picture.'),
    },
  },
  {
    id: 'alerts',
    label: 'Alerts and pop-ups',
    cells: {
      opendash: cell('yes', '15 flags on the band, pit alerts over the gear, pop-ups for lap times and setting changes.'),
      lovely: unchecked,
      dnr: cell('yes', 'Car state, incidents, setting changes and the full flag stack.'),
    },
  },
  {
    id: 'pitPage',
    label: 'Pit page on the face',
    cells: {
      opendash: cell('soon', 'Pit view is a page today. A pit page that opens on entering the lane is coming.', { issues: [383] }),
      lovely: unchecked,
      dnr: unchecked,
    },
  },
  {
    id: 'manager',
    label: 'Dashboard manager and first run',
    cells: {
      opendash: cell('soon', 'The plugin installs all 14 and updates itself. Browsing, removing and a first-run guide are coming.', { issues: [84, 85] }),
      lovely: cell('yes', 'A dashboard manager in the plugin.'),
      dnr: cell('yes', 'Through the plugin.'),
    },
  },
  {
    id: 'night',
    label: 'Night mode',
    cells: {
      opendash: cell('soon', 'Brightness and night brightness for the lights today. Screens coming.', { issues: [128] }),
      lovely: cell('yes', 'True Dark Mode.'),
      dnr: cell('paid', 'Dark mode with day and night switching, from Pit Crew.'),
    },
  },
  {
    id: 'themes',
    label: 'Themes and colours',
    cells: {
      opendash: cell('soon', '', { issues: [127, 99] }),
      lovely: unchecked,
      dnr: cell('paid', 'Colours and customisation from Pit Crew.'),
    },
  },
  {
    id: 'driverRows',
    label: 'Licence and rating on driver rows',
    cells: {
      opendash: cell('soon', '', { issues: [149] }),
      lovely: unchecked,
      dnr: unchecked,
    },
  },
  {
    id: 'flagsScreen',
    label: 'Flags screen for a second display',
    cells: {
      opendash: cell('soon', '', { issues: [116] }),
      lovely: cell('yes', 'Flags variants: standard, round and square.'),
      dnr: unchecked,
    },
  },
  {
    id: 'overlay',
    label: 'Stream overlay',
    cells: {
      opendash: cell('notBuilt', 'Not built. Nobody has asked for it.', { scope: 'the stream overlay is neither built nor refused; nobody has asked for it.' }),
      lovely: cell('yes', 'Lovely Overlay and Lovely Tower.'),
      dnr: cell('paid', 'Overlays on Team Driver, £6 a month.'),
    },
  },
  {
    id: 'invisible',
    label: 'Invisible dash',
    cells: {
      opendash: cell('notBuilt', 'Not built. A see-through dash over the game is an overlay, and nobody has asked for one.'),
      lovely: unchecked,
      dnr: cell('yes', 'Invisible, a see-through overlay.'),
    },
  },
  {
    id: 'teammates',
    label: 'Teammate telemetry',
    cells: {
      opendash: cell('notBuilt', 'Not built. Nothing about you leaves your machine.', { scope: 'telemetry about the user: nothing about the user leaves their machine.' }),
      lovely: cell('yes', 'TeamLINQ.'),
      dnr: cell('paid', 'DNR RELAY, from Pit Crew.'),
    },
  },
  {
    id: 'vendor',
    label: 'Vendor wheel integrations',
    cells: {
      opendash: cell('notBuilt', 'Not built. A strip is a shape, not a brand: any sides and centre are covered.'),
      lovely: cell('yes', 'Editions for several wheel makers.'),
      dnr: unchecked,
    },
  },
  {
    id: 'source',
    label: 'Buildable from source',
    cells: {
      opendash: cell('yes', 'Yes. TypeScript and design tokens, 1 command.', { word: 'Yes' }),
      lovely: cell('no', 'Source published, reuse forbidden.'),
      dnr: cell('no', 'Closed.'),
    },
  },
  {
    id: 'privacy',
    label: 'What leaves your machine',
    cells: {
      opendash: cell('yes', 'An optional daily update check to GitHub. Nothing else.', { word: 'Nothing' }),
      lovely: unchecked,
      dnr: unchecked,
    },
  },
  {
    id: 'community',
    label: 'Community',
    cells: {
      opendash: cell('partial', 'GitHub issues. No Discord.', { word: 'GitHub' }),
      lovely: cell('yes', 'A Discord of about 18,000.', { word: 'Discord' }),
      dnr: cell('yes', 'A Discord of about 7,800.', { word: 'Discord' }),
    },
  },
];

/** The coming-soon rows, for the list under the table. */
export const SCHEDULED = ROWS.filter((r) => r.cells.opendash.mark === 'soon');
