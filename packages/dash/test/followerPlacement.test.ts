/**
 * Where a unit sits, on every surface the build emits.
 *
 * A follower -- a unit, a denominator, the caption after a delta -- belongs to the figure in front
 * of it. The figure is drawn in monospace cells cut for the longest reading it can ever hold, so
 * placing the follower at the end of those cells puts it however many digits the reading happens
 * not to have away from the number it names. The VM pass photographed both halves of that: band D's
 * `MARGIN −4` carried `MIN` four empty cells out, nearer `EST. LAPS` than its own figure, and
 * `FUEL 30.35` carried `L` one cell out. #384 fixed it for the tyre corners; this is the rest.
 *
 * Three things are asserted of every pair, and none of them is something the fit tests can see, a
 * follower inside its box being a follower that fits wherever it is.
 *
 * **The design-time gap**, which is what DashStudio's editor, the overview thumbnails and every
 * preview draw, is one of the four the drawings use, measured from the characters the sample really
 * has rather than from the cells it is laid in.
 *
 * **The runtime binding**, which is what the dash draws, reads the figure and nothing else. A
 * `Left` that reads a property neither the value's own `Text` nor its own `Left` reads is a
 * follower placed from something other than the reading it follows, and that is drift no screenshot
 * of the editor can show.
 *
 * **The two are the same place.** A binding is arithmetic over literals once its conditions are set
 * aside, so `drawnRange` bounds every place it can put the mark, and the design-time place has to be
 * one of them. The first two claims pass a follower whose formula was written against a different
 * origin from its own rectangle, which is what the companion header did: right rects, a formula
 * reading the right lap, and both denominators bound to the header's top-left corner on all 21 pages
 * of both companions, through a green `bun run check`.
 *
 * A follower with no `Left` at all is a claim that its value never changes length -- a clock, a lap
 * time, the placeholder of the same shape. That claim is `drawn: 'fixed'` where a field makes it,
 * and {@link UNBOUND} is what the build comes to; a new one appearing is a question to answer in
 * review rather than on the VM.
 */
