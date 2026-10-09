/**
 * The race's length after a lap, `/ 30` and `of 30`, drawn only where `TotalLaps` is one (#989).
 *
 * iRacing's `TotalLaps` is the race's length in a race counted in laps and the leader's completed
 * laps in a timed one, and a session with no length has none at all. The session card knew this and
 * the bar, the companion header and the pit wall header did not: the bar drew its `/ 32` whenever its
 * slot held the lap, so a timed race read `12 / 14` and an open practice `4 / 0`, and the two headers
 * asked only whether the total was above nought. Three frames are enough to say what each surface
 * should draw, and every drawing of the total in every package is held to them, not only the three
 * the ticket named.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages, themesToBuild } from '../src/build.ts';
import { BAR_FIELDS } from '../src/contract.ts';
import { ncalc, type TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { itemsOf, walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const themes = themesToBuild({ themes: [], allThemes: true, touchedThemes: false }, () => {});
const PACKAGES = composePackages({ version: '0.0.0-test', themes, log: () => {} });

const A_WEEK = 604800;

/** A forty-five minute race, half an hour to run, the leader fourteen laps in: `TotalLaps` is 14. */
const TIMED: Props = {
  'DataCorePlugin.GameData.SessionTimeLeft': 1800,
  'DataCorePlugin.GameData.TotalLaps': 14,
  'DataCorePlugin.GameData.CurrentLap': 12,
  'DataCorePlugin.GameData.CompletedLaps': 11,
  'DataCorePlugin.GameData.RemainingLaps': 19,
};

/** An open practice: no clock, iRacing's week of time left, and no length. */
const OPEN: Props = {
  'DataCorePlugin.GameData.SessionTimeLeft': A_WEEK,
  'DataCorePlugin.GameData.TotalLaps': 0,
  'DataCorePlugin.GameData.CurrentLap': 4,
  'DataCorePlugin.GameData.CompletedLaps': 3,
  'DataCorePlugin.GameData.RemainingLaps': 0,
};

/** A thirty-lap race, which iRacing publishes with no clock. */
const LAPPED: Props = {
  'DataCorePlugin.GameData.SessionTimeLeft': A_WEEK,
  'DataCorePlugin.GameData.TotalLaps': 30,
  'DataCorePlugin.GameData.CurrentLap': 12,
  'DataCorePlugin.GameData.CompletedLaps': 11,
  'DataCorePlugin.GameData.RemainingLaps': 19,
};

const LAP_FIELD = BAR_FIELDS.find((f) => f.id === 'lap')!.number;

const formulaOf = (item: TextItem, target: 'Text' | 'Visible'): string | undefined => {
  const f = item.bindings?.[target]?.formula;
  return typeof f === 'string' ? f : undefined;
};

/**
 * The rig's settings an item reads, set the way that draws the total if anything does: every bar
 * slot on the lap, and the session's progress as `mode` asks.
 */
const settings = (item: TextItem, mode: string): Props => {
  const out: Props = { 'OpenDash.SessionProgress': mode };
  for (const target of ['Text', 'Visible'] as const) {
    const f = formulaOf(item, target);
    if (f === undefined) continue;
    for (const p of ncalc.referencedProperties(f)) if (/^OpenDash\.Face\w+Bar(?:Left|Right)\d$/.test(p)) out[p] = LAP_FIELD;
  }
  return out;
};

/** What the item puts on the screen in `frame`: its text, or nothing while it is hidden. */
const drawn = (item: TextItem, frame: Props, mode = 'auto'): string | null => {
  const props = { ...frame, ...settings(item, mode) };
  const visible = formulaOf(item, 'Visible');
  if (visible !== undefined && evalNcalc(visible, props) !== true) return null;
  const text = formulaOf(item, 'Text');
  return text === undefined ? item.text : String(evalNcalc(text, props));
};

interface Site {
  where: string;
  item: TextItem;
}

const texts = (items: Iterable<{ kind: string }>): TextItem[] => [...items].filter((i): i is TextItem => i.kind === 'text');

