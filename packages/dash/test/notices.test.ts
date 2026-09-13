/**
 * The licences a package owes for what it redistributes.
 *
 * A `.simhubdash` leaves this machine carrying five Barlow faces, which the SIL Open Font Licence
 * permits only when its notice travels with them. That the notice is present is therefore not a
 * tidiness check: a package without it is a package openDash has no right to publish.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { buildPackage, fontsForPackage } from '../src/dashboard.ts';
import { VENDORED_FONTS_DIR } from '../src/design/fontFiles.ts';
import { FONT_LICENCE, IMAGE_LICENCES, noticesForPackage, PANEL_NOTICES } from '../src/design/notices.ts';
import { layout1920x480 } from '../src/layouts/1920x480.ts';
import type { DashPackage } from '../src/generator.ts';

const withoutFonts = (pkg: DashPackage): DashPackage => ({ ...pkg, fonts: [] });

describe('what a package owes', () => {
  const pkg = buildPackage(layout1920x480, { version: '0.0.0-test' });

  test('a package that ships fonts carries the font licence', () => {
    expect(pkg.fonts.length).toBeGreaterThan(0);
    expect(noticesForPackage(pkg)).toEqual([FONT_LICENCE]);
  });

  test('the licence file exists and is the OFL, not an empty placeholder', () => {
    expect(existsSync(FONT_LICENCE.path)).toBe(true);
    const text = readFileSync(FONT_LICENCE.path, 'utf8');
    expect(text).toContain('SIL OPEN FONT LICENSE');
    expect(text.length).toBeGreaterThan(1000);
  });

  test('a package that ships no font owes nothing for one', () => {
    expect(noticesForPackage(withoutFonts(pkg))).toEqual([]);
  });

  test('every font the build packs is covered by the licence that travels with it', () => {
    // Asserted on the vendored sources rather than on what the build emits, because the condensed
    // faces are renamed to openDash Display so that WPF resolves them (XOR-108) and their file
    // names no longer say Barlow. The OFL covers Barlow; a face from another foundry would need
    // its own notice, and this is the assertion that would fail rather than a release quietly
    // breaking somebody's licence.
    const vendored = readdirSync(VENDORED_FONTS_DIR).filter((f) => f.toLowerCase().endsWith('.ttf'));
    expect(vendored.length).toBeGreaterThan(0);
    for (const font of vendored) expect(font.toLowerCase()).toContain('barlow');
    // And what is packed all came from there.
    expect(fontsForPackage().length).toBeGreaterThan(0);
    for (const font of fontsForPackage()) expect(font.startsWith(VENDORED_FONTS_DIR)).toBe(true);
  });

  test('the plugin zip owes the same notice, because it embeds the same faces', () => {
    expect(PANEL_NOTICES).toEqual([FONT_LICENCE]);
  });
});

describe('images', () => {
  const withImage = (): DashPackage => {
    const pkg = buildPackage(layout1920x480, { version: '0.0.0-test' });
    pkg.dashboards[0]!.images = [{ name: 'icon', extension: '.png', path: '/nowhere/icon.png', width: 1, height: 1, length: 1, md5: '0'.repeat(32) }];
    return pkg;
  };

  test('a package carrying an image with no registered licence refuses to be built', () => {
    // The artwork arrives with XOR-97. Until its licence is registered beside it, this throws,
    // which is the point: an unlicensed asset must not be able to reach a release quietly.
    if (IMAGE_LICENCES.length === 0) {
      expect(() => noticesForPackage(withImage())).toThrow(/no licence is registered/);
      return;
    }
    expect(noticesForPackage(withImage())).toEqual([FONT_LICENCE, ...IMAGE_LICENCES]);
  });
});
