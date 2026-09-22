/**
 * The file names the shapes are written under, which is the whole of how the plugin tells one
 * lighting profile from another.
 *
 * SimHub gives its RGB strips and its 8x8 matrix the same `.ledsprofile` extension, so a build drops
 * twenty files of two different kinds into one folder and the plugin embeds them side by side. Its
 * only discriminator is the file name: `FlagBoxProfile.SelectResource` takes the name it is looking
 * for and accepts nothing else, so a shape collects its own profile and the matrix driver is never
 * handed a ten-LED strip. That defence is only as good as the names being distinct, and the names
 * are made here, from the shape ids — which is why this is a test about the build and not about the
 * effects. `leds.test.ts` covers what a profile contains.
 */
import { describe, expect, test } from 'bun:test';
import { leds } from '../src/generator.ts';
import { ALL_SHAPES, BARE_RUN_LENGTHS, CENTRE_LENGTHS, GRID_SHAPES, LEGACY_SHAPES, SIDE_LENGTHS } from '../src/leds/strip.ts';
import { rpmStripFileName } from '../src/leds/rpmStrip.ts';
import { FLAG_BOX_FILE } from '../src/build.ts';

/** What the build writes, and what the plugin embeds: `OpenDash <id>.ledsprofile`. */
const fileOf = (shape: (typeof ALL_SHAPES)[number]): string => `${rpmStripFileName(shape)}${leds.LEDS_PROFILE_EXTENSION}`;

describe('the profile file names', () => {
  test('are the grid, less what a legacy shape already spells, plus the legacy shapes', () => {
    // The count is the product of the two ranges rather than a number typed here, so widening a range
    // moves it and dropping a shape from the legacy list moves it the other way. What is pinned is
    // the arithmetic: every side against every centre, the long bare runs after them, and the shapes
    // that shipped before the grid and fall outside it.
    expect(GRID_SHAPES.length).toBe(SIDE_LENGTHS.length * CENTRE_LENGTHS.length + BARE_RUN_LENGTHS.length);
    const spelled = new Set(LEGACY_SHAPES.map((shape) => shape.id));
    expect(ALL_SHAPES.length).toBe(GRID_SHAPES.filter((shape) => !spelled.has(shape.id)).length + LEGACY_SHAPES.length);
    // And the ranges themselves, which are the whole of what a driver picks between.
    expect(SIDE_LENGTHS).toEqual([0, 1, 2, 3, 4]);
    expect(CENTRE_LENGTHS[0]).toBe(4);
    expect(CENTRE_LENGTHS[CENTRE_LENGTHS.length - 1]).toBe(12);
    expect(BARE_RUN_LENGTHS[BARE_RUN_LENGTHS.length - 1]).toBe(25);
  });

  test('cover every A/B/A a driver can describe, so no wheel is missing from the list', () => {
    const ids = new Set(ALL_SHAPES.map((shape) => shape.id));
    for (const side of SIDE_LENGTHS) {
      for (const centre of CENTRE_LENGTHS) expect({ id: `${side}-${centre}-${side}`, has: ids.has(`${side}-${centre}-${side}`) }).toMatchObject({ has: true });
    }
    for (const centre of BARE_RUN_LENGTHS) expect({ id: `0-${centre}-0`, has: ids.has(`0-${centre}-0`) }).toMatchObject({ has: true });
  });

  test('never collide with each other', () => {
    const files = ALL_SHAPES.map(fileOf);
    expect(new Set(files).size).toBe(files.length);
  });

  test('never collide with the flag box, which belongs to the other driver entirely', () => {
    // The one that matters. A shape whose file spelled itself the way the flag box spells its own
    // would be handed to SimHub's MATRIX driver, which is the hazard Resources/README.md is about.
    for (const shape of ALL_SHAPES) {
      expect(fileOf(shape).toLowerCase()).not.toBe(FLAG_BOX_FILE.toLowerCase());
    }
  });

  test('are distinct case-insensitively, because the plugin will take a case-only variant', () => {
    // FlagBoxProfile.SelectResource falls back to a case-insensitive match when nothing spells the
    // name exactly, since copying a built file over an older one on Windows keeps the old casing.
    // Two shapes differing only in case would make that fallback pick between them arbitrarily.
    const folded = ALL_SHAPES.map((s) => fileOf(s).toLowerCase());
    expect(new Set(folded).size).toBe(folded.length);
  });

  test('carry no character that a resource name or a Windows path would lose', () => {
    // MSBuild names an embedded resource "<RootNamespace>.<folder>.<file name>" and turns only the
    // folder part into an identifier, so a space survives the round trip and FlagBoxProfile.FileNameOf
    // reads the name back out. A dot in the stem would not: it would read as another folder part.
    for (const shape of ALL_SHAPES) {
      const stem = rpmStripFileName(shape);
      expect({ id: shape.id, stem, ok: /^OpenDash [0-9A-Za-z-]+$/.test(stem) }).toMatchObject({ ok: true });
    }
  });
});
