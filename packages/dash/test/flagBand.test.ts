/**
/**
 * The flag band: the phase of its chequer, the weight its name is set in, the border every coloured
 * state carries, and the flash that must not uncover the page beneath it.
 *
 * A flag takes band D over when it comes out, because an alert outranks fuel, so whatever the band
 * does in that moment it has to leave nothing of the page showing. It takes it for a few seconds and
 * then settles, #380, into one rectangle in zone C's title line on the house faces, #731, and the second
 * half of this file is that: the same twenty conditions in the same order, in one smaller rectangle,
 * with the page a driver was reading back on the band. Every artboard fills the chequered
 * state with `repeating-conic-gradient(#F5F7FA 0 25%, #0A0B0D 0 50%)` over a tile the height of the
 * band, which sweeps clockwise from twelve o'clock, so the band opens dark at its left edge and the
 * first light square begins one square in; the round faces draw the same board as marks around the
 * rim, and whatever the band does at its left edge the ring does at twelve o'clock. The coloured
 * states carry the artboards' 3 px border in their own fill, their names are set in the artboards'
 * 700 rather than the label weight, and the yellow flashes an opaque band over its fill rather than
 * blinking the whole layer away.
 */
import { describe, expect, test } from 'bun:test';
import { ALERT_DISC_RATIO, chequerBand } from '../src/components/alertBand.ts';
import { BLACK_FLAG_BORDER, BLUE_FLAG_ID, FLAG_BLINK_MS, FLAG_NAME_WEIGHT, FLAG_TAKEOVER_MS, flagSettledWidth, flagTakingBand } from '../src/components/flagStrip.ts';
import { chequerCount, chequerStep } from '../src/components/flagRing.ts';
import { BLUE_FLAG_DETAILS } from '../src/contract.ts';
import { ALERT_CATALOGUE, bandNames, bandRaised, conditionRaised, FLAG_CATALOGUE, raisedRank } from '../src/flags.ts';
import { contains, rect } from '../src/design/geometry.ts';
import type { Item, LayerItem, RectangleItem, TextItem } from '../src/generator.ts';
import { hero } from '../src/hero/hero.ts';
import { layout480round, layout800round, type Layout } from '../src/layouts/index.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { measureText } from '../src/design/advances.ts';
import { ZONE_FACES, faceItems, zoneRegions, type ZoneLayout } from '../src/zones/index.ts';
import { houseFlagSettled } from '../src/zones/face.ts';

/** The two colours every artboard quotes for the chequer. */
const CHEQUER = '#F5F7FA';
const GROUND = '#0A0B0D';

/** The tile each artboard quotes, which is the band's height. */
const TILE: Record<string, number> = {
  OpenDash: 60,
  'OpenDash 1280x480': 60,
  'OpenDash 1280x400': 54,
  'OpenDash 850x480': 60,
  'OpenDash 800x480': 60,
  'OpenDash 1280x720': 60,
  'OpenDash 800x286': 58,
  'OpenDash 600x686': 56,
};

/**
 * The band format's states, which the face now draws inside a group of their own: the two formats
 * `OpenDash.<Face>FlagFormat` chooses between are both built and one is shown, so the six states are
 * one level further down than they were. Everything this file asserts is about the band format and
 * is found by its `flag.` prefix; the full-screen block is `flagFull.` and is held in
 * `flagFormat.test.ts`.
 */
const bandFormatItems = (items: readonly Item[]): Item[] => [...walkItems(items)];

const chequerChildren = (items: readonly Item[]): RectangleItem[] => {
  const layer = bandFormatItems(items).find((i): i is LayerItem => i.kind === 'layer' && i.name === 'flag.chequered');
  if (!layer) throw new Error('no chequered flag');
  return layer.children.map((c) => {
    if (c.kind !== 'rect') throw new Error(`${c.name} is not a rect`);
    return c;
  });
};

/** How far into a tile the first light square sits, as a fraction of the tile. */
const bandPhase = (face: ZoneLayout): number => {
  const { band } = face.zones;
  const top = chequerChildren(faceItems(face)).slice(1).filter((c) => c.rect.top === band.top);
  return (Math.min(...top.map((c) => c.rect.left)) - band.left) / band.height;
};

