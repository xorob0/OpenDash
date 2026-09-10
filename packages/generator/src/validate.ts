/**
 * Static checks on a DashPackage before it is written: things SimHub would reject or render
 * wrongly (bad colours, duplicate ids, impossible bindings, dangling widget files) are errors;
 * things that are probably mistakes (items off the canvas, fonts not bundled) are warnings.
 * The validator never throws on model content; it reports.
 */

import type { Binding, BindingTarget, DashPackage, Dashboard, Item, Screen } from './model.ts';
import { isHex } from './color.ts';
import { fontKey, fontNamesOf, subfamilyHasWeight, type TtfNames } from './fonts.ts';
import { dashboardPath, isGuid, screenPath, stableGuid, walkItems } from './ids.ts';
import { referencedProperties } from './ncalc.ts';

export interface ValidateOptions {
  /**
   * Properties the plugin exposes, with or without the prefix (`OpenDash.ShiftLights` or
   * `ShiftLights`). Every `[<prefix>.X]` read by an expression must be one of them.
   */
  declaredProperties: string[];
  /** Property prefix, e.g. `OpenDash`. */
  propertyPrefix: string;
}

export interface ValidationIssue {
  /** Stable machine readable code, e.g. `color/invalid`. */
  code: string;
  /** Where: `<package>/<dashboard>/<screen>/<item>` plus a `#Property` suffix when relevant. */
  path: string;
  message: string;
}

export interface ValidationResult {
  ok: boolean;
  errors: ValidationIssue[];
  warnings: ValidationIssue[];
}

const GEOMETRY_TARGETS: BindingTarget[] = ['Left', 'Top', 'Width', 'Height'];
/** `BorderColor` is not here: it lives on `BorderStyle`, so SimHub ignores it as an item binding. */
const DRAWABLE_TARGETS: BindingTarget[] = [...GEOMETRY_TARGETS, 'Visible', 'BackgroundColor', 'Opacity', 'BlinkEnabled'];

/** Which `Bindings` keys each item kind accepts. */
export const ALLOWED_BINDING_TARGETS: Record<Item['kind'], readonly BindingTarget[]> = {
  text: [...DRAWABLE_TARGETS, 'Text', 'TextColor', 'FontSize'],
  rect: DRAWABLE_TARGETS,
  layer: ['Visible', 'Opacity', 'BlinkEnabled'],
  widget: [...GEOMETRY_TARGETS, 'Visible', 'InitialScreenIndex'],
};

/** Targets a colour gradient (Mode 4) can drive. */
export const GRADIENT_TARGETS: readonly BindingTarget[] = ['TextColor', 'BackgroundColor'];

/**
 * Every property an expression reads: `[Plugin.Name]` in NCalc and `$prop('Plugin.Name')` in
 * JavaScript.
 */
