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
import { UNTIMED_SECONDS, hasLapTotal, isTimedSession } from '../src/second/values.ts';
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

/**
 * A fifteen-minute qualifying after its clock ran out, the cars on their last laps: iRacing's
 * `SessionTimeRemain` is at nought, the session still declares its 900 s in `SessionTimeTotal`, and
 * `TotalLaps` is the leader's seventeen laps. The pit wall read `L9 of 17` here (#1017).
 */
const CLOCK_OUT: Props = {
  'DataCorePlugin.GameData.SessionTimeLeft': 0,
  'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': 900,
  'DataCorePlugin.GameData.TotalLaps': 17,
  'DataCorePlugin.GameData.CurrentLap': 9,
  'DataCorePlugin.GameData.CompletedLaps': 8,
  'DataCorePlugin.GameData.RemainingLaps': 9,
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

/**
 * A thirty-lap race from a sim that publishes no clock for it at all: a `SessionTimeLeft` of nought
 * and no iRacing `SessionTimeTotal`, which is still a race with a length.
 */
const LAPPED_NO_CLOCK: Props = {
  'DataCorePlugin.GameData.SessionTimeLeft': 0,
  'DataCorePlugin.GameData.TotalLaps': 30,
  'DataCorePlugin.GameData.CurrentLap': 12,
  'DataCorePlugin.GameData.CompletedLaps': 11,
  'DataCorePlugin.GameData.RemainingLaps': 18,
};

/** Every frame in which `TotalLaps` is not the race's length. */
const NO_LENGTH = [['timed', TIMED], ['clock out', CLOCK_OUT], ['open', OPEN]] as const;

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

describe('whether `TotalLaps` is a length', () => {
  // The one rule every surface below draws by, read on its own (#1017). A session that had a clock
  // is timed until it ends, and its clock reading nought does not turn the leader's laps into a
  // length: the clock runs out a lap or two before the flag, and those are the laps a length would
  // be read on.
  test('a timed session whose clock has run out has none, and a lap race has one', () => {
    expect({ frame: 'clock out', has: evalNcalc(hasLapTotal(), CLOCK_OUT) }).toEqual({ frame: 'clock out', has: false });
    expect({ frame: 'lapped', has: evalNcalc(hasLapTotal(), LAPPED) }).toEqual({ frame: 'lapped', has: true });
  });

  test('so does every other frame, a sim with no clock for its lap race included', () => {
    for (const [frame, props, has] of [['timed', TIMED, false], ['open', OPEN, false], ['lapped, no clock', LAPPED_NO_CLOCK, true]] as const) {
      expect({ frame, has: evalNcalc(hasLapTotal(), props) }).toEqual({ frame, has });
    }
  });

  test('a session is timed by the length it declares as well as by its clock', () => {
    // iRacing's `SessionTimeTotal` is the week it writes for no limit in a lap race, and a session
    // that declares a day is timed for the same reason a day of time left is (see UNTIMED_SECONDS).
    const at = (total: number | undefined, left: number): Props => ({
      'DataCorePlugin.GameData.SessionTimeLeft': left,
      ...(total === undefined ? {} : { 'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': total }),
    });
    for (const [total, left, timed] of [
      [900, 0, true],
      [900, -1, true],
      [UNTIMED_SECONDS, 0, true],
      [A_WEEK, 0, false],
      [UNTIMED_SECONDS + 1, 0, false],
      [0, 0, false],
      [undefined, 0, false],
      [A_WEEK, 1800, true],
      [undefined, 1800, true],
      [undefined, A_WEEK, false],
    ] as const) {
      expect({ total, left, timed: evalNcalc(isTimedSession(), at(total, left)) }).toEqual({ total, left, timed });
    }
  });
});

describe('the race length after a lap', () => {
  test('every surface the ticket names is drawn somewhere', () => {
    for (const s of SURFACES) expect({ surface: s.name, found: s.sites.length > 0 }).toEqual({ surface: s.name, found: true });
  });

  test('a timed session, before or after its clock runs out, and an open practice draw no total on the bar or either header', () => {
    for (const s of SURFACES) {
      for (const { where, item } of s.sites) {
        for (const [frame, props] of NO_LENGTH) {
          expect({ surface: s.name, where, item: item.name, frame, reading: drawn(item, props) }).toEqual({ surface: s.name, where, item: item.name, frame, reading: null });
        }
      }
    }
  });

  test('a race counted in laps draws its length on the bar and on both headers', () => {
    for (const s of SURFACES) {
      for (const { where, item } of s.sites) {
        for (const [frame, props] of [['lapped', LAPPED], ['lapped, no clock', LAPPED_NO_CLOCK]] as const) {
          expect({ surface: s.name, where, item: item.name, frame, reading: drawn(item, props) }).toEqual({ surface: s.name, where, item: item.name, frame, reading: s.total });
        }
      }
    }
  });

  test('no drawing of the total in any package writes one where there is none, whatever the session progress', () => {
    // The session card, the session page and the AiM's caption over it read `SessionProgress`, and a
    // lap forced on in a timed race is still not followed by the leader's laps.
    expect(SITES.length).toBeGreaterThan(100);
    for (const { where, item } of SITES) {
      for (const mode of ['auto', 'laps', 'time']) {
        for (const [frame, props] of NO_LENGTH) {
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

describe('a right-hand lap without its length', () => {
  // A right-hand slot lays its denominator against the padding and the figure in front of it, so a
  // hidden denominator would leave the figure standing its room off the edge the label and the next
  // field are drawn against. The figure moves to that edge while the length is not drawn.
  const ends = PACKAGES.flatMap(({ pkg }) =>
    pkg.dashboards.flatMap((dashboard) => {
      const items = texts(itemsOf(dashboard));
      const find = (name: string): TextItem => {
        const found = items.find((i) => i.name === name);
        if (!found) throw new Error(`${pkg.folderName} / ${dashboard.name}: no ${name}`);
        return found;
      };
      return items
        .filter((i) => /^bar\.Right\d\.lap\.value$/.test(i.name))
        .map((value) => ({
          where: `${pkg.folderName} / ${dashboard.name} / ${value.name}`,
          value,
          label: find(value.name.replace(/value$/, 'label')),
          denominator: find(value.name.replace(/value$/, 'denominator')),
        }));
    }),
  );

  /** Where the item's box ends in `frame`, its `Left` binding evaluated when it has one. */
  const rightEdge = (item: TextItem, frame: Props): number => {
    const left = item.bindings?.Left?.formula;
    const x = typeof left === 'string' ? Number(evalNcalc(left, { ...frame, ...settings(item, 'auto') })) : item.rect.left;
    return x + item.rect.width;
  };

  test('is drawn in every package that has a bar', () => {
    expect(ends.length).toBeGreaterThan(10);
  });

  test('a timed race and an open practice draw the figure against the edge its label is drawn against', () => {
    for (const { where, value, label } of ends) {
      for (const [frame, props] of NO_LENGTH) {
        expect({ where, frame, right: rightEdge(value, props) }).toEqual({ where, frame, right: label.rect.left + label.rect.width });
      }
    }
  });

  test('a race counted in laps draws the figure where it was designed, clear of its length', () => {
    for (const { where, value, denominator } of ends) {
      const right = rightEdge(value, LAPPED);
      expect({ where, right, clear: right <= denominator.rect.left }).toEqual({ where, right: value.rect.left + value.rect.width, clear: true });
    }
  });
});