/** The face a round layout hangs its ring on, which is what the count and the step are read from. */
const ringFace = (layout: Layout) => {
  const { flags } = layout.hero;
  if (flags.kind !== 'flagRing') throw new Error(`${layout.folder} draws no flag ring`);
  return flags.face;
};

/** The same fraction on a rim, where a tile is one check and the gap after it. */
const ringPhase = (layout: Layout): number => (chequerChildren(hero(layout.hero))[0]!.rotation ?? 0) / chequerStep(ringFace(layout));

describe('the chequered band', () => {
  for (const face of ZONE_FACES) {
    describe(face.folder, () => {
      const { band } = face.zones;
      const check = band.height / 2;
      const children = chequerChildren(faceItems(face));
      const ground = children[0]!;
      const squares = children.slice(1);

      test('lays the ground over the whole band and tiles it with light squares half the band high', () => {
        expect(ds.color.surface.base).toBe(GROUND);
        expect(ds.purpose.flag.chequer).toBe(CHEQUER);
        expect(TILE[face.folder]).toBe(band.height);
        expect(ground.name).toBe('flag.chequered.band');
        expect(ground.rect).toEqual(band);
        expect(ground.backgroundColor).toBe(GROUND);
        expect(squares.length).toBe(Math.ceil(band.width / check));
        for (const square of squares) {
          expect({ name: square.name, color: square.backgroundColor }).toEqual({ name: square.name, color: CHEQUER });
          expect(square.rect.height).toBe(check);
          expect(square.rect.width).toBeLessThanOrEqual(check);
          expect({ name: square.name, inside: contains(band, square.rect) }).toEqual({ name: square.name, inside: true });
        }
      });

      test('opens on the ground: the top row starts one square in, and the bottom row is the one that starts at the left edge', () => {
        const columnOf = (square: RectangleItem): number => (square.rect.left - band.left) / check;
        const top = squares.filter((s) => s.rect.top === band.top);
        const bottom = squares.filter((s) => s.rect.top === band.top + check);
        expect(top.map(columnOf)).not.toContain(0);
        expect(top.every((s) => columnOf(s) % 2 === 1)).toBe(true);
        expect(bottom.every((s) => columnOf(s) % 2 === 0)).toBe(true);
        expect(top.find((s) => s.rect.left === band.left + check)?.rect).toEqual(rect(band.left + check, band.top, check, check));
        expect(bottom.find((s) => s.rect.left === band.left)?.rect).toEqual(rect(band.left, band.top + check, check, check));
        expect(squares.filter((s) => s.rect.top !== band.top && s.rect.top !== band.top + check)).toEqual([]);
      });

      test('carries no label', () => {
        expect([...walkItems(children)].filter((i) => i.kind === 'text')).toEqual([]);
      });
    });
  }
});

/**
 * The chequer on a band of any height, #998. The check is half the band, which on an odd band is a
 * half pixel, and the squares are cut between rounded edges so that the board still fills the band
 * exactly: whole pixels, inside the frame, two rows that meet and are a pixel apart at most, and one
 * set of column edges that both rows share. Heights 1 to 160 cover every band and corner block the
 * packages draw (12 to 150, the Porsche's 115 and the BMW's 53, 57 and 85 among them), at an odd
 * origin and at the widths of a band, a corner block and a settled strip.
 */
