/**
 * The lap review: the anatomy the pagesandalerts artboard draws, the window it is out in, and the
 * ranking that decides what it may cover.
 *
 * The drawing is the easy half. What is really asserted here is the ranking, because the panel
 * arrives on a face that already has three transient boxes on the same rectangle and a flag on the
 * band under it: the lap-time pop-up and the change notification have to be covered while it is
 * out, since all three are true of the same lap at the same moment, and band D, the rev bar and the
 * bar of settled values have to be left alone, since a flag and a lap review answer different
 * questions and a driver crossing the line under a caution needs both.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import {
  LAP_REVIEW_GROUP_GAP,
  LAP_REVIEW_HEIGHT,
  LAP_REVIEW_PAD_X,
  LAP_REVIEW_RULE,
  LAP_REVIEW_SECONDS,
  LAP_REVIEW_STRIP_HEIGHT,
  LAP_REVIEW_WIDTH,
  lapReview,
  lapReviewFit,
  lapReviewFrame,
  lapReviewOut,
  lapReviewWanted,
} from '../src/components/lapReview.ts';
import { CHANGE_NOTIFICATION_HEIGHT, CHANGE_NOTIFICATION_WIDTH, changeNotificationFrame } from '../src/components/changeNotification.ts';
import { POP_UP_HEIGHT, POP_UP_WIDTH, popUpFrame } from '../src/components/popUp.ts';
import { DEFAULT_LAP_REVIEW, FACE_SIZES, LAP_REVIEW_MODES, facePropertyNames, lapReviewSettingName, zone as zoneSetting } from '../src/contract.ts';
import { contains, overlaps, rect } from '../src/design/geometry.ts';
import { measureText } from '../src/design/advances.ts';
import { densityOf } from '../src/second/density.ts';
import { DELTA_DEADBAND, deltaColour } from '../src/second/values.ts';
import type { Item, LayerItem, Rect, RectangleItem, TextItem } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, layoutWithoutRevBar, sizeOf, type ZoneLayout } from '../src/zones/index.ts';

const D = densityOf('companion');

/** Both arrangements of every face: the one with the rev bar and the one that gives its room back. */
const arrangements: { name: string; layout: ZoneLayout; items: Item[] }[] = ZONE_FACES.flatMap((face) => [
  { name: face.folder, layout: face, items: faceItems(face) },
  { name: `${face.folder}, rev bar off`, layout: layoutWithoutRevBar(face), items: faceItems(layoutWithoutRevBar(face), { revBar: false }) },
]);

const reviewOf = (items: readonly Item[]): LayerItem => {
  const layer = items.find((i): i is LayerItem => i.kind === 'layer' && i.name === 'lapReview');
  if (!layer) throw new Error('no lap review on this face');
  return layer;
};

const partsOf = (items: readonly Item[]): Item[] => [...walkItems([reviewOf(items)])];
const named = (items: readonly Item[], name: string): Item | undefined => partsOf(items).find((i) => i.name === `lapReview.${name}`);
const textNamed = (items: readonly Item[], name: string): TextItem => {
  const item = named(items, name);
  if (!item || item.kind !== 'text') throw new Error(`lapReview.${name} is not a text item`);
  return item;
};
const rectNamed = (items: readonly Item[], name: string): RectangleItem => {
  const item = named(items, name);
  if (!item || item.kind !== 'rect') throw new Error(`lapReview.${name} is not a rect`);
  return item;
};

/** A token by its dotted path, whether it is written bare or wrapped in a `value`. */
const token = (name: string): unknown => {
  const tokens = JSON.parse(readFileSync(path.resolve(import.meta.dir, '../../../design/tokens.json'), 'utf8')) as Record<string, unknown>;
  const node = name.split('.').reduce<unknown>((at, key) => (at as Record<string, unknown>)[key], tokens);
  return node !== null && typeof node === 'object' && 'value' in (node as object) ? (node as { value: unknown }).value : node;
};

const reference = arrangements[0]!;
const REFERENCE_FRAME = lapReviewFrame(reference.layout.zones.zoneA, reference.layout.width);

