/**
 * What every face really draws, rectangle by rectangle: the shape each zone body turns out to be,
 * and what each of the catalogue's pages keeps once it is in there.
 *
 * The canvas puts a count on each of its chips -- "does not fit: sheds a field", "re-cut for this
 * box" -- and nothing in the repository held the other side of that comparison. `shedding.test.ts`
 * proves that what a page declares is what it draws; this records what that comes to at the sizes
 * the build actually emits, so that a rectangle moving, or a page quietly shedding one more field,
 * shows as a diff at review rather than as a surprise on the VM.
 *
 * The arrangements without the rev bar are here for the same reason: the sheets do not draw them
 * and the build emits them, so zones B and C are 469 x 320 and 469 x 361 at 1280 x 480 rather than
 * one rectangle, and a page has to fit both.
 *
 * The snapshot is meant to be read. It is not an assertion about what is right, only a record of
 * what is drawn.
 */
import { describe, expect, test } from 'bun:test';
import { MODULE_CATALOGUE } from '../src/contract.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { PARTS, SHEDDING, archetypeFor, keepsAt, type Archetype } from '../src/modules/shedding.ts';
import { densityForBox, type Density } from '../src/second/density.ts';
import { zoneFrame } from '../src/second/header.ts';
import { ARCHETYPES, shapeOf } from '../src/second/shape.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, layoutWithoutRevBar, zonesOf } from '../src/zones/index.ts';

interface Body {
  face: string;
  zone: string;
  body: { width: number; height: number };
  density: Density;
}

/** Every rectangle the build hands a zone that takes a catalogue page, both arrangements, deduplicated. */
const bodies = ((): Body[] => {
  const seen = new Map<string, Body>();
  for (const layout of ZONE_FACES) {
    for (const arrangement of [layout, layoutWithoutRevBar(layout)]) {
      for (const zone of zonesOf(arrangement)) {
        if (zone.zone === 'A' || zone.zone === 'D') continue;
        const key = `${layout.width}x${layout.height} ${zone.size.width}x${zone.size.height}`;
        if (seen.has(key)) continue;
        const density = densityForBox(zone.size);
        // The body a zone really gives its page, which is the frame less the artboards' 22 px title
        // row inside 6 px and 12 px of padding, and not the zone rectangle itself.
        const { body } = zoneFrame(
          'zone',
          { frame: rect(0, 0, zone.size.width, zone.size.height), title: 'Lap times', counter: { kind: 'reserved', widest: '21 / 21' } },
          density,
          'face',
        );
        seen.set(key, { face: `${layout.width}x${layout.height}`, zone: `${zone.size.width}x${zone.size.height}`, body, density });
      }
    }
  }
  return [...seen.values()];
})();

/** Every item name a page draws in a box. */
const namesIn = (id: string, box: { width: number; height: number }, density: Density): string[] => {
  const module = MODULES.find((m) => m.id === id)!;
  const items = module.build({ frame: rect(0, 0, box.width, box.height), density, prefix: '', shape: shapeOf(box) });
  return items.flatMap((item) => [...walkItems([item])]).map((item) => item.name);
};

const drew = (names: readonly string[], id: string): boolean =>
  names.some((name) => name === id || name.startsWith(`${id}.`) || name.endsWith(`.row.${id}`) || name.includes(`.row.${id}.`));

/** Everything a page can shed, which is the union of what it keeps at the four drawings. */
const everyId = (page: string): string[] => {
  const entry = SHEDDING[page];
  if (!entry || entry.kind === 'nothing') return [];
  const all = new Set<string>();
  for (const archetype of ARCHETYPES) for (const id of entry.keeps[archetype]) all.add(id);
  return [...all];
};

const report = (): string => {
  const lines: string[] = [];
  for (const { face, zone, body, density } of bodies) {
    const archetypeOfPage = (page: string): Archetype => archetypeFor(page, shapeOf(body), body);
    lines.push(`${face}  zone ${zone}  body ${body.width}x${body.height}  ${density}`);
    for (const page of MODULE_CATALOGUE) {
      const archetype = archetypeOfPage(page.id);
      const names = namesIn(page.id, body, density);
      const all = everyId(page.id);
      const kept = all.filter((id) => drew(names, id));
      const shed = all.filter((id) => !drew(names, id));
      const declared = keepsAt(page.id, archetype) ?? [];
      const parts = PARTS[page.id]?.[archetype] ?? [];
      const head = `  ${page.id.padEnd(14)} ${archetype.padEnd(10)} `;
      const body_ =
        SHEDDING[page.id]?.kind === 'nothing'
          ? 'one drawing, nothing to shed'
          : `keeps ${kept.join(' ') || '-'}` +
            (shed.length ? `  sheds ${shed.join(' ')}` : '') +
            (declared.length !== kept.length ? `  declared ${declared.join(' ') || '-'}` : '');
      lines.push(head + body_ + (parts.length ? `  parts ${parts.join(' ')}` : ''));
    }
    lines.push('');
  }
  return lines.join('\n');
};

describe('what each face draws, per zone rectangle', () => {
  test('the shape of every zone body, and what each page keeps in it', () => {
    expect(report()).toMatchSnapshot();
  });

  test('every rectangle the build emits is covered, both arrangements of every face', () => {
    // The guard on the report rather than on the code: a face added without its rev-bar-off
    // arrangement would otherwise drop out of the snapshot without a diff anybody reads.
    expect(bodies.length).toBeGreaterThanOrEqual(ZONE_FACES.length);
    for (const layout of ZONE_FACES) {
      expect(bodies.filter((b) => b.face === `${layout.width}x${layout.height}`).length).toBeGreaterThan(0);
    }
  });
});
