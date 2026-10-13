/**
 * The session clock: what every surface draws while a session is timed, where it has no clock, and
 * where there is no session at all.
 *
 * Read by evaluating the NCalc the packages emit, as `session.test.ts` reads the card's three modes,
 * so what is asserted is what a driver sees rather than what a call site was written with. #439.
 */
import { describe, expect, test } from 'bun:test';
import { BAR_FIELDS, PROPERTY_PREFIX, barFieldSettingName, type BarSlot } from '../src/contract.ts';
import { measureText } from '../src/design/advances.ts';
import { buildLayout } from '../src/dashboard.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { LAYOUTS } from '../src/layouts/index.ts';
import { SCREEN_PACKAGES, buildScreenPackage } from '../src/screens/index.ts';
import { ZONE_FACES, buildZoneFace, sizeOf } from '../src/zones/index.ts';
import { CHARS, NO_CLOCK, UNTIMED_MARK, UNTIMED_SECONDS, isUntimedSession, sessionClock } from '../src/second/values.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const A_WEEK = 604800;

/** What the sim publishes, in the three states a session clock has. */
const game = (timeLeft: number): Props => ({ 'DataCorePlugin.GameData.SessionTimeLeft': timeLeft });

describe('the mark an untimed session draws is measured before it is drawn', () => {
  test('it is U+221E, and no cell a value is laid in can hold it', () => {
    // Rule 19, and the reason the mark is a second item rather than a string this clock can be bound
    // to: the digit cell is cut for the widest ink a digit draws and this glyph advances a third
    // further in both faces a value is set in. A monospaced item bound to it would be clipped by WPF
    // exactly as the week of time left it replaces was.
    expect(UNTIMED_MARK).toBe('∞');
    const faces = [
      ['BarlowCondensedSemiBold', ds.font.cell.semiBold.digit],
      ['BarlowCondensedBold', ds.font.cell.bold.digit],
    ] as const;
    for (const [face, cell] of faces) {
      const em = measureText(face, UNTIMED_MARK, 1000) / 1000;
      expect({ face, em, cell, overruns: em > cell }).toMatchObject({ overruns: true });
      // And by a third, not by a rounding: a cell would have to be cut 39% and 34% wider for it,
      // which is the whole clock beside it moving.
      expect({ face, ratio: Math.round((em / cell) * 100) / 100 }).toMatchObject({ ratio: face === 'BarlowCondensedSemiBold' ? 1.39 : 1.34 });
    }
  });

  test('it overruns the cell for the reason the banned set does, and is not the widest thing banned', () => {
    // The claim worth holding is the one above -- a third over the cell, so not a cell -- and not a
    // ranking against `font.cell.excluded`. An earlier draft of this fix wrote that the mark was
    // further over the cell than anything in that set; it is not, and a reader taking that from the
    // comment would believe the cells have never refused anything wider. Half the set is wider.
    for (const face of ['BarlowCondensedSemiBold', 'BarlowCondensedBold'] as const) {
      const em = (ch: string): number => measureText(face, ch, 1000) / 1000;
      const wider = [...ds.font.cell.excluded].filter((ch) => em(ch) > em(UNTIMED_MARK)).sort();
      expect({ face, wider }).toEqual({ face, wider: ['%', '@', 'W', 'm'] });
    }
  });

  test('the cells were not widened for it either, which is what the ratio above would cost', () => {
    // The alternative the measurement rules out. The mark is one glyph and the clock beside it is
    // eight cells, so holding the mark in a cell widens `12:34:56` by a third wherever it is drawn.
    expect(ds.font.cell.semiBold.digit).toBe(0.47);
    expect(ds.font.cell.bold.digit).toBe(0.49);
  });
});

