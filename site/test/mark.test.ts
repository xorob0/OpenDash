/**
 * The mark is media/logo.svg, and the site draws it three times: inline in the wordmark, as the
 * favicon, and in the OpenGraph image. All three hold that file's path character for character,
 * so a redrawn mark reaches the site or fails here.
 */
import { expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const site = path.resolve(import.meta.dir, '..');
const read = (rel: string): string => readFileSync(path.join(site, rel), 'utf8');
const pathOf = (svg: string): string => /\bd="([^"]+)"/.exec(svg)?.[1] ?? '';

const logo = pathOf(read('../media/logo.svg'));

test('the logo draws one path', () => {
  expect(logo.length).toBeGreaterThan(20);
});

test('the favicon is the logo', () => {
  expect(pathOf(read('public/icon.svg'))).toBe(logo);
});

test('the wordmark draws the logo', () => {
  expect(pathOf(read('components/Wordmark.tsx'))).toBe(logo);
});

test('the OpenGraph image draws the logo', () => {
  expect(/const MARK = '([^']+)'/.exec(read('app/opengraph-image.tsx'))?.[1]).toBe(logo);
});
