/**
 * What a shipped font declares itself to be.
 *
 * `BarlowCondensed-Bold.ttf` looks like it carries the family "Barlow Condensed", and it does, but
 * WPF reads the width word out of that name and files the face under "Barlow" instead, so a dash
 * asking for "Barlow Condensed" got a face a fifth wider than every measurement assumes. Nothing
 * caught it, because nothing here had ever opened a name table. These do.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { FACE_FONT_FILES, fontsForPackage } from '../src/dashboard.ts';
import { FAMILY_RENAMES, fontsForPanel, needsRename, renameFamily, renamedFileName } from '../src/design/fontFiles.ts';
import { fontsForScreens } from '../src/screens/index.ts';
import { ds } from '../src/tokens.ts';
import { familyOf, fontName, loadFont, measure, NAME_ID, parseFont } from '../../../tools/measure-font/measure.ts';

const VENDORED = path.resolve(import.meta.dir, '..', 'fonts');

/** The tags whose stored checksum disagrees with their bytes, plus 'head' when the file sum does. */
function checksumFaults(bytes: Uint8Array): string[] {
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const sum = (offset: number, length: number): number => {
    let total = 0;
    for (let i = 0; i < length; i += 4) {
      let word = 0;
      for (let b = 0; b < 4; b++) word = ((word << 8) | (i + b < length ? view.getUint8(offset + i + b) : 0)) >>> 0;
      total = (total + word) >>> 0;
    }
    return total;
  };
  const faults: string[] = [];
  let head = -1;
  for (let i = 0; i < view.getUint16(4); i++) {
    const record = 12 + i * 16;
    const tag = String.fromCharCode(view.getUint8(record), view.getUint8(record + 1), view.getUint8(record + 2), view.getUint8(record + 3));
    const offset = view.getUint32(record + 8);
    if (tag === 'head') head = offset;
    else if (sum(offset, view.getUint32(record + 12)) !== view.getUint32(record + 4)) faults.push(tag);
  }
  const stored = view.getUint32(head + 8);
  const zeroed = new Uint8Array(bytes);
  new DataView(zeroed.buffer).setUint32(head + 8, 0);
  const padded = new Uint8Array(Math.ceil(zeroed.length / 4) * 4);
  padded.set(zeroed);
  const whole = new DataView(padded.buffer);
  let total = 0;
  for (let i = 0; i < padded.length; i += 4) total = (total + whole.getUint32(i)) >>> 0;
  if (stored !== ((0xb1b0afba - total) >>> 0)) faults.push('head.checkSumAdjustment');
  return faults;
}
const shipped = (): string[] => [...fontsForPackage(), ...fontsForScreens()];
const familyOfFile = (file: string): string | undefined => familyOf(loadFont(file));
/** Bold and Regular carry no typographic names, being the pair a legacy family can name on its own. */
const weightOfFile = (file: string): string | undefined => {
  const font = loadFont(file);
  return fontName(font, NAME_ID.typographicSubfamily) ?? fontName(font, NAME_ID.subfamily);
};

/**
 * The words WPF reads out of a family name and files as a stretch rather than as part of the
 * family. `FontStretches` in PresentationCore is where the list comes from; only the ones that can
 * plausibly appear in a face openDash would ship are here, and "Condensed" is the one that cost us
 * a release.
 */
const WIDTH_WORDS = ['Condensed', 'Narrow', 'Compressed', 'Extended', 'Expanded', 'Wide', 'SemiCondensed', 'UltraCondensed', 'ExtraCondensed'] as const;

describe('no family openDash asks for can be folded again', () => {
  // This is the guard rather than a description of today's names. The bug was not that the family
  // was called "Barlow Condensed"; it was that nothing in the repository would have noticed if a
  // family with a width word in it were asked for again, and a .djson has no way to ask for a
  // stretch back. Any future face -- a second numeral family, a user's chosen typeface -- passes
  // through here.
  test('neither family the tokens name carries a width word', () => {
    for (const family of [ds.font.data, ds.font.label]) {
      for (const word of WIDTH_WORDS) {
        expect({ family, word, folds: new RegExp(`(^|[\\s-])${word}\\b`, 'i').test(family) }).toMatchObject({ folds: false });
      }
    }
  });

  test('every family a shipped file declares is one of them', () => {
    for (const file of shipped()) {
      const family = familyOfFile(file);
      for (const word of WIDTH_WORDS) {
        expect({ file: path.basename(file), family, word, folds: new RegExp(`(^|[\\s-])${word}\\b`, 'i').test(family ?? '') }).toMatchObject({ folds: false });
      }
    }
  });

  test('the panel asks for nothing a dashboard would not', () => {
    for (const file of fontsForPanel()) {
      const family = familyOfFile(file);
      for (const word of WIDTH_WORDS) {
        expect({ file: path.basename(file), family, word, folds: new RegExp(`(^|[\\s-])${word}\\b`, 'i').test(family ?? '') }).toMatchObject({ folds: false });
      }
    }
  });
});

