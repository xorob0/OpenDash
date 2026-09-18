/**
 * The change notification: the box, the list it draws from, and the rule about the trend mark's
 * colour.
 *
 * Three things here are worth a test rather than a reading. The box is measured against a 400 px
 * rectangle it has to hold a name, a 64 px reading and a mark inside, which is where it would clip;
 * only one notification may be out at a time, over a rectangle the pop-ups also use; and the mark's
 * ink is in the pixels of a file rather than in a token, so the only way to hold it to the canvas's
 * rule is to open the file and look.
 */
import { describe, expect, test } from 'bun:test';
import { inflateSync } from 'node:zlib';
import { readFileSync } from 'node:fs';
import {
  CHANGE_NOTIFICATION_HEIGHT,
  CHANGE_NOTIFICATION_MS,
  CHANGE_NOTIFICATION_PAD_X,
  CHANGE_NOTIFICATION_RULE,
  CHANGE_NOTIFICATION_VALUE_SIZE,
  CHANGE_NOTIFICATION_WIDTH,
  TREND_GAP,
  TREND_SIZE,
  changeNotificationFrame,
  changeNotificationVisible,
  changeNotifications,
} from '../src/components/changeNotification.ts';
import { POP_UPS, popUpFrame } from '../src/components/popUp.ts';
import { assetPath, imageOf, RANK_UP, TREND_DOWN, TREND_UP } from '../src/design/assets.ts';
import { measureText } from '../src/design/advances.ts';
import { rect } from '../src/design/geometry.ts';
import { STRIP_CELLS } from '../src/zones/bar.ts';
import { TRACKED_VALUES, trackedValue } from '../src/second/tracked.ts';
import { ds } from '../src/tokens.ts';
import type { ImageItem, RectangleItem, TextItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, rectOf } from '../src/zones/index.ts';

const HERO = rect(824, 141, 272, 200);
const items = [...walkItems(changeNotifications(HERO))];
const named = (id: string, part: string): (typeof items)[number] => items.find((i) => i.name === `notice.${id}.${part}`)!;

describe('one list, two boxes', () => {
  test('the strip draws the tracked values and cannot hold a list of its own', () => {
    expect(STRIP_CELLS.map((cell) => cell.id)).toEqual(TRACKED_VALUES.map((value) => value.id));
    for (const cell of STRIP_CELLS) {
      const value = trackedValue(cell.id);
      expect({ id: cell.id, read: cell.expr, pattern: cell.pattern }).toEqual({ id: value.id, read: value.read, pattern: value.pattern });
    }
  });

  test('and the two names differ where the two boxes differ, which is the point of keeping both', () => {
    // The strip's cell is a few tens of pixels and the notification has four hundred, so the short
    // form is not a shortening of the long one that a single name could serve.
    expect(STRIP_CELLS.map((cell) => cell.label)).toEqual(['Slip', 'TC', 'Cut', 'Bias', 'ABS', 'Map', 'Diff']);
    expect(TRACKED_VALUES.map((value) => value.notice)).toEqual(['Slip', 'TC', 'TC cut', 'Brake bias', 'ABS', 'Engine map', 'Diff']);
    // The canvas's three examples, which are the three the sheet draws.
    expect(trackedValue('bias').notice).toBe('Brake bias');
    expect(trackedValue('tc').notice).toBe('TC');
    expect(trackedValue('map').notice).toBe('Engine map');
  });

  test('a value that can never fire is not in the list', () => {
    // ERS mode is on the canvas's list and left out while iRacing reports ERSPercent as zero and
    // EngineMap as -1: an entry that cannot fire is worse than an absent one.
    expect(TRACKED_VALUES.map((v) => v.id)).not.toContain('ers');
  });
});

