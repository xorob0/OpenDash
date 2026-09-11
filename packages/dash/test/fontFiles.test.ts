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
const shipped = (): string[] => [...fontsForPackage(), ...fontsForScreens()];
const familyOfFile = (file: string): string | undefined => familyOf(loadFont(file));
/** Bold and Regular carry no typographic names, being the pair a legacy family can name on its own. */
const weightOfFile = (file: string): string | undefined => {
  const font = loadFont(file);
  return fontName(font, NAME_ID.typographicSubfamily) ?? fontName(font, NAME_ID.subfamily);
};

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

  test('the label family ships Medium', () => {
    const weights = shipped()
      .filter((f) => familyOfFile(f) === ds.font.label)
      .map(weightOfFile);
    expect(weights).toContain('Medium');
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
  const listed = [...panelFontsCs.matchAll(/"([\w.-]+\.(?:ttf|txt))"/g)].map((m) => m[1]!);

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

  test('every replacement is the same length as what it replaces', () => {
    // That is what lets this be a byte substitution: no name record changes length, so no offset in
    // the file moves and no table but head needs a new checksum.
    for (const [from, to] of FAMILY_RENAMES) expect([from, to.length]).toEqual([from, from.length]);
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

  test('the file a package ships is the same size as the one it was made from', () => {
    const renamed = shipped().find((f) => path.basename(f) === 'openDashDisplay-Bold.ttf');
    expect(renamed).toBeDefined();
    expect(readFileSync(renamed!).byteLength).toBe(readFileSync(source).byteLength);
    expect(familyOfFile(renamed!)).toBe(ds.font.data);
  });
});