describe('the chequer on a band of any height', () => {
  const whole = (r: { left: number; top: number; width: number; height: number }): boolean =>
    [r.left, r.top, r.width, r.height].every(Number.isInteger) && r.width > 0 && r.height > 0;
  for (const width of [1280, 176, 16]) {
    test(`fills a band ${width} wide exactly, at every height from 1 to 160`, () => {
      for (let height = 1; height <= 160; height++) {
        const frame = rect(10, 333, width, height);
        const at = `${width}x${height}`;
        const [ground, ...squares] = chequerBand('flag.chequered', frame) as RectangleItem[];
        expect({ at, ground: ground!.rect }).toEqual({ at, ground: frame });
        for (const square of squares) {
          expect({ at, square: square.name, whole: whole(square.rect), inside: contains(frame, square.rect) }).toEqual({ at, square: square.name, whole: true, inside: true });
        }

        const rowOf = (square: RectangleItem): number => Number(/\.r(\d)c/.exec(square.name)![1]);
        const rows = [0, 1].map((row) => squares.filter((s) => rowOf(s) === row));
        const top = frame.top;
        const h0 = rows[0]![0]?.rect.height ?? Math.round(height / 2);
        for (const s of rows[0]!) expect({ at, square: s.name, top: s.rect.top, height: s.rect.height }).toEqual({ at, square: s.name, top, height: h0 });
        for (const s of rows[1]!) expect({ at, square: s.name, top: s.rect.top, height: s.rect.height }).toEqual({ at, square: s.name, top: top + h0, height: height - h0 });
        expect({ at, apart: Math.abs(h0 - (height - h0)) <= 1 }).toEqual({ at, apart: true });

        // Below a check of one pixel the board is narrower than a pixel and has no columns to hold.
        if (height < 2) continue;
        const check = height / 2;
        const right = frame.left + frame.width;
        const edges = [...new Set([frame.left, right, ...squares.flatMap((s) => [s.rect.left, s.rect.left + s.rect.width])])].sort((a, b) => a - b);
        edges.slice(1).forEach((edge, i) => {
          const run = edge - edges[i]!;
          const last = edge === right;
          const ok = last ? run >= 1 && run <= Math.ceil(check) : run === Math.floor(check) || run === Math.ceil(check);
          expect({ at, from: edges[i], run, ok }).toEqual({ at, from: edges[i], run, ok: true });
        });
        // Every square spans one run between the shared edges, row 0 every odd run and row 1 every
        // even one: no overlap, no gap with a dark square between, and row 1's columns start where
        // row 0's end.
        for (const row of [0, 1]) {
          if (row === 1 && h0 === height) continue;
          const runs = rows[row]!.map((s) => {
            const i = edges.indexOf(s.rect.left);
            expect({ at, square: s.name, spansOne: edges[i + 1] === s.rect.left + s.rect.width }).toEqual({ at, square: s.name, spansOne: true });
            return i;
          });
          const expected = edges.slice(0, -1).map((_, i) => i).filter((i) => i % 2 === (row + 1) % 2);
          expect({ at, row, runs }).toEqual({ at, row, runs: expected });
        }
      }
    });
  }
});

/** The four states the band fills with one colour and names; the chequer and the black differ. */
const COLOURED = ['yellow', 'blue', 'white', 'green'] as const;

const flagLayers = (face: ZoneLayout): Map<string, LayerItem> =>
  new Map(
    bandFormatItems(faceItems(face))
      .filter((i): i is LayerItem => i.kind === 'layer' && i.name.startsWith('flag.') && i.name.split('.').length === 2)
      .map((l) => [l.name.slice('flag.'.length), l]),
  );

const layerOf = (face: ZoneLayout, id: string): LayerItem => {
  const layer = flagLayers(face).get(id);
  if (!layer) throw new Error(`no ${id} flag on ${face.folder}`);
  return layer;
};

const labelsOf = (items: readonly Item[]): TextItem[] => [...walkItems(items)].filter((i): i is TextItem => i.kind === 'text');

const bandOf = (face: ZoneLayout, id: string): RectangleItem => {
  const fill = layerOf(face, id).children.find((c): c is RectangleItem => c.kind === 'rect' && c.name === `flag.${id}.band`);
  if (!fill) throw new Error(`no band under the ${id} flag`);
  return fill;
};

describe('the flag name', () => {
  for (const face of ZONE_FACES) {
    test(`${face.folder} writes every name in the label family at ${FLAG_NAME_WEIGHT}`, () => {
      // 700 is what every FaceVariants sheet sets the band's name in, against the 500 of every
      // other label; Barlow Bold is shipped and measured so that the fit tests measure the face
      // the band is drawn in rather than the one it used to be.
      for (const id of [...COLOURED, 'black']) {
        const names = labelsOf(layerOf(face, id).children);
        // One run each, and three on the blue: it is the one band that can say more than its own
        // name, one run per value of BlueFlagDetail with one of the three visible at a time. They
        // are the same line box in the same face, which is what this test is about.
        expect({ id, drawn: names.length }).toEqual({ id, drawn: id === BLUE_FLAG_ID ? BLUE_FLAG_DETAILS.length : 1 });
        for (const name of names) expect({ id, font: name.font, weight: name.fontWeight }).toEqual({ id, font: ds.font.label, weight: FLAG_NAME_WEIGHT });
      }
      expect(labelsOf(layerOf(face, 'chequered').children)).toEqual([]);
    });
  }
});

