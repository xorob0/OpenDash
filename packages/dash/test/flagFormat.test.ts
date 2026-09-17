/**
 * The two flag formats, and what choosing between them costs.
 *
 * `OpenDash.<Face>FlagFormat` is `band` or `full`. The band format is the sixty pixels of band D
 * the face has always drawn; the full format is zones B, A and C together, which is a flag that
 * cannot be missed at the price of the gear. Both are built into both arrangements of every face
 * and SimHub shows one, exactly as it does with the rev bar, so the assertions here are structural:
 * that the block is the union of the three zones and nothing more, that the parts outside it are
 * untouched, that the two formats cannot both draw, and that the name fits the block it is centred
 * on at all eight sizes rather than being clipped by WPF on the narrow ones.
 *
 * The rectangles the FaceVariants sheets quote are pinned below, not because the code reads them --
 * it derives the block from each layout -- but because the derivation is only worth having if it
 * agrees with what was drawn.
 */
import { describe, expect, test } from 'bun:test';
import { zone as zoneSetting } from '../src/contract.ts';
import { FLAG_FULL_NAMES, FLAG_FULL_NAME_PAD, flagFullNameSize } from '../src/components/flagFull.ts';
import { flagVisible } from '../src/components/flagStrip.ts';
import { bandRaised, conditionVisible, FLAG_CATALOGUE } from '../src/flags.ts';
import { measureText } from '../src/design/advances.ts';
import { bottom, contains, overlaps, rect, right } from '../src/design/geometry.ts';
import type { Item, LayerItem, Rect, TextItem } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, layoutWithoutRevBar, sizeOf, type ZoneLayout } from '../src/zones/index.ts';
import { bodyRect } from '../src/zones/layout.ts';

/** The six states the block draws, in the order the component lists them. */
const STATES = ['black', 'chequered', 'yellow', 'blue', 'white', 'green'] as const;

/** Every arrangement of every face: the one the sheets draw, and the one with the rev bar's room given back. */
const ARRANGEMENTS: { face: ZoneLayout; arrangement: ZoneLayout; revBar: boolean }[] = ZONE_FACES.flatMap((face) => [
  { face, arrangement: face, revBar: true },
  { face, arrangement: layoutWithoutRevBar(face), revBar: false },
]);

const groupOf = (items: readonly Item[], name: string): LayerItem => {
  const group = items.find((i): i is LayerItem => i.kind === 'layer' && i.name === name);
  if (!group) throw new Error(`no ${name} group`);
  return group;
};

const stateOf = (group: LayerItem, id: string): LayerItem => {
  const layer = group.children.find((c): c is LayerItem => c.kind === 'layer' && c.name === `${group.name}.${id}`);
  if (!layer) throw new Error(`no ${id} state under ${group.name}`);
  return layer;
};

const visibleOf = (item: Item): string => String(item.bindings?.Visible?.formula ?? '');

/** The parts a format must leave drawing, named as the face names them. The nano has no bar. */
const keptParts = (layout: ZoneLayout, revBar: boolean): string[] => [...(revBar ? ['well'] : []), ...(layout.zones.bar ? ['bar.ground'] : []), 'band.ground'];

/** The rectangles the FaceVariants sheets quote for the full-screen block, with the rev bar on. */
const SHEET_BLOCK: Record<string, Rect> = {
  openDash: rect(0, 105, 1920, 314),
  'openDash 1280x480': rect(0, 99, 1280, 320),
  'openDash 1280x400': rect(0, 87, 1280, 258),
  'openDash 1280x720': rect(0, 105, 1280, 554),
  'openDash 850x480': rect(0, 91, 850, 328),
  'openDash 800x480': rect(0, 91, 800, 328),
  'openDash 800x286': rect(0, 33, 800, 194),
  'openDash 600x686': rect(0, 83, 600, 546),
};

/** The name size each sheet quotes, which the ratio has to land within a pixel or two of. */
const SHEET_NAME_SIZE: Record<string, number> = {
  openDash: 142,
  'openDash 1280x480': 143,
  'openDash 1280x400': 115,
  'openDash 1280x720': 248,
  'openDash 850x480': 148,
  'openDash 800x480': 148,
};

