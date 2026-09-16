/**
 * Builds `openDash Probe`, a throwaway package that answers one question per row: does SimHub
 * apply a binding to this property? Each row draws a literal that reads FAIL and binds a constant
 * that reads PASS, so the answer needs no telemetry, no plugin and no running sim.
 *
 * The items come from `packages/generator` so that their shape is exactly the shape SimHub already
 * accepts, ids and all. The rows the generator will not model, meaning a target outside
 * `BindingTarget` and a binding on a sub-object rather than on the item, are injected into the
 * serialised JSON afterwards, by item name. Writing the whole file by hand instead cost an afternoon: a screen
 * whose `ScreenEnabledExpression` was null took SimHub down with a NullReferenceException that
 * appears only in its log.
 *
 *   bun tools/binding-probe/probe.ts build
 *   bun run vm install 'openDash Probe'
 *
 * See tools/binding-probe/README.md for the rows and docs/decisions/0011-personalisation.md for
 * what the answers settled.
 */
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { DEFAULT_AUTHOR } from '../../packages/dash/src/dashboard.ts';
import type { DashPackage, Hex, Item, RectangleItem, TextItem } from '../../packages/generator/src/model.ts';
import { writePackage, zipPackage } from '../../packages/generator/src/package.ts';
import { validatePackage } from '../../packages/generator/src/validate.ts';

const W = 1280;
const H = 760;
const PASS: Hex = '#00D96A';
const FAIL: Hex = '#FF2D46';
const RAMP_END: Hex = '#2E7BFF';
const GREY: Hex = '#8A9099';
const NAME = 'openDash Probe';

/** One row per question: a grey question on the left, the subject on the right. */
const ROW_H = 58;
const TOP = 24;
const LABEL_W = 440;
const SUBJECT_X = 480;
const row = (i: number): number => TOP + i * ROW_H;

const items: Item[] = [];
let r = 0;

const question = (text: string): TextItem => ({
  kind: 'text',
  name: `q${r + 1}`,
  rect: { left: 20, top: row(r), width: LABEL_W, height: 40 },
  text,
  font: 'Barlow',
  fontWeight: 'Medium',
  fontSize: 20,
  textColor: GREY,
  hAlign: 'left',
  vAlign: 'center',
});

const subject = (over: Partial<TextItem> = {}): TextItem => ({
  kind: 'text',
  name: `s${r + 1}`,
  rect: { left: SUBJECT_X, top: row(r), width: 320, height: 44 },
  text: 'PASS',
  font: 'Barlow',
  fontWeight: 'Medium',
  fontSize: 26,
  textColor: PASS,
  hAlign: 'left',
  vAlign: 'center',
  ...over,
});

const box = (over: Partial<RectangleItem> = {}): RectangleItem => ({
  kind: 'rect',
  name: `s${r + 1}`,
  rect: { left: SUBJECT_X, top: row(r) + 2, width: 140, height: 40 },
  backgroundColor: '#00FFFFFF',
  ...over,
});

const ask = (text: string, ...subjects: Item[]): void => {
  items.push(question(text), ...subjects);
  r++;
};

const literal = (expression: string) => ({ mode: 'formula' as const, formula: expression });
const colour = (hex: Hex): string => `'#FF${hex.slice(1)}'`;

// 1. TextColor in formula mode: a Color property bound to a colour string.
ask('1  TextColor formula', subject({ textColor: FAIL, bindings: { TextColor: literal(colour(PASS)) } }));

// 2. TextColor in gradient mode: the formula's 1 lands on the ramp's end colour, so PASS is blue.
ask(
  '2  TextColor gradient, expect blue',
  subject({
    textColor: FAIL,
    bindings: { TextColor: { mode: 'gradient', formula: '1', startColor: `#FF${FAIL.slice(1)}`, startValue: 0, endColor: `#FF${RAMP_END.slice(1)}`, endValue: 1 } },
  }),
);

// 3. FontSize: a double. Drawn small, bound large.
ask('3  FontSize 14 -> 34', subject({ fontSize: 14, bindings: { FontSize: literal('34') } }));

// 4 and 5 are the two properties SimHub marks [NoBinding]; injected below, since the generator
// models neither as a target. Their rows are reserved here so the labels line up.
ask('4  Font -> Courier New', subject({ name: 'font-swap' }));
ask(
  '5  CharWidth 14 -> 40',
  subject({ name: 'char-width', text: '888', monospace: { charWidth: 14, specialCharsWidth: 14 } }),
);

// 6. BorderColor on the BorderStyle sub-object, which is an IBindable of its own. Injected.
ask('6  BorderStyle.Bindings.BorderColor', box({ name: 'border-colour', border: { color: `#FF${FAIL.slice(1)}`, top: 4, bottom: 4, left: 4, right: 4 } }));

// 7. The same target at item level, which BindingHelper cannot resolve. Expected to stay red.
ask('7  item Bindings.BorderColor, expect red', box({ name: 'border-colour-item', border: { color: `#FF${FAIL.slice(1)}`, top: 4, bottom: 4, left: 4, right: 4 } }));