describe('the chequered ring', () => {
  for (const layout of [layout480round, layout800round]) {
    test(`${layout.folder} draws ${chequerCount(ringFace(layout))} checks in the band's phase, none of them at twelve o'clock`, () => {
      const checks = chequerChildren(hero(layout.hero));
      const step = chequerStep(ringFace(layout));
      expect(checks).toHaveLength(chequerCount(ringFace(layout)));
      checks.forEach((check, k) => {
        expect({ name: check.name, color: check.backgroundColor }).toEqual({ name: check.name, color: CHEQUER });
        expect(check.rotation).toBe((k + 0.5) * step);
      });
      expect(checks.map((c) => (c.rotation ?? 0) % 360)).not.toContain(0);
    });
  }

  test('the ring and the band open on the ground by the same fraction of a tile', () => {
    for (const face of ZONE_FACES) expect({ face: face.folder, phase: bandPhase(face) }).toEqual({ face: face.folder, phase: 0.5 });
    for (const layout of [layout480round, layout800round]) expect({ face: layout.folder, phase: ringPhase(layout) }).toEqual({ face: layout.folder, phase: 0.5 });
  });
});

describe('the coloured bands', () => {
  for (const face of ZONE_FACES) {
    test(`${face.folder} fills band D and borders it ${BLACK_FLAG_BORDER} px in its own fill`, () => {
      // The artboards draw the border on every state and leave it transparent over the fill, which
      // over that fill is these three pixels of it; what the code cannot do is omit the border on
      // four states and carry it on one, which is what it did.
      for (const id of COLOURED) {
        const fill = bandOf(face, id);
        const colour = ds.purpose.flag[id];
        expect({ id, rect: fill.rect, colour: fill.backgroundColor }).toEqual({ id, rect: face.zones.band, colour });
        expect({ id, border: fill.border }).toEqual({
          id,
          border: { color: colour, top: BLACK_FLAG_BORDER, bottom: BLACK_FLAG_BORDER, left: BLACK_FLAG_BORDER, right: BLACK_FLAG_BORDER },
        });
      }
    });
  }
});

describe('the waved yellow flash', () => {
  for (const face of ZONE_FACES) {
    test(`${face.folder} is opaque in both phases, the page never showing through`, () => {
      // The flash was BlinkEnabled on the whole layer, so half of every cycle drew no band at all
      // and band D's page read through the flag that had taken it over. What blinks is one opaque
      // band over another, at the rate design/tokens.json states.
      //
      // It is the waved yellow that flashes and not the standing one, which is the rule the box
      // keeps under "waving is blinking". The band used to flash on SimHub's `Flag_Yellow`, which
      // folds `yellow`, `yellowWaving`, `caution` and `cautionWaving` into one, so a standing yellow
      // strobed for as long as it was out; band D reads the bits now and can tell them apart.
      const layer = layerOf(face, 'yellowWaving');
      expect(layer.blink).toBeUndefined();

      const blinking = layer.children.filter((c) => c.blink?.enabled);
      expect(blinking.map((c) => c.name)).toEqual(['flag.yellowWaving.flash']);

      const flash = blinking[0]!;
      if (flash.kind !== 'rect') throw new Error('the flash is a band');
      expect(flash.blink).toEqual({ enabled: true, delayMs: FLAG_BLINK_MS });
      expect(flash.backgroundColor).toBe(ds.color.surface.base);
      expect(contains(face.zones.band, flash.rect)).toBe(true);
      // Last, so that it is drawn over the fill it alternates with rather than under it.
      expect(layer.children[layer.children.length - 1]?.name).toBe(flash.name);

      // The ground under it holds the whole band whichever phase the flash is in.
      const ground = bandOf(face, 'yellowWaving');
      expect(ground.blink).toBeUndefined();
      expect({ rect: ground.rect, colour: ground.backgroundColor }).toEqual({ rect: face.zones.band, colour: ds.purpose.flag.yellow });
    });
  }

  test('and no other state flashes at all', () => {
    for (const face of ZONE_FACES) {
      for (const id of ALERT_CATALOGUE.map((c) => c.id).filter((id) => id !== 'yellowWaving')) {
        const layer = layerOf(face, id);
        expect({ face: face.folder, id, blinking: [...walkItems([layer])].filter((i) => i.blink?.enabled).map((i) => i.name) }).toEqual({
          face: face.folder,
          id,
          blinking: [],
        });
      }
    }
  });
});

