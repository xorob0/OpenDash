/**
 * The anatomy explains one face, and its lede speaks for all of them (#1094). The parts it offers
 * are the base face's, and a rectangular face that lacks one of them is a face the lede may not
 * call the same: the 800 x 286 face has no room for the bar. These hold the sentence to the face
 * definitions, so a face that drops a part changes what the page says about it.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { BASE_FACE, ZONE_FACES } from '../../packages/dash/src/zones/index.ts';
import { anatomyLede } from '../lib/anatomy.ts';
import { heroFace, rectangularFaces, type SiteFaceParts } from '../scripts/content.ts';

const shown = heroFace(BASE_FACE);
const faces = rectangularFaces(ZONE_FACES);
const offered = shown.parts.map((p) => p.id);
const lacking = faces.filter((f) => offered.some((id) => !f.parts.includes(id)));

describe('the rectangular faces', () => {
  test('are every zone face the build makes', () => {
    expect(faces.map((f) => f.folder)).toEqual(ZONE_FACES.map((l) => l.folder));
  });

  test('the 800 x 286 face draws every part but the bar', () => {
    const nano = faces.find((f) => f.width === 800 && f.height === 286);
    expect(nano?.parts).toEqual(offered.filter((id) => id !== 'bar'));
  });
});

describe('the anatomy lede', () => {
  const lede = anatomyLede(shown, faces);

  test('does not say every while a rectangular face lacks a part the anatomy offers', () => {
    expect(lacking.length).toBeGreaterThan(0);
    expect(lede).not.toMatch(/\bevery\b/i);
  });

  test('names each face that lacks a part, and the part', () => {
    expect(lede).toContain('the 800 × 286 face has no bar');
    for (const f of lacking) expect(lede).toContain(`${f.width} × ${f.height} face has no`);
  });

  test('says every when every face draws every part', () => {
    const whole: SiteFaceParts[] = faces.map((f) => ({ ...f, parts: offered }));
    expect(anatomyLede(shown, whole)).toBe('Every rectangular face is built the same way. Hover or tap a part.');
  });

  test('names two faces, and two parts of one', () => {
    const two: SiteFaceParts[] = [
      { folder: 'a', width: 800, height: 286, parts: offered.filter((id) => id !== 'bar') },
      { folder: 'b', width: 640, height: 200, parts: offered.filter((id) => id !== 'bar' && id !== 'band') },
    ];
    expect(anatomyLede(shown, two)).toBe(
      'The rectangular faces are built the same way, but the 800 × 286 face has no bar and the 640 × 200 face has no bar or band D. Hover or tap a part.',
    );
  });

  test('is the one the Screens page prints, rather than a sentence typed into it', () => {
    const page = readFileSync(path.resolve(import.meta.dir, '..', 'app', 'screens', 'page.tsx'), 'utf8');
    const section = page.slice(page.indexOf('id="anatomy"'));
    expect(section.slice(0, section.indexOf('>'))).toContain('lede={anatomyLede(HERO_FACE, RECTANGULAR_FACES)}');
  });
});