describe('the clock the surfaces bind', () => {
  test('it counts down while the session is timed, a 24-hour race included', () => {
    for (const [secs, reading] of [[1800, '0:30:00'], [5025, '1:23:45'], [86399, '23:59:59'], [1, '0:00:01']] as const) {
      expect({ secs, reading: evalNcalc(sessionClock(), game(secs)) }).toEqual({ secs, reading });
    }
  });

  test('it counts whole seconds down, so a clock a few tenths past the second still reads that second', () => {
    // SimHub's clock is never a whole number of seconds. With an Int32 clamp in front of it the
    // seconds were rounded before they were truncated, and the clock ticked over half a second early
    // (#831), which this evaluator could see only once its `max` answered as NCalc's does (#1046).
    for (const [secs, reading] of [[59.6, '0:00:59'], [3599.5, '0:59:59'], [5025.9, '1:23:45'], [0.6, '0:00:00']] as const) {
      expect({ secs, reading: evalNcalc(sessionClock(), game(secs)) }).toEqual({ secs, reading });
    }
  });

  test('a day exactly is a clock and not the mark, and it is the six cells every clock is cut for', () => {
    // The boundary on purpose rather than in passing. Daytona, Le Mans and the Nurburgring are 86400 s
    // exactly, and `SessionTimeRemain` sits on `SessionTimeTotal` until the clock starts, so a
    // threshold that excluded the boundary would have the one class of race where time left is the
    // whole point open by asserting it has no end -- at full strength, now that the state has a mark.
    // The reading is the longest a clock can be and is still six digit cells and two separators.
    expect({ reading: evalNcalc(sessionClock(), game(UNTIMED_SECONDS)), marked: evalNcalc(isUntimedSession(), game(UNTIMED_SECONDS)) }).toEqual({
      reading: '24:00:00',
      marked: false,
    });
    const reading = '24:00:00';
    expect({ digits: [...reading].filter((c) => /[0-9]/.test(c)).length, specials: [...reading].filter((c) => c === ':').length }).toEqual({
      digits: CHARS.clock.digits,
      specials: CHARS.clock.specials,
    });
    // One second over it is the mark, so the two remain complementary across the line.
    expect({ marked: evalNcalc(isUntimedSession(), game(UNTIMED_SECONDS + 1)) }).toEqual({ marked: true });
  });

  test('it is the unset clock where there is no session, and the mark takes the untimed one', () => {
    for (const secs of [0, -1]) {
      expect({ secs, reading: evalNcalc(sessionClock(), game(secs)), marked: evalNcalc(isUntimedSession(), game(secs)) }).toEqual({
        secs,
        reading: NO_CLOCK,
        marked: false,
      });
    }
    // Above the sentinel the clock is hidden and the mark is shown, so what this expression reads
    // there is never drawn -- but it is the placeholder rather than a week either way.
    for (const secs of [UNTIMED_SECONDS + 1, A_WEEK]) {
      expect({ secs, reading: evalNcalc(sessionClock(), game(secs)), marked: evalNcalc(isUntimedSession(), game(secs)) }).toEqual({
        secs,
        reading: NO_CLOCK,
        marked: true,
      });
    }
  });
});

/**
 * Every place a session clock is drawn, over the packages as they ship.
 *
 * The bug was one surface out of five drawing the clock its own way, so the claim worth holding is
 * not that a call site was edited but that no clock anywhere is left without its mark: the dash
 * faces' bar and session card, the session module on every companion, and the pit wall's header and
 * Session panel.
 */
