/**
/**
 * The flag band: the phase of its chequer, the weight its name is set in, the border every coloured
 * state carries, and the flash that must not uncover the page beneath it.
 *
 * A flag takes band D over when it comes out, because an alert outranks fuel, so whatever the band
 * does in that moment it has to leave nothing of the page showing. It takes it for a few seconds and
 * then settles into the block at each end, #380, and the second half of this file is that: the same
 * fifteen conditions in the same order, in two smaller rectangles, with the page a driver was reading
 * back between them. Every artboard fills the chequered
 * state with `repeating-conic-gradient(#F5F7FA 0 25%, #0A0B0D 0 50%)` over a tile the height of the
 * band, which sweeps clockwise from twelve o'clock, so the band opens dark at its left edge and the
 * first light square begins one square in; the round faces draw the same board as marks around the
 * rim, and whatever the band does at its left edge the ring does at twelve o'clock. The coloured
 * states carry the artboards' 3 px border in their own fill, their names are set in the artboards'
 * 700 rather than the label weight, and the yellow flashes an opaque band over its fill rather than
 * blinking the whole layer away.
 */
import { describe, expect, test } from 'bun:test';
import { ALERT_DISC_RATIO } from '../src/components/alertBand.ts';
import { BLACK_FLAG_BORDER, BLUE_FLAG_ID, FLAG_BLINK_MS, FLAG_NAME_WEIGHT, FLAG_TAKEOVER_MS, flagTakingBand } from '../src/components/flagStrip.ts';
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
import { bandCornerWidths, bandFlagBlocks, bandMetrics, bandPageRoom } from '../src/zones/bandPages.ts';
import { ZONE_FACES, faceItems, type ZoneLayout } from '../src/zones/index.ts';

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
 * What the flag does once its few seconds are up: it settles into the block at each end of the band
 * and gives the page back, #380.
 *
 * The ticket's own case is a full course yellow on the 850 x 480 face with 2.1 litres in the tank: the
 * band read SAFETY CAR then, and nothing on the face said fuel, for as long as the caution lasted,
 * which is several minutes, and a caution is exactly when a driver decides whether to pit. The flag
 * has said everything it has to say after two seconds.
 *
 * The blocks are the band's own, not the flag's: `bandFlagBlocks` hands back the corner blocks on the
 * four faces that draw them and the side padding on the four that do not, so a settled flag can
 * never be laid into room a page is using. That is what the first test below measures, against the
 * room the page is actually given.
 *
 * What it does take, besides a corner block's two fields, is the band's side padding and the room the
 * zone letter stood in before #708, both being inside the width `bandCornerWidths` reserves. No face
 * draws the letter any more, so there is nothing for a settled flag to cover or to leave showing, and
 * that is measured below so that a letter cannot come back under the flag without being noticed.
 */
const cornerLayers = (face: ZoneLayout): Map<string, LayerItem> =>
  new Map(
    faceItems(face)
      .filter((i): i is LayerItem => i.kind === 'layer' && i.name === 'flagCorner')
      .flatMap((g) => g.children.filter((c): c is LayerItem => c.kind === 'layer'))
      .map((l) => [l.name.slice('flagCorner.'.length), l]),
  );

const cornerLayerOf = (face: ZoneLayout, id: string): LayerItem => {
  const layer = cornerLayers(face).get(id);
  if (!layer) throw new Error(`no settled ${id} flag on ${face.folder}`);
  return layer;
};

