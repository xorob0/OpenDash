/**
 * Snapshots of the SimHub JSON the build emits: every card, the hero, and the two dashboards of
 * the 1920 x 480 build with their metadata sidecars. A changed snapshot is the JSON consequence
 * of a TypeScript change: review the diff, then refresh with `bun test --update-snapshots`.
 * The version is pinned so that bumping VERSION does not churn these files.
 */
import { describe, expect, test } from 'bun:test';
import { CARDS } from '../src/cards/index.ts';
import { buildLayout, MAIN_SCREEN_NAME } from '../src/dashboard.ts';
import { buildItemObject, screenPath, serializeDashboard, serializeMetadata, type Item } from '../src/generator.ts';
import { hero } from '../src/hero/hero.ts';
import { layout1920x480 } from '../src/layouts/1920x480.ts';
import { CARDS_DASHBOARD_NAME, cardOrigin } from '../src/slots.ts';

const VERSION = '0.0.0-snapshot';
const packageName = layout1920x480.folder;
const ctx = { packageName };

/** Items as SimHub JSON under the screen path the real build uses, so the ids match the dashboard snapshots. */
const itemsJson = (items: readonly Item[], parentPath: string): string =>
  JSON.stringify(items.map((item) => buildItemObject(item, parentPath)), null, 2);

describe('card snapshots', () => {
  const origin = cardOrigin(layout1920x480);
  for (const card of CARDS) {
    test(`card ${card.number} ${card.id}`, () => {
      const items = card.build(origin, `${card.id}.`);
      expect(itemsJson(items, screenPath(packageName, CARDS_DASHBOARD_NAME, card.id))).toMatchSnapshot();
    });
  }
});

describe('hero snapshot', () => {
  test('hero of the 1920x480 layout', () => {
    expect(itemsJson(hero(layout1920x480.hero), screenPath(packageName, packageName, MAIN_SCREEN_NAME))).toMatchSnapshot();
  });
});

describe('dashboard snapshots', () => {
  const { main, cards } = buildLayout(layout1920x480, { version: VERSION });

  test('openDash.djson', () => {
    expect(serializeDashboard(main, ctx)).toMatchSnapshot();
  });

  test('openDash.djson.metadata', () => {
    expect(serializeMetadata(main)).toMatchSnapshot();
  });

  test('cards.djson', () => {
    expect(serializeDashboard(cards, ctx)).toMatchSnapshot();
  });

  test('cards.djson.metadata', () => {
    expect(serializeMetadata(cards)).toMatchSnapshot();
  });
});
