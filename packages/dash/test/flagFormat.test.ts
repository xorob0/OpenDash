/**
 * The two flag formats, and what choosing between them costs.
 *
 * `OpenDash.<Face>FlagFormat` is `band` or `full`. The band format is the sixty pixels of band D
 * the face has always drawn; the full format is zones B, A and C together, which is a flag that
 * cannot be missed at the price of the gear. Both are built into both arrangements of every face
 * and SimHub shows one, exactly as it does with the rev bar, so the assertions here are structural:
 * that the block is the union of the three zones and nothing more, that the parts outside it are
 * untouched, that the two formats cannot both draw, and that the name fits the block it is centred
 * on at all eight sizes rather than being clipped by WPF on the narrow ones. The chequer is held on
 * every screen that draws the block, the companion and the pit wall included, since those are where
 * a board cut at its edge showed first; #473.
 *
 * The rectangles the FaceVariants sheets quote are pinned below, not because the code reads them --
 * it derives the block from each layout -- but because the derivation is only worth having if it
 * agrees with what was drawn.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages } from '../src/build.ts';
import { zone as zoneSetting } from '../src/contract.ts';
import { FLAG_FULL_NAMES, FLAG_FULL_NAME_PAD, FLAG_FULL_NAME_RATIO, flagFullNameSize } from '../src/components/flagFull.ts';
import { flagTakingBand } from '../src/components/flagStrip.ts';
import { ALERT_CATALOGUE, bandVisible, isFlag } from '../src/flags.ts';
import { ncalc } from '../src/generator.ts';
import { measureText } from '../src/design/advances.ts';
import { bottom, contains, overlaps, rect, right } from '../src/design/geometry.ts';
import type { Item, LayerItem, Rect, RectangleItem, TextItem } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { itemsOf, walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, layoutWithoutRevBar, sizeOf, type ZoneLayout } from '../src/zones/index.ts';
import { bodyRect } from '../src/zones/layout.ts';

/** The states the band draws, which are the catalogue's conditions in the catalogue's order. */
const STATES: readonly string[] = ALERT_CATALOGUE.map((c) => c.id);

/**
 * The two alerts that are the driver's own hand, whose white is two flags' colour without their
 * name: they draw nothing where no name is written, and the block never takes the body for them.
 */
const NEUTRAL: ReadonlySet<string> = new Set(['pushToPass', 'headlightFlash']);

/** The states the block draws: the catalogue less the two neutral alerts, in the same order. */
const BLOCK_STATES: readonly string[] = STATES.filter((id) => !NEUTRAL.has(id));

/**
 * What each condition reads as on the block, pinned here rather than derived, because the table in
 * the component is the answer to "what does a driver see" and a silent edit of it is a change to
 * the face. The chequer is absent: it is the board and carries no name.
 */
const BLOCK_NAME: Record<string, string> = {
  ignition: 'IGNITION',
  engine: 'ENGINE',
  red: 'RED',
  disqualify: 'DSQ',
  furled: 'FURLED',
  black: 'BLACK',
  meatball: 'MEATBALL',
  caution: 'SAFETY',
  yellowWaving: 'YELLOW',
  yellow: 'YELLOW',
  debris: 'DEBRIS',
  incident: 'INCIDENT',
  blue: 'BLUE',
  white: 'WHITE',
  green: 'GREEN',
  startSet: 'SET',
  startReady: 'READY',
};

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

/** The three groups a face draws a flag in: the band's two phases and the full-screen block. */
const FLAG_GROUPS = ['flag', 'flagCorner', 'flagFull'] as const;

/** The rectangles the FaceVariants sheets quote for the full-screen block, with the rev bar on. */
const SHEET_BLOCK: Record<string, Rect> = {
  OpenDash: rect(0, 105, 1920, 314),
  'OpenDash 1280x480': rect(0, 99, 1280, 320),
  'OpenDash 1280x400': rect(0, 87, 1280, 258),
  'OpenDash 1280x720': rect(0, 105, 1280, 554),
  'OpenDash 850x480': rect(0, 91, 850, 328),
  'OpenDash 800x480': rect(0, 91, 800, 328),
  'OpenDash 800x286': rect(0, 33, 800, 194),
  'OpenDash 600x686': rect(0, 83, 600, 546),
};