/** Every item whose text writes the total out of `TotalLaps` as it stands, in every package and every module box. */
const SITES: Site[] = [
  ...PACKAGES.flatMap(({ pkg }) =>
    pkg.dashboards.flatMap((dashboard) => texts(itemsOf(dashboard)).map((item) => ({ where: `${pkg.folderName} / ${dashboard.name}`, item }))),
  ),
  ...moduleBoxes().flatMap((box) =>
    MODULES.flatMap((module) =>
      texts(walkItems(module.build({ frame: box.frame, density: box.density, prefix: '' }))).map((item) => ({ where: `${module.id} on a ${box.name}`, item })),
    ),
  ),
].filter(({ item }) => /format\(isnull\(\[DataCorePlugin\.GameData\.TotalLaps\], 0\), '0'\)|format\(\[DataCorePlugin\.GameData\.TotalLaps\], '0'\)/.test(formulaOf(item, 'Text') ?? ''));

const named = (pattern: RegExp): Site[] => SITES.filter((s) => pattern.test(s.item.name));

/** The three surfaces the ticket names, each with the way it writes thirty laps. */
const SURFACES: { name: string; sites: Site[]; total: string }[] = [
  { name: "the bar's Lap", sites: named(/^bar\.(?:Left|Right)\d\.lap\.denominator$/), total: '/ 30' },
  { name: 'the companion header', sites: named(/\.header\.lap\.denominator$/), total: '/ 30' },
  { name: "the pit wall header's portrait `Lap 12 / 30`", sites: named(/^portrait\.header\.lap\.\d+$/), total: '/ 30' },
  { name: "the pit wall header's `L12 of 30`", sites: named(/\.header\.session\.\d+$/), total: 'of 30' },
];

/** Whether a reading writes a lap total: `/ 14`, `of 0` and the AiM's `LAP  /  14` alike. */
const writesTotal = (reading: string | null): boolean => reading !== null && /(?:\/|of)\s+\d/.test(reading);

describe('the race length after a lap', () => {
  test('every surface the ticket names is drawn somewhere', () => {
    for (const s of SURFACES) expect({ surface: s.name, found: s.sites.length > 0 }).toEqual({ surface: s.name, found: true });
  });

  test('a timed race and an open practice draw no total on the bar or either header', () => {
    for (const s of SURFACES) {
      for (const { where, item } of s.sites) {
        for (const [frame, props] of [['timed', TIMED], ['open', OPEN]] as const) {
          expect({ surface: s.name, where, item: item.name, frame, reading: drawn(item, props) }).toEqual({ surface: s.name, where, item: item.name, frame, reading: null });
        }
      }
    }
  });

  test('a race counted in laps draws its length on the bar and on both headers', () => {
    for (const s of SURFACES) {
      for (const { where, item } of s.sites) {
        expect({ surface: s.name, where, item: item.name, reading: drawn(item, LAPPED) }).toEqual({ surface: s.name, where, item: item.name, reading: s.total });
      }
    }
  });

  test('no drawing of the total in any package writes one where there is none, whatever the session progress', () => {
    // The session card, the session page and the AiM's caption over it read `SessionProgress`, and a
    // lap forced on in a timed race is still not followed by the leader's laps.
    expect(SITES.length).toBeGreaterThan(100);
    for (const { where, item } of SITES) {
      for (const mode of ['auto', 'laps', 'time']) {
        for (const [frame, props] of [['timed', TIMED], ['open', OPEN]] as const) {
          const reading = drawn(item, props, mode);
          expect({ where, item: item.name, mode, frame, reading, total: writesTotal(reading) }).toEqual({ where, item: item.name, mode, frame, reading, total: false });
        }
      }
      // And in a lap race, whatever draws a total draws the race's.
      const reading = drawn(item, LAPPED, 'laps');
      if (writesTotal(reading)) expect({ where, item: item.name, reading: reading!.replace(/\s+/g, ' ') }).toEqual({ where, item: item.name, reading: expect.stringMatching(/(?:\/|of) 30$/) });
    }
  });
});