describe('the panel the artboard draws', () => {
  test('is 1200 by 160 in the pop-up surface with a 2 px rule on top, 32 px of side padding and 40 px between groups', () => {
    expect(LAP_REVIEW_WIDTH).toBe(1200);
    expect(token('indicator.lapReview.height')).toBe(LAP_REVIEW_HEIGHT);
    expect(LAP_REVIEW_PAD_X).toBe(ds.space[6]);
    expect(LAP_REVIEW_GROUP_GAP).toBe(40);
    const box = rectNamed(reference.items, 'box');
    expect(box.rect).toEqual(REFERENCE_FRAME);
    expect(box.backgroundColor).toBe(ds.purpose.popUp.surface);
    const rule = rectNamed(reference.items, 'rule');
    expect(rule.rect).toEqual(rect(REFERENCE_FRAME.left, REFERENCE_FRAME.top, REFERENCE_FRAME.width, LAP_REVIEW_RULE));
    expect(rule.backgroundColor).toBe(ds.purpose.popUp.rule);
    // The side padding is the left edge of the first group and the right edge of the last.
    expect(textNamed(reference.items, 'lap.value').rect.left).toBe(REFERENCE_FRAME.left + LAP_REVIEW_PAD_X);
    const fuelLeft = textNamed(reference.items, 'fuelLeft.unit');
    expect(fuelLeft.rect.left + fuelLeft.rect.width).toBe(REFERENCE_FRAME.left + REFERENCE_FRAME.width - LAP_REVIEW_PAD_X);
  });

  test('is out for four seconds at the line, read off the lap and not off a clock', () => {
    expect(token('indicator.lapReview.durationMs')).toBe(LAP_REVIEW_SECONDS * 1000);
    const when = String(reviewOf(reference.items).bindings?.Visible?.formula);
    expect(when).toContain('DataCorePlugin.GameData.CurrentLapTime');
    expect(when).toContain(`< (${LAP_REVIEW_SECONDS})`);
    // And it waits for a lap to have been set, or the out lap opens on a panel of placeholders.
    expect(when).toContain('DataCorePlugin.GameData.LastLapTime');
    expect(when).not.toContain('blink(');
  });

  test('is enabled per session type, per screen, and off until somebody asks', () => {
    expect(LAP_REVIEW_MODES).toEqual(['off', 'race', 'all']);
    expect(DEFAULT_LAP_REVIEW).toBe('off');
    // The token says the switch exists; the contract says what its values are.
    expect(token('indicator.lapReview.perSessionType')).toBe(true);
    for (const face of FACE_SIZES) expect(facePropertyNames(face)).toContain(lapReviewSettingName(face));
    // The screen's own property and no other face's, which is what a per-screen setting promises.
    const face = sizeOf(reference.layout);
    const when = String(reviewOf(reference.items).bindings?.Visible?.formula);
    expect(when).toContain(`OpenDash.${lapReviewSettingName(face)}`);
    for (const other of FACE_SIZES.filter((f) => f !== face)) expect(when).not.toContain(`OpenDash.${lapReviewSettingName(other)}`);
    // `all` in every session, `race` only in the one session name SimHub publishes that openDash
    // can match, which is the string the pit wall header already writes beside the session.
    expect(lapReviewWanted(face)).toContain(zoneSetting.lapReviewIs(face, 'all'));
    expect(lapReviewWanted(face)).toContain(zoneSetting.lapReviewIs(face, 'race'));
    expect(lapReviewWanted(face)).toContain("'RACE'");
    expect(lapReviewOut(face)).toContain(lapReviewWanted(face));
  });
});

