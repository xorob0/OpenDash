/**
 * The changelog renderer, against the markdown CHANGELOG.md actually contains.
 *
 * `lib/markdown.ts` deliberately covers a subset rather than the language, so the thing worth
 * testing is the edge of that subset: that the forms it claims are rendered, that the ones it does
 * not claim survive as readable text, and that the two asterisk forms cannot be mistaken for one
 * another. The last is not hypothetical — `*italic*` listed before `**bold**` in the alternation
 * turns every bold run into an italic empty string between stray asterisks, and the changelog is
 * full of bold.
 */
import { describe, expect, test } from 'bun:test';
import { renderInline, toBlocks } from '../lib/markdown.ts';
import { readFileSync } from 'node:fs';
import path from 'node:path';

/** The rendered tree as a tag string, which is all these assertions need to see. */
function render(node: unknown): string {
  if (node === null || node === undefined || typeof node === 'boolean') return '';
  if (typeof node === 'string' || typeof node === 'number') return String(node);
  if (Array.isArray(node)) return node.map(render).join('');
  const element = node as { type?: unknown; props?: { children?: unknown } };
  const tag = typeof element.type === 'string' ? element.type : '';
  const inner = render(element.props?.children);
  return tag ? `<${tag}>${inner}</${tag}>` : inner;
}

describe('renderInline', () => {
  test.each([
    ['plain text', 'plain text'],
    ['a **bold** word', 'a <strong>bold</strong> word'],
    ['an *italic* word', 'an <em>italic</em> word'],
    ['**bold** beside *italic*', '<strong>bold</strong> beside <em>italic</em>'],
    ['`OpenDash.RevBar` carries three states', '<code>OpenDash.RevBar</code> carries three states'],
    ['two **bold** runs **here**', 'two <strong>bold</strong> runs <strong>here</strong>'],
  ])('renders %p', (input, expected) => {
    expect(render(renderInline(input))).toBe(expected);
  });

  test('an absolute link is a link', () => {
    expect(render(renderInline('see [the docs](https://example.test/x)'))).toBe('see <a>the docs</a>');
  });

  test('a repository-relative link keeps its text and drops the href', () => {
    // The site is not the repository, so `docs/design/zones.md` would 404. The words still read.
    expect(render(renderInline('see [zones.md](docs/design/zones.md)'))).toBe('see zones.md');
  });

  test('multiplication is not emphasis', () => {
    // `* ` with a space after it is a bullet elsewhere and arithmetic here; neither is an <em>.
    expect(render(renderInline('44 rows of 480, 2 * 3 * 4'))).toBe('44 rows of 480, 2 * 3 * 4');
  });

  test('an unclosed delimiter is left alone rather than swallowing the rest of the line', () => {
    expect(render(renderInline('a **bold start that never ends'))).toBe('a **bold start that never ends');
  });
});

describe('toBlocks', () => {
  test('a wrapped bullet is one item', () => {
    const blocks = toBlocks('- one line\n  continued here\n- two');
    expect(blocks).toEqual([{ kind: 'list', items: ['one line continued here', 'two'] }]);
  });

  test('a wrapped paragraph is one paragraph', () => {
    expect(toBlocks('A paragraph\nwrapped over lines.')).toEqual([
      { kind: 'paragraph', text: 'A paragraph wrapped over lines.' },
    ]);
  });

  test('a heading ends whatever was open', () => {
    const blocks = toBlocks('intro text\n\n### Added\n\n- item');
    expect(blocks.map((b) => b.kind)).toEqual(['paragraph', 'heading', 'list']);
  });

  test('a blank line ends a list', () => {
    const blocks = toBlocks('- one\n\n- two');
    expect(blocks).toEqual([
      { kind: 'list', items: ['one'] },
      { kind: 'list', items: ['two'] },
    ]);
  });
});

describe('against the real changelog', () => {
  const changelog = readFileSync(path.resolve(import.meta.dir, '..', '..', 'CHANGELOG.md'), 'utf8');

  test('every release body parses into blocks and loses no words', () => {
    // A section is cut at each `## ` heading, which is what scripts/changelog.ts does for a release
    // body. Whatever the renderer does to the markup, the words have to survive it.
    const sections = changelog.split(/^## /m).slice(1);
    expect(sections.length).toBeGreaterThan(0);
    for (const section of sections) {
      const body = section.split('\n').slice(1).join('\n');
      const blocks = toBlocks(body);
      const rendered = blocks
        .map((b) => (b.kind === 'list' ? b.items.map((i) => render(renderInline(i))).join(' ') : render(renderInline(b.kind === 'heading' ? b.text : b.text))))
        .join(' ');
      // Every word of the source, minus the markup characters, appears in the output.
      const words = body
        .replace(/[*`#\[\]()]/g, ' ')
        .split(/\s+/)
        .filter((w) => w.length > 3 && /^[A-Za-z]+$/.test(w));
      const plain = rendered.replace(/<[^>]*>/g, ' ');
      for (const word of new Set(words)) expect(plain).toContain(word);
    }
  });

  test('no release body renders a stray asterisk', () => {
    // The symptom of the bold/italic ordering bug, and of an unbalanced delimiter in the source.
    const sections = changelog.split(/^## /m).slice(1);
    for (const section of sections) {
      const body = section.split('\n').slice(1).join('\n');
      for (const block of toBlocks(body)) {
        const texts = block.kind === 'list' ? block.items : [block.text];
        for (const text of texts) {
          // A bare `*` between word characters is arithmetic or a literal, and is allowed; a `*`
          // hugging a word is markup the renderer failed to consume.
          expect(render(renderInline(text))).not.toMatch(/\*\w|\w\*/);
        }
      }
    }
  });
});