describe('the families a package ships', () => {
  test('every shipped file declares a family the dashboard actually asks for', () => {
    const asked = [ds.font.data, ds.font.label];
    for (const file of shipped()) {
      expect([path.basename(file), familyOfFile(file)]).toEqual([path.basename(file), expect.stringMatching(new RegExp(`^(${asked.join('|')})$`))]);
    }
  });

  test('the data family ships the two weights the face draws in', () => {
    // The numerals are SemiBold and the gear is Bold; a missing weight is synthesised by WPF, which
    // is a different shape again from the one the advances were measured on.
    const weights = shipped()
      .filter((f) => familyOfFile(f) === ds.font.data)
      .map(weightOfFile);
    expect(weights).toContain('SemiBold');
    expect(weights).toContain('Bold');
  });

  test('the label family ships Medium and the Bold a flag band is named in', () => {
    // A label is Medium everywhere but on the flag band, which the artboards set in 700; the face
    // has to be carried rather than synthesised, since design/advances.ts measures it and the fit
    // tests believe what it measures.
    const weights = shipped()
      .filter((f) => familyOfFile(f) === ds.font.label)
      .map(weightOfFile);
    expect(weights).toContain('Medium');
    expect(weights).toContain('Bold');
  });

  test('the vendored originals are left exactly as they were downloaded', () => {
    // The renaming happens on the way into a package, so what the repository holds stays upstream
    // Barlow and a later `fonts/` refresh does not have to be re-patched by hand.
    for (const file of FACE_FONT_FILES.filter(needsRename)) {
      expect([file, familyOfFile(path.join(VENDORED, file))]).toEqual([file, 'Barlow Condensed']);
    }
  });
});

/**
 * The settings panel is WPF as well, and it embeds its own copies of the same faces. Its list of
 * files is C# and cannot import this one, so it is read back and compared: a panel embedding
 * `BarlowCondensed-Bold.ttf` would ask for a family that resolves to Barlow, exactly the bug this
 * fixes, and would do it where no dashboard test looks.
 */
describe('the faces the settings panel embeds', () => {
  const panelFontsCs = readFileSync(path.resolve(import.meta.dir, '..', '..', '..', 'plugin', 'OpenDash', 'PanelFonts.cs'), 'utf8');
  // Only the Files array, so that a .ttf named anywhere else in that file, a comment included, does
  // not join the list and fail this for a reason that has nothing to do with what it checks.
  const filesArray = /private static readonly string\[\] Files =\s*\{([^}]*)\}/.exec(panelFontsCs);
  const listed = [...(filesArray?.[1] ?? '').matchAll(/"([^"]+)"/g)].map((m) => m[1]!);

  test('the panel lists exactly the fonts the build prepares for it, plus the licence', () => {
    expect(listed).toEqual([...fontsForPanel().map((f) => path.basename(f)), 'OFL.txt']);
  });

  test('those files declare the families the panel asks for', () => {
    for (const file of fontsForPanel()) {
      expect([path.basename(file), familyOfFile(file)]).toEqual([path.basename(file), expect.stringMatching(/^(openDash Display|Barlow)$/)]);
    }
  });

  test('the panel asks for the family the tokens name', () => {
    const theme = readFileSync(path.resolve(import.meta.dir, '..', '..', '..', 'plugin', 'OpenDash', 'Theme.cs'), 'utf8');
    expect(theme).toContain(`public const string FontData = "${ds.font.data}";`);
    expect(theme).toContain(`public const string FontLabel = "${ds.font.label}";`);
  });
});

describe('renaming a family', () => {
  const source = path.join(VENDORED, 'BarlowCondensed-Bold.ttf');

  test('a renamed file is written under the name it now carries', () => {
    expect(renamedFileName('/x/BarlowCondensed-SemiBold.ttf')).toBe('openDashDisplay-SemiBold.ttf');
    expect(renamedFileName('/x/Barlow-Medium.ttf')).toBe('Barlow-Medium.ttf');
    expect(needsRename('/x/Barlow-Medium.ttf')).toBe(false);
  });

  test('renaming changes the family and nothing about the outlines', () => {
    const before = loadFont(source);
    const bytes = new Uint8Array(readFileSync(source));
    expect(renameFamily(bytes)).toBeGreaterThan(0);
    const after = parseFont(bytes);

    expect(familyOf(after)).toBe(ds.font.data);
    expect(familyOf(after)).not.toBe(familyOf(before));
    expect(after.unitsPerEm).toBe(before.unitsPerEm);
    expect(after.numGlyphs).toBe(before.numGlyphs);
    for (const ch of '0123456789NR') expect([ch, measure(after, ch)]).toEqual([ch, measure(before, ch)]);
  });

  test('only the name table is searched, so nothing outside it can be rewritten', () => {
    // The needles are long, and none of the vendored files carries one outside the name table, so
    // this has never mattered. It would matter silently the first time a refreshed font did.
    const bytes = new Uint8Array(readFileSync(source));
    const view = new DataView(bytes.buffer);
    const count = view.getUint16(4);
    let name = { offset: 0, length: 0 };
    for (let i = 0; i < count; i++) {
      const record = 12 + i * 16;
      const tag = String.fromCharCode(view.getUint8(record), view.getUint8(record + 1), view.getUint8(record + 2), view.getUint8(record + 3));
      if (tag === 'name') name = { offset: view.getUint32(record + 8), length: view.getUint32(record + 12) };
    }
    // A needle planted in the glyph data must survive, and the family must still be renamed.
    const planted = name.offset > 2048 ? 1024 : bytes.length - 64;
    bytes.set([...'BarlowCondensed'].map((c) => c.charCodeAt(0)), planted);
    const before = bytes.slice(planted, planted + 15);
    expect(renameFamily(bytes)).toBeGreaterThan(0);
    expect([...bytes.slice(planted, planted + 15)]).toEqual([...before]);
    expect(familyOf(parseFont(bytes))).toBe(ds.font.data);
  });

  test('every checksum in a renamed file is the one the file claims', () => {
    // The name table's bytes changed, so its directory checksum did; head.checkSumAdjustment covers
    // the whole file and so has to be computed after that. Both were wrong at first, silently: no
    // renderer validates them and only a font tool would have said so.
    for (const file of [...shipped(), ...fontsForPanel()]) {
      expect([path.basename(file), ...checksumFaults(new Uint8Array(readFileSync(file)))]).toEqual([path.basename(file)]);
    }
  });
});
