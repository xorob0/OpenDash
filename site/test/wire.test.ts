/**
 * What leaves the machine is said in three places, and each is held to the one list in
 * `lib/wire.ts`: the plugin's code, the table under the telemetry refusal in docs/scope.md, and the
 * comparison's "What leaves your machine" cell. The cell once named the update check and the car
 * tables while the plugin fetched the Porsche crest at a start without a press (#1096), and nothing
 * read the plugin to notice.
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { rows } from '../lib/compare.ts';
import { WIRE } from '../lib/wire.ts';
import { SAMPLE_COUNTS, SAMPLE_LISTED } from './sampleCounts.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const pluginDir = path.join(repoRoot, 'plugin', 'OpenDash');

/**
 * The plugin's sources by file name, with comments removed so a sentence about a request is not one.
 * Line comments go first: a line comment that mentions a path like `plugin/*.dc.html` would otherwise
 * open a block comment that swallows the code up to the next `*\/` in the file.
 */
function pluginSources(): Map<string, string> {
  const out = new Map<string, string>();
  for (const name of readdirSync(pluginDir).sort()) {
    if (!name.endsWith('.cs')) continue;
    const code = readFileSync(path.join(pluginDir, name), 'utf8')
      .replace(/^\s*\/\/.*$/gm, '')
      .replace(/\s\/\/ .*$/gm, '')
      .replace(/\/\*[\s\S]*?\*\//g, '');
    out.set(name, code);
  }
  return out;
}

/**
 * What opens a connection in .NET, written the ways the plugin could write it: HttpWebRequest.Create as
 * well as WebRequest.Create, and a type named with its namespace, `new System.Net.WebClient()`, as well
 * as one named bare.
 */
const CONNECTION =
  /\b(?:Http)?WebRequest\.Create(?:Http)?\b|\bnew\s+(?:[\w.:]+\.)?(?:HttpClient|WebClient|TcpClient|UdpClient|Socket|HttpListener|ClientWebSocket|Ping)\b|\bDns\.Get/;

/**
 * Every fetch through IReleaseSource, as "File.cs Method", once per call site. The names fetched
 * through are the ones a file declares as an IReleaseSource or a ReleaseClient, and a ReleaseClient
 * fetched through as it is made counts too.
 */
function fetchCalls(sources: Map<string, string>): string[] {
  const calls: string[] = [];
  for (const [file, code] of sources) {
    if (file === 'ReleaseClient.cs') continue;
    const names = new Set<string>();
    for (const m of code.matchAll(/\b(?:IReleaseSource|ReleaseClient)\s+(\w+)/g)) names.add(m[1]!);
    for (const m of code.matchAll(/\b(?:var|IReleaseSource)\s+(\w+)\s*=\s*new\s+ReleaseClient\b/g)) names.add(m[1]!);
    for (const m of code.matchAll(/\b(\w+)\s*\??\.\s*(GetString|GetBytes)\s*\(/g)) if (names.has(m[1]!)) calls.push(`${file} ${m[2]}`);
    for (const m of code.matchAll(/new\s+ReleaseClient\([^)]*\)\s*\.\s*(GetString|GetBytes)\s*\(/g)) calls.push(`${file} ${m[1]}`);
  }
  return calls.sort();
}

/** The rows of the table under docs/scope.md's telemetry refusal, as [request, to, when]. */
function scopeTable(): string[][] {
  const doc = readFileSync(path.join(repoRoot, 'docs', 'scope.md'), 'utf8');
  const start = doc.indexOf('**Telemetry about the user.**');
  const end = doc.indexOf('\n**', start + 1);
  const section = doc.slice(start, end);
  return section
    .split('\n')
    .filter((line) => /^\|/.test(line) && !/^\|\s*-/.test(line))
    .slice(1)
    .map((line) =>
      line
        .split('|')
        .slice(1, -1)
        .map((c) => c.trim()),
    );
}

const privacyCell = (): string => rows(SAMPLE_COUNTS, SAMPLE_LISTED).find((r) => r.id === 'privacy')!.cells.opendash.text;

describe('what leaves the machine', () => {
  const sources = pluginSources();

  test('only ReleaseClient.cs opens a connection', () => {
    const opening = [...sources].filter(([, code]) => CONNECTION.test(code)).map(([file]) => file);
    expect(opening).toEqual(['ReleaseClient.cs']);
  });

  test('every fetch the plugin makes is listed, and every one listed is made', () => {
    expect(fetchCalls(sources)).toEqual(WIRE.flatMap((r) => r.calls).sort());
  });

  test('the address a request carries is on the host the list says', () => {
    const wrong: string[] = [];
    for (const r of WIRE) {
      if (!r.address) continue;
      const code = sources.get(r.address.file) ?? '';
      const m = new RegExp(`\\b${r.address.constant}\\s*=\\s*"([^"]+)"`).exec(code);
      if (!m) wrong.push(`${r.name}: no ${r.address.constant} in ${r.address.file}`);
      else if (new URL(m[1]!).host !== r.address.host) wrong.push(`${r.name}: ${m[1]} is not on ${r.address.host}`);
    }
    expect(wrong).toEqual([]);
  });

  test('docs/scope.md lists the same requests, and says which wait for a press', () => {
    const table = scopeTable();
    expect(table.map((row) => row[0])).toEqual(WIRE.map((r) => r.name));
    for (const r of WIRE) {
      const when = table.find((row) => row[0] === r.name)?.[2] ?? '';
      expect({ request: r.name, withoutAPress: /without a press/.test(when) }).toEqual({ request: r.name, withoutAPress: !r.pressed });
    }
  });

  test('the comparison names every request', () => {
    const cell = privacyCell();
    expect(WIRE.filter((r) => !cell.includes(r.cell)).map((r) => r.name)).toEqual([]);
  });

  test('the comparison says nothing about the user is in them', () => {
    expect(privacyCell()).toContain('Nothing about you');
  });
});
