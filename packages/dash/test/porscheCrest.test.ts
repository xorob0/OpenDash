/**
 * The Porsche badge draws the crest the plugin fetched into the user's own folder, and nothing OpenDash
 * ships carries it (#714, #194, the trade dress paragraph of docs/scope.md).
 *
 * The second half is the one that matters. The crest is a registered trade mark, and the rule that keeps
 * OpenDash MIT is that no package, release or file of this repository carries it: the plugin carries an
 * address and the SHA-256 of the file expected there, and the copy on the rig is the user's own. So this
 * hashes every file a package, the plugin's resources and the image folders hold, and every entry of every
 * built package, against the hash `CarCrest.cs` carries, and refuses a picture named for a crest.
 */
import { describe, expect, test } from 'bun:test';
import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { ncalc, readZip, type Item } from '../src/generator.ts';
import { composeTheme } from '../src/build.ts';
import { setting, themeEntry } from '../src/contract.ts';

const ROOT = path.resolve(import.meta.dir, '../../..');
const sha256 = (bytes: Uint8Array): string => createHash('sha256').update(bytes).digest('hex');

/** The hash the plugin checks the default crest against, read from the plugin rather than copied here. */
const crestHash = (): string => {
  const source = readFileSync(path.join(ROOT, 'plugin/OpenDash/CarCrest.cs'), 'utf8');
  const found = /public const string DefaultSha256 = "([0-9a-f]{64})";/.exec(source);
  if (!found) throw new Error('CarCrest.cs carries no DefaultSha256');
  return found[1]!;
};

const filesUnder = (dir: string): string[] => {
  if (!existsSync(dir)) return [];
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? filesUnder(full) : [full];
  });
};

/** A picture whose name says it is a crest, a badge or a logo of the marque. */
const NAMED_FOR_A_MARK = /(crest|wappen|badge|logo|emblem)[^/]*\.(png|jpe?g|gif|bmp|svg|webp)$/i;

describe('the Porsche badge', () => {
  // Composed as the build composes it, in a process drawn in the Porsche's colours.
  const [face] = composeTheme({ theme: themeEntry('porsche')!, version: '0.0.0-test', simHubVersion: '9.12.6' });
  const flatten = (list: readonly Item[]): Item[] => list.flatMap((item) => (item.kind === 'layer' ? [item, ...flatten(item.children)] : [item]));
  const items = face!.pkg.dashboards.flatMap((d) => d.screens.flatMap((s) => flatten(s.items)));
  const byName = (name: string): Item => {
    const item = items.find((i) => i.name === name);
    if (!item) throw new Error(`no ${name} in the Porsche package`);
    return item;
  };
  const noCrest = ncalc.eq(setting.porscheCrest(), ncalc.str(''));

  test('draws the file OpenDash.PorscheCrest names, in the placeholder\'s own rectangle', () => {
    const crest = byName('porscheFoot.badge.crest');
    const shield = byName('porscheFoot.badge');
    expect(crest.kind).toBe('imageFromFile');
    if (crest.kind !== 'imageFromFile' || shield.kind !== 'rect') throw new Error('unexpected kinds');
    expect(crest.rect).toEqual(shield.rect);
    expect(crest.rect).toMatchObject({ left: 14, width: 54, height: 62 });
    expect(crest.bindings?.ImagePath).toEqual({ mode: 'formula', formula: "isnull([OpenDash.PorscheCrest], '')" });
    // Nothing in the package names a file: the path is the plugin's, at run time.
    expect(crest.imagePath ?? '').toBe('');
    // Over the placeholder, so that the crest is what shows once it is there.
    expect(items.indexOf(crest)).toBeGreaterThan(items.indexOf(shield));
    // One badge, on the one face the Porsche draws today; the faces that shed it (#205, #713) draw none.
    expect(items.filter((i) => i.kind === 'imageFromFile')).toHaveLength(1);
  });

  test('draws the empty shield, words and all, exactly while there is no crest', () => {
    for (const name of ['porscheFoot.badge', 'porscheFoot.badge.your', 'porscheFoot.badge.badge']) {
      const item = byName(name);
      const visible = item.bindings?.Visible;
      expect({ name, visible: visible?.mode === 'formula' ? visible.formula : undefined }).toEqual({ name, visible: noCrest });
    }
  });
});

describe('no crest ships', () => {
  const hash = crestHash();
  const shipped = [
    path.join(ROOT, 'packages/dash/images'),
    path.join(ROOT, 'packages/dash/src'),
    path.join(ROOT, 'design'),
    path.join(ROOT, 'plugin/OpenDash/Resources'),
    path.join(ROOT, 'site/public'),
  ].flatMap(filesUnder);

  test('no file a package, the plugin or the site is made from is the crest', () => {
    expect(shipped.length).toBeGreaterThan(0);
    const crests = shipped.filter((file) => statSync(file).size < 16 * 1024 * 1024 && sha256(readFileSync(file)) === hash);
    expect(crests).toEqual([]);
  });

  test('no picture among them is named for a crest, a badge or a logo of the marque', () => {
    const named = shipped.filter((file) => /porsche/i.test(file) && NAMED_FOR_A_MARK.test(path.basename(file)));
    expect(named).toEqual([]);
  });

  test('no entry of any built package is the crest', () => {
    const build = path.join(ROOT, 'build');
    const packages = existsSync(build) ? readdirSync(build).filter((f) => f.endsWith('.simhubdash')) : [];
    if (packages.length === 0) {
      console.log('no built packages in build/ to look inside; run `bun run build --all-themes` to check them');
      return;
    }
    for (const file of packages) {
      const outer = readZip(new Uint8Array(readFileSync(path.join(build, file))));
      for (const [name, bytes] of Object.entries(outer)) {
        // An image sidecar is a zip of its own, which is where a package's pictures live.
        const inner = name.endsWith('.ressources') ? Object.entries(readZip(bytes)).map(([n, b]) => [`${name}/${n}`, b] as const) : [];
        for (const [entry, content] of [[name, bytes] as const, ...inner]) {
          expect({ file, entry, crest: sha256(content) === hash }).toEqual({ file, entry, crest: false });
        }
      }
    }
  });
});