describe('the fields, left to right', () => {
  test('are the lap, the driver line over the sector strip with the two deltas, and the fuel pair at the right edge', () => {
    const order = partsOf(reference.items)
      .filter((i): i is TextItem => i.kind === 'text')
      .map((i) => i.name.slice('lapReview.'.length));
    expect(order).toEqual([
      'lap.label',
      'lap.value',
      'fuelUsed.label',
      'fuelUsed.value',
      'fuelUsed.unit',
      'fuelLeft.label',
      'fuelLeft.value',
      'fuelLeft.unit',
      'driver',
      'vsBest.label',
      'vsBest.value',
      'vsPrevious.label',
      'vsPrevious.value',
    ]);
    // Left to right on the face, whatever order they are built in: the fuel pair is placed from the
    // right edge and so is emitted before the group it is to the right of.
    const at = (name: string): number => textNamed(reference.items, name).rect.left;
    expect(at('lap.value')).toBeLessThan(at('driver'));
    expect(at('driver')).toBeLessThan(at('fuelUsed.value'));
    expect(at('fuelUsed.value')).toBeLessThan(at('fuelLeft.value'));
    expect(at('vsBest.value')).toBeLessThan(at('vsPrevious.value'));
  });

  test('sets the lap time at 116, the deltas and the fuel at 46 and the fuel unit at 13', () => {
    expect(textNamed(reference.items, 'lap.value').fontSize).toBe(D.hero);
    for (const name of ['vsBest', 'vsPrevious', 'fuelUsed', 'fuelLeft']) expect(textNamed(reference.items, `${name}.value`).fontSize).toBe(D.mid);
    for (const name of ['fuelUsed', 'fuelLeft']) expect(textNamed(reference.items, `${name}.unit`).fontSize).toBe(D.labelSm);
    // Every label of the panel is the one label size, which is the pair every artboard draws.
    for (const item of partsOf(reference.items)) {
      if (item.kind !== 'text' || !item.name.endsWith('.label')) continue;
      expect({ name: item.name, size: item.fontSize }).toEqual({ name: item.name, size: D.label });
    }
  });

  test('binds every value it draws, and the lap number with it', () => {
    const bound = (name: string): string => String(textNamed(reference.items, name).bindings?.Text?.formula);
    expect(bound('lap.label')).toContain('CompletedLaps');
    expect(bound('lap.value')).toContain('LastLapTime');
    expect(bound('driver')).toContain('drivercarnumber');
    // Slot zero's delta to the session best, addressed the way the lap history addresses its rows.
    expect(bound('vsBest.value')).toContain("'PersistantTrackerPlugin.PreviousLap_') + (format(0, '00')) + ('_DeltaToSessionBest'");
    // The lap before the one being reported, not the one being reported: slot zero is this lap, so
    // a difference against it would be nought on every lap of every race.
    expect(bound('vsPrevious.value')).toContain("'PersistantTrackerPlugin.PreviousLap_') + (format(1, '00')");
    expect(bound('vsPrevious.value')).toContain('LastLapTime');
    expect(bound('fuelUsed.value')).toContain('Fuel_LastLapConsumption');
    expect(bound('fuelLeft.value')).toContain('GameData.Fuel');
    for (const name of ['fuelUsed', 'fuelLeft']) expect(String(textNamed(reference.items, `${name}.unit`).bindings?.Text?.formula)).toContain('FuelUnit');
  });

  test('draws the three-cell strip, each cell in its own sector colour', () => {
    const cells = partsOf(reference.items).filter((i): i is RectangleItem => i.kind === 'rect' && i.name.includes('.sectors.'));
    // Three and not the canvas's twelve: SimHub publishes no mini-sectors, and a strip of invented
    // subdivisions would be a picture of nothing. `second/sectors.ts` records the same substitution.
    expect(cells).toHaveLength(3);
    for (const cell of cells) {
      expect({ name: cell.name, height: cell.rect.height }).toEqual({ name: cell.name, height: LAP_REVIEW_STRIP_HEIGHT });
      expect(typeof cell.bindings?.BackgroundColor?.formula).toBe('string');
    }
  });
});

describe('the two deltas', () => {
  test('are coloured through deltaColour rather than by a second reading of the comparison', () => {
    for (const name of ['vsBest', 'vsPrevious']) {
      const value = textNamed(reference.items, `${name}.value`);
      const colour = String(value.bindings?.TextColor?.formula);
      expect({ name, red: colour.includes(ds.purpose.delta.slower) }).toEqual({ name, red: true });
      expect({ name, green: colour.includes(ds.purpose.delta.faster) }).toEqual({ name, green: true });
    }
  });

  test('keep deltaColour’s third state, which the canvas does not state', () => {
    // The canvas gives the review a two-colour rule. The code has three: a delta inside the deadband
    // is `purpose.delta.zero`, and the reason it is kept for a finished lap as well as a live one is
    // written beside DELTA_DEADBAND. What this pins is that the decision was made once: every
    // surface that draws a delta goes through the same expression.
    expect(DELTA_DEADBAND).toBeGreaterThan(0);
    expect(deltaColour('0')).toContain(ds.purpose.delta.zero);
    for (const name of ['vsBest', 'vsPrevious']) {
      expect(String(textNamed(reference.items, `${name}.value`).bindings?.TextColor?.formula)).toContain(ds.purpose.delta.zero);
    }
  });
});

