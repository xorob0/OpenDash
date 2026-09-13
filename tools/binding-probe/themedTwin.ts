/**
 * Builds a themed twin of a built package: every non-transparent colour property becomes a
 * Mode 2 binding of the shape a theme setting would have, `isnull([OpenDash.Theme<n>], '<token>')`,
 * with the literal it replaces as the fallback. The twin draws identically with no plugin
 * installed, so the difference between it and the original is the cost of the bindings alone.
 *
 * This is what the personalisation tickets would emit if colour were a runtime setting. It exists
 * to be measured, not to be shipped. See docs/decisions/0011-personalisation.md.
 */
import { cpSync, existsSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { zipPackage } from '../../packages/generator/src/package.ts';

type J = Record<string, unknown>;

const COLOUR_KEYS = ['TextColor', 'BackgroundColor', 'FillColor', 'EllipseColor', 'LineColor', 'GaugeColor', 'AlternateGaugeColor'] as const;
/** SimHub's transparent. Binding it would be paying for a colour nobody sees. */
const TRANSPARENT = '#00FFFFFF';

const isItem = (n: J): boolean => String(n['$type'] ?? '').includes('GraphicalDash.Models');

/** One property name per distinct literal, so the twin reads as many settings as the theme has colours. */
const names = new Map<string, string>();
const settingFor = (hex: string): string => {
  let name = names.get(hex);
  if (name === undefined) {
    name = `Theme${String(names.size + 1).padStart(2, '0')}`;
    names.set(hex, name);
  }
  return name;
};

let bound = 0;
const bind = (hex: string): J => {
  bound++;
  return { Formula: { Expression: `isnull([OpenDash.${settingFor(hex)}],'${hex}')` }, Mode: 2 };
};

function theme(node: unknown): void {
  if (Array.isArray(node)) { for (const n of node) theme(n); return; }
  if (node === null || typeof node !== 'object') return;
  const o = node as J;
  if (isItem(o)) {
    const existing = (o['Bindings'] as J | undefined) ?? {};
    for (const key of COLOUR_KEYS) {
      const value = o[key];
      if (typeof value !== 'string' || value === TRANSPARENT) continue;
      if (existing[key] !== undefined) continue; // already dynamic; a theme would fold into that expression
      existing[key] = bind(value);
    }
    if (Object.keys(existing).length > 0) o['Bindings'] = existing;
    const border = o['BorderStyle'] as J | undefined;
    if (border && typeof border['BorderColor'] === 'string' && border['BorderColor'] !== TRANSPARENT) {
      const b = (border['Bindings'] as J | undefined) ?? {};
      if (b['BorderColor'] === undefined) b['BorderColor'] = bind(border['BorderColor'] as string);
      border['Bindings'] = b;
    }
  }
  for (const v of Object.values(o)) theme(v);
}

const source = process.argv[2];
const suffix = process.argv[3] ?? ' Themed';
if (source === undefined) throw new Error('usage: themedTwin.ts <built package folder> [name suffix]');
const from = basename(source);
const to = from + suffix;
const outDir = join(source, '..', to);
rmSync(outDir, { recursive: true, force: true });
cpSync(source, outDir, { recursive: true });

// A package folder is flat: the dashboard, its widgets, their metadata sidecars, and _SHFonts.
for (const file of readdirSync(outDir)) {
  if (!file.endsWith('.djson')) continue;
  const path = join(outDir, file);
  const dashboard = JSON.parse(readFileSync(path, 'utf8')) as J;
  theme(dashboard);
  const renamed = file === `${from}.djson` ? `${to}.djson` : file;
  writeFileSync(join(outDir, renamed), JSON.stringify(dashboard, null, 2));
  const meta = `${path}.metadata`;
  if (existsSync(meta)) {
    const m = JSON.parse(readFileSync(meta, 'utf8')) as J;
    if (m['Title'] === from) m['Title'] = to;
    writeFileSync(join(outDir, `${renamed}.metadata`), JSON.stringify(m, null, 2));
  }
  if (renamed !== file) { rmSync(path); rmSync(meta, { force: true }); }
}

const { path } = zipPackage(dirname(outDir), to);
console.log(`wrote ${outDir} and ${path}: ${bound} colour bindings over ${names.size} distinct colours`);
