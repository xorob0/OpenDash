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
import { ALL_SHAPES, BROW_SHAPES, STRIP_SHAPES } from '../src/leds/strip.ts';
import { rpmStripFileName } from '../src/leds/rpmStrip.ts';
import { FLAG_BOX_FILE } from '../src/build.ts';

/** What the build writes, and what the plugin embeds: `openDash <id>.ledsprofile`. */
const fileOf = (shape: (typeof ALL_SHAPES)[number]): string => `${rpmStripFileName(shape)}${leds.LEDS_PROFILE_EXTENSION}`;

describe('the profile file names', () => {
  test('are thirteen strips and seven brows, which with the flag box is the twenty-one a release carries', () => {
    // The plugin's Lights section offers a row per embedded profile, so this count is the number of
    // rows a driver sees. It is pinned because it is easy to lose one: a shape dropped from the
    // declarations is a device that silently stops being installable.
    expect(STRIP_SHAPES.length).toBe(13);
    expect(BROW_SHAPES.length).toBe(7);
    expect(ALL_SHAPES.length).toBe(20);
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
      expect({ id: shape.id, stem, ok: /^openDash [0-9A-Za-z-]+$/.test(stem) }).toMatchObject({ ok: true });
    }
  });
});
