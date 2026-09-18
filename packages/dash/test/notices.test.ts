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
import { ASSET_SOURCES, imageOf, WHEEL_CHANGE_TICK, type AssetSource, type AssetSourceId } from '../src/design/assets.ts';
import { VENDORED_FONTS_DIR } from '../src/design/fontFiles.ts';
import { FONT_LICENCE, NOTICES_BY_SOURCE, noticesForPackage, PANEL_NOTICES } from '../src/design/notices.ts';
import { layout1920x480 } from '../src/layouts/1920x480.ts';
import type { DashPackage, ImageAsset } from '../src/generator.ts';

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
    // faces are renamed to openDash Display so that WPF resolves them (#159) and their file
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
  const carrying = (image: ImageAsset): DashPackage => {
    const pkg = buildPackage(layout1920x480, { version: '0.0.0-test' });
    pkg.dashboards[0]!.images = [image];
    return pkg;
  };

  test('a package carrying an image no asset claims refuses to be built', () => {
    // Which is the point: a picture that reached a package without passing the registry has no
    // recorded licence, and an unlicensed asset must not be able to reach a release quietly.
    const stranger: ImageAsset = { name: 'icon', extension: '.png', path: '/nowhere/icon.png', width: 1, height: 1, length: 1, md5: '0'.repeat(32) };
    expect(() => noticesForPackage(carrying(stranger))).toThrow(/no licence is registered/);
  });

  test("a package carrying openDash's own artwork owes nothing beyond the font licence", () => {
    expect(NOTICES_BY_SOURCE.openDash).toEqual([]);
    expect(noticesForPackage(carrying(imageOf(WHEEL_CHANGE_TICK)))).toEqual([FONT_LICENCE]);
  });

  test('every notice a source owes is a file that is there to be copied', () => {
    for (const notices of Object.values(NOTICES_BY_SOURCE)) for (const notice of notices) expect(existsSync(notice.path)).toBe(true);
  });

  test("a source that owes nothing is openDash's own work, and nobody else's", () => {
    // The empty list is a real answer for artwork the project made and publishes itself, and a
    // licence breach for anything taken from elsewhere. Which of the two it is has to be read off
    // the source rather than assumed, or the day somebody registers a source and leaves its notices
    // empty is the day a release ships somebody else's drawing with nothing attached to it.
    for (const [id, source] of Object.entries(ASSET_SOURCES) as [AssetSourceId, AssetSource][]) {
      if (NOTICES_BY_SOURCE[id].length > 0) continue;
      expect([id, source.who, source.licence]).toEqual([id, 'the openDash authors', 'MIT']);
    }
  });
});
