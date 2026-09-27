/**
 * A module that needs a session says so while there is none (#406).
 *
 * The mechanism is `withSessionGate`'s: the drawing goes into one group whose Visible is the session
 * test, and `placeholder`'s notice takes the same rectangle gated the other way. What this file
 * checks is that every module the catalogue declares gets exactly that arrangement, at every box a
 * module is drawn in, that no other module gets it, that band D's four timing pages and zone A's
 * track page get the same, that the pit wall's Track panel says it once over the whole panel rather
 * than over the map alone, and that the condition under all of them is one expression rather than
 * several spellings.
 */
import { describe, expect, test } from 'bun:test';
import { BAND_D_PAGES, MODULE_CATALOGUE, ZONE_A_PAGES } from '../src/contract.ts';
import { ncalc, type Item, type LayerItem, type TextItem } from '../src/generator.ts';
import { rect, contains } from '../src/design/geometry.ts';
import type { Rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { SESSION_REASON, sessionGroupName, sessionNotice } from '../src/modules/module.ts';
import { inSession } from '../src/second/values.ts';
import { racePage } from '../src/screens/pitwall.ts';
import { ZONE_FACES, sizeOf, zonesOf } from '../src/zones/index.ts';
import { BAND_PAGES_NEEDING_SESSION, zonePageScreen } from '../src/zones/pages.ts';
import { walkItems } from '../src/walk.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const SESSION = inSession();
const NO_SESSION = ncalc.not(SESSION);

const formulaOf = (item: Item, target: 'Visible'): string | undefined => {
  const binding = item.bindings?.[target];
  if (!binding) return undefined;
  return typeof binding.formula === 'string' ? binding.formula : binding.formula.expression;
};

/**
 * The boxes a module is drawn in, which is `secondScreens.test.ts`'s list rather than a second one.
 *
 * It was a copy of that function when this file was written, and the copy was already wrong on the
 * day it was written: it passed `zone` as the density of every face rectangle, where `densityForBox`
 * answers `compact` for eight of them, so the notice the build draws at 13 px was measured here at
 * 15 in a body rect cut by the wrong ramp. It also left out the arrangement without the rev bar and
 * every rectangle a pit wall places, which is where `wide` lives. The list is derived from the
 * geometry that hands the boxes out and its header says why; deriving it twice is how the second one
 * drifts.
 */
const BOXES = moduleBoxes();

describe('the condition is one expression', () => {
  /**
   * Both halves spelled out, because both are easy to get wrong and neither would fail loudly.
   *
   * `DataCorePlugin.GameRunning` is an `AttachedProperty<int>` rather than a bool, so it is compared
   * to zero; written as a bare term it would hand NCalc an integer where it wants a boolean, and a
   * throwing binding is muted for thirty seconds rather than reported. And the game half has to be
   * there at all: `GameData.*` is declared once over a `StatusDataBase` that DataCorePlugin only
   * reassigns while the game runs, so the session name alone still reads `Race` on a desktop with
   * nothing running, which is precisely the state this ticket is about.
   */
  test('in a session means the game is running and has named one', () => {
    expect(SESSION).toBe("((isnull([DataCorePlugin.GameRunning], 0)) > (0)) and ((isnull([DataCorePlugin.GameData.SessionTypeName], '')) != (''))");
  });

  test('the catalogue names the modules that need one', () => {
    expect(MODULE_CATALOGUE.filter((m) => m.needsSession).map((m) => m.id)).toEqual([
      'lapTimes', 'delta', 'sectors', 'fuel', 'pitView', 'session', 'track', 'leaderboard', 'relative', 'opponents', 'stint', 'lapHistory',
    ]);
  });

  test('the page called Session says the instruction once rather than stuttering its own name', () => {
    expect(sessionNotice({ name: 'Session' })).toBe(SESSION_REASON);
    expect(sessionNotice({ name: 'Leaderboard' })).toBe(`LEADERBOARD · ${SESSION_REASON}`);
  });
});

describe('a module that needs a session draws its notice while there is none', () => {
  /**
   * The notice is measured at every size of type it is ever set in, which is what the copied list
   * stopped being true of. `compact` is the one that matters and the one the copy lost: eight face
   * rectangles are drawn at it, the label is 13 px rather than 15, and 245 by 156 is the shortest
   * box a module is given anywhere.
   */
  test('the list spans every density, the smallest type included', () => {
    expect([...new Set(BOXES.map((b) => b.density))].sort()).toEqual(['compact', 'companion', 'wide', 'zone']);
  });

  for (const box of BOXES) {
    test(`on a ${box.name}`, () => {
      for (const module of MODULES) {
        const prefix = `${module.id}.`;
        const items = module.build({ frame: box.frame, density: box.density, prefix });
        const groups = items.filter((i): i is LayerItem => i.kind === 'layer' && i.name === sessionGroupName(prefix));
        const notices = items.filter((i): i is TextItem => i.kind === 'text' && i.name.startsWith(`${prefix}placeholder`) && formulaOf(i, 'Visible') === NO_SESSION);
        if (!module.needsSession) {
          expect({ module: module.id, groups: groups.length, notices: notices.length }).toEqual({ module: module.id, groups: 0, notices: 0 });
          continue;
        }
        // The content is one group, first, gated on the session; everything else is the notice.
        expect({ module: module.id, first: items[0]?.name }).toEqual({ module: module.id, first: sessionGroupName(prefix) });
        expect(groups).toHaveLength(1);
        expect(formulaOf(groups[0]!, 'Visible')).toBe(SESSION);
        expect(groups[0]!.children.length).toBeGreaterThan(0);
        expect(items.slice(1)).toEqual(notices);
        expect(notices.length).toBeGreaterThan(0);
        // The notice fits the box, names the module or at least says what to do, and is dim.
        const drawn = notices.map((n) => n.text).join(' ');
        expect({ module: module.id, drawn }).toMatchObject({ drawn: expect.stringContaining(SESSION_REASON) });
        if (notices.length === 1 && !drawn.includes('·')) expect(drawn).toBe(SESSION_REASON);
        else if (drawn.includes('·')) expect(drawn).toBe(sessionNotice(module));
        for (const n of notices) {
          expect({ module: module.id, item: n.name, inside: contains(box.frame, n.rect) }).toMatchObject({ inside: true });
        }
      }
    });
  }
});

/**
 * Zone A's track page is the track module, drawn titleless in the column's own padding, so it gets
 * the gate by being that module rather than by any decision taken here. Which is the right answer
 * and not the one the plan for this ticket wrote down -- it said zone A's pages do not take a notice
 * -- so it is pinned rather than left to be discovered on a face. Zone A's other three pages are
 * the gear and the speed, which need no session and get nothing.
 */
describe("zone A's track page carries the notice, because it is the track module", () => {
  for (const layout of ZONE_FACES) {
    const face = sizeOf(layout);
    const a = zonesOf(layout).find((z) => z.zone === 'A');
    if (!a) continue;
    test(`on the ${layout.folder} column`, () => {
      for (const page of ZONE_A_PAGES) {
        const frame = rect(0, 0, a.size.width, a.size.height);
        const screen = zonePageScreen(face, ['A'], page, a.size, a.corners);
        const group = screen.items.find((i): i is LayerItem => i.kind === 'layer' && i.name === sessionGroupName(`${page.id}.`));
        const notices = screen.items.filter((i): i is TextItem => i.kind === 'text' && formulaOf(i, 'Visible') === NO_SESSION);
        expect({ page: page.id, gated: group !== undefined }).toEqual({ page: page.id, gated: page.id === 'track' });
        if (page.id !== 'track') {
          expect({ page: page.id, notices: notices.length }).toEqual({ page: page.id, notices: 0 });
          continue;
        }
        expect(formulaOf(group!, 'Visible')).toBe(SESSION);
        expect(notices.map((n) => n.text).join(' ')).toContain(SESSION_REASON);
        for (const n of notices) expect({ page: page.id, inside: contains(frame, n.rect) }).toMatchObject({ inside: true });
      }
    });
  }
});

describe("band D's timing pages say so too", () => {
  test('four of the eight', () => {
    expect(BAND_PAGES_NEEDING_SESSION).toEqual(['fuel', 'stint', 'sectors', 'relative']);
    for (const id of BAND_PAGES_NEEDING_SESSION) expect(BAND_D_PAGES.map((p) => p.id)).toContain(id);
  });

  for (const layout of ZONE_FACES) {
    const face = sizeOf(layout);
    const band = zonesOf(layout).find((z) => z.zone === 'D')!;
    test(`on the ${layout.folder} band`, () => {
      for (const page of BAND_D_PAGES) {
        const screen = zonePageScreen(face, ['D'], page, band.size, band.corners);
        const group = screen.items.find((i): i is LayerItem => i.kind === 'layer' && i.name === sessionGroupName(`${page.id}.`));
        const notices = screen.items.filter((i): i is TextItem => i.kind === 'text' && formulaOf(i, 'Visible') === NO_SESSION);
        if (!BAND_PAGES_NEEDING_SESSION.includes(page.id)) {
          expect({ page: page.id, gated: group !== undefined, notices: notices.length }).toEqual({ page: page.id, gated: false, notices: 0 });
          continue;
        }
        expect({ page: page.id, gated: group !== undefined }).toEqual({ page: page.id, gated: true });
        expect(formulaOf(group!, 'Visible')).toBe(SESSION);
        expect(notices.length).toBeGreaterThan(0);
        expect(notices.map((n) => n.text).join(' ')).toContain(SESSION_REASON);
        const frame = rect(0, 0, band.size.width, band.size.height);
        for (const n of notices) expect({ page: page.id, inside: contains(frame, n.rect) }).toMatchObject({ inside: true });
        // The corner blocks, where the band has them, stay outside the group: a flag's lamps are not timing.
        for (const item of walkItems(screen.items)) {
          if (item.name.startsWith(`${page.id}.corner.`)) expect(group!.children.map((c) => c.name)).not.toContain(item.name);
        }
      }
    });
  }
});

/**
 * The pit wall's Track panel embeds the track module, and one panel says one thing.
 *
 * Letting the module draw its own notice here half-gated the panel: `TRACK · GO INTO A SESSION` was
 * centred in the map's left half while SESSION BEST and a lap time stayed drawn 20 px to the right
 * of that sentence, and the notice read as the map alone having failed. So the module is built with
 * `notice: false` and the panel gates its whole body. What is pinned is the shape of that, since the
 * mixed state passed every fit test it had.
 */
describe("the pit wall's Track panel says it once, over the whole panel", () => {
  const items = racePage(1920, 1080).items;
  const group = items.find((i): i is LayerItem => i.kind === 'layer' && i.name === sessionGroupName('race.track.'));
  const notices = [...walkItems(items)].filter((i): i is TextItem => i.kind === 'text' && i.name.startsWith('race.track.placeholder'));

  test('the map and the field column are in one gated group', () => {
    expect(group).toBeDefined();
    expect(formulaOf(group!, 'Visible')).toBe(SESSION);
    const inside = group!.children.map((c) => c.name);
    // The map, and every reading beside it: the session best, the two temperatures, the three assists.
    expect(inside).toContain('race.track.map.map');
    for (const field of ['sessionBest', 'road', 'air', 'tc', 'abs', 'bb']) {
      expect({ field, gated: inside.some((n) => n.startsWith(`race.track.${field}.`)) }).toEqual({ field, gated: true });
    }
  });

  test('the notice is one sentence in the panel body, not in the map half', () => {
    expect(notices.length).toBeGreaterThan(0);
    for (const n of notices) expect(formulaOf(n, 'Visible')).toBe(NO_SESSION);
    expect(notices.map((n) => n.text).join(' ')).toBe(sessionNotice({ name: 'Track' }));
    // The panel body, which is twice the map's width: the sentence is centred on the panel.
    const map = [...walkItems(items)].find((i) => i.name === 'race.track.map.map')!;
    for (const n of notices) expect(n.rect.width).toBeGreaterThan(map.kind === 'layer' ? 0 : map.rect.width);
  });

  test('the embedded module draws no second notice of its own', () => {
    expect([...walkItems(items)].filter((i) => i.name.startsWith('race.track.map.placeholder'))).toHaveLength(0);
    expect([...walkItems(items)].filter((i) => i.name === sessionGroupName('race.track.map.'))).toHaveLength(0);
  });
});