/** The name size each sheet quotes, which the ratio has to land within a pixel or two of. */
const SHEET_NAME_SIZE: Record<string, number> = {
  OpenDash: 142,
  'OpenDash 1280x480': 143,
  'OpenDash 1280x400': 115,
  'OpenDash 1280x720': 248,
  'OpenDash 850x480': 148,
  'OpenDash 800x480': 148,
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
      const flagged = new Set([...walkItems(FLAG_GROUPS.map((name) => groupOf(items, name)))].map((i) => i.name));
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
      const cornerGroup = groupOf(items, 'flagCorner');
      const fullGroup = groupOf(items, 'flagFull');

      // The groups read the same property and compare it with the two values it can take, so
      // whichever a driver has chosen, the band format's two phases draw or the block does.
      const band = zoneSetting.flagFormatIs(size, 'band');
      const taking = flagTakingBand();
      expect(visibleOf(bandGroup)).toBe(ncalc.and(band, taking));
      expect(visibleOf(cornerGroup)).toBe(ncalc.and(band, ncalc.not(taking)));
      expect(visibleOf(fullGroup)).toBe(zoneSetting.flagFormatIs(size, 'full'));
      expect(visibleOf(bandGroup)).not.toBe(visibleOf(fullGroup));
      // The band's two phases are one window and its negation, and the window is one expression
      // rather than two, which is what makes them exclusive and exhaustive: #380 splits where a flag
      // is drawn, never whether it is. SimHub keys `changed` by the text of the value expression, so
      // two windows written the same way would share state anyway; one is what says so.
      expect(taking).toContain('changed(');
      expect(visibleOf(bandGroup).split(taking)).toHaveLength(2);
      expect(visibleOf(cornerGroup).split(taking)).toHaveLength(2);
      expect(visibleOf(cornerGroup)).toContain(`!(${taking})`);
      expect(visibleOf(bandGroup)).not.toContain(`!(${taking})`);

      // And inside them the same conditions, ranked from the one catalogue rather than from two
      // lists that could disagree about which of two live flags wins.
      //
      // Both now rank them by the same expression. The block drew the six properties SimHub
      // normalises until the format read the catalogue, which meant that under a red flag or a
      // full-course caution the band named the condition and the block, having no state for it,
      // drew nothing at all: a driver who had chosen the format that cannot be missed saw the one
      // thing it was chosen for least. The same conditions on both sides, same order, same
      // `bandVisible` reading, so the two formats differ in the rectangle and in nothing else.
      //
      // The settled form is the third list and is ranked the same way again, for the same reason:
      // the phase a flag is in decides the rectangle, never which flag wins.
      //
      // Two conditions are ranked in all three and drawn in fewer: push to pass and the headlight
      // flash are white, which is the white flag's fill and the black family's outline, so they draw
      // only where their name is written. The block writes no such name and a face without corner
      // blocks has no room for one. They still rank, being in every other layer's negated terms.
      const statesIn = (group: LayerItem): string[] =>
        group.children.filter((c): c is LayerItem => c.kind === 'layer').map((c) => c.name.slice(`${group.name}.`.length));
      const expected = new Map([
        [bandGroup, STATES],
        [cornerGroup, arrangement.bandCorners ? STATES : BLOCK_STATES],
        [fullGroup, BLOCK_STATES],
      ]);
      for (const [group, states] of expected) expect({ group: group.name, states: statesIn(group) }).toEqual({ group: group.name, states: [...states] });
      for (const [group, states] of expected) {
        for (const id of states) {
          const condition = ALERT_CATALOGUE.find((c) => c.id === id)!;
          expect({ id, group: group.name, name: stateOf(group, id).name }).toMatchObject({ id, group: group.name, name: `${group.name}.${id}` });
          expect({ id, group: group.name, visible: visibleOf(stateOf(group, id)) }).toEqual({ id, group: group.name, visible: bandVisible(condition) });
        }
      }
      // Every flag is in every group: only a car alert that is the driver's own hand is ever left out.
      for (const [group, states] of expected) {
        const flags = ALERT_CATALOGUE.filter(isFlag).map((c) => c.id);
        expect({ group: group.name, missing: flags.filter((id) => !states.includes(id)) }).toEqual({ group: group.name, missing: [] });
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
      expect(names.map((n) => n.name)).toEqual(BLOCK_STATES.filter((id) => BLOCK_NAME[id] !== undefined).map((id) => `flagFull.${id}.name`));
      expect(names.map((n) => n.text)).toEqual(BLOCK_STATES.filter((id) => BLOCK_NAME[id] !== undefined).map((id) => BLOCK_NAME[id]!));
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
    const portrait = ZONE_FACES.find((f) => f.folder === 'OpenDash 600x686')!;
    const block = bodyRect(portrait);
    // 0.447 of a 546 px block is 244 px, and YELLOW at 244 px is half as wide again as the face.
    expect(measureText('BarlowCondensedBold', 'YELLOW', Math.floor(0.447 * block.height))).toBeGreaterThan(block.width);
    expect(flagFullNameSize(block)).toBeLessThan(Math.floor(0.447 * block.height));
  });

  test('and the catalogue costs the type on one face only, which is what the short names buy', () => {
    // The size is the widest name divided into the block, so the whole cost of drawing eighteen
    // conditions rather than six is the difference between the widest of each set. Shortened to the
    // sheets' one word, that is MEATBALL against YELLOW, and it is only binding where the block is
    // narrow against its height: every landscape face still lands on the fraction the sheets quote,
    // and the portrait one drops from 183 px to 143. The band's own labels would have made it 69,
    // which is why the block does not simply write them. The three car alerts it gained with #109,
    // IGNITION, ENGINE and INCIDENT, are all narrower than MEATBALL and so cost nothing.
    const widest = (names: readonly string[]): string => names.reduce((a, b) => (measureText('BarlowCondensedBold', b, 1) > measureText('BarlowCondensedBold', a, 1) ? b : a));
    expect(widest(FLAG_FULL_NAMES)).toBe('MEATBALL');
    expect(FLAG_FULL_NAMES).toEqual([...new Set(BLOCK_STATES.filter((id) => BLOCK_NAME[id] !== undefined).map((id) => BLOCK_NAME[id]!))]);
    const unchanged = ZONE_FACES.filter((f) => f.folder !== 'OpenDash 600x686');
    for (const face of unchanged) {
      const block = bodyRect(face);
      expect({ face: face.folder, size: flagFullNameSize(block) }).toEqual({ face: face.folder, size: Math.floor(FLAG_FULL_NAME_RATIO * block.height) });
    }
    expect(flagFullNameSize(bodyRect(ZONE_FACES.find((f) => f.folder === 'OpenDash 600x686')!))).toBe(143);
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
      for (const id of BLOCK_STATES) {
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

/**
 * Every full-screen chequer the build draws, once per block: the faces in both arrangements, both
 * companions and both pit walls. A companion repeats its block on twenty-one screens, so a board is
 * keyed by its package and its size rather than tested once per screen.
 */
const CHEQUERS: { where: string; ground: RectangleItem; squares: RectangleItem[] }[] = (() => {
  const found = new Map<string, { where: string; ground: RectangleItem; squares: RectangleItem[] }>();
  for (const { pkg } of composePackages({ version: '0.0.0-test', log: () => {} }, true)) {
    for (const item of pkg.dashboards.flatMap(itemsOf)) {
      if (item.kind !== 'layer' || !item.name.endsWith('flagFull.chequered')) continue;
      const [ground, ...squares] = item.children;
      if (ground?.kind !== 'rect' || !squares.every((s): s is RectangleItem => s.kind === 'rect')) throw new Error(`${item.name} is not a ground and its squares`);
      const where = `${pkg.folderName} ${ground.rect.width} x ${ground.rect.height}`;
      if (!found.has(where)) found.set(where, { where, ground, squares });
    }
  }
  return [...found.values()];
})();

describe('the full-screen chequer is a board of whole checks', () => {
  test('on every block the build draws one on', () => {
    // Eight faces in two arrangements each, two companions and two pit walls.
    expect(CHEQUERS.map((c) => c.where)).toHaveLength(20);
  });

  for (const { where, ground, squares } of CHEQUERS) {
    test(`${where} ends on a whole check at every edge, in the band's phase`, () => {
      const block = ground.rect;
      expect({ where, ground: ground.backgroundColor }).toEqual({ where, ground: ds.color.surface.base });

      // The lines between the checks, read off the squares. A light square in every other cell of
      // every row puts an edge on every line, the block's own four included.
      const lines = (edges: number[]): number[] => [...new Set(edges)].sort((a, b) => a - b);
      const xs = lines(squares.flatMap((s) => [s.rect.left, right(s.rect)]));
      const ys = lines(squares.flatMap((s) => [s.rect.top, bottom(s.rect)]));
      expect({ where, left: xs[0], right: xs.at(-1), top: ys[0], bottom: ys.at(-1) }).toEqual({ where, left: block.left, right: right(block), top: block.top, bottom: bottom(block) });

      // Odd both ways, so the board is the same at either end and at the top and bottom; and three
      // at least, because fewer is a quartered flag or two stripes rather than a chequer.
      const columns = xs.length - 1;
      const rows = ys.length - 1;
      expect({ where, columns, rows, odd: columns % 2 === 1 && rows % 2 === 1, enough: Math.min(columns, rows) >= 3 }).toMatchObject({ odd: true, enough: true });

      // Every cell is within a pixel of every other, which is what a square cut at the block's edge
      // is not, and near enough square to read as a check rather than as a bar.
      const spans = (at: number[]): number[] => at.slice(1).map((edge, i) => edge - at[i]!);
      const widths = spans(xs);
      const heights = spans(ys);
      expect({ where, widths: Math.max(...widths) - Math.min(...widths) <= 1, heights: Math.max(...heights) - Math.min(...heights) <= 1 }).toEqual({ where, widths: true, heights: true });

      // Each square is exactly one cell, the cell is light where row and column differ in parity, so
      // the board opens on the ground at the top left, and every such cell is drawn once.
      const cells = new Set<string>();
      for (const square of squares) {
        const column = xs.indexOf(square.rect.left);
        const row = ys.indexOf(square.rect.top);
        const aspect = square.rect.width / square.rect.height;
        expect({
          square: square.name,
          colour: square.backgroundColor,
          oneCell: xs[column + 1] === right(square.rect) && ys[row + 1] === bottom(square.rect),
          light: (row + column) % 2 === 1,
          nearSquare: aspect >= 0.85 && aspect <= 1.18,
        }).toEqual({ square: square.name, colour: ds.purpose.flag.chequer, oneCell: true, light: true, nearSquare: true });
        cells.add(`${row},${column}`);
      }
      expect({ where, drawn: cells.size, squares: squares.length }).toEqual({ where, drawn: Math.floor((columns * rows) / 2), squares: Math.floor((columns * rows) / 2) });
    });
  }
});
