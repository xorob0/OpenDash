/**
 * The comparison with Lovely Sim Racing and Daniel Newman Racing, row by row.
 *
 * Every competitor cell was read from that product's own pages, release notes or shipped package,
 * and nothing is guessed: a claim we could not stand behind was cut rather than softened, which is
 * why no cell says "not checked". OpenDash's column marks what is shipped, what is scheduled (with
 * the issue it is scheduled under) and what is not built, with the scope line where one exists. No
 * competitor image appears anywhere; the comparison is words and OpenDash's own captures.
 */
import { and, counted, type Counts } from './counts';
import { sizeLabel } from './packages';
import { FREE_FOREVER, NOTHING_TO_UNLOCK, THEMES_FREE } from './site';

/**
 * The date the competitor cells were last read against their products' own pages. The table prints
 * it, so that a reader knows how old a line about Lovely or DNR is. A whole table read again moves
 * it; a row read again on its own, or written after this date, carries its own `readOn` instead,
 * and the page prints that row's date beside this one, so neither date claims a reading the other
 * row did not have.
 */
export const CHECKED_ON = '2026-09-22';

export type ProductId = 'opendash' | 'lovely' | 'dnr';

export type Mark = 'yes' | 'partial' | 'no' | 'paid' | 'soon' | 'notBuilt';

export interface Cell {
  mark: Mark;
  /** The word in the cell: Free, Included, Compatible, Paid, Coming soon, Not built. */
  word: string;
  text: string;
  /** The issues a coming feature is scheduled under. Required when the mark is `soon`. */
  issues?: number[];
  /**
   * The line in docs/scope.md a refusal cites, when one exists: the refusal's name as the document
   * sets it in bold, a colon, and words of that refusal's own paragraphs, quoted. A test holds both
   * halves to the document, so a refusal that moves takes the cell with it (#548).
   */
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
  /** The date this row's competitor cells were last read, when that is later than CHECKED_ON. */
  readOn?: string;
}

export const PRODUCTS: Product[] = [
  { id: 'opendash', name: 'OpenDash', short: 'openDash' },
  { id: 'lovely', name: 'Lovely Sim Racing', short: 'Lovely' },
  { id: 'dnr', name: 'Daniel Newman Racing', short: 'DNR' },
];

const WORD: Record<Mark, string> = { yes: 'Included', partial: 'Partly', no: 'No', paid: 'Paid', soon: 'Coming soon', notBuilt: 'Not built' };
const cell = (mark: Mark, text: string, extra: Partial<Cell> = {}): Cell => ({ mark, word: WORD[mark], text, ...extra });

/** A screen as the build makes it, for a cell that names its size. */
export interface Screen {
  width: number;
  height: number;
  round: boolean;
}

/** A car theme beside the default, as the catalogue has it. */
export interface Theme {
  name: string;
  /** The cars it is drawn for. */
  cars: readonly string[];
  /** The face sizes it claims. */
  sizes: readonly unknown[];
}

/**
 * What a cell lists rather than counts, read from the build like the counts (#819): the faces, the
 * companions and the pit walls in reading order, and the car themes beside the default. It is a
 * parameter rather than an import so that this file still reads nothing generated, and the tests
 * pass lists of their own.
 */
export interface Listed {
  faces: readonly Screen[];
  companions: readonly Screen[];
  pitWalls: readonly Screen[];
  themes: readonly Theme[];
}

/** The faces' cell: the count, then every size, so that the one cannot say ten and the other list nine. */
const facesCell = (faces: readonly Screen[]): string =>
  faces.length > 0 ? `${counted(faces.length, 'face')}: ${faces.map(sizeLabel).join(', ')}.` : 'A face for every size the build makes.';

/** The pit walls' cell. The page count is the landscape package's; the portrait one has a page of its own. */
function pitWallCell(walls: readonly Screen[], pages: number): string {
  const landscape = walls.filter((w) => w.width >= w.height).map(sizeLabel);
  const portrait = walls.filter((w) => w.width < w.height).map(sizeLabel);
  const lead = landscape.length > 0 ? `${and(landscape)} with ${counted(pages, 'page')}` : `${counted(pages, 'page')}`;
  return portrait.length > 0 ? `${lead}, and ${and(portrait)}.` : `${lead}.`;
}

