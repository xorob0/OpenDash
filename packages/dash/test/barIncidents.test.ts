/**
 * The bar's incidents on a sim that has none (#567).
 *
 * The count is iRacing's `PlayerCarMyIncidentCount`, which Assetto Corsa, ACC and Le Mans Ultimate do
 * not publish, and the field read it through an `isnull` to 0, so a driver on any of them who had put the
 * incidents in a slot was shown `0x` as if it were a count. The field hides there instead, label and
 * value together, as a strip cell does for a setting the car does not have.
 */
import { describe, expect, test } from 'bun:test';
import type { TextItem } from '../src/generator.ts';
import { itemsOf } from '../src/walk.ts';
import { BAR_FIELDS } from '../src/contract.ts';
import { buildZoneFace, ZONE_FACES } from '../src/zones/index.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const COUNT = 'DataCorePlugin.GameRawData.Telemetry.PlayerCarMyIncidentCount';
const INCIDENTS = BAR_FIELDS.find((f) => f.id === 'incidents')!.number;
const SLOT = /\.bar\.(Left|Right)\d\.incidents\.(label|value)$|^bar\.(Left|Right)\d\.incidents\.(label|value)$/;

describe("the bar's incidents", () => {
  // The nano has no bar at all, so there is nothing of it to test.
  for (const face of ZONE_FACES.filter((f) => f.folder !== 'OpenDash 800x286')) {
    const built = buildZoneFace(face, OPTS);
    const items = [built.main, ...built.zones].flatMap((d) => [...itemsOf(d)]).filter((i): i is TextItem => i.kind === 'text' && SLOT.test(i.name));

    test(`${face.folder}: hide on a sim with no count, and show the count where there is one`, () => {
      expect(items.length).toBeGreaterThan(0);
      for (const item of items) {
        const visible = item.bindings?.Visible?.formula as string;
        // The slot's own property is whatever face and end it is; every one of them is set to the field.
        const chosen: Props = Object.fromEntries([...visible.matchAll(/\[(OpenDash\.[^\]]+)\]/g)].map(([, name]) => [name!, INCIDENTS]));
        expect({ item: item.name, shown: evalNcalc(visible, chosen) }).toEqual({ item: item.name, shown: false });
        expect({ item: item.name, shown: evalNcalc(visible, { ...chosen, [COUNT]: 0 }) }).toEqual({ item: item.name, shown: true });
        expect({ item: item.name, shown: evalNcalc(visible, { ...chosen, [COUNT]: 4 }) }).toEqual({ item: item.name, shown: true });
      }
    });
  }
});