describe('the full-screen block is the union of zones B, A and C', () => {
  for (const { face, arrangement, revBar } of ARRANGEMENTS) {
    test(`${face.folder}${revBar ? '' : ', rev bar off'} draws it over all three zones and nothing else`, () => {
      const z = arrangement.zones;
      const block = bodyRect(arrangement);
      for (const zone of [z.zoneA, z.zoneB, z.zoneC]) {
        expect({ face: face.folder, zone, covered: contains(block, zone) }).toMatchObject({ covered: true });
      }
      // And it is no bigger than they are: the block's edges are theirs, not the face's.
      expect({ face: face.folder, top: block.top }).toMatchObject({ top: Math.min(z.zoneA.top, z.zoneB.top, z.zoneC.top) });
      expect({ face: face.folder, bottom: bottom(block) }).toMatchObject({ bottom: Math.max(bottom(z.zoneA), bottom(z.zoneB), bottom(z.zoneC)) });
      expect({ face: face.folder, width: block.width, left: block.left }).toMatchObject({ width: arrangement.width, left: 0 });

      // Every rectangle the format draws lies inside it, which is what makes "full screen" a
      // statement about the body rather than about the screen. A name's box is a WPF line box and
      // is a tenth of its size taller than the run inside it, so it is held to the block's width and
      // to the face, not to the block's height.
      const full = groupOf(faceItems(arrangement, { revBar }), 'flagFull');
      for (const item of walkItems(full.children)) {
        if (item.kind === 'layer') continue;
        if (item.kind === 'text') {
          const onFace = item.rect.top >= 0 && bottom(item.rect) <= arrangement.height;
          expect({ item: item.name, rect: item.rect, inside: item.rect.left >= block.left && right(item.rect) <= right(block) && onFace }).toMatchObject({ inside: true });
          continue;
        }
        expect({ item: item.name, rect: item.rect, inside: contains(block, item.rect) }).toMatchObject({ inside: true });
      }
    });
  }

  test('and at the sizes the sheets draw, it is the rectangle they drew', () => {
    for (const face of ZONE_FACES) {
      expect({ face: face.folder, block: bodyRect(face) }).toEqual({ face: face.folder, block: SHEET_BLOCK[face.folder]! });
    }
  });
});

describe('the format leaves the rev bar, the bar and band D alone', () => {
  for (const { face, arrangement, revBar } of ARRANGEMENTS) {
    test(`${face.folder}${revBar ? '' : ', rev bar off'} keeps the block clear of them`, () => {
      const z = arrangement.zones;
      const block = bodyRect(arrangement);
      const kept: [string, Rect | undefined][] = [
        ['rev bar well', revBar ? z.revBarWell : undefined],
        ['bar', z.bar],
        ['band D', z.band],
      ];
      for (const [name, r] of kept) {
        if (!r) continue;
        expect({ face: face.folder, part: name, overlapped: overlaps(block, r) }).toMatchObject({ overlapped: false });
      }
    });

    test(`${face.folder}${revBar ? '' : ', rev bar off'} draws those parts once, outside either flag group`, () => {
      const items = faceItems(arrangement, { revBar });
      const flagged = new Set([...walkItems([groupOf(items, 'flag'), groupOf(items, 'flagFull')])].map((i) => i.name));
      for (const name of keptParts(arrangement, revBar)) {
        expect({ face: face.folder, part: name, drawn: items.some((i) => i.name === name), inFlag: flagged.has(name) }).toMatchObject({ drawn: true, inFlag: false });
      }
    });
  }
});

describe('the two formats cannot both draw', () => {
  for (const { face, arrangement, revBar } of ARRANGEMENTS) {
    test(`${face.folder}${revBar ? '' : ', rev bar off'} asks the format once per group and the flag once per state`, () => {
      const size = sizeOf(arrangement);
      const items = faceItems(arrangement, { revBar });
      const bandGroup = groupOf(items, 'flag');
      const fullGroup = groupOf(items, 'flagFull');

      // The two groups read the same property and compare it with the two values it can take, so
      // whichever a driver has chosen, exactly one of the two groups draws.
      expect(visibleOf(bandGroup)).toBe(zoneSetting.flagFormatIs(size, 'band'));
      expect(visibleOf(fullGroup)).toBe(zoneSetting.flagFormatIs(size, 'full'));
      expect(visibleOf(bandGroup)).not.toBe(visibleOf(fullGroup));

      // And inside them the same conditions, ranked from the one catalogue rather than from two
      // lists that could disagree about which of two live flags wins.
      //
      // The two no longer rank them by the same expression. The band draws all fifteen conditions
      // of FLAG_CATALOGUE off the `SessionFlagsDetails` bits, while the block still draws the six
      // SimHub normalises, off the `Flag_*` properties; both take their order from that catalogue,
      // through FACE_FLAG_PRIORITY, so within the six they cannot disagree. What the block cannot
      // say it does not say: under a red flag or a full-course caution the band names it and the
      // block draws nothing. Moving the block onto the catalogue as well is the author's, since it
      // would measure every name in the block down to fit "DISQUALIFIED".
      const bandStates = bandGroup.children.filter((c): c is LayerItem => c.kind === 'layer').map((c) => c.name.slice('flag.'.length));
      const fullStates = fullGroup.children.filter((c): c is LayerItem => c.kind === 'layer').map((c) => c.name.slice('flagFull.'.length));
      expect(fullStates).toEqual([...STATES]);
      expect(bandStates).toEqual(FLAG_CATALOGUE.map((c) => c.id));
      // Every state of the block is a condition of the catalogue, ranked among the band's in the
      // same relative order.
      expect(bandStates.filter((id) => fullStates.includes(id))).toEqual(FLAG_CATALOGUE.filter((c) => c.faceFlag !== undefined).map((c) => c.id));
      for (const id of STATES) {
        expect({ id, band: stateOf(bandGroup, id).name, full: stateOf(fullGroup, id).name }).toMatchObject({ id, band: `flag.${id}`, full: `flagFull.${id}` });
        const condition = FLAG_CATALOGUE.find((c) => c.id === id);
        expect({ id, full: visibleOf(stateOf(fullGroup, id)) }).toEqual({ id, full: flagVisible(condition!.faceFlag!) });
        expect({ id, band: visibleOf(stateOf(bandGroup, id)) }).toEqual({ id, band: conditionVisible(condition!, false, FLAG_CATALOGUE, bandRaised) });
      }
    });
  }
});

