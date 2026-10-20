/**
 * The caption over the bar's first slot, in the sessions a driver runs.
 *
 * The slot's default field is `raceTime`, which binds `sessionClock()`: the time left of whatever
 * session is running. It was captioned `Race`, so the `quali` scenario drew `Race 0:04:37` beside a
 * session page reading `Lone Qualify`, and a timed practice drew `Race` over the practice clock. The
 * clock was right and the caption named a session that was not running (#1043).
 *
 * Read by evaluating what each slot draws against a frame of each session, as a driver would read
 * it, rather than by asserting the catalogue's string: a caption bound to the session later is held
 * to the same rule.
 */
import { describe, expect, test } from 'bun:test';
import { BAR_FIELDS, DEFAULT_BAR_FIELDS, PROPERTY_PREFIX, barFieldSettingName, type BarSlot } from '../src/contract.ts';
import { measureText } from '../src/design/advances.ts';
import type { TextItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, buildZoneFace, sizeOf } from '../src/zones/index.ts';
import { ds } from '../src/tokens.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

/** A session of each kind, under the names a sim gives them, with a clock running. */
const SESSIONS: Record<string, Props> = {
  race: { 'DataCorePlugin.GameData.SessionTypeName': 'Race', 'DataCorePlugin.GameData.SessionTimeLeft': 1694 },
  qualifying: { 'DataCorePlugin.GameData.SessionTypeName': 'Lone Qualify', 'DataCorePlugin.GameData.SessionTimeLeft': 277 },
  practice: { 'DataCorePlugin.GameData.SessionTypeName': 'Practice', 'DataCorePlugin.GameData.SessionTimeLeft': 1800 },
};

const RACE_TIME = BAR_FIELDS.find((f) => f.id === 'raceTime')!.number;

/** Every slot showing the race time, which is the first slot's default and any other's on request. */
const raceTimeEverywhere = (layout: (typeof ZONE_FACES)[number]): Props =>
  Object.fromEntries((Object.keys(DEFAULT_BAR_FIELDS) as BarSlot[]).map((slot) => [`${PROPERTY_PREFIX}.${barFieldSettingName(sizeOf(layout), slot)}`, RACE_TIME]));

/** What a text item draws in a frame: its bound text where it has one, its own text where not. */
const drawn = (item: TextItem, props: Props): string => {
  const formula = item.bindings?.Text?.formula;
  if (formula === undefined) return item.text;
  return String(evalNcalc(String(typeof formula === 'string' ? formula : formula.expression), props));
};

const shown = (item: TextItem, props: Props): boolean => {
  const formula = item.bindings?.Visible?.formula;
  if (formula === undefined) return true;
  return evalNcalc(String(typeof formula === 'string' ? formula : formula.expression), props) === true;
};

describe("the bar's first slot is captioned truly in every session", () => {
  test('the first slot opens on the race time, which is the field the caption was wrong on', () => {
    expect(DEFAULT_BAR_FIELDS.Left1).toBe(RACE_TIME);
  });

  for (const layout of ZONE_FACES.filter((f) => sizeOf(f).hasBar)) {
    test(`${layout.folder}: no slot showing the session clock says Race outside a race, nor in one`, () => {
      const { main } = buildZoneFace(layout, OPTS);
      const labels = main.screens
        .flatMap((screen) => [...walkItems(screen.items)])
        .filter((i): i is TextItem => i.kind === 'text' && /bar\.(Left1|Left2|Right1|Right2)\.raceTime\.label$/.test(i.name));
      expect({ face: layout.folder, found: labels.length > 0 }).toEqual({ face: layout.folder, found: true });
      for (const [session, frame] of Object.entries(SESSIONS)) {
        const props = { ...frame, ...raceTimeEverywhere(layout) };
        for (const label of labels) {
          expect({ face: layout.folder, session, label: label.name, shown: shown(label, props) }).toMatchObject({ shown: true });
          const caption = drawn(label, props);
          // The clock is the session's, so the caption is a word about the clock: true in a race and
          // in qualifying alike, and the word `Race` is false in two of the three.
          expect({ face: layout.folder, session, label: label.name, caption }).toEqual({ face: layout.folder, session, label: label.name, caption: 'Left' });
          expect({ face: layout.folder, session, label: label.name, saysRace: /race/i.test(caption) }).toMatchObject({ saysRace: false });
          // And it fits the box it is drawn in, with the pixel WPF wants, in the face it is set in.
          const ink = measureText('BarlowMedium', caption, label.fontSize) + 1;
          expect({ face: layout.folder, label: label.name, fits: ink <= label.rect.width }).toMatchObject({ fits: true });
        }
      }
    });
  }
});