// 8. An int on the same sub-object. Injected.
ask('8  BorderStyle.Bindings.BorderTop 2 -> 18', box({ name: 'border-top', border: { color: `#FF${PASS.slice(1)}`, top: 2, bottom: 2, left: 2, right: 2 } }));

// 9. Left: positional, and bound in shipping third-party dashboards.
ask('9  Left 480 -> 700', subject({ bindings: { Left: literal('700') } }));

// 10. Opacity: 25 is visibly faded, so a fully lit PASS is the failure.
ask('10 Opacity 100 -> 25, expect faded', subject({ opacity: 100, bindings: { Opacity: literal('25') } }));

// 11. A target no item has. The row must draw normally: an unknown name is a silent no-op. Injected.
ask('11 Bindings.Nonsense, expect drawn', subject({ name: 'nonsense' }));

// 12. PaddingLeft on TextPadding, the other sub-object on a TextItem. Injected.
ask('12 TextPadding.Bindings.PaddingLeft 0 -> 60', subject({ name: 'padding-left', padding: { left: 0, top: 0, right: 0, bottom: 0 } }));

const pkg: DashPackage = {
  folderName: NAME,
  dashboards: [
    {
      name: NAME,
      width: W,
      height: H,
      backgroundColor: '#0A0B0D',
      screens: [{ name: 'probe', items, backgroundColor: '#0A0B0D' }],
      metadata: {
        title: NAME,
        author: DEFAULT_AUTHOR,
        description: 'Which properties SimHub applies a binding to (#124)',
        version: '0.0.0-probe',
        simHubVersion: '9.12.6',
      },
    },
  ],
  fonts: [],
};

// The probe reads no plugin property, so the declared set is empty; everything else the validator
// checks (colours, ids, geometry, which targets an item kind accepts) is exactly what it is for.
const result = validatePackage(pkg, { declaredProperties: [], propertyPrefix: 'OpenDash' });
for (const w of result.warnings) console.warn(`warn ${w.code} ${w.path}: ${w.message}`);
if (!result.ok) {
  for (const e of result.errors) console.error(`${e.code} ${e.path}: ${e.message}`);
  throw new Error(`${result.errors.length} validation error(s)`);
}

const outDir = process.argv[2] ?? 'build';
mkdirSync(outDir, { recursive: true });
const written = writePackage(pkg, outDir);

/**
 * The rows the model will not express. Each is keyed by the item's `Name`, and each is exactly
 * what a generator that grew the capability would have to emit.
 */
type Json = Record<string, unknown>;
const formula = (expression: string): Json => ({ Formula: { Expression: expression }, Mode: 2 });
/** The sub-object, created when the serialiser left it out: it omits one whose values are all default. */
const sub = (item: Json, key: string): Json => {
  const existing = item[key];
  if (existing !== null && typeof existing === 'object') return existing as Json;
  const created: Json = {};
  item[key] = created;
  return created;
};
const INJECT: Record<string, (item: Json) => void> = {
  // [NoBinding] on TextItem. SimHub's editor will not offer it; ApplyBindings never reads the attribute.
  'font-swap': (i) => { i['Bindings'] = { ...(i['Bindings'] as Json), Font: formula("'Courier New'") }; },
  'char-width': (i) => { i['Bindings'] = { ...(i['Bindings'] as Json), CharWidth: formula('40') }; },
  // The sub-objects carry their own Bindings; ApplyBindings recurses into every IBindable property.
  'border-colour': (i) => { sub(i, 'BorderStyle')['Bindings'] = { BorderColor: formula(colour(PASS)) }; },
  'border-top': (i) => { sub(i, 'BorderStyle')['Bindings'] = { BorderTop: formula('18') }; },
  'padding-left': (i) => { sub(i, 'TextPadding')['Bindings'] = { PaddingLeft: formula('60') }; },
  // Item level, where the property does not exist. Expected to do nothing at all.
  'border-colour-item': (i) => { i['Bindings'] = { BorderColor: formula(colour(PASS)) }; },
  nonsense: (i) => { i['Bindings'] = { Nonsense: formula('1') }; },
};

const djson = join(written.folder, `${NAME}.djson`);
const dashboard = JSON.parse(readFileSync(djson, 'utf8')) as Json;
const injected: string[] = [];
const walk = (node: unknown): void => {
  if (Array.isArray(node)) { for (const n of node) walk(n); return; }
  if (node === null || typeof node !== 'object') return;
  const o = node as Json;
  const name = o['Name'];
  if (typeof name === 'string') {
    const inject = INJECT[name];
    if (inject) {
      inject(o);
      injected.push(name);
    }
  }
  for (const v of Object.values(o)) walk(v);
};
walk(dashboard);
const missing = Object.keys(INJECT).filter((k) => !injected.includes(k));
if (missing.length > 0) throw new Error(`no item named ${missing.join(', ')}: the probe rows and the injections disagree`);
writeFileSync(djson, JSON.stringify(dashboard, null, 2));

const { path } = zipPackage(outDir, NAME);
console.log(`wrote ${written.folder} and ${path}: ${r} probes, ${injected.length} injected`);