describe('what the panel may cover', () => {
  for (const { name, layout, items } of arrangements) {
    test(`${name} draws it over the hero, inside the face, and clear of the settled parts`, () => {
      const z = layout.zones;
      const frame = lapReviewFrame(z.zoneA, layout.width);
      expect({ name, inside: contains(rect(0, 0, layout.width, layout.height), frame) }).toMatchObject({ inside: true });
      // The rev bar, the bar of settled values and band D are outside it, for the reason they are
      // outside a pop-up: a flag has the better claim on those sixty pixels, and a lap review is not
      // an answer to the question band D is answering.
      const untouchable: [string, Rect | undefined][] = [
        ['the rev bar well', name.endsWith('rev bar off') ? undefined : z.revBarWell],
        ['the bar', z.bar],
        ['band D', z.band],
      ];
      for (const [what, box] of untouchable) {
        if (!box) continue;
        expect({ name, what, covered: overlaps(frame, box) }).toMatchObject({ covered: false });
      }
      // And nothing of the panel is drawn outside the panel, which is the rule every module keeps.
      for (const item of partsOf(items)) {
        if (item.kind === 'layer') continue;
        expect({ item: item.name, rect: item.rect, inside: contains(frame, item.rect) }).toMatchObject({ inside: true });
      }
    });

    test(`${name} covers the lap-time pop-up and the change notification while it is out`, () => {
      // The ranking, and the substance of this component. All three are true of the same lap at the
      // same moment, so the review is larger than both in both directions over the same centre and
      // is pushed after them, which is what makes "one at a time" true here without an exclusion
      // chain reaching into `popUp.ts`, whose conditions know nothing of a face.
      const frame = lapReviewFrame(layout.zones.zoneA, layout.width);
      expect({ name, covers: contains(frame, popUpFrame(layout.zones.zoneA)) }).toMatchObject({ covers: true });
      expect({ name, covers: contains(frame, changeNotificationFrame(layout.zones.zoneA)) }).toMatchObject({ covers: true });
      expect(LAP_REVIEW_HEIGHT).toBeGreaterThan(POP_UP_HEIGHT);
      expect(LAP_REVIEW_HEIGHT).toBeGreaterThan(CHANGE_NOTIFICATION_HEIGHT);
      expect(Math.min(LAP_REVIEW_WIDTH, layout.width)).toBeGreaterThanOrEqual(Math.max(POP_UP_WIDTH, CHANGE_NOTIFICATION_WIDTH));
      const names = items.map((i) => i.name);
      expect(names.indexOf('lapReview')).toBeGreaterThan(names.indexOf('popUp.lap'));
      expect(names.indexOf('lapReview')).toBeGreaterThan(names.indexOf('notice.tc'));
      // The limiter banner is pushed before the whole family and so is the one thing over a zone
      // that the review does not take: a driver serving a stop still has to know the limiter is on.
      expect(names.indexOf('lapReview')).toBeGreaterThan(names.indexOf('pitAlert.limiter'));
    });
  }
});

describe('a panel narrower than the artboard sheds rather than drawing outside', () => {
  test('keeps everything at 1200 and drops the fuel pair before anything else', () => {
    expect(lapReviewFit(rect(0, 0, LAP_REVIEW_WIDTH, LAP_REVIEW_HEIGHT))).toEqual({ lapFs: D.hero, strip: true, deltas: true, fuel: true });
    expect(lapReviewFit(rect(0, 0, 850, LAP_REVIEW_HEIGHT))).toMatchObject({ lapFs: D.hero, fuel: false });
  });

  test('shrinks the lap time rather than shedding the deltas, which is the floor', () => {
    // The panel covers the lap-time pop-up while it is out, and that pop-up already carries the lap
    // and one delta in 560 px. A review shed down to a lap time alone would be four seconds of the
    // gear spent saying less than the box it replaced, so the portrait face's 600 keeps both deltas
    // and steps the lap time down the density's own ramp instead.
    const portrait = lapReviewFit(rect(0, 0, 600, LAP_REVIEW_HEIGHT));
    expect(portrait).toEqual({ lapFs: D.big, strip: false, deltas: true, fuel: false });
    const items = [...walkItems([lapReview(rect(0, 0, 600, LAP_REVIEW_HEIGHT), 'true')])];
    expect(items.some((i) => i.name === 'lapReview.vsBest.value')).toBe(true);
    expect(items.some((i) => i.name === 'lapReview.sectors.strip1')).toBe(false);
    for (const item of items) {
      if (item.kind === 'layer') continue;
      expect({ item: item.name, inside: contains(rect(0, 0, 600, LAP_REVIEW_HEIGHT), item.rect) }).toMatchObject({ inside: true });
    }
  });

  test('every face that ships keeps the lap time and both deltas', () => {
    for (const { name, layout } of arrangements) {
      const fit = lapReviewFit(lapReviewFrame(layout.zones.zoneA, layout.width));
      expect({ name, deltas: fit.deltas }).toMatchObject({ deltas: true });
    }
  });

  test('a panel too narrow for even that keeps the lap time and nothing else', () => {
    // A guard rather than a case: nothing that ships is this narrow. What it promises is that the
    // panel sheds rather than drawing past its own edge, whatever rectangle it is handed.
    const tiny = rect(0, 0, 320, LAP_REVIEW_HEIGHT);
    expect(lapReviewFit(tiny)).toEqual({ lapFs: D.big, strip: false, deltas: false, fuel: false });
    for (const item of [...walkItems([lapReview(tiny, 'true')])]) {
      if (item.kind === 'layer') continue;
      expect({ item: item.name, inside: contains(tiny, item.rect) }).toMatchObject({ inside: true });
    }
  });

  test('the driver line is measured from the widest it can draw, not from its sample', () => {
    const line = textNamed(reference.items, 'driver');
    expect(line.widest).toBe('YOU · #9999 · P99');
    expect(measureText('BarlowMedium', line.widest!, line.fontSize)).toBeLessThanOrEqual(line.rect.width);
  });
});
