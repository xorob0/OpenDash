/**
 * The clips the site promises exist, within their weight, and the sidecar says where they came
 * from. Until the first recording lands there is no sidecar and the test says so rather than
 * passing quietly.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, statSync } from 'node:fs';
import path from 'node:path';
import { readClips } from '../lib/clips.ts';
import { mergeClip } from '../scripts/sync-clips.ts';

const clipsDir = path.resolve(import.meta.dir, '..', 'public', 'clips');
const sidecarPath = path.join(clipsDir, 'clips.json');
const present = existsSync(sidecarPath);

const BUDGET = { webm: 1.5 * 1024 * 1024, mp4: 3 * 1024 * 1024, total: 10 * 1024 * 1024 };

describe('the clips', () => {
  test.if(!present)('are not recorded yet: bun run clips, then site/scripts/sync-clips.ts', () => {
    expect(present).toBe(false);
  });

  test.if(present)('exist, one webm, one mp4 and one poster each', () => {
    const sidecar = readClips(sidecarPath);
    expect(sidecar.clips.length).toBeGreaterThan(0);
    for (const c of sidecar.clips) {
      for (const f of Object.values(c.files)) expect({ f, exists: existsSync(path.join(clipsDir, f)) }).toEqual({ f, exists: true });
      expect(c.takenAt).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    }
  });

  test.if(present)('stay within the weight budget', () => {
    const sidecar = readClips(sidecarPath);
    let total = 0;
    for (const c of sidecar.clips) {
      const webm = statSync(path.join(clipsDir, c.files.webm)).size;
      const mp4 = statSync(path.join(clipsDir, c.files.mp4)).size;
      expect({ slug: c.slug, webmUnderBudget: webm <= BUDGET.webm, mp4UnderBudget: mp4 <= BUDGET.mp4 }).toEqual({ slug: c.slug, webmUnderBudget: true, mp4UnderBudget: true });
      total += webm + mp4 + statSync(path.join(clipsDir, c.files.poster)).size;
    }
    expect(total).toBeLessThanOrEqual(BUDGET.total);
  });

  test.if(present)('include the hero, which the home page names', () => {
    expect(readClips(sidecarPath).clips.some((c) => c.slug === 'opendash-850x480')).toBe(true);
  });
});

describe('merging a clip', () => {
  const entry = (slug: string) => ({
    slug,
    package: slug,
    scenario: 'gallery',
    width: 850,
    height: 480,
    fps: 20,
    seconds: 6,
    frames: 140,
    dropped: 0,
    takenAt: '2026-09-23T10:00:00.000Z',
    files: { webm: `${slug}.webm`, mp4: `${slug}.mp4`, poster: `${slug}.png` },
    bytes: { webm: 1, mp4: 1, poster: 1 },
  });

  test('replaces by slug and keeps the rest', () => {
    const before = { schema: 1 as const, version: '0.3.0-rc.5', commit: 'a', simHubVersion: '9.12.6', clips: [entry('b'), entry('a')] };
    const after = mergeClip(before, { ...entry('b'), frames: 141 }, { version: '0.3.0-rc.6', commit: 'c', simHubVersion: '9.12.6' });
    expect(after.clips.map((c) => c.slug)).toEqual(['a', 'b']);
    expect(after.clips[1]?.frames).toBe(141);
    expect(after.version).toBe('0.3.0-rc.6');
  });
});
