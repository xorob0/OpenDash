/**
 * The font files a package ships, renamed so that SimHub can actually find them.
 *
 * openDash draws its numerals in Barlow Condensed and, until this existed, SimHub drew them in
 * Barlow. WPF does not treat "Barlow Condensed" as a family: it reads the width out of the name,
 * folds the condensed faces into "Barlow" as a stretch, and a `.djson` has only `Font` and
 * `FontWeight` with nothing to ask a stretch for. The request therefore fell through to a
 * non-condensed face about a fifth wider than the design, which is what every measurement in
 * `advances.ts`, every monospace cell and every layout assumes it is not.
 *
 * So the bundled files are renamed to a family with no width word in it, and the dashboard asks
 * for that. The vendored files under `fonts/` stay exactly as they were downloaded; the renaming
 * happens here, on the way into a package, so that what the repository holds is still upstream
 * Barlow and what ships is openDash's own build of it.
 *
 * This is Barlow. The SIL Open Font License permits the modification and requires that it travel
 * with the licence, which `fonts/OFL.txt` does; the Barlow copyright declares no Reserved Font
 * Name, so the family may be renamed. The copyright and licence strings inside each file are
 * untouched and still credit the Barlow authors.
 */
import { copyFileSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';

/** Where the renamed files are written: build output, beside the vendored originals. */
export const GENERATED_FONTS_DIR = '.generated';

/**
 * What is rewritten inside the name table, longest first so that the shorter form cannot eat the
 * start of the longer one. Each replacement is the same length as what it replaces, which is what
 * lets the whole thing be a substitution rather than a rebuild: no name record changes length and
 * no offset moves, so only the `name` table's own checksum and `head.checkSumAdjustment` change.
 *
 * "Display" is the operative choice: it is not one of the width or weight words WPF reads out of a
 * family name (Condensed, Narrow, Compressed, Expanded, Extended, Wide, and the weights), so the
 * family survives as its own.
 */
export const FAMILY_RENAMES: readonly (readonly [from: string, to: string])[] = [
  ['Barlow Condensed', 'openDash Display'],
  ['BarlowCondensed', 'openDashDisplay'],
];

/** The file a renamed face is written as, so that `_SHFonts/` says what it holds. */
export const renamedFileName = (source: string): string => {
  const base = path.basename(source);
  for (const [from, to] of FAMILY_RENAMES) if (base.startsWith(from)) return to + base.slice(from.length);
  return base;
};

/** True when the file's family has to be rewritten before SimHub will resolve it. */
export const needsRename = (source: string): boolean => FAMILY_RENAMES.some(([from]) => path.basename(source).startsWith(from));

const utf16be = (s: string): number[] => [...s].flatMap((c) => [c.charCodeAt(0) >> 8, c.charCodeAt(0) & 0xff]);
const ascii = (s: string): number[] => [...s].map((c) => c.charCodeAt(0));

interface TableRecord {
  /** Offset of the table's 16 byte entry in the directory, where its checksum lives. */
  record: number;
  offset: number;
  length: number;
}

/** The table directory, by tag. */
function tables(view: DataView): Map<string, TableRecord> {
  const found = new Map<string, TableRecord>();
  const count = view.getUint16(4);
  for (let i = 0; i < count; i++) {
    const record = 12 + i * 16;
    const tag = String.fromCharCode(view.getUint8(record), view.getUint8(record + 1), view.getUint8(record + 2), view.getUint8(record + 3));
    found.set(tag, { record, offset: view.getUint32(record + 8), length: view.getUint32(record + 12) });
  }
  return found;
}

/** A table's checksum: its bytes summed as big-endian 32 bit words, zero padded to a multiple of four. */
function tableChecksum(view: DataView, offset: number, length: number): number {
  let sum = 0;
  for (let i = 0; i < length; i += 4) {
    let word = 0;
    for (let b = 0; b < 4; b++) word = ((word << 8) | (i + b < length ? view.getUint8(offset + i + b) : 0)) >>> 0;
    sum = (sum + word) >>> 0;
  }
  return sum;
}

/** Every index within `[from, to)` at which `needle` occurs. */
function occurrences(bytes: Uint8Array, needle: readonly number[], from: number, to: number): number[] {
  const at: number[] = [];
  outer: for (let i = from; i + needle.length <= to; i++) {
    for (let k = 0; k < needle.length; k++) if (bytes[i + k] !== needle[k]) continue outer;
    at.push(i);
  }
  return at;
}

/**
 * The bytes of `source` with its family renamed. Returns how many strings were rewritten.
 *
 * Only the `name` table is searched. The needles are long enough that a chance hit in outline data
 * is not credible, and none of the vendored files has one anywhere else, but a substitution loose
 * in a binary is the kind of thing that is fine until a font is refreshed and then silently is not.
 */
export function renameFamily(bytes: Uint8Array): number {
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const directory = tables(view);
  const name = directory.get('name');
  const head = directory.get('head');
  if (!name || !head) throw new Error('the font has no name or head table');

  let rewritten = 0;
  for (const [from, to] of FAMILY_RENAMES) {
    if (from.length !== to.length) throw new Error(`"${from}" and "${to}" are not the same length, so the name table would have to be rebuilt`);
    // Both encodings: the Windows name records are UTF-16BE, the Macintosh ones are single byte.
    for (const encode of [utf16be, ascii]) {
      const replacement = encode(to);
      for (const at of occurrences(bytes, encode(from), name.offset, name.offset + name.length)) {
        bytes.set(replacement, at);
        rewritten++;
      }
    }
  }
  if (rewritten === 0) return 0;

  // The name table's own bytes changed, so its directory checksum has. Then checkSumAdjustment,
  // which is 0xB1B0AFBA less the whole file summed the same way with that field zeroed, and which
  // therefore has to be computed after the directory is correct.
  view.setUint32(name.record + 4, tableChecksum(view, name.offset, name.length));
  view.setUint32(head.offset + 8, 0);
  const padded = new Uint8Array(Math.ceil(bytes.length / 4) * 4);
  padded.set(bytes);
  view.setUint32(head.offset + 8, (0xb1b0afba - tableChecksum(new DataView(padded.buffer), 0, padded.length)) >>> 0);
  return rewritten;
}

/**
 * The faces the settings panel draws in, which no dashboard bundles: the panel is WPF too, and it
 * asks for the same family, so it needs the same renamed files. Barlow Regular is the panel's own
 * body face and is here because this is the one list of what a release ships; Barlow SemiBold is the
 * weight the panel's headings ask for, which WPF would otherwise synthesise out of Medium.
 */
export const PANEL_FONT_FILES = [
  'Barlow-Regular.ttf',
  'Barlow-Medium.ttf',
  'Barlow-SemiBold.ttf',
  'BarlowCondensed-Light.ttf',
  'BarlowCondensed-SemiBold.ttf',
  'BarlowCondensed-Bold.ttf',
] as const;

/** What has already been prepared in this process, so that fourteen packages do not redo the work. */
const prepared = new Map<string, string>();

/**
 * Writes the renamed font into `outDir` and returns its path. A font that needs no renaming is
 * copied as it is, so that a caller gets one directory holding everything a package ships.
 *
 * Written once per process and never reused from a previous one. A timestamp comparison was the
 * obvious cache and the wrong one: `unzip`, `tar x` and a font downloaded from upstream all carry
 * an mtime older than whatever is already in `.generated`, so refreshing `fonts/` would have gone
 * unnoticed and the old face would have shipped. An interrupted write left a truncated file that
 * looked newer than its source and was then reused for ever, which is worse.
 *
 * The write goes to a temporary name and is renamed into place, which is atomic on every platform
 * this runs on, so a reader never sees half a font however the build is interrupted or repeated.
 */
export function prepareFont(source: string, outDir: string): string {
  const target = path.join(outDir, renamedFileName(source));
  const done = prepared.get(target);
  if (done !== undefined) return done;

  mkdirSync(outDir, { recursive: true });
  const temporary = `${target}.${process.pid}.tmp`;
  if (needsRename(source)) {
    const bytes = new Uint8Array(readFileSync(source));
    if (renameFamily(bytes) === 0) throw new Error(`${path.basename(source)} carries none of the names this renames, so its family would not resolve`);
    writeFileSync(temporary, bytes);
  } else {
    copyFileSync(source, temporary);
  }
  renameSync(temporary, target);
  prepared.set(target, target);
  return target;
}

/** Where the vendored files live, which is the one place a font is read from. */
export const VENDORED_FONTS_DIR = path.resolve(import.meta.dir, '..', '..', 'fonts');

/** Absolute paths of the panel's fonts, prepared the same way a package's are. */
export const fontsForPanel = (): string[] =>
  PANEL_FONT_FILES.map((f) => prepareFont(path.join(VENDORED_FONTS_DIR, f), path.join(VENDORED_FONTS_DIR, GENERATED_FONTS_DIR)));