describe('the flag settles into the blocks at the ends of the band', () => {
  for (const face of ZONE_FACES) {
    const band = face.zones.band;
    const blocks = bandFlagBlocks(band, face.bandCorners);

    test(`${face.folder} keeps its two blocks at the band's ends and clear of the page`, () => {
      // Full height and hard against each end: a settled flag is the end of the band, not a chip
      // floating in it.
      expect(blocks.left.top).toBe(band.top);
      expect(blocks.right.top).toBe(band.top);
      expect({ left: blocks.left.height, right: blocks.right.height }).toEqual({ left: band.height, right: band.height });
      expect(blocks.left.left).toBe(band.left);
      expect(blocks.right.left + blocks.right.width).toBe(band.left + band.width);

      // And clear of the room the page is laid into, which is the one thing a band must never take.
      const room = bandPageRoom(band, face.bandCorners);
      expect(blocks.left.left + blocks.left.width).toBeLessThanOrEqual(room.left);
      expect(blocks.right.left).toBeGreaterThanOrEqual(room.left + room.width);
    });

    test(`${face.folder} takes ${face.bandCorners ? 'the corner blocks' : 'the side padding, there being no corner block'}`, () => {
      // The two answers, and which one a face gets is the layout's `bandCorners` rather than a
      // threshold this file re-derives. Where there are corner blocks the flag takes them whole,
      // incidents and track state at one end, lamps and clocks at the other, and the page is what
      // comes back. Where there are none there is nothing to take, so the flag keeps the padding:
      // sixteen pixels of colour at each end, which is all the room no page is ever laid into.
      const padX = bandMetrics(band).padX;
      const widths = face.bandCorners ? bandCornerWidths(band) : { left: padX, right: padX };
      expect({ left: blocks.left.width, right: blocks.right.width }).toEqual(widths);
    });

    test(`${face.folder} has no band D letter for a settled flag to cover or to leave showing`, () => {
      // Before #708 the face drew a `D` at `padX` and pushed the flag groups after it, so a corner
      // taken whole covered the letter and the side padding stopped short of it. The letter is gone
      // from every face, and the left block still starts at the band's own edge either way.
      const letter = [...walkItems(faceItems(face))].find((i): i is TextItem => i.kind === 'text' && i.name === 'zoneD.letter');
      expect({ face: face.folder, letter }).toEqual({ face: face.folder, letter: undefined });
      expect({ face: face.folder, left: blocks.left.left }).toEqual({ face: face.folder, left: band.left });
    });

    test(`${face.folder} draws every condition in both blocks, in the shape and colour it draws on the whole band`, () => {
      // Every condition but a neutral alert on a face with no corner block: push to pass and the
      // headlight flash are white, which without their name would be the white flag or the black
      // one, and sixteen pixels hold no name. They have no settled layer there at all.
      const neutral = (id: string): boolean => id === 'pushToPass' || id === 'headlightFlash';
      expect([...cornerLayers(face).keys()]).toEqual(ALERT_CATALOGUE.map((c) => c.id).filter((id) => face.bandCorners || !neutral(id)));
      for (const condition of ALERT_CATALOGUE.filter((c) => face.bandCorners || !neutral(c.id))) {
        const layer = cornerLayerOf(face, condition.id);
        const children = [...walkItems(layer.children)].filter((i) => i.kind !== 'layer');
        // Both ends, always: a flag at one end of the band would read as a fault rather than a flag.
        for (const end of ['left', 'right'] as const) {
          const drawn = children.filter((i) => i.name.startsWith(`flagCorner.${condition.id}.${end}.`));
          expect({ face: face.folder, id: condition.id, end, drawn: drawn.length > 0 }).toMatchObject({ drawn: true });
          for (const item of drawn) {
            // A name's box is a WPF line box and is taller than the block, so it is held to the
            // block's width; everything else is inside the block outright.
            if (item.kind === 'text') {
              expect({ item: item.name, within: item.rect.left === blocks[end].left && item.rect.width === blocks[end].width }).toMatchObject({ within: true });
              continue;
            }
            expect({ item: item.name, rect: item.rect, inside: contains(blocks[end], item.rect) }).toMatchObject({ inside: true });
          }
        }
        // The ground carries the condition's own colour where the shape is the filled one, exactly as
        // the whole band does; the outlined family and the chequer lay `surface.base` instead.
        if (condition.band.shape === 'filled') {
          for (const end of ['left', 'right'] as const) {
            const ground = layer.children.find((c): c is RectangleItem => c.kind === 'rect' && c.name === `flagCorner.${condition.id}.${end}.band`)!;
            expect({ id: condition.id, end, rect: ground.rect, colour: ground.backgroundColor }).toEqual({
              id: condition.id,
              end,
              rect: blocks[end],
              colour: condition.band.colour,
            });
          }
        }
      }
    });

    test(`${face.folder} writes a name in a block only where the name fits it`, () => {
      // A name too wide for the block is not shrunk and not clipped: it is not written, and the
      // block is colour alone, which is what the nano's twelve-pixel strip already is. The faces
      // with no corner blocks are that case, sixteen pixels holding no word at all; the four with
      // corner blocks hold every one of the eighteen labels, the chequer and the meatball having none.
      // Where a condition has two names, which is the full course yellow's FULL COURSE YELLOW and FCY,
      // it writes the longest that fits the narrower block, and writes it at both ends: one settled flag
      // carrying two names, or a name at one end and none at the other, would read as two conditions
      // rather than one. The blue flag's detail is held to the same rule, #497: where the widest of its
      // three runs fits the narrower block, both ends write the three, and where it does not, the name.
      const wholeBlue = labelsOf(layerOf(face, BLUE_FLAG_ID).children);
      const detail = wholeBlue.map((run) => run.text);
      for (const condition of ALERT_CATALOGUE) {
        const settled = cornerLayers(face).get(condition.id);
        if (!settled) continue;
        const names = labelsOf(settled.children);
        for (const name of names) {
          expect({ item: name.name, font: name.font, weight: name.fontWeight }).toEqual({ item: name.name, font: ds.font.label, weight: FLAG_NAME_WEIGHT });
          const drawn = measureText('BarlowBold', name.widest ?? name.text, name.fontSize);
          expect({ item: name.name, drawn, box: name.rect.width, fits: drawn < name.rect.width }).toMatchObject({ fits: true });
        }
        const room = Math.min(blocks.left.width, blocks.right.width) - 2 * BLACK_FLAG_BORDER;
        const text = bandNames(condition.band).find((name) => measureText('BarlowBold', name, ds.size.label) < room);
        const detailFits = condition.id === BLUE_FLAG_ID && wholeBlue.every((run) => measureText('BarlowBold', run.widest ?? run.text, ds.size.label) < room);
        const expected = detailFits ? [...detail, ...detail] : text === undefined ? [] : [text, text];
        expect({ face: face.folder, id: condition.id, names: names.map((n) => n.text) }).toEqual({ face: face.folder, id: condition.id, names: expected });
      }
      // And where the blue block writes the car behind, it writes it exactly as the whole band does: each
      // end carries the band's three runs, each shown by the same BlueFlagDetail, bound to the same
      // expression and measured from the same widest. That is every face with corner blocks, and none
      // of the four whose sixteen pixels hold no word.
      const asWritten = (runs: readonly TextItem[]) => runs.map(({ text, widest, bindings }) => ({ text, widest, bindings }));
      const settledBlue = labelsOf(cornerLayerOf(face, BLUE_FLAG_ID).children);
      expect({ face: face.folder, detail: settledBlue.some((l) => l.bindings?.Text !== undefined) }).toEqual({ face: face.folder, detail: face.bandCorners });
      if (face.bandCorners) {
        for (const end of ['left', 'right'] as const) {
          const runs = settledBlue.filter((l) => l.name.startsWith(`flagCorner.${BLUE_FLAG_ID}.${end}.`));
          expect({ face: face.folder, end, runs: asWritten(runs) }).toEqual({ face: face.folder, end, runs: asWritten(wholeBlue) });
        }
      }
      // The incident is the other way: the block writes INCIDENT, unbound, and its count against the
      // limit is the whole band's for the seconds it has it.
      for (const label of labelsOf(cornerLayerOf(face, 'incident').children)) {
        expect({ item: label.name, text: label.text, bound: label.bindings?.Text !== undefined }).toEqual({ item: label.name, text: 'INCIDENT', bound: false });
      }
    });

    test(`${face.folder} draws the debris flag's red stripes in both blocks, so a settled debris flag is never a yellow`, () => {
      // Where a block holds no name, colour is all a driver has, and the debris flag's yellow alone
      // was the yellow flag's. Its stripes are never fewer than three, so even sixteen pixels carry
      // one red between two yellow, clear of both ends of the block (#498).
      const layer = cornerLayerOf(face, 'debris');
      for (const end of ['left', 'right'] as const) {
        const parts = layer.children.filter((c): c is RectangleItem => c.kind === 'rect' && c.name.startsWith(`flagCorner.debris.${end}.`));
        const stripes = parts.filter((p) => p.backgroundColor === ds.purpose.flag.debrisStripe);
        expect({ face: face.folder, end, ground: parts[0]?.backgroundColor, stripes: stripes.length >= 1 }).toEqual({ face: face.folder, end, ground: ds.purpose.flag.debris, stripes: true });
        for (const stripe of stripes) {
          expect({
            stripe: stripe.name,
            fullHeight: stripe.rect.top === blocks[end].top && stripe.rect.height === blocks[end].height,
            clearOfTheEnds: stripe.rect.left > blocks[end].left && stripe.rect.left + stripe.rect.width < blocks[end].left + blocks[end].width,
          }).toEqual({ stripe: stripe.name, fullHeight: true, clearOfTheEnds: true });
        }
      }
    });

    test(`${face.folder} draws the meatball in both blocks as its disc on the near-black, with no name`, () => {
      // A black box with an orange disc in the middle and no text, which is the author's ruling on
      // #498, in a settled block as on the whole band. The disc is two thirds of the block's shorter
      // side, which on the faces with no corner block is the sixteen or twelve pixels of its width.
      const layer = cornerLayerOf(face, 'meatball');
      for (const end of ['left', 'right'] as const) {
        const parts = layer.children.filter((c) => c.name.startsWith(`flagCorner.meatball.${end}.`));
        const [ground, disc] = parts;
        if (parts.length !== 2 || ground?.kind !== 'rect' || disc?.kind !== 'ellipse') throw new Error(`${face.folder} draws the ${end} meatball as a ground and a disc`);
        const block = blocks[end];
        expect({
          face: face.folder,
          end,
          ground: ground.rect,
          fill: ground.backgroundColor,
          border: ground.border,
          disc: disc.fillColor,
          inside: contains(block, disc.rect),
          size: Math.abs(disc.rect.width - ALERT_DISC_RATIO * Math.min(block.width, block.height)) <= 1,
        }).toEqual({ face: face.folder, end, ground: block, fill: ds.color.surface.base, border: undefined, disc: ds.purpose.flag.orange, inside: true, size: true });
      }
    });

    test(`${face.folder} keeps the waved yellow blinking in both blocks, and blinks nothing else`, () => {
      // A blinking flag keeps blinking in the block: waving is what a waved yellow means, and it
      // does not stop meaning it because the page came back.
      const flashing = [...walkItems(cornerLayerOf(face, 'yellowWaving').children)].filter((i) => i.blink?.enabled);
      expect(flashing.map((i) => i.name)).toEqual(['flagCorner.yellowWaving.left.flash', 'flagCorner.yellowWaving.right.flash']);
      for (const flash of flashing) {
        expect(flash.blink).toEqual({ enabled: true, delayMs: FLAG_BLINK_MS });
        if (flash.kind !== 'rect') throw new Error('the flash is a band');
        expect(flash.backgroundColor).toBe(ds.color.surface.base);
      }
      for (const condition of ALERT_CATALOGUE.filter((c) => c.id !== 'yellowWaving' && cornerLayers(face).has(c.id))) {
        const layer = cornerLayerOf(face, condition.id);
        expect({ face: face.folder, id: condition.id, blinking: [...walkItems([layer])].filter((i) => i.blink?.enabled).map((i) => i.name) }).toEqual({
          face: face.folder,
          id: condition.id,
          blinking: [],
        });
      }
    });

    test(`${face.folder} writes no name on the settled chequer either`, () => {
      expect(labelsOf(cornerLayerOf(face, 'chequered').children)).toEqual([]);
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
