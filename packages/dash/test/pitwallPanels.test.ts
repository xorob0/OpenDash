/**
 * The fixed panels of the pit wall's right column, against the heights and the fields their
 * artboards draw.
 *
 * These four panels are not zones: a driver cannot put another page in one, so what they draw is
 * settled here rather than by whatever `fitFields` finds room for. That distinction is the whole
 * point of the file. Before it, the Session panel drew one field of five and the Lap data panel one
 * of four, because both boxes were a few pixels short and the fitter answered by shedding.
 *
 * The heights are read off `PitWall1920x1080.dc.html`'s own `.panel` styles. They are tight: the
 * sheet sized each of them as padding plus a label plus a value and left out the five pixels
 * between the label and the value, so a panel holds its row only when the row is drawn from the
 * top edge down. The frame check below is the assertion that matters, since it is what says the
 * last of the bottom padding is spent rather than the rule crossed.
 */
import { describe, expect, test } from 'bun:test';
import { portraitPage, racePage, towerPage } from '../src/screens/pitwall.ts';
import { walkItems } from '../src/walk.ts';
import type { Item, Rect, TextItem } from '../src/generator.ts';

/** An item with a box, which is everything a panel draws: a layer carries none and is not one. */
type Placed = Extract<Item, { rect: Rect }>;

/** The right column of the race page, in the order the artboard stacks it. */
const RACE_PANELS: readonly { id: string; height: number }[] = [
  { id: 'session', height: 108 },
  { id: 'lapDelta', height: 158 },
  { id: 'lapData', height: 96 },
  { id: 'track', height: 236 },
];

/** The artboard's right column and the body it stacks into, both off the sheet. */
const COLUMN_WIDTH = 639;
const BODY_TOP = 64;

const itemsOf = (screen: { items: Item[] }): Placed[] => [...walkItems(screen.items)].filter((i): i is Placed => i.kind !== 'layer');

const named = (items: readonly Placed[], prefix: string): Placed[] => items.filter((i) => i.name.startsWith(prefix));

const texts = (items: readonly Placed[], prefix: string): TextItem[] => named(items, prefix).filter((i): i is TextItem => i.kind === 'text');

/** A field's value item, which is the one named for the field itself. */
const value = (items: readonly Placed[], name: string): TextItem => {
  const item = texts(items, `${name}.value`)[0];
  if (!item) throw new Error(`no value item named ${name}.value`);
  return item;
};

describe('the race column stacks the artboard heights', () => {
  const items = itemsOf(racePage(1920, 1080));

  test('each panel is the height its artboard declares', () => {
    let top = BODY_TOP;
    const built = RACE_PANELS.map((p) => {
      const rule = named(items, `race.${p.id}.rule`)[0];
      if (!rule) throw new Error(`no rule after race.${p.id}`);
      const height = rule.rect.top - top;
      top = rule.rect.top + 1;
      return { id: p.id, height, left: rule.rect.left, width: rule.rect.width };
    });
    expect(built).toEqual(RACE_PANELS.map((p) => ({ id: p.id, height: p.height, left: 1920 - COLUMN_WIDTH, width: COLUMN_WIDTH })));
  });

  test('what is left of the body goes to the two zones and the rule between them', () => {
    const panels = RACE_PANELS.reduce((sum, p) => sum + p.height + 1, 0);
    expect(panels).toBe(602);
    expect(1080 - BODY_TOP - panels).toBe(414);
  });
});

/**
 * Every item of a panel inside the frame the page gave it.
 *
 * A panel is a title and a set of items rather than a container SimHub clips to, so a row that
 * outgrows its body does not vanish: it carries on over the rule and into the panel below. This is
 * the check that says it does not.
 */
describe('no panel draws past its own frame', () => {
  const cases: { page: string; items: Placed[]; panels: readonly { id: string; top: number; height: number }[] }[] = [];
  {
    let top = BODY_TOP;
    const panels = RACE_PANELS.map((p) => {
      const at = { id: `race.${p.id}`, top, height: p.height };
      top += p.height + 1;
      return at;
    });
    cases.push({ page: 'race', items: itemsOf(racePage(1920, 1080)), panels });
  }
  cases.push({ page: 'tower', items: itemsOf(towerPage(1920, 1080)), panels: [{ id: 'tower.track', top: BODY_TOP, height: 400 }] });
  cases.push({
    page: 'portrait',
    items: itemsOf(portraitPage(1080, 1920)),
    panels: [
      { id: 'portrait.session', top: 921, height: 110 },
      { id: 'portrait.lapData', top: 921, height: 110 },
    ],
  });

  for (const c of cases) {
    for (const p of c.panels) {
      test(`${p.id} keeps its items between ${p.top} and ${p.top + p.height}`, () => {
        const own = named(c.items, `${p.id}.`).filter((i) => !i.name.endsWith('.rule'));
        expect(own.length).toBeGreaterThan(0);
        for (const item of own) {
          const top = item.rect.top;
          const bottom = top + item.rect.height;
          expect({ item: item.name, top, bottom, frame: [p.top, p.top + p.height], fits: top >= p.top && bottom <= p.top + p.height }).toMatchObject({ fits: true });
        }
      });
    }
  }
});