/**
 * What the flag does once its few seconds are up: it settles into one rectangle of the face and gives
 * the band's page back, #380 and #731.
 *
 * The ticket's own case is a full course yellow on the 850 x 480 face with 2.1 litres in the tank: the
 * band read SAFETY CAR then, and nothing on the face said fuel, for as long as the caution lasted,
 * which is several minutes, and a caution is exactly when a driver decides whether to pit. The flag
 * has said everything it has to say after two seconds.
 *
 * It settled into a block at each end of the band until #731, and the author ruled that out: with the
 * takeover that was the flag's colour in three places, the blocks covered band D's corner fields on four
 * faces and were sixteen nameless pixels on the other four. On the house faces the rectangle is in zone
 * C's title line, against the counter, `houseFlagSettled`; `flagSettled.test.ts` holds it, and every
 * theme's, to a part of the face no reading uses. What is held here is how the condition is drawn in it.
 */
const settledLayers = (face: ZoneLayout): Map<string, LayerItem> =>
  new Map(
    faceItems(face)
      .filter((i): i is LayerItem => i.kind === 'layer' && i.name === 'flagCorner')
      .flatMap((g) => g.children.filter((c): c is LayerItem => c.kind === 'layer'))
      .map((l) => [l.name.slice('flagCorner.'.length), l]),
  );

const settledLayerOf = (face: ZoneLayout, id: string): LayerItem => {
  const layer = settledLayers(face).get(id);
  if (!layer) throw new Error(`no settled ${id} flag on ${face.folder}`);
  return layer;
};