import { describe, expect, test } from 'bun:test';
import { packImages } from '../src/build.ts';
import { buildPackage, fontsForPackage } from '../src/dashboard.ts';
import { LAYOUTS } from '../src/layouts/index.ts';
import { buildScreenPackage, SCREEN_PACKAGES } from '../src/screens/index.ts';
import { ZONE_FACES, buildZoneFace } from '../src/zones/index.ts';
import { ncalc, type DashPackage, type TextItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';
import { drawnRange, textWidth } from '../src/second/drawn.ts';
import { DENOMINATOR_GAP, UNIT_GAP } from '../src/second/field.ts';
import { measureText, type MeasuredFace } from '../src/design/advances.ts';
import { bindingExpression } from './monoGlyphs.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

const packed = (pkg: DashPackage): DashPackage => {
  packImages(pkg);
  return pkg;
};

/** Every package the build produces: the car faces, the zone faces, the companions and the pit walls. */
const SURFACES: { name: string; pkg: DashPackage }[] = [
  ...LAYOUTS.map((layout) => ({
    name: layout.folder,
    pkg: packed(buildPackage(layout, { version: OPTS.version, simHubVersion: OPTS.simHubVersion, strategy: 'widget' as const })),
  })),
  ...ZONE_FACES.map((face) => {
    const built = buildZoneFace(face, OPTS);
    return { name: face.folder, pkg: packed({ folderName: face.folder, dashboards: [built.main, ...built.zones], fonts: fontsForPackage() }) };
  }),
  ...SCREEN_PACKAGES.map((screen) => ({ name: screen.folder, pkg: packed(buildScreenPackage(screen, OPTS)) })),
];

/**
 * The texts of each screen on its own, and not of the package as a whole.
 *
 * A name is unique within a screen and not within a package: zone A's speed page is drawn once per
 * zone dashboard, so a package-wide index pairs one screen's unit with another screen's value and
 * reports a gap that neither of them has.
 */
const screensOf = (pkg: DashPackage): TextItem[][] =>
  pkg.dashboards.flatMap((d) => d.screens.map((screen) => [...walkItems(screen.items)].filter((i): i is TextItem => i.kind === 'text')));

/**
 * The four gaps the drawings put between a figure and what follows it.
 *
 * Five on a tyre corner and on band D, six for a unit on a second screen and for the bar's
 * denominator, eight for a denominator on a second screen and for a card's follower, ten for the
 * delta's reference caption and for zone A, whose artboards draw the pair wider. Four literals
 * rather than a range, so that a fifth has to be added here and named.
 */
const GAPS = [5, UNIT_GAP, DENOMINATOR_GAP, 10];

/**
 * What a design-time place may differ from its formula by, and why it is not nought.
 *
 * A rect is laid on whole pixels and a formula is not: a rank that centres its row halves the slack
 * it has left, so the 850's speedo places its unit at 98 where the same arithmetic in NCalc comes to
 * 97.5. One pixel is the rounding and is not drift; what this catches is a follower written against a
 * different origin from its rect, which misses by hundreds.
 */
const ROUNDING = 1;

/** Which measured face an item draws in: the family it names, at the weight it asks for. */
const faceOf = (item: TextItem): MeasuredFace => {
  if (item.font === 'Barlow') return item.fontWeight === 'Bold' ? 'BarlowBold' : 'BarlowMedium';
  if (item.fontWeight === 'Bold') return 'BarlowCondensedBold';
  if (item.fontWeight === 'Light') return 'BarlowCondensedLight';
  return 'BarlowCondensedSemiBold';
};

/** What an item's text takes across: its cells when it is monospaced, its advances when it is not. */
const inkWidth = (item: TextItem): number => (item.monospace ? textWidth(item.text, item.monospace) : measureText(faceOf(item), item.text, item.fontSize));

/**
 * Where an item's ink starts, which is not where its box does: the bar draws a field of its
 * right-hand end right aligned in a box cut for the widest entry of the catalogue.
 */
const inkStart = (item: TextItem): number => {
  const width = inkWidth(item);
  if (item.hAlign === 'right') return item.rect.left + item.rect.width - width;
  if (item.hAlign === 'center') return item.rect.left + (item.rect.width - width) / 2;
  return item.rect.left;
};

const inkEnd = (item: TextItem): number => inkStart(item) + inkWidth(item);

/**
 * The arithmetic a drawn width is made of, which says nothing about where a reading comes from.
 *
 * Everything else an expression names -- a property, a `driver…` lookup, the player's own row -- is
 * a reading, and a follower may only read what its figure reads.
 */
const WIDTH_FUNCTIONS = new Set(['if', 'round', 'abs']);

/** What an expression reads: the properties it names and the functions it calls. */
const readings = (expression: string): string[] => [
  ...ncalc.referencedProperties(expression),
  ...[...expression.matchAll(/\b([a-z][a-z0-9_]*)\s*\(/g)].map((m) => m[1] ?? '').filter((name) => !WIDTH_FUNCTIONS.has(name)),
];

/** A follower's value: the item named `…value` beside it, or the value its name is suffixed onto. */
const valueFor = (name: string, byName: Map<string, TextItem>): TextItem | undefined => {
  const base = name.replace(/(unit|denominator)$/, '');
  return byName.get(`${base}value`) ?? byName.get(base.replace(/\.$/, ''));
};

/** The followers that carry no `Left`, which is every one whose value is a fixed-length reading. */
const UNBOUND: readonly string[] = [];

/**
 * The four marks zone A draws at the end of a budget, by name.
 *
 * Its two pages that carry a unit are A1 `gearSpeedRevs` and A3 `speed`, each prefixing its items
 * with its own page id, and each drawing a speed and an rpm.
 */
const ZONE_A_FOLLOWERS = new Set(['gearSpeedRevs.speed.unit', 'gearSpeedRevs.revs.unit', 'speed.speed.unit', 'speed.revs.unit']);

describe('a follower sits beside the figure it belongs to', () => {
  for (const surface of SURFACES) {
    test(surface.name, () => {
      const screens = screensOf(surface.pkg);
      const follower = (item: TextItem): boolean => /\.(unit|denominator)$/.test(item.name);
      expect({ surface: surface.name, followers: screens.some((items) => items.some(follower)) }).toMatchObject({ followers: true });
      const unbound: string[] = [];
      for (const items of screens) {
        const byName = new Map(items.map((i) => [i.name, i]));
        for (const mark of items.filter(follower)) {
          const value = valueFor(mark.name, byName);
          // A unit with no value in front of it is a label of its own -- band D's `TYRES °C` heads
          // four readings rather than following one -- and has nothing to follow.
          if (value === undefined || value.monospace === undefined) continue;
          // A right-anchored pair is laid from the padding inwards rather than from the figure
          // outwards: the bar's right-hand end draws its denominator flush to the padding and its
          // value one gap in front of it, so what stands between the two runs of ink is the slack
          // in the denominator's own budget. That gap does not drift with the figure, which is what
          // this file is about; `docs/design/zones.md` §10 holds the question for the canvas.
          if (value.hAlign === 'right') continue;
          // Zone A is the other arrangement this rule does not reach, and it is the one place the
          // build still draws a mark at the end of a budget: its rows are centred as a group on the
          // cells, so pulling `KM/H` in against `81` and `RPM` in against `5,851` moves the ink off
          // the column's middle -- 31 px at 600 × 268, where the row is fitted to the column and has
          // two to give. Centring on the ink instead costs the portrait face either its rpm or a
          // step of its speed, which is a decision for the canvas rather than for this branch;
          // `docs/design/zones.md` §10 records it with the measurement.
          //
          // Named one by one rather than by their page prefix. Zone A's pages prefix every item with
          // the page id, so `speed.` reads as a prefix here and is also the whole name of the speed
          // *card*, whose follower is the precedent this rule is built on: a bare prefix let ten
          // `cards.djson` items out of a test that holds them up as the model.
          if (ZONE_A_FOLLOWERS.has(mark.name)) continue;
          const gap = Math.round(inkStart(mark) - inkEnd(value));
          expect({ surface: surface.name, item: mark.name, sample: value.text, gap, known: GAPS.includes(gap) }).toMatchObject({ known: true });

          const left = bindingExpression(mark, 'Left');
          if (left === '') {
            if (value.bindings?.Text !== undefined) unbound.push(mark.name);
            continue;
          }
          // The value's own `Left` counts as well as its `Text`: a rank that closes over a field the
          // sim does not publish moves every field after it, and the follower travels with its
          // figure through the same expression.
          const reads = readings(left);
          const figure = [...readings(bindingExpression(value, 'Text')), ...readings(bindingExpression(value, 'Left'))];
          expect({
            surface: surface.name,
            item: mark.name,
            follows: reads.length > 0,
            strays: [...new Set(reads.filter((p) => !figure.includes(p)))],
          }).toMatchObject({ follows: true, strays: [] });

          // And the two have to be the same place. A formula built against one origin and a rect
          // placed at another satisfies both claims above -- the rect is right, the formula reads the
          // right property -- and still lands somewhere else entirely on the dash, which is what the
          // companion header did: 84 items bound to 32-44 px with rects at 704 and 797, through a
          // green `bun run check`. `drawnRange` takes both branches of every `if` and reads nothing,
          // so the interval it returns is every place the mark can take; the design-time place is one
          // of them or the two were written against different origins.
          const range = drawnRange(left);
          expect({ surface: surface.name, item: mark.name, left: mark.rect.left, range, bounded: range !== undefined }).toMatchObject({ bounded: true });
          if (range) {
            const inRange = mark.rect.left >= range.min - ROUNDING && mark.rect.left <= range.max + ROUNDING;
            expect({ surface: surface.name, item: mark.name, left: mark.rect.left, range, inRange }).toMatchObject({ inRange: true });
          }
        }
      }
      expect({ surface: surface.name, unbound: [...new Set(unbound)].sort() }).toMatchObject({ unbound: UNBOUND.filter((n) => unbound.includes(n)) });
    });
  }
});
