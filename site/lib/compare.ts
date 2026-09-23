/**
 * The comparison with Lovely Sim Racing and Daniel Newman Racing, row by row.
 *
 * Every competitor cell was read from that product's own pages, release notes or shipped package,
 * and nothing is guessed: a claim we could not stand behind was cut rather than softened, which is
 * why no cell says "not checked". OpenDash's column marks what is shipped, what is scheduled (with
 * the issue it is scheduled under) and what is not built, with the scope line where one exists. No
 * competitor image appears anywhere; the comparison is words and OpenDash's own captures.
 */
import { FREE_FOREVER, NOTHING_TO_UNLOCK } from './site';

export type ProductId = 'opendash' | 'lovely' | 'dnr';

export type Mark = 'yes' | 'partial' | 'no' | 'paid' | 'soon' | 'notBuilt';

export interface Cell {
  mark: Mark;
  /** The word in the cell: Free, Included, Compatible, Paid, Coming soon, Not built. */
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
}

export interface CompareRow {
  id: string;
  label: string;
  cells: Record<ProductId, Cell>;
}

export const PRODUCTS: Product[] = [
  { id: 'opendash', name: 'OpenDash', short: 'openDash' },
  { id: 'lovely', name: 'Lovely Sim Racing', short: 'Lovely' },
  { id: 'dnr', name: 'Daniel Newman Racing', short: 'DNR' },
];

const WORD: Record<Mark, string> = { yes: 'Included', partial: 'Partly', no: 'No', paid: 'Paid', soon: 'Coming soon', notBuilt: 'Not built' };
const cell = (mark: Mark, text: string, extra: Partial<Cell> = {}): Cell => ({ mark, word: WORD[mark], text, ...extra });

