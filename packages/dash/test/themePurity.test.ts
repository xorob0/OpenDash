/**
 * A theme derives from the core's signals and never reads a property (ADR 0022). Under
 * `packages/dash/src/themes/` no call reads the sim: not `game(`, `raw(`, `prop(`, `computed(` or
 * `driver(`, and not `changed(`, whose window is state a theme would be keeping on its own. A theme
 * that needs a reading the core lacks adds it to `second/values.ts`, under a name, in the pull request
 * that needs it, so that the house, the other themes and the lights read the same rule.
 *
 * Calls are found through the TypeScript parser, as `hexLiterals.test.ts` finds colours, so that a
 * name in a comment or a string is not mistaken for a read; `ncalc.game(...)` and a destructured
 * `game(...)` are both calls of the name.
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import ts from 'typescript';

const SRC = path.resolve(import.meta.dir, '../src');
const THEMES = path.join(SRC, 'themes');

/** The reads a theme may not make, each the generator's helper of that name. */
export const PROPERTY_READS: readonly string[] = ['game', 'raw', 'prop', 'computed', 'driver', 'changed'];

const sourcesUnder = (dir: string): string[] =>
  (readdirSync(dir, { recursive: true, withFileTypes: true }) as import('node:fs').Dirent[])
    .filter((entry) => entry.isFile() && entry.name.endsWith('.ts'))
    .map((entry) => path.join(entry.parentPath, entry.name))
    .sort();

/** Every call of one of `names` in `file`, as `file:line name`. */
export function propertyReads(file: string, names: readonly string[] = PROPERTY_READS): string[] {
  const text = readFileSync(file, 'utf8');
  const source = ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true);
  const found: string[] = [];
  const visit = (node: ts.Node): void => {
    if (ts.isCallExpression(node)) {
      const callee = node.expression;
      const name = ts.isIdentifier(callee) ? callee.text : ts.isPropertyAccessExpression(callee) && ts.isIdentifier(callee.expression) && callee.expression.text === 'ncalc' ? callee.name.text : undefined;
      if (name !== undefined && names.includes(name)) {
        const { line } = source.getLineAndCharacterOfPosition(node.getStart(source));
        found.push(`${path.relative(SRC, file)}:${line + 1} ${name}`);
      }
    }
    node.forEachChild(visit);
  };
  visit(source);
  return found;
}

describe('packages/dash/src/themes', () => {
  const files = sourcesUnder(THEMES);

  test('is scanned, so the rule cannot pass on an empty list', () => {
    expect(files.length).toBeGreaterThan(20);
    expect(files.map((f) => path.relative(SRC, f))).toContain('themes/porsche/register.ts');
  });

  test('the scanner finds a read where there is one', () => {
    // The semantic layer is where the sim is read, so it is the control: a scanner that finds nothing
    // there would pass the themes for the wrong reason.
    expect(propertyReads(path.join(SRC, 'second/values.ts')).length).toBeGreaterThan(50);
  });

  test('reads no property of its own', () => {
    expect(files.flatMap((file) => propertyReads(file))).toEqual([]);
  });
});