describe('the flag settles into one rectangle in zone C’s title line', () => {
  for (const face of ZONE_FACES) {
    const one = houseFlagSettled(zoneRegions(face));
    const zoneC = face.zones.zoneC;

    test(`${face.folder} keeps it in zone C's title line, against the counter`, () => {
      // The title line is the 22 px under the zone's 6 px of top padding; the rectangle is that line's
      // height, and ends a few pixels short of the counter's widest reading.
      expect({ face: face.folder, top: one.top - zoneC.top, height: one.height }).toEqual({ face: face.folder, top: 6, height: 22 });
      expect({ face: face.folder, inZone: contains(zoneC, one) }).toEqual({ face: face.folder, inZone: true });
      // No wider than the longest name asks, and on every face wide enough for every condition's shortest.
      expect(one.width).toBeLessThanOrEqual(flagSettledWidth());
      const shortest = Math.max(...ALERT_CATALOGUE.map((c) => bandNames(c.band).at(-1)).filter((n): n is string => n !== undefined).map((n) => measureText('BarlowBold', n, ds.size.label)));
      expect({ face: face.folder, width: one.width, fits: shortest + 2 * BLACK_FLAG_BORDER < one.width }).toEqual({ face: face.folder, width: one.width, fits: true });
    });

    test(`${face.folder} draws every condition in it, in the shape and colour it draws on the whole band`, () => {
      // Every condition, neutral alerts included: each writes its name there, so push to pass and the
      // headlight flash say what they are rather than reading as a white flag or a black one.
      expect([...settledLayers(face).keys()]).toEqual(ALERT_CATALOGUE.map((c) => c.id));
      for (const condition of ALERT_CATALOGUE) {
        const layer = settledLayerOf(face, condition.id);
        for (const item of [...walkItems(layer.children)].filter((i) => i.kind !== 'layer')) {
          // A name's box is a WPF line box and is taller than the rectangle, so it is held to the
          // rectangle's width; everything else is inside the rectangle outright.
          if (item.kind === 'text') {
            expect({ item: item.name, within: item.rect.left === one.left && item.rect.width === one.width }).toMatchObject({ within: true });
            continue;
          }
          expect({ item: item.name, rect: item.rect, inside: contains(one, item.rect) }).toMatchObject({ inside: true });
        }
        // The ground carries the condition's own colour where the shape is the filled one, exactly as
        // the whole band does; the outlined family and the chequer lay `surface.base` instead.
        if (condition.band.shape === 'filled') {
          const ground = layer.children.find((c): c is RectangleItem => c.kind === 'rect' && c.name === `flagCorner.${condition.id}.band`)!;
          expect({ id: condition.id, rect: ground.rect, colour: ground.backgroundColor }).toEqual({ id: condition.id, rect: one, colour: condition.band.colour });
        }
      }
    });

    test(`${face.folder} writes the longest name that fits it`, () => {
      // A name too wide for the rectangle is not shrunk and not clipped: the condition writes its short
      // form instead, FCY for FULL COURSE YELLOW, and every condition has one that fits. The blue flag's
      // detail is held to the same rule, #497: where the widest of its three runs fits, the rectangle
      // writes the three, and where it does not, the name.
      const wholeBlue = labelsOf(layerOf(face, BLUE_FLAG_ID).children);
      const room = one.width - 2 * BLACK_FLAG_BORDER;
      for (const condition of ALERT_CATALOGUE) {
        const names = labelsOf(settledLayerOf(face, condition.id).children);
        for (const name of names) {
          expect({ item: name.name, font: name.font, weight: name.fontWeight }).toEqual({ item: name.name, font: ds.font.label, weight: FLAG_NAME_WEIGHT });
          const drawn = measureText('BarlowBold', name.widest ?? name.text, name.fontSize);
          expect({ item: name.name, drawn, box: name.rect.width, fits: drawn < name.rect.width }).toMatchObject({ fits: true });
        }
        const text = bandNames(condition.band).find((name) => measureText('BarlowBold', name, ds.size.label) < room);
        const detailFits = condition.id === BLUE_FLAG_ID && wholeBlue.every((run) => measureText('BarlowBold', run.widest ?? run.text, ds.size.label) < room);
        const expected = detailFits ? wholeBlue.map((run) => run.text) : text === undefined ? [] : [text];
        expect({ face: face.folder, id: condition.id, names: names.map((n) => n.text) }).toEqual({ face: face.folder, id: condition.id, names: expected });
      }
      // The incident is the other way: the rectangle writes INCIDENT, or INC, unbound, and its count
      // against the limit is the whole band's for the seconds it has it.
      for (const label of labelsOf(settledLayerOf(face, 'incident').children)) {
        expect({ item: label.name, text: ['INCIDENT', 'INC'].includes(label.text), bound: label.bindings?.Text !== undefined }).toEqual({ item: label.name, text: true, bound: false });
      }
    });

    test(`${face.folder} draws the debris flag's red stripes in it, so a settled debris flag is never a yellow`, () => {
      const parts = settledLayerOf(face, 'debris').children.filter((c): c is RectangleItem => c.kind === 'rect');
      const stripes = parts.filter((p) => p.backgroundColor === ds.purpose.flag.debrisStripe);
      expect({ face: face.folder, ground: parts[0]?.backgroundColor, stripes: stripes.length >= 1 }).toEqual({ face: face.folder, ground: ds.purpose.flag.debris, stripes: true });
      for (const stripe of stripes) {
        expect({ stripe: stripe.name, fullHeight: stripe.rect.top === one.top && stripe.rect.height === one.height, inside: contains(one, stripe.rect) }).toEqual({
          stripe: stripe.name,
          fullHeight: true,
          inside: true,
        });
      }
    });

    test(`${face.folder} draws the meatball in it as its disc on the near-black, with no name`, () => {
      // A black box with an orange disc in the middle and no text, which is the author's ruling on
      // #498, in the settled rectangle as on the whole band. The disc is two thirds of its shorter side.
      const parts = settledLayerOf(face, 'meatball').children;
      const [ground, disc] = parts;
      if (parts.length !== 2 || ground?.kind !== 'rect' || disc?.kind !== 'ellipse') throw new Error(`${face.folder} draws the meatball as a ground and a disc`);
      expect({
        face: face.folder,
        ground: ground.rect,
        fill: ground.backgroundColor,
        border: ground.border,
        disc: disc.fillColor,
        inside: contains(one, disc.rect),
        size: Math.abs(disc.rect.width - ALERT_DISC_RATIO * Math.min(one.width, one.height)) <= 1,
      }).toEqual({ face: face.folder, ground: one, fill: ds.color.surface.base, border: undefined, disc: ds.purpose.flag.orange, inside: true, size: true });
    });

    test(`${face.folder} keeps the waved yellow blinking in it, and blinks nothing else`, () => {
      // A blinking flag keeps blinking: waving is what a waved yellow means, and it does not stop
      // meaning it because the page came back.
      const flashing = [...walkItems(settledLayerOf(face, 'yellowWaving').children)].filter((i) => i.blink?.enabled);
      expect(flashing.map((i) => i.name)).toEqual(['flagCorner.yellowWaving.flash']);
      for (const flash of flashing) {
        expect(flash.blink).toEqual({ enabled: true, delayMs: FLAG_BLINK_MS });
        if (flash.kind !== 'rect') throw new Error('the flash is a band');
        expect(flash.backgroundColor).toBe(ds.color.surface.base);
      }
      for (const condition of ALERT_CATALOGUE.filter((c) => c.id !== 'yellowWaving')) {
        const layer = settledLayerOf(face, condition.id);
        expect({ face: face.folder, id: condition.id, blinking: [...walkItems([layer])].filter((i) => i.blink?.enabled).map((i) => i.name) }).toEqual({
          face: face.folder,
          id: condition.id,
          blinking: [],
        });
      }
    });

    test(`${face.folder} writes no name on the settled chequer either`, () => {
      expect(labelsOf(settledLayerOf(face, 'chequered').children)).toEqual([]);
    });
  }
});

