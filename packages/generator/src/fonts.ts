/**
 * Minimal TrueType `name` table reader, enough to learn which family and style a `.ttf`
 * declares. WPF keys font families by this family name, so the validator uses it to check that
 * every font a dashboard references is bundled under `_SHFonts/`.
 */

import { existsSync, readFileSync } from 'node:fs';
import { basename } from 'node:path';

export interface TtfNames {
  /** Typographic family (name id 16) when present, else the legacy family (name id 1). */
  family: string;
  /** Typographic subfamily (17) or legacy subfamily (2), e.g. "SemiBold". */
  subfamily: string;
  /** Legacy family (name id 1), which for non-RIBBI styles carries the style: "Barlow Medium". */
  legacyFamily: string;
  fullName: string;
  postScriptName: string;
}

const NAME_FAMILY = 1;
const NAME_SUBFAMILY = 2;
const NAME_FULL = 4;
const NAME_POSTSCRIPT = 6;
const NAME_TYPO_FAMILY = 16;
const NAME_TYPO_SUBFAMILY = 17;

const decodeName = (platformId: number, bytes: Uint8Array): string =>
  platformId === 3 || platformId === 0
    ? new TextDecoder('utf-16be').decode(bytes)
    : new TextDecoder('latin1').decode(bytes);

/** Parses the `name` table of a TrueType/OpenType font file held in memory. */
export const parseTtfNames = (data: Uint8Array): TtfNames => {
  const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
  if (data.byteLength < 12) throw new Error('Not a TrueType font: file too short');
  const tag = view.getUint32(0);
  // 0x00010000 (TrueType), 'true', 'OTTO' (CFF OpenType)
  if (tag !== 0x00010000 && tag !== 0x74727565 && tag !== 0x4f54544f) {
    throw new Error('Not a TrueType font: bad sfnt version');
  }
  const numTables = view.getUint16(4);
  let nameOffset = -1;
  for (let i = 0; i < numTables; i++) {
    const record = 12 + i * 16;
    const t = String.fromCharCode(data[record] ?? 0, data[record + 1] ?? 0, data[record + 2] ?? 0, data[record + 3] ?? 0);
    if (t === 'name') nameOffset = view.getUint32(record + 8);
  }
  if (nameOffset < 0) throw new Error('Not a TrueType font: no name table');

  const count = view.getUint16(nameOffset + 2);
  const stringsOffset = nameOffset + view.getUint16(nameOffset + 4);
  // platform 3 (Windows) wins over platform 1 (Macintosh); both usually agree.
  const byId = new Map<number, { platform: number; value: string }>();
  for (let i = 0; i < count; i++) {
    const r = nameOffset + 6 + i * 12;
    const platformId = view.getUint16(r);
    const nameId = view.getUint16(r + 6);
    const length = view.getUint16(r + 8);
    const offset = view.getUint16(r + 10);
    if (platformId !== 3 && platformId !== 1) continue;
    const existing = byId.get(nameId);
    if (existing && existing.platform === 3) continue;
    const start = stringsOffset + offset;
    if (start + length > data.byteLength) continue;
    byId.set(nameId, { platform: platformId, value: decodeName(platformId, data.subarray(start, start + length)) });
  }
  const get = (id: number): string => byId.get(id)?.value ?? '';
  const legacyFamily = get(NAME_FAMILY);
  return {
    family: get(NAME_TYPO_FAMILY) || legacyFamily,
    subfamily: get(NAME_TYPO_SUBFAMILY) || get(NAME_SUBFAMILY),
    legacyFamily,
    fullName: get(NAME_FULL),
    postScriptName: get(NAME_POSTSCRIPT),
  };
};

/** Reads the names of a font file on disk. */
export const readTtfNames = (path: string): TtfNames => parseTtfNames(new Uint8Array(readFileSync(path)));

/**
 * Names a font file declares, or a guess from its file name when the file cannot be read
 * (`BarlowCondensed-SemiBold.ttf` gives family "BarlowCondensed", subfamily "SemiBold").
 */
export const fontNamesOf = (path: string): TtfNames => {
  if (existsSync(path)) {
    try {
      return readTtfNames(path);
    } catch {
      // fall through to the file name heuristic
    }
  }
  const stem = basename(path).replace(/\.[^.]+$/, '');
  const dash = stem.indexOf('-');
  const family = dash >= 0 ? stem.slice(0, dash) : stem;
  const subfamily = dash >= 0 ? stem.slice(dash + 1) : 'Regular';
  return { family, subfamily, legacyFamily: family, fullName: stem, postScriptName: stem };
};

/** Family names compared without spaces or case, so "Barlow Condensed" matches "BarlowCondensed". */
export const fontKey = (family: string): string => family.replace(/\s+/g, '').toLowerCase();

/** WPF weight names and the subfamily names font files use for them. */
const WEIGHT_ALIASES: Record<string, string[]> = {
  thin: ['thin', 'hairline'],
  extralight: ['extralight', 'ultralight'],
  light: ['light'],
  normal: ['normal', 'regular', 'book'],
  medium: ['medium'],
  semibold: ['semibold', 'demibold'],
  bold: ['bold'],
  extrabold: ['extrabold', 'ultrabold'],
  black: ['black', 'heavy'],
};

/** True when a subfamily such as "SemiBold" or "Bold Italic" provides the WPF weight. */
export const subfamilyHasWeight = (subfamily: string, weight: string): boolean => {
  const sub = fontKey(subfamily).replace(/italic|oblique/g, '') || 'regular';
  const aliases = WEIGHT_ALIASES[weight.toLowerCase()] ?? [weight.toLowerCase()];
  return aliases.includes(sub);
};