export const propertyReferences = (expression: string): string[] => {
  const out = referencedProperties(expression);
  const js = /\$prop\(\s*['"]([A-Za-z0-9_.]+)['"]\s*\)/g;
  let m: RegExpExecArray | null;
  while ((m = js.exec(expression)) !== null) out.push(m[1] ?? '');
  return out;
};

const formulaExpressions = (binding: Binding): unknown[] => {
  const f: unknown = binding.formula;
  if (typeof f === 'string') return [f];
  if (f === null || typeof f !== 'object') return [f];
  const o = f as { expression?: unknown; preExpression?: unknown };
  return o.preExpression !== undefined && o.preExpression !== null ? [o.expression, o.preExpression] : [o.expression];
};

class Collector {
  readonly errors: ValidationIssue[] = [];
  readonly warnings: ValidationIssue[] = [];
  error(code: string, path: string, message: string): void {
    this.errors.push({ code, path, message });
  }
  warn(code: string, path: string, message: string): void {
    this.warnings.push({ code, path, message });
  }
}

const finite = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value);

const checkColor = (c: Collector, path: string, property: string, value: unknown, required: boolean): void => {
  if (value === undefined) {
    if (required) c.error('color/missing', `${path}#${property}`, `${property} is required`);
    return;
  }
  if (!isHex(value)) c.error('color/invalid', `${path}#${property}`, `${property} ${JSON.stringify(value)} is not #RRGGBB or #AARRGGBB`);
};

const checkNumber = (c: Collector, path: string, property: string, value: unknown): boolean => {
  if (value === undefined) return true;
  if (!finite(value)) {
    c.error('number/invalid', `${path}#${property}`, `${property} must be a finite number, got ${String(value)}`);
    return false;
  }
  return true;
};

interface FontRef {
  family: string;
  weight: string;
  path: string;
}

interface Context {
  c: Collector;
  opts: ValidateOptions;
  pkg: DashPackage;
  /** `<dashboard>.djson` file names (lower-cased) mapped to their dashboards. */
  files: Map<string, Dashboard>;
  ids: Map<string, string>;
  fonts: FontRef[];
}

const checkId = (ctx: Context, id: string, explicit: boolean, path: string): void => {
  if (explicit && !isGuid(id)) ctx.c.error('id/invalid', path, `id ${JSON.stringify(id)} is not a lower-case GUID`);
  const other = ctx.ids.get(id);
  if (other !== undefined) ctx.c.error('id/duplicate', path, `id ${id} is already used by ${other}`);
  else ctx.ids.set(id, path);
};

const checkProperties = (ctx: Context, expression: string, path: string): void => {
  const prefix = `${ctx.opts.propertyPrefix}.`;
  const declared = new Set(
    ctx.opts.declaredProperties.map((p) => (p.startsWith(prefix) ? p.slice(prefix.length) : p)),
  );
  for (const ref of propertyReferences(expression)) {
    if (!ref.startsWith(prefix)) continue;
    const name = ref.slice(prefix.length);
    if (!declared.has(name)) {
      ctx.c.error('property/undeclared', path, `[${ref}] is not a declared ${ctx.opts.propertyPrefix} property`);
    }
  }
};

const checkBindings = (ctx: Context, item: Item, path: string): void => {
  const { c } = ctx;
  const allowed = ALLOWED_BINDING_TARGETS[item.kind];
  for (const [target, binding] of Object.entries(item.bindings ?? {})) {
    if (!binding) continue;
    const bpath = `${path}#Bindings.${target}`;
    if (!allowed.includes(target as BindingTarget)) {
      c.error('binding/unknown-target', bpath, `${target} cannot be bound on a ${item.kind} item`);
    }
    if (binding.mode === 'gradient') {
      if (!GRADIENT_TARGETS.includes(target as BindingTarget)) {
        c.error('binding/gradient-target', bpath, `a gradient can only drive a colour, not ${target}`);
      }
      checkColor(c, bpath, 'startColor', binding.startColor, true);
      checkColor(c, bpath, 'endColor', binding.endColor, true);
      checkColor(c, bpath, 'middleColor', binding.middleColor, false);
      checkNumber(c, bpath, 'startValue', binding.startValue);
      checkNumber(c, bpath, 'endValue', binding.endValue);
      checkNumber(c, bpath, 'middleValue', binding.middleValue);
    } else if (binding.mode !== 'formula') {
      c.error('binding/unknown-mode', bpath, `unknown binding mode ${JSON.stringify((binding as { mode: unknown }).mode)}`);
    }
    for (const expression of formulaExpressions(binding)) {
      if (typeof expression !== 'string') {
        c.error('binding/invalid-expression', bpath, 'expression must be a string');
        continue;
      }
      if (expression.trim() === '') c.warn('binding/empty-expression', bpath, `${target} is bound to an empty expression`);
      checkProperties(ctx, expression, bpath);
    }
  }
};

const checkItem = (ctx: Context, item: Item, path: string, id: string, dashboard: Dashboard, ownFile: string): void => {
  const { c } = ctx;
  if (typeof item.name !== 'string' || item.name.trim() === '') c.error('name/empty', path, 'item name is empty');
  checkId(ctx, id, item.id !== undefined, path);
  if (item.opacity !== undefined && checkNumber(c, path, 'opacity', item.opacity) && (item.opacity < 0 || item.opacity > 100)) {
    c.error('opacity/range', `${path}#opacity`, `opacity ${item.opacity} is outside 0..100`);
  }
  if (item.blink?.delayMs !== undefined && checkNumber(c, path, 'blink.delayMs', item.blink.delayMs) && item.blink.delayMs <= 0) {
    c.error('blink/delay', `${path}#blink.delayMs`, `blink delay must be positive, got ${item.blink.delayMs}`);
  }
  checkBindings(ctx, item, path);

  if (item.kind === 'layer') {
    if (item.backgroundColor !== undefined) checkColor(c, path, 'backgroundColor', item.backgroundColor, false);
    return;
  }

  checkColor(c, path, 'backgroundColor', item.backgroundColor, false);
  const r = item.rect;
  const geometryOk = ['left', 'top', 'width', 'height'].every((k) => checkNumber(c, path, `rect.${k}`, r[k as keyof typeof r]));
  if (geometryOk) {
    if (r.width < 0 || r.height < 0) c.error('size/negative', `${path}#rect`, `size ${r.width}x${r.height} is negative`);
    else if (r.width === 0 || r.height === 0) c.warn('size/empty', `${path}#rect`, `size ${r.width}x${r.height} draws nothing`);
    if (r.left < 0 || r.top < 0 || r.left + r.width > dashboard.width || r.top + r.height > dashboard.height) {
      c.warn(
        'geometry/outside-canvas',
        `${path}#rect`,
        `rect (${r.left}, ${r.top}, ${r.width}x${r.height}) leaves the ${dashboard.width}x${dashboard.height} canvas`,
      );
    }
  }

  if (item.kind === 'text' || item.kind === 'rect') {
    const b = item.border;
    if (b) {
      checkColor(c, path, 'border.color', b.color, false);
      for (const k of ['top', 'bottom', 'left', 'right'] as const) checkNumber(c, path, `border.${k}`, b[k]);
      if (typeof b.radius === 'object') {
        for (const k of ['topLeft', 'topRight', 'bottomLeft', 'bottomRight'] as const) checkNumber(c, path, `border.radius.${k}`, b.radius[k]);
      } else checkNumber(c, path, 'border.radius', b.radius);
    }
  }

  if (item.kind === 'text') {
    checkColor(c, path, 'textColor', item.textColor, true);
    if (checkNumber(c, path, 'fontSize', item.fontSize) && item.fontSize <= 0) {
      c.error('font/size', `${path}#fontSize`, `font size must be positive, got ${item.fontSize}`);
    }
    if (typeof item.font !== 'string' || item.font.trim() === '') c.error('font/empty', `${path}#font`, 'font family is empty');
    else ctx.fonts.push({ family: item.font, weight: item.fontWeight, path });
    if (item.monospace) {
      checkNumber(c, path, 'monospace.charWidth', item.monospace.charWidth);
      checkNumber(c, path, 'monospace.specialCharsWidth', item.monospace.specialCharsWidth);
    }
    if (item.padding) {
      for (const k of ['top', 'bottom', 'left', 'right'] as const) checkNumber(c, path, `padding.${k}`, item.padding[k]);
    }
  }

  if (item.kind === 'widget') {
    const key = item.fileName.toLowerCase();
    const target = ctx.files.get(key);
    if (!target) {
      c.error('widget/missing-file', `${path}#fileName`, `${item.fileName} is not a dashboard of this package`);
    } else {
      if (`${target.name}.djson` !== item.fileName) {
        c.warn('widget/file-case', `${path}#fileName`, `${item.fileName} differs in case from ${target.name}.djson`);
      }
      if (key === ownFile.toLowerCase()) c.error('widget/self-reference', `${path}#fileName`, `${item.fileName} embeds its own dashboard`);
      if (checkNumber(c, path, 'initialScreenIndex', item.initialScreenIndex)) {
        const n = target.screens.length;
        if (!Number.isInteger(item.initialScreenIndex) || item.initialScreenIndex < 0 || item.initialScreenIndex >= n) {
          c.error('widget/screen-index', `${path}#initialScreenIndex`, `screen ${item.initialScreenIndex} does not exist in ${item.fileName} (${n} screens)`);
        }
      }
    }
  }
};

const checkScreen = (ctx: Context, screen: Screen, dashboard: Dashboard): void => {
  const { c } = ctx;
  const path = screenPath(ctx.pkg.folderName, dashboard.name, screen.name);
  if (typeof screen.name !== 'string' || screen.name.trim() === '') c.error('name/empty', path, 'screen name is empty');
  checkId(ctx, screen.id ?? stableGuid(path), screen.id !== undefined, path);
  checkColor(c, path, 'backgroundColor', screen.backgroundColor, false);
  if (screen.enabledExpression) checkProperties(ctx, screen.enabledExpression, `${path}#enabledExpression`);
  if (!Array.isArray(screen.items) || screen.items.length === 0) {
    c.error('screen/no-items', path, 'screen has no items');
    return;
  }
  const names = new Map<string, string>();
  walkItems(screen.items, path, (v) => {
    const other = names.get(v.item.name);
    if (other !== undefined) c.error('name/duplicate', v.path, `item name ${JSON.stringify(v.item.name)} is already used by ${other} in this screen`);
    else names.set(v.item.name, v.path);
    checkItem(ctx, v.item, v.path, v.id, dashboard, `${dashboard.name}.djson`);
  });
};

const checkDashboard = (ctx: Context, dashboard: Dashboard): void => {
  const { c } = ctx;
  const path = dashboardPath(ctx.pkg.folderName, dashboard.name);
  if (typeof dashboard.name !== 'string' || dashboard.name.trim() === '') c.error('name/empty', path, 'dashboard name is empty');
  checkId(ctx, dashboard.id ?? stableGuid(path), dashboard.id !== undefined, path);
  checkColor(c, path, 'backgroundColor', dashboard.backgroundColor, true);
  for (const k of ['width', 'height'] as const) {
    if (checkNumber(c, path, k, dashboard[k]) && dashboard[k] <= 0) c.error('size/negative', `${path}#${k}`, `${k} must be positive`);
  }
  if (!dashboard.metadata) c.error('metadata/missing', path, 'dashboard has no metadata');
  else {
    if (!dashboard.metadata.version) c.error('metadata/version', `${path}#metadata.version`, 'metadata.version is empty');
    if (!dashboard.metadata.simHubVersion) c.error('metadata/simhub-version', `${path}#metadata.simHubVersion`, 'metadata.simHubVersion is empty');
    const preview = dashboard.metadata.mainPreviewIndex;
    if (preview !== undefined && (!Number.isInteger(preview) || preview < 0 || preview >= dashboard.screens.length)) {
      c.error('metadata/preview-index', `${path}#metadata.mainPreviewIndex`, `screen ${preview} does not exist`);
    }
  }
  if (!Array.isArray(dashboard.screens) || dashboard.screens.length === 0) {
    c.error('dashboard/no-screens', path, 'dashboard has no screens');
    return;
  }
  const screenNames = new Set<string>();
  for (const screen of dashboard.screens) {
    if (screenNames.has(screen.name)) c.error('name/duplicate', screenPath(ctx.pkg.folderName, dashboard.name, screen.name), `screen name ${JSON.stringify(screen.name)} is used twice`);
    screenNames.add(screen.name);
    checkScreen(ctx, screen, dashboard);
  }
};

const checkFonts = (ctx: Context): void => {
  if (ctx.fonts.length === 0) return;
  const bundled: { path: string; names: TtfNames }[] = ctx.pkg.fonts.map((p) => ({ path: p, names: fontNamesOf(p) }));
  const reported = new Set<string>();
  for (const ref of ctx.fonts) {
    const key = fontKey(ref.family);
    const familyMatches = bundled.filter(
      (f) => fontKey(f.names.family) === key || fontKey(f.names.legacyFamily) === key || fontKey(f.path.split(/[\\/]/).pop() ?? '').includes(key),
    );
    const reportKey = `${key}|${ref.weight}`;
    if (familyMatches.length === 0) {
      if (reported.has(key)) continue;
      reported.add(key);
      ctx.c.warn('font/missing', `${ref.path}#font`, `font family ${JSON.stringify(ref.family)} is not bundled in the package fonts`);
      continue;
    }
    if (reported.has(reportKey)) continue;
    if (!familyMatches.some((f) => subfamilyHasWeight(f.names.subfamily, ref.weight))) {
      reported.add(reportKey);
      ctx.c.warn('font/weight-missing', `${ref.path}#fontWeight`, `no bundled ${JSON.stringify(ref.family)} file provides weight ${ref.weight}`);
    }
  }
};

/** Validates a package. `errors` block the build; `warnings` are printed. */
export const validatePackage = (pkg: DashPackage, opts: ValidateOptions): ValidationResult => {
  const c = new Collector();
  const ctx: Context = { c, opts, pkg, files: new Map(), ids: new Map(), fonts: [] };
  const root = pkg.folderName;

  if (typeof root !== 'string' || root.trim() === '') c.error('package/folder-name', root, 'folderName is empty');
  else if (/[\\/:*?"<>|]/.test(root)) c.error('package/folder-name', root, `folderName ${JSON.stringify(root)} contains characters not allowed in a file name`);

  if (!Array.isArray(pkg.dashboards) || pkg.dashboards.length === 0) {
    c.error('package/empty', root, 'package has no dashboards');
    return { ok: false, errors: c.errors, warnings: c.warnings };
  }
  const main = pkg.dashboards[0];
  if (main && main.name !== root) {
    c.error('package/main-name', dashboardPath(root, main.name), `the first dashboard must be named ${JSON.stringify(root)} (the folder name), got ${JSON.stringify(main.name)}`);
  }
  for (const d of pkg.dashboards) {
    const key = `${d.name}.djson`.toLowerCase();
    if (ctx.files.has(key)) c.error('package/duplicate-dashboard', dashboardPath(root, d.name), `dashboard ${JSON.stringify(d.name)} is defined twice`);
    else ctx.files.set(key, d);
    if (/[\\/:*?"<>|]/.test(d.name)) c.error('name/invalid', dashboardPath(root, d.name), `dashboard name ${JSON.stringify(d.name)} contains characters not allowed in a file name`);
  }
  for (const d of pkg.dashboards) checkDashboard(ctx, d);

  const fontFiles = new Set<string>();
  for (const f of pkg.fonts ?? []) {
    const base = f.split(/[\\/]/).pop() ?? f;
    if (fontFiles.has(base.toLowerCase())) c.error('font/duplicate-file', `${root}/_SHFonts/${base}`, `two fonts would both be written as ${base}`);
    fontFiles.add(base.toLowerCase());
  }
  checkFonts(ctx);

  return { ok: c.errors.length === 0, errors: c.errors, warnings: c.warnings };
};

/** `code path: message` lines, for build logs. */
export const formatIssues = (issues: ValidationIssue[]): string => issues.map((i) => `${i.code} ${i.path}: ${i.message}`).join('\n');
