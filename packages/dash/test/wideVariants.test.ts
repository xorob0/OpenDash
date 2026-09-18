/**
 * The wide zone pages that are variants of their module rather than the plain drawing.
 *
 * Three of the six are named on the canvas for something the standard zone does not draw: lap
 * history carries the delta to the session best, opponents carries the best lap beside the last,
 * and tyres carries the pressure in both units. The other three are the module as it stands.
 *
 * `secondScreens.test.ts` already measures every text of these pages against its box, which is the
 * constraint the renderer enforces; what it cannot say is whether the wide page draws anything the
 * standard one does not. That is what this file is for, and it reads the pages out of the packages
 * the build really produces rather than out of a frame written here, so that a zone resized on a
 * pit wall sheet is measured at its new size.
 */
import { describe, expect, test } from 'bun:test';
import { contains, rect } from '../src/design/geometry.ts';
import type { Dashboard, Item, Screen, TextItem } from '../src/generator.ts';
import { CORNERS } from '../src/second/values.ts';
import { wheel } from '../src/second/wheel.ts';
import { SCREEN_PACKAGES, buildScreenPackage } from '../src/screens/index.ts';
import { itemsOf, walkItems } from '../src/walk.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const ZONES = SCREEN_PACKAGES.flatMap((def) => buildScreenPackage(def, OPTS).dashboards).filter((d) => d.name.startsWith('zones'));

const kindOf = (d: Dashboard): 'wide' | 'standard' => (d.name.startsWith('zones-wide') ? 'wide' : 'standard');

/** Every drawing of one page across the zone dashboards of a kind; a size each, and at least one. */
function pages(kind: 'wide' | 'standard', id: string): { zone: string; items: Item[] }[] {
  const found = ZONES.filter((d) => kindOf(d) === kind).flatMap((d) => {
    const screen: Screen | undefined = d.screens.find((s) => s.name === id);
    return screen ? [{ zone: d.name, items: itemsOf({ ...d, screens: [screen] }) }] : [];
  });
  expect({ kind, id, zones: found.length }).not.toMatchObject({ zones: 0 });
  return found;
}

const textsIn = (items: readonly Item[]): TextItem[] => [...walkItems(items)].filter((i): i is TextItem => i.kind === 'text');

const named = (items: readonly Item[], name: string): TextItem | undefined => textsIn(items).find((i) => i.name === name);

/** The formula a text draws, or its static text where it draws one. */
const drawn = (item: TextItem | undefined): string => {
  const binding = item?.bindings?.Text;
  return binding && binding.mode === 'formula' && typeof binding.formula === 'string' ? binding.formula : (item?.text ?? '');
};

describe('opponents · best and last', () => {
  test('the wide page reads the best lap beside the last, both cars alike', () => {
    for (const { zone, items } of pages('wide', 'opponents')) {
      for (const side of ['ahead', 'behind']) {
        const detail = named(items, `opponents.${side}.lastLap`);
        expect({ zone, side, drawn: drawn(detail) }).toMatchObject({ drawn: expect.stringContaining('driverbestlap(') });
        expect({ zone, side, drawn: drawn(detail) }).toMatchObject({ drawn: expect.stringContaining('driverlastlap(') });
      }
    }
  });

  test('and keeps the rating it has always carried', () => {
    for (const { zone, items } of pages('wide', 'opponents')) {
      expect({ zone, drawn: drawn(named(items, 'opponents.ahead.rating')) }).toMatchObject({ drawn: expect.stringContaining('driveriracingirating(') });
    }
  });

  test('the standard page reads the last lap alone', () => {
    for (const { zone, items } of pages('standard', 'opponents')) {
      const detail = named(items, 'opponents.ahead.lastLap');
      expect({ zone, drawn: drawn(detail) }).toMatchObject({ drawn: expect.stringContaining('driverlastlap(') });
      expect(drawn(detail)).not.toContain('driverbestlap(');
    }
  });
});

describe('tyres · both pressure units', () => {
  test('the wide page reads each corner twice, in the sim unit and in the other one', () => {
    for (const { zone, items } of pages('wide', 'tyres')) {
      for (const corner of CORNERS) {
        const first = named(items, `tyres.${corner}.pressure`);
        const second = named(items, `tyres.${corner}.pressure.alt`);
        expect({ zone, corner, first: first !== undefined, second: second !== undefined }).toMatchObject({ first: true, second: true });
        // The same corner read twice: one property, two conversions of it.
        expect(drawn(second)).toContain(`TyrePressure${corner}`);
        expect({ zone, corner, unit: drawn(named(items, `tyres.${corner}.pressure.alt.unit`)) }).toMatchObject({ unit: expect.stringContaining("'kPa'") });
      }
    }
  });

  test('and the second reading is the first in another unit, never the same number twice', () => {
    for (const { items } of pages('wide', 'tyres')) {
      for (const corner of CORNERS) {
        const first = named(items, `tyres.${corner}.pressure`);
        const second = named(items, `tyres.${corner}.pressure.alt`);
        expect({ corner, first: first?.text, second: second?.text }).not.toMatchObject({ second: first?.text });
      }
    }
  });

  test('the standard page reads the sim unit alone', () => {
    for (const { zone, items } of pages('standard', 'tyres')) {
      for (const corner of CORNERS) {
        expect({ zone, corner, second: named(items, `tyres.${corner}.pressure.alt`) !== undefined }).toMatchObject({ second: false });
      }
    }
  });
});

describe('lap history · delta to best', () => {
  test('the wide page gives its third column to the delta to the session best', () => {
    for (const { zone, items } of pages('wide', 'lapHistory')) {
      expect({ zone, head: named(items, 'lapHistory.head.delta')?.text }).toMatchObject({ head: 'Δ BEST' });
      expect({ zone, drawn: drawn(named(items, 'lapHistory.row.delta')) }).toMatchObject({ drawn: expect.stringContaining('DeltaToSessionBest') });
    }
  });
});

describe('a cell too narrow for two pressures keeps one', () => {
  // A wide cell 200 px across holds both readings; 120 holds one. Both are tall enough for two rows
  // and not for three, which is what the wide zone's own cell is, so the only thing moving is width.
  const cell = (width: number): Item[] => wheel('w', rect(0, 0, width, 87), 'FrontLeft', 'wide', { numbers: 'right' });
  const drawsAlt = (width: number): boolean => named(cell(width), 'w.pressure.alt') !== undefined;

  test('the room decides: two readings where it is there, one where it is not', () => {
    expect({ wide: drawsAlt(200), narrow: drawsAlt(120) }).toMatchObject({ wide: true, narrow: false });
  });

  test('the reading itself survives the loss of its second unit', () => {
    expect(named(cell(120), 'w.pressure')).toBeDefined();
  });

  test('and nothing is drawn outside the cell at either width', () => {
    for (const width of [200, 120, 96, 72]) {
      const frame = rect(0, 0, width, 87);
      for (const item of walkItems(cell(width))) {
        if (item.kind === 'layer') continue;
        expect({ width, item: item.name, rect: item.rect, inside: contains(frame, item.rect) }).toMatchObject({ inside: true });
      }
    }
  });
});
