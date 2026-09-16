/**
 * The changelog parser, against the markdown CHANGELOG.md actually contains.
 *
 * `lib/markdown.ts` deliberately covers a subset rather than the language, so the thing worth
 * testing is the edge of that subset: that the forms it claims are parsed, that the ones it does
 * not claim survive as readable text, and that the two asterisk forms cannot be mistaken for one
 * another. The last is not hypothetical — `*italic*` ahead of `**bold**` in the alternation turns
 * every bold run into an italic empty string between stray asterisks, and the changelog is full of
 * bold.
 *
 * The parser returns data and imports nothing, so this runs under the repository's own `bun test`
 * without the site's dependencies installed. That is the reason it returns data.
 */
import { describe, expect, test } from 'bun:test';
import { plainText, toBlocks, toInline, type Inline } from '../lib/markdown.ts';
import { readFileSync } from 'node:fs';
import path from 'node:path';

/** The parsed run as a tag string, which is all these assertions need to see. */
const shape = (spans: readonly Inline[]): string =>
  spans.map((s) => (s.kind === 'text' ? s.text : `<${s.kind}>${s.text}</${s.kind}>`)).join('');

describe('toInline', () => {
  test.each([
    ['plain text', 'plain text'],
    ['a **bold** word', 'a <strong>bold</strong> word'],
    ['an *italic* word', 'an <em>italic</em> word'],
    ['**bold** beside *italic*', '<strong>bold</strong> beside <em>italic</em>'],
    ['`OpenDash.RevBar` carries three states', '<code>OpenDash.RevBar</code> carries three states'],
    ['two **bold** runs **here**', 'two <strong>bold</strong> runs <strong>here</strong>'],
    ['see [the docs](https://example.test/x)', 'see <link>the docs</link>'],
    ['see [zones.md](docs/design/zones.md)', 'see <link>zones.md</link>'],
  ])('parses %p', (input, expected) => {
    expect(shape(toInline(input))).toBe(expected);
  });

  test('an absolute link keeps its href', () => {
    expect(toInline('[x](https://example.test/a)')).toEqual([
      { kind: 'link', text: 'x', href: 'https://example.test/a' },
    ]);
  });

  test('a repository-relative link drops its href, because the site is not the repository', () => {
    expect(toInline('[x](docs/x.md)')).toEqual([{ kind: 'link', text: 'x' }]);
  });

  test('multiplication is not emphasis', () => {
    // `*` followed by a space is a bullet elsewhere and arithmetic here; neither is emphasis.
    expect(shape(toInline('44 rows of 480, 2 * 3 * 4'))).toBe('44 rows of 480, 2 * 3 * 4');
  });

  test('an unclosed delimiter is left alone rather than swallowing the rest of the line', () => {
    expect(shape(toInline('a **bold start that never ends'))).toBe('a **bold start that never ends');
  });

  test('the words survive whatever the markup does', () => {
    expect(plainText(toInline('a **bold** and `code` and [link](https://x.test)'))).toBe(
      'a bold and code and link',
    );
  });
});

describe('toBlocks', () => {
  test('a wrapped bullet is one item', () => {
    expect(toBlocks('- one line\n  continued here\n- two')).toEqual([
      { kind: 'list', items: ['one line continued here', 'two'] },
    ]);
  });

  test('a wrapped paragraph is one paragraph', () => {
    expect(toBlocks('A paragraph\nwrapped over lines.')).toEqual([
      { kind: 'paragraph', text: 'A paragraph wrapped over lines.' },
    ]);
  });

  test('a heading ends whatever was open', () => {
    expect(toBlocks('intro text\n\n### Added\n\n- item').map((b) => b.kind)).toEqual([
      'paragraph',
      'heading',
      'list',
    ]);
  });

  test('a blank line ends a list', () => {
    expect(toBlocks('- one\n\n- two')).toEqual([
      { kind: 'list', items: ['one'] },
      { kind: 'list', items: ['two'] },
    ]);
  });
});

describe('against the real changelog', () => {
  const changelog = readFileSync(path.resolve(import.meta.dir, '..', '..', 'CHANGELOG.md'), 'utf8');
  /** Each `## ` section's body, which is what a release page shows and what CI cuts a release from. */
  const bodies = changelog
    .split(/^## /m)
    .slice(1)
    .map((section) => section.split('\n').slice(1).join('\n'));

  test('there is something to parse', () => {
    expect(bodies.length).toBeGreaterThan(3);
  });

  test('no release body loses a word', () => {
    for (const body of bodies) {
      const parsed = toBlocks(body)
        .map((b) => (b.kind === 'list' ? b.items : [b.text]))
        .flat()
        .map((line) => plainText(toInline(line)))
        .join(' ');
      const words = body
        .replace(/[*`#\[\]()]/g, ' ')
        .split(/\s+/)
        .filter((w) => w.length > 3 && /^[A-Za-z]+$/.test(w));
      for (const word of new Set(words)) expect(parsed).toContain(word);
    }
  });

  test('no release body leaves a stray asterisk', () => {
    // The symptom of the bold/italic ordering bug, and of an unbalanced delimiter in the source.
    for (const body of bodies) {
      for (const block of toBlocks(body)) {
        const lines = block.kind === 'list' ? block.items : [block.text];
        // A bare `*` between spaces is arithmetic and allowed; one hugging a word is markup the
        // parser failed to consume.
        for (const line of lines) expect(shape(toInline(line))).not.toMatch(/\*\w|\w\*/);
      }
    }
  });
});