describe('the box', () => {
  const frame = changeNotificationFrame(HERO);

  test('it is the sheet’s 400 by 96, centred where a pop-up is centred', () => {
    expect({ width: frame.width, height: frame.height }).toEqual({ width: CHANGE_NOTIFICATION_WIDTH, height: 96 });
    expect(CHANGE_NOTIFICATION_HEIGHT).toBe(ds.indicator.changeNotification.height);
    const popUp = popUpFrame(HERO);
    expect({ x: frame.left + frame.width / 2, y: frame.top + frame.height / 2 }).toEqual({
      x: popUp.left + popUp.width / 2,
      y: popUp.top + popUp.height / 2,
    });
  });

  test('the ground and the rule are the pop-up’s two tokens, and the rule is 2 px along the top', () => {
    const box = named('bias', 'box') as RectangleItem;
    const rule = named('bias', 'rule') as RectangleItem;
    expect({ ground: box.backgroundColor, rect: box.rect }).toEqual({ ground: ds.purpose.popUp.surface, rect: frame });
    expect({ ink: rule.backgroundColor, rect: rule.rect }).toEqual({
      ink: ds.purpose.popUp.rule,
      rect: rect(frame.left, frame.top, frame.width, CHANGE_NOTIFICATION_RULE),
    });
  });

  test('the name is on the left at the sheet’s padding, and the reading is 64 px', () => {
    const label = named('bias', 'label') as TextItem;
    const value = named('bias', 'value') as TextItem;
    // Upper-cased as every label on a face is, which is what the sheet draws.
    expect({ text: label.text, left: label.rect.left, size: label.fontSize }).toEqual({
      text: 'BRAKE BIAS',
      left: frame.left + CHANGE_NOTIFICATION_PAD_X,
      size: ds.size.label,
    });
    expect(value.fontSize).toBe(CHANGE_NOTIFICATION_VALUE_SIZE);
  });

  test('the mark is 14 px square, 12 px after the reading, against the right padding', () => {
    for (const direction of ['up', 'down']) {
      const mark = named('bias', `trend.${direction}`) as ImageItem;
      expect({ direction, width: mark.rect.width, height: mark.rect.height }).toEqual({ direction, width: TREND_SIZE, height: TREND_SIZE });
      expect({ direction, right: mark.rect.left + mark.rect.width }).toEqual({ direction, right: frame.left + frame.width - CHANGE_NOTIFICATION_PAD_X });
    }
    const value = named('bias', 'value') as TextItem;
    const mark = named('bias', 'trend.up') as ImageItem;
    expect(mark.rect.left - (value.rect.left + value.rect.width)).toBe(TREND_GAP);
  });

  test('every part of every notification stays inside the box, which is where it would clip', () => {
    const frames = new Set(TRACKED_VALUES.map((v) => v.id));
    expect(frames.size).toBe(TRACKED_VALUES.length);
    for (const item of items) {
      if (item.kind === 'layer') continue;
      const r = item.rect;
      const inside = r.left >= frame.left && r.left + r.width <= frame.left + frame.width && r.top >= frame.top && r.top + r.height <= frame.top + frame.height;
      expect({ item: item.name, rect: r, inside }).toMatchObject({ inside: true });
    }
  });

  test('the longest name and the widest reading do not meet in the middle', () => {
    for (const value of TRACKED_VALUES) {
      const label = named(value.id, 'label') as TextItem;
      const reading = named(value.id, 'value') as TextItem;
      const text = Math.ceil(measureText('BarlowMedium', label.text.toUpperCase(), label.fontSize));
      expect({ id: value.id, clear: label.rect.left + text < reading.rect.left }).toMatchObject({ clear: true });
    }
  });
});

describe('when it is out', () => {
  test('the window is the token’s three seconds, and it is SimHub’s window rather than ours', () => {
    expect(CHANGE_NOTIFICATION_MS).toBe(ds.indicator.changeNotification.durationMs);
    const visible = changeNotificationVisible('bias');
    expect(visible).toContain(`changed(${CHANGE_NOTIFICATION_MS}`);
    // Nothing keeps state of its own, which is what ADR 0009 allows and what it refuses.
    expect(visible).not.toContain('setvalue');
    expect(visible).not.toContain('getvalue');
  });

  test('only one is ever out: each carries the negation of every value above it', () => {
    for (const [index, value] of TRACKED_VALUES.entries()) {
      const visible = changeNotificationVisible(value.id);
      for (const higher of TRACKED_VALUES.slice(0, index)) expect({ value: value.id, higher: higher.id, ranked: visible.includes(higher.read) }).toMatchObject({ ranked: true });
    }
    // The first carries no negation of a tracked value, since nothing is above it.
    const first = changeNotificationVisible(TRACKED_VALUES[0]!.id);
    for (const other of TRACKED_VALUES.slice(1)) expect({ other: other.id, ranked: first.includes(other.read) }).toMatchObject({ ranked: false });
  });

  test('and it waits for the lap time, which is the one pop-up that is an event', () => {
    const visible = changeNotificationVisible('tc');
    const lap = POP_UPS.find((p) => p.id === 'lap')!;
    expect({ under: visible.includes(lap.when) }).toMatchObject({ under: true });
    // And not for the other two, which are states: low fuel is true for the rest of a stint and
    // DRS for every straight, so waiting for either would silence the box when it is most wanted.
    for (const popUp of POP_UPS.filter((p) => p.id !== 'lap')) {
      expect({ popUp: popUp.id, under: visible.includes(popUp.when) }).toMatchObject({ under: false });
    }
  });

  test('a driver out of the car silences it, read as a boolean and defaulted to true', () => {
    const visible = changeNotificationVisible('abs');
    // A raw telemetry boolean reaches a binding as `true`, where GameData's arrive as 1: comparing
    // this one with a number would silence every notification and nothing would say so.
    expect(visible).toContain('isnull([DataCorePlugin.GameRawData.Telemetry.IsOnTrack], true)');
    expect(visible).not.toContain('IsOnTrack], 1)');
  });

  test('a car that does not have the setting raises nothing, tested where the reading is defaulted', () => {
    // The brake bias reads through an isnull default and is never null itself, so the presence test
    // is the property behind it, exactly as the strip's cell does it.
    expect(changeNotificationVisible('bias')).toContain('!(isnull([DataCorePlugin.GameData.BrakeBias]))');
    expect(changeNotificationVisible('bias')).not.toContain('isnull(isnull(');
  });

  test('the mark asks which way within the same window', () => {
    const up = named('bias', 'trend.up') as ImageItem;
    const down = named('bias', 'trend.down') as ImageItem;
    expect(String(up.bindings!.Visible!.formula)).toBe(`isincreasing(${CHANGE_NOTIFICATION_MS}, ${trackedValue('bias').read})`);
    expect(String(down.bindings!.Visible!.formula)).toBe(`isdecreasing(${CHANGE_NOTIFICATION_MS}, ${trackedValue('bias').read})`);
  });
});

