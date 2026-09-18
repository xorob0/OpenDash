/**
 * A markdown parser for exactly the subset CHANGELOG.md uses, and nothing else.
 *
 * A general markdown library would be the obvious answer, and it is the wrong one here for a
 * reason worth writing down: the only markdown this site renders is one file in this repository,
 * written by the people who maintain this repository, and a parser that accepts exactly what that
 * file contains is easier to be sure of than one that accepts everything. It also means no
 * `dangerouslySetInnerHTML` anywhere on the site — this returns data, the component turns it into
 * elements, and a changelog entry cannot inject markup even in principle.
 *
 * It returns data rather than React nodes deliberately. The presentation belongs to the component,
 * and a parser with no React import is one the repository's own `bun test` can run without the
 * site's dependencies installed.
 *
 * The subset, which `toBlocks` and `toInline` between them cover:
 *
 *   ### Heading        a group within a release: Added, Changed, Fixed, Known
 *   - item             a bullet, which may wrap onto indented continuation lines
 *   paragraph          one or more lines, ended by a blank line
 *   **bold**           emphasis
 *   *italic*           the lighter emphasis, which the changelog uses sparingly
 *   `code`             a property name, a file name, a command
 *   [text](url)        a link
 *
 * Anything else — a table, an image, a nested list, a fenced block — is passed through as plain
 * text. That is a deliberate floor rather than a bug: the text still reads, and the missing
 * formatting is visible enough in review to be noticed and either added here or avoided there.
 */

export type Block =
  | { kind: 'heading'; text: string }
  | { kind: 'list'; items: string[] }
  | { kind: 'paragraph'; text: string };

export type Inline =
  | { kind: 'text'; text: string }
  | { kind: 'strong'; text: string }
  | { kind: 'em'; text: string }
  | { kind: 'code'; text: string }
  /** `href` is absent for a repository-relative target, which the site has nowhere to point at. */
  | { kind: 'link'; text: string; href?: string };

/**
 * Split a release body into blocks.
 *
 * The wrapping is the only fiddly part. CHANGELOG.md is hard-wrapped at about 100 columns, so a
 * bullet is usually several lines and a paragraph always is; a line that is not blank, not a
 * heading and not a new bullet continues whatever came before it.
 */
export function toBlocks(markdown: string): Block[] {
  const blocks: Block[] = [];
  let list: string[] | null = null;
  let para: string[] | null = null;

  const endList = () => {
    if (list && list.length > 0) blocks.push({ kind: 'list', items: list });
    list = null;
  };
  const endPara = () => {
    if (para && para.length > 0) blocks.push({ kind: 'paragraph', text: para.join(' ') });
    para = null;
  };

  for (const raw of markdown.split('\n')) {
    const line = raw.trimEnd();

    if (line.trim() === '') {
      endList();
      endPara();
      continue;
    }

    const heading = line.match(/^#{2,6}\s+(.*)$/);
    if (heading) {
      endList();
      endPara();
      blocks.push({ kind: 'heading', text: heading[1]!.trim() });
      continue;
    }

    const bullet = line.match(/^\s*[-*]\s+(.*)$/);
    if (bullet) {
      endPara();
      list ??= [];
      list.push(bullet[1]!.trim());
      continue;
    }

    // A continuation: of the bullet above it if there is one, otherwise of the paragraph.
    if (list && list.length > 0) {
      list[list.length - 1] += ` ${line.trim()}`;
      continue;
    }
    para ??= [];
    para.push(line.trim());
  }

  endList();
  endPara();
  return blocks;
}

/**
 * Inline markup, as a flat run of spans.
 *
 * One pass with one alternation, so the forms cannot nest and cannot be mistaken for one another:
 * whichever opens first wins, and its own closing delimiter ends it. Nesting is not in the subset
 * because the changelog does not use it, and a parser that half-supports it is worse than one that
 * does not.
 *
 * `**bold**` is listed before `*italic*` because the alternation is ordered and the double
 * delimiter has to be tried first — the other way round, every bold run would come back as an
 * italic empty string between stray asterisks, and the changelog is full of bold.
 */
export function toInline(text: string): Inline[] {
  const pattern = /\*\*([^*]+)\*\*|\*([^*\s][^*]*)\*|`([^`]+)`|\[([^\]]+)\]\(([^)\s]+)\)/g;
  const spans: Inline[] = [];
  let last = 0;
  let match: RegExpExecArray | null;

  while ((match = pattern.exec(text)) !== null) {
    if (match.index > last) spans.push({ kind: 'text', text: text.slice(last, match.index) });
    const [, strong, em, code, linkText, href] = match;
    if (strong !== undefined) spans.push({ kind: 'strong', text: strong });
    else if (em !== undefined) spans.push({ kind: 'em', text: em });
    else if (code !== undefined) spans.push({ kind: 'code', text: code });
    else if (linkText !== undefined && href !== undefined) {
      // A changelog link is usually relative to the repository, which this site is not. Only an
      // absolute one keeps its href; the rest keep their text, which is what the reader needs.
      spans.push(/^https?:\/\//.test(href) ? { kind: 'link', text: linkText, href } : { kind: 'link', text: linkText });
    }
    last = pattern.lastIndex;
  }

  if (last < text.length) spans.push({ kind: 'text', text: text.slice(last) });
  return spans;
}

/** The words a run of spans carries, with the markup gone. For tests and for a plain-text fallback. */
export const plainText = (spans: readonly Inline[]): string => spans.map((s) => s.text).join('');