/** The car themes' cell: how many, their names, how many cars they draw, and at which sizes. */
function themesCell(themes: readonly Theme[], faces: readonly Screen[]): string {
  if (themes.length === 0) return 'A face drawn like the car’s own display, at the sizes the theme claims.';
  const cars = themes.reduce((n, t) => n + t.cars.length, 0);
  const rectangular = faces.filter((f) => !f.round).length;
  const where = rectangular > 0 && themes.every((t) => t.sizes.length === rectangular) ? 'at every rectangular size' : 'at the sizes each claims';
  return `${counted(themes.length, 'car theme')}, ${and(themes.map((t) => t.name))}, for ${counted(cars, 'car')}, each drawn like the display those cars carry, ${where}.`;
}

/**
 * The rows, as a function of the build's counts and lists so that a number or a size in a cell is
 * read and not retyped (#560, #819). The pages call `rows(COUNTS, LISTED)`; the tests call it with
 * counts and lists of their own.
 */
export const rows = (c: Counts, l: Listed): CompareRow[] => [
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
      opendash: cell('yes', `${FREE_FOREVER} ${THEMES_FREE} ${NOTHING_TO_UNLOCK}`, { word: 'Free' }),
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
      opendash: cell('yes', facesCell(l.faces)),
      lovely: cell('yes', '850 × 480, 1280 × 480, 1280 × 400, 1920 × 480, 800 × 286, 600 × 686, plus round and square.'),
      dnr: cell('yes', '800 × 480, 1280 × 400, 1920 × 480, 1920 × 720 and 1920 × 1080, across 7 dashboards.'),
    },
  },
  {
    id: 'noPlugin',
    label: 'Works without the plugin',
    cells: {
      opendash: cell('no', 'The plugin is the only way in. A screen whose plugin is not enabled yet still draws its defaults.', {
        scope: 'a way in that does not begin with the plugin: a user who cannot put a DLL into SimHub’s own folder, or who will not, has no route in at all.',
      }),
      lovely: cell('no', 'The dashboards cannot run without the plugin. Their words.'),
      dnr: cell('no', 'Without the plugin a dashboard draws a notice screen instead of your data.'),
    },
  },
  {
    id: 'companion',
    label: 'Second screen',
    cells: {
      opendash: cell('yes', `${l.companions.length > 0 ? `${and(l.companions.map(sizeLabel))}, ` : ''}${counted(c.pages, 'page')}.`),
      lovely: cell('yes', 'A companion, landscape and portrait, free.'),
      dnr: cell('yes', 'Co-Pilot, 14 panels. Choosing which ones appear costs £3 a month.'),
    },
  },
  {
    id: 'pitWall',
    label: 'Pit wall',
    cells: {
      opendash: cell('yes', pitWallCell(l.pitWalls, c.pitWallPages)),
      lovely: cell('paid', 'Lovely Pit Wall, on the €3 tier and up.'),
      dnr: cell('yes', 'Race Control, free: the field, a track map and traces.'),
    },
  },
  {
    id: 'leds',
    label: 'LED profiles and per-car shift lights',
    cells: {
      opendash: cell('yes', `${c.stripShapes} strip shapes, and the car’s own lights from the open Lovely car data, fetched by the plugin.`, { word: 'Free' }),
      lovely: cell('partial', 'Lovely dropped its own LED profiles in 2023. Per-car shift lights come from ATSR, a separate app, free only on the €9 tier.', { word: 'Third party' }),
      dnr: cell('paid', 'Every profile needs a membership, from £3 a month. Shift points measured for over 600 cars.'),
    },
  },
  {
    id: 'flagBox',
    label: 'Flag box, 8 × 8 matrix',
    cells: {
      opendash: cell('yes', `${c.glyphs} glyphs, generated. Flags, pit states, warnings, the gear and the spotter.`, { word: 'Free' }),
      lovely: cell('no', 'Nothing in the dashboards or the plugin drives a matrix.'),
      dnr: cell('paid', 'An 8 × 8 box and the SimRep panel, on the £6 tier.'),
    },
  },
  {
    id: 'idle',
    label: 'Idle screen',
    cells: {
      opendash: cell('yes', 'In every package: the wordmark, the time and one line saying no game is running. Nothing to point at it.'),
      lovely: cell('yes', 'An animated screen, free. It needs SimHub’s HTML renderer.'),
      dnr: cell('paid', 'The logo, a driver tag or the car. Choosing costs £3 a month.'),
    },
  },
  {
    id: 'alerts',
    label: 'Alerts and pop-ups',
    cells: {
      opendash: cell('yes', `${c.flags} flags on the band, pit alerts over the gear, pop-ups for lap times and setting changes.`),
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
      opendash: cell('soon', 'The plugin installs every package and updates itself. Browsing, removing and a first-run guide are coming.', { issues: [734, 85] }),
      lovely: cell('yes', 'A dashboard manager that installs and updates.'),
      dnr: cell('yes', 'A wizard that puts a dashboard on each screen you own, plus updates and older versions.'),
    },
  },
  {
    id: 'night',
    label: 'Night mode',
    cells: {
      opendash: cell('soon', 'The lights have a night brightness today. The screens are coming.', { issues: [740] }),
      lovely: cell('yes', 'True Dark Mode, free, on your headlights or a hotkey.'),
      dnr: cell('paid', 'Dark mode from £3 a month. It follows the game’s own night in 2 sims.'),
    },
  },
  {
    id: 'themes',
    label: 'Car themes',
    // The row is younger than CHECKED_ON. Lovely's per-car logo fetch is what the privacy row read,
    // and the car as DNR's idle screen is what the idle row read. That none of DNR's dashboards is
    // drawn for a car was read in its own dashboards FAQ on 2026-10-14, which lists the seven by the
    // racing each is for, and the page prints that date for the row (#811).
    readOn: '2026-10-14',
    cells: {
      opendash: cell('yes', themesCell(l.themes, l.faces), { word: 'Free' }),
      lovely: cell('partial', 'Your car’s logo on the dash, fetched per car. No face drawn like the car’s own display.', { word: 'Logo' }),
      dnr: cell('no', 'The car appears only as a picture on the idle screen, from £3 a month. No face drawn like the car’s own display.'),
    },
  },
  {
    id: 'colours',
    label: 'Your own colours',
    cells: {
      opendash: cell('soon', 'Every colour comes from one token file today. Your own, chosen in the panel, are coming.', { issues: [127, 735] }),
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
      opendash: cell('soon', 'Flags are on every face’s band and in the flag box today. A screen of nothing but flags is coming.', { issues: [739] }),
      lovely: cell('yes', 'Lovely Flags, free, in 3 shapes.'),
      dnr: cell('partial', 'A flags panel drawn over the game, on the £6 tier. No screen of its own.'),
    },
  },
  {
    id: 'overlay',
    label: 'Stream overlay',
    cells: {
      opendash: cell('no', 'A face for a viewer on the other side of a broadcast is a different product, and is refused rather than left open.', {
        word: 'Refused',
        scope: 'the stream overlay: a different reading distance, a different set of readings, and an audience that is not holding a wheel.',
      }),
      lovely: cell('yes', 'Lovely Overlay and Lovely Tower, free.'),
      dnr: cell('paid', '5 overlays, on the £6 tier.'),
    },
  },
  {
    id: 'invisible',
    label: 'Invisible dash',
    cells: {
      opendash: cell('no', 'A see-through dash over the game is refused as a choice about focus. SimHub could draw one.', {
        word: 'Refused',
        scope: 'the invisible dash: the refusal is a choice about focus rather than a limitation of the format.',
      }),
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
      // Every request in lib/wire.ts, which test/wire.test.ts holds to the plugin and to docs/scope.md (#1096).
      opendash: cell(
        'yes',
        'An update check to GitHub, and the Porsche crest: one file, once, from the address on the panel, only with a screen that draws it, and never once you clear the address. The car tables and a new release when you press for them. Nothing about you, ever.',
        { word: 'Nothing' },
      ),
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

/**
 * The issues a row's OpenDash cell is scheduled under, for a page that makes the same promise
 * outside the table, so that its link is the table's and cannot point somewhere else (#813).
 */
export const issuesOf = (all: readonly CompareRow[], id: string): number[] => all.find((r) => r.id === id)?.cells.opendash.issues ?? [];

/** The coming-soon rows, for the list under the table. */
export const scheduled = (c: Counts, l: Listed): CompareRow[] => rows(c, l).filter((r) => r.cells.opendash.mark === 'soon');
