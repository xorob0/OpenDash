/**
 * The server cannot know whether a reader asked for reduced motion, so what it renders has to be
 * right for one who did: the picture, never a clip that starts playing before the page has asked.
 * The screen picker is rendered here as the home page's server renders it, with a face that has a
 * clip chosen, and its markup must hold the picture and no video.
 *
 * React lives in site/node_modules, which the root install does not reach, so where the site has not
 * been installed the test says so rather than failing on the import. `bun run check` installs it
 * before it tests, and so does the Website job in CI.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync } from 'node:fs';
import path from 'node:path';
import type { PickerFace } from '../lib/faces.ts';

const site = path.resolve(import.meta.dir, '..');
const installed = existsSync(path.join(site, 'node_modules', 'react-dom', 'server.js'));

const face = (slug: string, clip: boolean): PickerFace => ({
  folder: `OpenDash ${slug}`,
  slug,
  width: 850,
  height: 480,
  round: false,
  capture: `/clips/${slug}.webp`,
  clip: clip ? { webm: `/clips/${slug}.webm`, mp4: `/clips/${slug}.mp4` } : null,
});

describe('the screen picker, as the server renders it', () => {
  test.if(!installed)('is not checked: the site is not installed (cd site && bun install)', () => {
    expect(installed).toBe(false);
  });

  test.if(installed)('draws the chosen face as its picture and starts no clip', async () => {
    const { createElement } = await import('react');
    const { renderToString } = await import('react-dom/server');
    const { ScreenPicker } = await import('../components/ScreenPicker.tsx');
    const faces = [face('850x480', true), face('1280x480', true)];
    const html = renderToString(createElement(ScreenPicker, { faces, initial: '850x480' }));
    expect(html).toContain('aria-checked="true"');
    expect(html).toContain('850x480.webp');
    expect(html).not.toContain('<video');
    expect(html).not.toContain('.webm');
  });
});
