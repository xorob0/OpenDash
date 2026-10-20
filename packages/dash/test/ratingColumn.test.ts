/**
 * The iRating column gives its width back where no sim publishes one (#1162).
 *
 * Only iRacing fills `iracingirating`; on Assetto Corsa, ACC and Le Mans Ultimate the race and
 * portrait boards drew `--` down a 76 px column. A stamped layout cannot move a column at run time,
 * so a board with the column is drawn twice and the player's own rating being null picks the drawing
 * without it, whose driver column is wider by what the rating took. The opponents page hides its
 * `iR` label on the same test. By ADR 0021 the test reads the property and never the game's name.
 */
import { describe, expect, test } from 'bun:test';
import { PORTRAIT_COLUMNS, RACE_COLUMNS, portraitPage, racePage } from '../src/screens/pitwall.ts';
import { columnWidths, UNRATED_SUFFIX } from '../src/second/table.ts';
import { ratingIsPublished } from '../src/second/values.ts';
import { MODULES } from '../src/modules/index.ts';
import { rect } from '../src/design/geometry.ts';
import { ncalc, type Item, type LayerItem, type TextItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';

const formulaOf = (item: Item, target: 'Visible' | 'Text'): string | undefined => {
  const binding = (item as { bindings?: Record<string, { mode: string; formula?: unknown }> }).bindings?.[target];
  return binding?.mode === 'formula' && typeof binding.formula === 'string' ? binding.formula : undefined;
};

const PAGES = [
  { page: 'race', screen: racePage(1920, 1080), columns: RACE_COLUMNS },
  { page: 'portrait', screen: portraitPage(1080, 1920), columns: PORTRAIT_COLUMNS },
];

describe('the rating test', () => {
  test("asks whether the player's own iracingirating is null, and never which game is running", () => {
    const expr = ratingIsPublished();
    expect(expr).toContain('driveriracingirating(');
    expect(expr.toLowerCase()).toContain('isnull(');
    for (const named of ['CurrentGame', 'GameName', 'IRacing', 'AssettoCorsa']) expect(expr).not.toContain(named);
  });
});

describe('a board with an iRating column', () => {
  for (const { page, screen, columns } of PAGES) {
    const items = [...walkItems(screen.items)];
    const rated = items.find((i): i is LayerItem => i.kind === 'layer' && i.name === `${page}.board.rated`);
    const unrated = items.find((i): i is LayerItem => i.kind === 'layer' && i.name === `${page}.board${UNRATED_SUFFIX}.layer`);

    test(`${page}: is drawn twice, shown by complementary halves of one test`, () => {
      expect(rated).toBeDefined();
      expect(unrated).toBeDefined();
      expect(formulaOf(rated!, 'Visible')).toBe(ratingIsPublished());
      expect(formulaOf(unrated!, 'Visible')).toBe(ncalc.not(ratingIsPublished()));
    });

    test(`${page}: with the column where a rating is published, unchanged`, () => {
      const drawn = [...walkItems(rated!.children)];
      expect(drawn.find((i) => i.name === `${page}.board.head.rating`)).toBeDefined();
      expect(drawn.find((i) => i.name === `${page}.board.row.rating`)).toBeDefined();
    });

    test(`${page}: and without it where none is, the driver column taking its width`, () => {
      const drawn = [...walkItems(unrated!.children)];
      expect(drawn.filter((i) => i.name.endsWith('.rating'))).toEqual([]);
      // No cell anywhere in the drawing reads the rating.
      expect(drawn.filter((i) => (formulaOf(i, 'Text') ?? '').includes('iracingirating'))).toEqual([]);
      const rows = drawn.find((i) => i.kind === 'rect' && i.name.endsWith('.row.background'))!;
      const width = (rows as { rect: { width: number } }).rect.width;
      const withRating = columnWidths(columns, width, 'zone', page === 'race' ? 34 : 32, true);
      const without = columnWidths(columns.filter((id) => id !== 'rating'), width, 'zone', page === 'race' ? 34 : 32, true);
      const ratingWidth = withRating[columns.indexOf('rating')]!;
      expect(ratingWidth).toBeGreaterThan(0);
      expect(without[columns.indexOf('name')]).toBe(withRating[columns.indexOf('name')]! + ratingWidth);
      const name = drawn.find((i): i is TextItem => i.kind === 'text' && i.name === `${page}.board${UNRATED_SUFFIX}.row.name`)!;
      const ratedName = [...walkItems(rated!.children)].find((i): i is TextItem => i.kind === 'text' && i.name === `${page}.board.row.name`)!;
      expect(name.rect.width).toBeGreaterThan(ratedName.rect.width);
    });
  }
});

describe('the opponents page', () => {
  test("hides its iR label where the player's own rating is null", () => {
    const opponents = MODULES.find((m) => m.id === 'opponents')!;
    let labels = 0;
    for (const frame of [rect(0, 0, 745, 276), rect(0, 0, 576, 112), rect(0, 0, 1280, 300)]) {
      for (const item of opponents.build({ frame, density: 'zone', prefix: 'opponents.' }).flatMap((i) => [...walkItems([i])])) {
        if (!(formulaOf(item, 'Text') ?? '').includes('iracingirating')) continue;
        labels += 1;
        expect({ item: item.name, visible: formulaOf(item, 'Visible') }).toEqual({ item: item.name, visible: ratingIsPublished() });
      }
    }
    expect(labels).toBeGreaterThan(0);
  });
});