describe('the full-screen name fits the block it is centred on', () => {
  for (const { face, arrangement, revBar } of ARRANGEMENTS) {
    test(`${face.folder}${revBar ? '' : ', rev bar off'} measures every name down to the block`, () => {
      const block = bodyRect(arrangement);
      const size = flagFullNameSize(block);
      const room = block.width - 2 * FLAG_FULL_NAME_PAD;
      for (const name of FLAG_FULL_NAMES) {
        const width = measureText('BarlowCondensedBold', name, size);
        expect({ face: face.folder, name, size, width, room, fits: width <= room }).toMatchObject({ fits: true });
      }

      // As drawn: one name per state that carries one, in the block's own colours, and no wider than
      // the box SimHub hands WPF as MaxTextWidth.
      const full = groupOf(faceItems(arrangement, { revBar }), 'flagFull');
      const names = [...walkItems(full.children)].filter((i): i is TextItem => i.kind === 'text');
      expect(names.map((n) => n.name)).toEqual(['flagFull.black.name', 'flagFull.yellow.name', 'flagFull.blue.name', 'flagFull.white.name', 'flagFull.green.name']);
      for (const item of names) {
        expect({ item: item.name, font: item.font, weight: item.fontWeight, size: item.fontSize }).toMatchObject({ font: ds.font.data, weight: 'Bold', size });
        const drawn = measureText('BarlowCondensedBold', item.text, item.fontSize);
        expect({ item: item.name, drawn, box: item.rect.width, fits: drawn <= item.rect.width }).toMatchObject({ fits: true });
        // Centred on the block: the box spans it, so the name sits on its middle rather than on its own.
        expect({ item: item.name, left: item.rect.left, right: right(item.rect) }).toMatchObject({ left: block.left, right: right(block) });
        expect(item.hAlign).toBe('center');
      }
      // The chequer carries none, as it carries none on the band: #0A0B0D ink on a board that is
      // half #0A0B0D would be readable over half of itself.
      expect([...walkItems(stateOf(full, 'chequered').children)].filter((i) => i.kind === 'text')).toEqual([]);
    });
  }

  test('and lands on the size the sheets drew, where the block is wide enough for it', () => {
    for (const face of ZONE_FACES) {
      const quoted = SHEET_NAME_SIZE[face.folder];
      if (quoted === undefined) continue;
      const drawn = flagFullNameSize(bodyRect(face));
      expect({ face: face.folder, drawn, quoted, within: Math.abs(drawn - quoted) <= 2 }).toMatchObject({ within: true });
    }
  });

  test('and is cut to the face where it is not: the portrait block is the case that proves it', () => {
    const portrait = ZONE_FACES.find((f) => f.folder === 'openDash 600x686')!;
    const block = bodyRect(portrait);
    // 0.447 of a 546 px block is 244 px, and YELLOW at 244 px is half as wide again as the face.
    expect(measureText('BarlowCondensedBold', 'YELLOW', Math.floor(0.447 * block.height))).toBeGreaterThan(block.width);
    expect(flagFullNameSize(block)).toBeLessThan(Math.floor(0.447 * block.height));
  });
});

describe('the full-screen format costs the gear', () => {
  for (const { face, arrangement, revBar } of ARRANGEMENTS) {
    test(`${face.folder}${revBar ? '' : ', rev bar off'} covers zone A opaquely while a flag is out`, () => {
      const items = faceItems(arrangement, { revBar });
      const full = groupOf(items, 'flagFull');
      const zoneA = arrangement.zones.zoneA;
      // Every state lays an opaque rectangle over the whole of zone A before it draws anything else,
      // which is what makes the trade the canvas names a fact rather than an intention.
      for (const id of STATES) {
        const ground = stateOf(full, id).children[0]!;
        if (ground.kind !== 'rect') throw new Error(`${id} does not open with a rectangle`);
        expect({ id, covers: contains(ground.rect, zoneA) }).toMatchObject({ id, covers: true });
      }
      // And the group is drawn after the zone widgets, so the gear is under it rather than over it.
      const widget = items.findIndex((i) => i.name === 'zoneA');
      expect({ widget, group: items.indexOf(full), after: items.indexOf(full) > widget }).toMatchObject({ after: true });
    });
  }
});