/**
 * The clock that decides which of the two phases the band is in, which is SimHub's and not ours.
 *
 * `changed(ms, value)` is true for `ms` after `value` last moved, and ADR 0009 admits it for exactly
 * this: the window is state, but it is SimHub's, kept and aged by SimHub, so a package installed
 * without the plugin evaluates the same window. What it watches is the *rank of the winning
 * condition* rather than any one condition's bits, and that distinction is the whole reason
 * `raisedRank` exists: a full course yellow clearing to the local yellow underneath it never moves
 * the yellow's bit, and the yellow is nonetheless a new thing to tell a driver.
 */
describe('the window that decides which phase the band is in', () => {
  test('is the duration the canvas gives an alert, over the rank of the winning condition', () => {
    expect(FLAG_TAKEOVER_MS).toBe(ds.indicator.alert.durationMs);
    expect(FLAG_TAKEOVER_MS).toBe(3000);
    expect(flagTakingBand()).toBe(`changed(${FLAG_TAKEOVER_MS}, ${raisedRank(bandRaised)})`);
  });

  test('ranks the catalogue in the catalogue’s own order, and 0 when nothing is raised', () => {
    // Read with a stub, so that the nesting is legible: the first condition is 1, the twentieth is
    // 20, and nothing raised is 0. The whole catalogue and not the flags alone, since a car alert
    // takes the band exactly as a flag does and a change of winner to or from one is a change.
    const ranked = raisedRank((condition) => condition.id);
    let expected = '0';
    for (let i = ALERT_CATALOGUE.length - 1; i >= 0; i--) expected = `if(${ALERT_CATALOGUE[i]!.id}, ${i + 1}, ${expected})`;
    expect(ranked).toBe(expected);
    expect(ALERT_CATALOGUE).toHaveLength(20);
  });

  test('and reads the bits the way the band reads them, so the phase and the flag cannot disagree', () => {
    // The band is null-safe and honours the green flag's limiter; the box is neither. A window over
    // the box's reading would hold the band over for a green flag iRacing keeps set for a whole
    // stint, which is the one condition this ever mattered for.
    const green = FLAG_CATALOGUE.find((c) => c.id === 'green')!;
    expect(raisedRank(bandRaised)).toContain(bandRaised(green));
    expect(bandRaised(green)).not.toBe(conditionRaised(green));
  });
});