/**
 * The pixels of a trend mark, for the one claim about it that no token can hold.
 *
 * Filter 0 only, which is what the files are written with; anything else throws rather than
 * returning a colour read off a scanline it did not reconstruct.
 */
const inkOf = (file: string): { r: number; g: number; b: number } => {
  const bytes = new Uint8Array(readFileSync(file));
  const data: number[] = [];
  let offset = 8;
  while (offset < bytes.length) {
    const length = ((bytes[offset]! << 24) | (bytes[offset + 1]! << 16) | (bytes[offset + 2]! << 8) | bytes[offset + 3]!) >>> 0;
    const type = String.fromCharCode(bytes[offset + 4]!, bytes[offset + 5]!, bytes[offset + 6]!, bytes[offset + 7]!);
    if (type === 'IDAT') data.push(...bytes.slice(offset + 8, offset + 8 + length));
    offset += 12 + length;
  }
  const pixels = new Uint8Array(inflateSync(Uint8Array.from(data)));
  const width = ((bytes[16]! << 24) | (bytes[17]! << 16) | (bytes[18]! << 8) | bytes[19]!) >>> 0;
  const stride = width * 4 + 1;
  for (let row = 0; row * stride < pixels.length; row++) {
    const filter = pixels[row * stride];
    if (filter !== 0) throw new Error(`${file} row ${row} uses PNG filter ${filter}; this test reads filter 0 only`);
    for (let x = 0; x < width; x++) {
      const at = row * stride + 1 + x * 4;
      if (pixels[at + 3] === 255) return { r: pixels[at]!, g: pixels[at + 1]!, b: pixels[at + 2]! };
    }
  }
  throw new Error(`${file} has no opaque pixel`);
};

describe('the trend mark wears the colour the canvas gives it', () => {
  test('both files are drawn in text.secondary and in no purpose colour', () => {
    const hex = (c: { r: number; g: number; b: number }): string =>
      `#${[c.r, c.g, c.b].map((v) => v.toString(16).padStart(2, '0')).join('').toUpperCase()}`;
    for (const asset of [TREND_UP, TREND_DOWN]) {
      expect({ asset: asset.name, ink: hex(inkOf(assetPath(asset))) }).toEqual({ asset: asset.name, ink: ds.color.text.secondary.toUpperCase() });
    }
    // Which way a setting moved is information and not a state, so it is emphatically not the
    // leaderboard's convention: that mark is the delta green and red and is a different file.
    expect(hex(inkOf(assetPath(RANK_UP)))).not.toBe(ds.color.text.secondary.toUpperCase());
    expect(imageOf(TREND_UP).md5).not.toBe(imageOf(RANK_UP).md5);
  });
});

describe('every face draws it', () => {
  test('the notification is over zone A on all eight, and takes no room of its own', () => {
    for (const layout of ZONE_FACES) {
      const zoneA = rectOf(layout, 'A');
      const frame = changeNotificationFrame(zoneA);
      expect({ face: layout.folder, width: frame.width, height: frame.height }).toEqual({
        face: layout.folder,
        width: CHANGE_NOTIFICATION_WIDTH,
        height: CHANGE_NOTIFICATION_HEIGHT,
      });
      // Inside the pop-up's own box, which is what carries over everything popUp.test.ts holds the
      // pop-up to: it clears the rev bar, the bar of settled values, the limiter banner and band D,
      // where a flag has the better claim on the same sixty pixels.
      const popUp = popUpFrame(zoneA);
      const inside =
        frame.left >= popUp.left &&
        frame.left + frame.width <= popUp.left + popUp.width &&
        frame.top >= popUp.top &&
        frame.top + frame.height <= popUp.top + popUp.height;
      expect({ face: layout.folder, frame, popUp, inside }).toMatchObject({ inside: true });
    }
  });
});