describe('the Session panel draws the three fields of the sheet', () => {
  for (const [page, screen] of [
    ['race.session', racePage(1920, 1080)],
    ['portrait.session', portraitPage(1080, 1920)],
  ] as const) {
    const items = itemsOf(screen);

    test(`${page} draws the time left, the position and the class`, () => {
      expect(named(items, `${page}.`).filter((i) => i.name.endsWith('.value')).map((i) => i.name)).toEqual([`${page}.left.value`, `${page}.position.value`, `${page}.class.value`]);
    });

    test(`${page} sets all three at 34 px, 24 apart`, () => {
      const row = [value(items, `${page}.left`), value(items, `${page}.position`), value(items, `${page}.class`)];
      expect(row.map((i) => i.fontSize)).toEqual([34, 34, 34]);
      expect(row.map((i) => i.rect.top)).toEqual([row[0]!.rect.top, row[0]!.rect.top, row[0]!.rect.top]);
      const labels = ['left', 'position', 'class'].map((id) => texts(items, `${page}.${id}.label`)[0]!);
      expect(labels[1]!.rect.left - (labels[0]!.rect.left + labels[0]!.rect.width)).toBe(24);
      expect(labels[2]!.rect.left - (labels[1]!.rect.left + labels[1]!.rect.width)).toBe(24);
    });

    test(`${page} writes the session type into the first label and the time left under it`, () => {
      const label = texts(items, `${page}.left.label`)[0]!;
      expect(label.text).toBe('Race');
      expect(label.widest).toBe('OFFLINE TESTING');
      expect(value(items, `${page}.left`).text).toBe('0:42:15');
    });

    test(`${page} follows the position with a 23 px "/ 24"`, () => {
      const denominator = texts(items, `${page}.position.denominator`)[0]!;
      expect(denominator.text).toBe('/ 24');
      expect(denominator.fontSize).toBe(23);
      expect(denominator.textColor).toBe('#8A9099');
    });
  }
});

describe('the Lap data panel draws the three lap times of the sheet', () => {
  for (const [page, screen] of [
    ['race.lapData', racePage(1920, 1080)],
    ['portrait.lapData', portraitPage(1080, 1920)],
  ] as const) {
    const items = itemsOf(screen);

    test(`${page} draws the estimate, the personal best and the last lap`, () => {
      expect(named(items, `${page}.`).filter((i) => i.name.endsWith('.value')).map((i) => i.name)).toEqual([
        `${page}.estimated.value`,
        `${page}.yourBest.value`,
        `${page}.last.value`,
      ]);
      expect(texts(items, `${page}.estimated.label`)[0]!.text).toBe('EST.');
    });

    test(`${page} sets them at 34 px, 20 apart`, () => {
      const row = ['estimated', 'yourBest', 'last'].map((id) => value(items, `${page}.${id}`));
      expect(row.map((i) => i.fontSize)).toEqual([34, 34, 34]);
      expect(row[1]!.rect.left - (row[0]!.rect.left + row[0]!.rect.width)).toBe(20);
      expect(row[2]!.rect.left - (row[1]!.rect.left + row[1]!.rect.width)).toBe(20);
    });

    test(`${page} leaves the session best to the Track panel`, () => {
      expect(named(items, `${page}.sessionBest`)).toHaveLength(0);
    });
  }
});

describe('the Track panel carries the session best', () => {
  for (const [page, screen] of [
    ['race.track', racePage(1920, 1080)],
    ['tower.track', towerPage(1920, 1080)],
  ] as const) {
    const items = itemsOf(screen);

    test(`${page} draws it at 34 px in the board's own purple`, () => {
      const best = value(items, `${page}.sessionBest`);
      expect(best.text).toBe('1:41.877');
      expect(best.fontSize).toBe(34);
      expect(best.textColor).toBe('#B14BFF');
    });

    test(`${page} leads its column with it rather than burying it on the second line`, () => {
      const best = value(items, `${page}.sessionBest`);
      for (const id of ['air', 'road', 'tc', 'abs', 'bb']) {
        const top = value(items, `${page}.${id}`).rect.top;
        expect({ id, top, sessionBest: best.rect.top, below: top >= best.rect.top }).toMatchObject({ below: true });
      }
    });

    test(`${page} names the track and its state over the map`, () => {
      expect(texts(items, `${page}.map.title`)).toHaveLength(1);
      expect(texts(items, `${page}.map.state`)[0]!.widest).toBe('MODERATE');
    });
  }
});
