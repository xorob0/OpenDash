/**
 * No hex escapes the tokens: a colour written as a literal anywhere in packages/dash/src is a bug,
 * since it would follow neither design/tokens.json nor the theme being built. In a `.ts` file only
 * string and template literals are read, through the TypeScript parser, so that a colour quoted in
 * a comment to explain a choice is not mistaken for one drawn. A theme's `overlay.json` is a token
 * file and the one place under src where a colour may be written (ADR 0015); any other file is
 * read whole.
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import ts from 'typescript';

const SRC = path.resolve(import.meta.dir, '../src');
const HEX = /#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?(?![0-9A-Fa-f])/g;

/** The literals allowed a colour, each with the reason it is not a token. */
const EXCEPTIONS: readonly { file: string; value: string; why: string }[] = [
  { file: 'tokens.ts', value: '#00FFFFFF', why: "SimHub's transparent, the absence of a colour, which is defined there as TRANSPARENT" },
  { file: 'components/wordmark.ts', value: '#00FFFFFF', why: 'the same transparent, written out' },
  { file: 'leds/sheet.ts', value: '#16181C', why: "the contact sheet's unlit lamp, an SVG for people reading the profiles and never drawn on a dash" },
  { file: 'leds/sheet.ts', value: '#0A0B0D', why: "the contact sheet's paper" },
  { file: 'leds/sheet.ts', value: '#8A9099', why: "the contact sheet's ink" },
];

const OVERLAY = /^themes\/[^/]+\/overlay\.json$/;

const files = (readdirSync(SRC, { recursive: true, withFileTypes: true }) as import('node:fs').Dirent[])
  .filter((entry) => entry.isFile())
  .map((entry) => path.relative(SRC, path.join(entry.parentPath, entry.name)).split(path.sep).join('/'))
  .sort();
const sources = files.filter((f) => f.endsWith('.ts'));

const literalColours = (file: string): string[] => {
  const text = readFileSync(path.join(SRC, file), 'utf8');
  if (!file.endsWith('.ts')) return text.match(HEX) ?? [];
  const source = ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true);
  const found: string[] = [];
  const visit = (node: ts.Node): void => {
    if (ts.isStringLiteralLike(node) || ts.isTemplateLiteralToken(node)) found.push(...(node.text.match(HEX) ?? []));
    node.forEachChild(visit);
  };
  visit(source);
  return found;
};

describe('packages/dash/src', () => {
  const found = files.filter((file) => !OVERLAY.test(file)).flatMap((file) => literalColours(file).map((value) => ({ file, value })));
  const excepted = (f: { file: string; value: string }): boolean => EXCEPTIONS.some((e) => e.file === f.file && e.value === f.value);

  test('is scanned, so the rule cannot pass on an empty list', () => {
    expect(sources.length).toBeGreaterThan(100);
    expect(sources).toContain('tokens.ts');
    expect(files).toContain('themes/default/overlay.json');
  });

  test('writes no colour literal outside the token file', () => {
    expect(found.filter((f) => !excepted(f))).toEqual([]);
  });

  test('every exception is still needed', () => {
    for (const e of EXCEPTIONS) expect({ ...e, found: found.some((f) => f.file === e.file && f.value === e.value) }).toEqual({ ...e, found: true });
  });
});