describe('every session clock a package draws carries its mark', () => {
  const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

  /**
   * A clock and the mark that stands in for it, paired by name, one screen at a time.
   *
   * One screen at a time because a face's two arrangements -- with the rev bar and without it -- are
   * two screens of one dashboard drawing the same names at different heights, so a pair taken across
   * a whole dashboard could match a mark against the other arrangement's clock.
   */
  const clocks = (items: Iterable<Item>): { value: TextItem; mark: TextItem }[] => {
    const texts = [...items].filter((i): i is TextItem => i.kind === 'text');
    const pairs: { value: TextItem; mark: TextItem }[] = [];
    for (const mark of texts.filter((i) => i.name.endsWith('.mark'))) {
      // A field names its two parts `<field>.value` and `<field>.mark`; a header part and a bar slot
      // name the mark after the value's own name, so both spellings are looked for.
      const base = mark.name.slice(0, -'.mark'.length);
      const value = texts.find((i) => i.name === `${base}.value`) ?? texts.find((i) => i.name === base);
      if (value === undefined) throw new Error(`${mark.name} stands in for nothing`);
      pairs.push({ value, mark });
    }
    return pairs;
  };

  /** What a package's clocks are called, without the screen, zone or slot that carries them. */
  const fieldsOf = (pairs: { mark: TextItem }[]): string[] => [...new Set(pairs.map((p) => p.mark.name.replace(/^.*?([A-Za-z]+)\.mark$/, '$1')))].sort();

  /**
   * The pair, held to the rule: the mark is drawn in the state the clock cannot read, the clock in
   * the other two, and never both at once.
   *
   * `chosen` is what else has to be true for the reading to be drawn at all -- on the bar, the slot
   * setting naming this field, since a slot draws all ten of the catalogue and shows one.
   */
  const expectPaired = (where: string, pairs: { value: TextItem; mark: TextItem }[], chosen: (markName: string) => Props = () => ({})): void => {
    for (const { value, mark } of pairs) {
      // The mark is the glyph, set proportionally and measured from itself; the clock is the cells.
      expect({ where, mark: mark.name, text: mark.text, mono: mark.monospace, widest: mark.widest, bound: mark.bindings?.Text }).toMatchObject({
        text: UNTIMED_MARK,
        mono: undefined,
        widest: UNTIMED_MARK,
        bound: undefined,
      });
      expect({ where, value: value.name, mono: value.monospace !== undefined }).toMatchObject({ mono: true });
      // Complementary, and complementary in the sim's terms rather than by string: exactly one of
      // the two is drawn in each of the three states a session clock has.
      for (const [secs, drawn] of [[1800, 'value'], [0, 'value'], [A_WEEK, 'mark']] as const) {
        const props: Props = { ...game(secs), 'OpenDash.SessionProgress': 'time', ...chosen(mark.name) };
        const shown = ([['value', value], ['mark', mark]] as const).filter(([, item]) => {
          const formula = item.bindings?.Visible?.formula;
          if (formula === undefined) return true;
          return evalNcalc(String(typeof formula === 'string' ? formula : formula.expression), props) === true;
        });
        expect({ where, mark: mark.name, secs, shown: shown.map(([which]) => which) }).toEqual({ where, mark: mark.name, secs, shown: [drawn] });
      }
      // And it is drawn where the clock is, on the same line and at the same size, inside the room
      // the clock was given: a mark that needed room of its own would move the field it sits in.
      expect({ where, mark: mark.name, top: mark.rect.top, fs: mark.fontSize }).toMatchObject({ top: value.rect.top, fs: value.fontSize });
      const room = { left: value.rect.left, right: value.rect.left + value.rect.width };
      const ink = measureText(mark.fontWeight === 'Bold' ? 'BarlowCondensedBold' : 'BarlowCondensedSemiBold', UNTIMED_MARK, mark.fontSize);
      const left = mark.hAlign === 'right' ? mark.rect.left + mark.rect.width - ink : mark.rect.left;
      expect({ where, mark: mark.name, inside: left >= room.left && left + ink <= room.right }).toMatchObject({ inside: true });
    }
  };

  for (const layout of ZONE_FACES) {
    test(`${layout.folder}: the bar's two clock fields, and the session module in its zones`, () => {
      const { main, zones } = buildZoneFace(layout, OPTS);
      const pairs = [main, ...zones].flatMap((dashboard) => dashboard.screens.flatMap((screen) => clocks(walkItems(screen.items))));
      // Both catalogue clocks, in every slot the face has: a slot draws all ten fields and shows the
      // one the driver chose, so a face with two fields per end carries four slots' worth. This is
      // the field the bug was photographed on -- `raceTime` is the default of the first slot. The
      // 800 x 286 face has no bar at all, and its session module is where its clock is.
      const bar = fieldsOf(pairs.filter((p) => p.mark.name.includes('bar.')));
      expect({ face: layout.folder, bar }).toMatchObject({ bar: sizeOf(layout).hasBar ? ['raceTime', 'timeLeft'] : [] });
      expect({ face: layout.folder, module: fieldsOf(pairs.filter((p) => !p.mark.name.includes('bar.'))) }).toMatchObject({ module: ['timeLeft'] });
      const chosen = (markName: string): Props => {
        const slot = /bar\.(Left1|Left2|Right1|Right2)\.([A-Za-z]+)\.mark$/.exec(markName);
        if (slot === null) return {};
        const number = BAR_FIELDS.find((f) => f.id === slot[2])?.number;
        if (number === undefined) throw new Error(`the catalogue has no bar field ${String(slot[2])}`);
        return { [`${PROPERTY_PREFIX}.${barFieldSettingName(sizeOf(layout), slot[1] as BarSlot)}`]: number };
      };
      expectPaired(layout.folder, pairs, chosen);
    });
  }

  for (const layout of LAYOUTS) {
    test(`${layout.folder}: the session card`, () => {
      const { main, cards } = buildLayout(layout, OPTS);
      const pairs = [main, cards].flatMap((dashboard) => dashboard.screens.flatMap((screen) => clocks(walkItems(screen.items))));
      expect({ face: layout.folder, card: fieldsOf(pairs) }).toMatchObject({ card: ['session'] });
      expect({ face: layout.folder, clocks: pairs.length }).toMatchObject({ clocks: 1 });
      expectPaired(layout.folder, pairs);
    });
  }

  for (const def of SCREEN_PACKAGES) {
    test(`${def.folder}: every clock of every screen`, () => {
      const pkg = buildScreenPackage(def, OPTS);
      let found = 0;
      for (const dashboard of pkg.dashboards) {
        for (const screen of dashboard.screens) {
          const pairs = clocks(walkItems(screen.items));
          found += pairs.length;
          expectPaired(`${def.folder} ${dashboard.name} ${screen.name}`, pairs);
        }
      }
      // The pit walls draw one in their header and one in their Session panel, the companions one in
      // the session module of every page that carries it.
      expect({ folder: def.folder, found: found > 0 }).toMatchObject({ found: true });
    });
  }
});