export const ROWS: CompareRow[] = [
  {
    id: 'sims',
    label: 'Sims',
    cells: {
      opendash: cell('partial', 'Built and tested on iRacing. Other sims may work. We do not claim them.', { word: 'iRacing' }),
      lovely: cell('yes', '12 sims natively, and any sim SimHub reads.', { word: 'Compatible' }),
      dnr: cell('yes', '9 sims with their own presets, 7 with measured car data, and any sim SimHub reads.', { word: 'Compatible' }),
    },
  },
  {
    id: 'price',
    label: 'Price',
    cells: {
      opendash: cell('yes', `${FREE_FOREVER} ${NOTHING_TO_UNLOCK}`, { word: 'Free' }),
      lovely: cell('paid', 'The free tier caps you at 3 modules a side and has no pit wall. €1 a month lifts the cap, €3 adds the pit wall, €9 adds the partner LED app. Tax included.'),
      dnr: cell('paid', 'The dashboards are free the way they ship. Changing any setting starts at £3 a month, then £6 and £9, all before VAT.'),
    },
  },
  {
    id: 'licence',
    label: 'Licence',
    cells: {
      opendash: cell('yes', 'Use it, change it, sell it. Nobody to ask.', { word: 'MIT' }),
      lovely: cell('partial', 'Non-commercial. One key on 2 PCs. Remix it privately, never share what you make. No refunds.', { word: 'Restricted' }),
      dnr: cell('partial', 'One PC per plan, two on the £9 tier. No sharing the files, no taking them apart.', { word: 'Restricted' }),
    },
  },
  {
    id: 'accounts',
    label: 'Activation and accounts',
    cells: {
      opendash: cell('yes', 'Download and race.', { word: 'None' }),
      lovely: cell('partial', 'A key for the paid tiers, checked online every 15 minutes.', { word: 'Licence key' }),
      dnr: cell('partial', 'An account and a sign-in, even to install the free dashboards.', { word: 'Account' }),
    },
  },
  {
    id: 'sizes',
    label: 'Screen sizes',
    cells: {
      opendash: cell('yes', '10 faces: 1920 × 480, 1280 × 720, 1280 × 480, 1280 × 400, 850 × 480, 800 × 480, 800 × 286, 600 × 686, 800 round, 480 round.'),
      lovely: cell('yes', '850 × 480, 1280 × 480, 1280 × 400, 1920 × 480, 800 × 286, 600 × 686, plus round and square.'),
      dnr: cell('yes', '800 × 480, 1280 × 400, 1920 × 480, 1920 × 720 and 1920 × 1080, across 7 dashboards.'),
    },
  },
  {
    id: 'noPlugin',
    label: 'Works without the plugin',
    cells: {
      opendash: cell('yes', 'Every package carries its own defaults.', { word: 'Yes' }),
      lovely: cell('no', 'The dashboards cannot run without the plugin. Their words.'),
      dnr: cell('no', 'Without the plugin a dashboard draws a notice screen instead of your data.'),
    },
  },
  {
    id: 'companion',
    label: 'Second screen',
    cells: {
      opendash: cell('yes', '850 × 480 and 480 × 850, 21 pages.'),
      lovely: cell('yes', 'A companion, landscape and portrait, free.'),
      dnr: cell('yes', 'Co-Pilot, 14 panels. Choosing which ones appear costs £3 a month.'),
    },
  },
  {
    id: 'pitWall',
    label: 'Pit wall',
    cells: {
      opendash: cell('yes', '1920 × 1080 with 3 pages, and 1080 × 1920.'),
      lovely: cell('paid', 'Lovely Pit Wall, on the €3 tier and up.'),
      dnr: cell('yes', 'Race Control, free: the field, a track map and traces.'),
    },
  },
  {
    id: 'leds',
    label: 'LED profiles and per-car shift lights',
    cells: {
      opendash: cell('yes', '62 strip shapes, and the car’s own lights from the open Lovely car data, fetched by the plugin.', { word: 'Free' }),
      lovely: cell('partial', 'Lovely dropped its own LED profiles in 2023. Per-car shift lights come from ATSR, a separate app, free only on the €9 tier.', { word: 'Third party' }),
      dnr: cell('paid', 'Every profile needs a membership, from £3 a month. Shift points measured for over 600 cars.'),
    },
  },
  {
    id: 'flagBox',
    label: 'Flag box, 8 × 8 matrix',
    cells: {
      opendash: cell('yes', '69 glyphs, generated. Flags, pit states, warnings, the gear and the spotter.', { word: 'Free' }),
      lovely: cell('no', 'Nothing in the dashboards or the plugin drives a matrix.'),
      dnr: cell('paid', 'An 8 × 8 box and the SimRep panel, on the £6 tier.'),
    },
  },
  {
    id: 'idle',
    label: 'Idle screen',
    cells: {
      opendash: cell('soon', '', { issues: [113] }),
      lovely: cell('yes', 'An animated screen, free. It needs SimHub’s HTML renderer.'),
      dnr: cell('paid', 'The logo, a driver tag or the car. Choosing costs £3 a month.'),
    },
  },
  {
    id: 'alerts',
    label: 'Alerts and pop-ups',
    cells: {
      opendash: cell('yes', '15 flags on the band, pit alerts over the gear, pop-ups for lap times and setting changes.'),
      lovely: cell('yes', 'Weather, damage, setup changes, a lap review and a pit-now warning.'),
      dnr: cell('yes', 'Flags, car state and setting changes, in 3 sizes. Choosing the size costs £3 a month.'),
    },
  },
  {
    id: 'pitPage',
    label: 'Pit page on the face',
    cells: {
      opendash: cell('soon', 'Pit view is a page today. A page that opens the moment you cross the line is coming.', { issues: [383] }),
      lovely: cell('partial', 'No pit page. The whole interface turns blue in the lane.'),
      dnr: cell('partial', 'A pit page, but you page to it yourself.'),
    },
  },
  {
    id: 'manager',
    label: 'Dashboard manager and first run',
    cells: {
      opendash: cell('soon', 'The plugin installs every package and updates itself. Browsing, removing and a first-run guide are coming.', { issues: [84, 85] }),
      lovely: cell('yes', 'A dashboard manager that installs and updates.'),
      dnr: cell('yes', 'A wizard that puts a dashboard on each screen you own, plus updates and older versions.'),
    },
  },
  {
    id: 'night',
    label: 'Night mode',
    cells: {
      opendash: cell('soon', 'The lights have a night brightness today. The screens are coming.', { issues: [128] }),
      lovely: cell('yes', 'True Dark Mode, free, on your headlights or a hotkey.'),
      dnr: cell('paid', 'Dark mode from £3 a month. It follows the game’s own night in 2 sims.'),
    },
  },
  {
    id: 'themes',
    label: 'Themes and colours',
    cells: {
      opendash: cell('soon', '', { issues: [127, 99] }),
      lovely: cell('yes', '5 colour themes, free, plus your name, number and logo.'),
      dnr: cell('paid', 'Themes, gauges and colours, from £3 a month.'),
    },
  },
  {
    id: 'driverRows',
    label: 'Licence and rating on driver rows',
    cells: {
      opendash: cell('soon', 'iRating is on the opponents page and the pit wall tower. The nationality flag and the licence badge are coming.', { issues: [149] }),
      lovely: cell('yes', 'Licence and iRating on the opponents modules, iRacing only.'),
      dnr: cell('yes', 'Licence, rating, pit status and strength of field.'),
    },
  },
  {
    id: 'flagsScreen',
    label: 'Flags screen for a second display',
    cells: {
      opendash: cell('soon', '', { issues: [116] }),
      lovely: cell('yes', 'Lovely Flags, free, in 3 shapes.'),
      dnr: cell('partial', 'A flags panel drawn over the game, on the £6 tier. No screen of its own.'),
    },
  },
  {
    id: 'overlay',
    label: 'Stream overlay',
    cells: {
      opendash: cell('notBuilt', 'Nobody has asked for one.', { scope: 'the stream overlay is neither built nor refused; nobody has asked for it.' }),
      lovely: cell('yes', 'Lovely Overlay and Lovely Tower, free.'),
      dnr: cell('paid', '5 overlays, on the £6 tier.'),
    },
  },
  {
    id: 'invisible',
    label: 'Invisible dash',
    cells: {
      opendash: cell('notBuilt', 'A see-through dash over the game is an overlay, and nobody has asked for one.'),
      lovely: cell('no', 'Nothing of the kind ships.'),
      dnr: cell('yes', 'Invisible: the sim drawn through your own dash screen, on a hotkey. Free.'),
    },
  },
  {
    id: 'teammates',
    label: 'Teammate telemetry',
    cells: {
      opendash: cell('notBuilt', 'Nothing about you leaves your machine.', { scope: 'telemetry about the user: nothing about the user leaves their machine.' }),
      lovely: cell('paid', 'TeamLINQ, on the team plans only, from €5 a month. Still in beta.'),
      dnr: cell('paid', 'RELAY, from £3 a month, and every driver needs their own.'),
    },
  },
  {
    id: 'vendor',
    label: 'Vendor wheel integrations',
    cells: {
      opendash: cell('partial', 'Named wirings for SimRep, Ascher, GridSim and Fanatec. Every other device is matched by its shape, not its brand.', { word: 'Some' }),
      lovely: cell('yes', 'Ascher, MOZA, Heusinkveld, Conspit and more, each with its own build.'),
      dnr: cell('yes', 'Button layouts drawn for around 60 rims.'),
    },
  },
  {
    id: 'source',
    label: 'Buildable from source',
    cells: {
      opendash: cell('yes', 'TypeScript and design tokens. One command.', { word: 'Yes' }),
      lovely: cell('no', 'The public repository holds a readme. The dashboards ship as binaries.'),
      dnr: cell('no', 'Closed, and the licence forbids taking it apart.'),
    },
  },
  {
    id: 'privacy',
    label: 'What leaves your machine',
    cells: {
      opendash: cell('yes', 'An update check to GitHub, and the car tables when you press the button. Nothing about you, ever.', { word: 'Nothing' }),
      lovely: cell('partial', 'Your key and PC name every 15 minutes, and the car you are driving, to fetch its logo. No privacy statement covers the plugin.', { word: 'Some' }),
      dnr: cell('partial', 'Your name, your email, every machine you sign in on with the IP it came from, and a count of which locked buttons you press.', { word: 'Some' }),
    },
  },
  {
    id: 'community',
    label: 'Community',
    cells: {
      opendash: cell('partial', 'Issues and pull requests. No Discord.', { word: 'GitHub' }),
      lovely: cell('yes', 'About 18,700.', { word: 'Discord' }),
      dnr: cell('yes', 'About 7,800.', { word: 'Discord' }),
    },
  },
];

/** The coming-soon rows, for the list under the table. */
export const SCHEDULED = ROWS.filter((r) => r.cells.opendash.mark === 'soon');
