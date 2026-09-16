/**
 * Static checks on a DashPackage before it is written: things SimHub would reject or render
 * wrongly (bad colours, duplicate ids, impossible bindings, dangling widget files) are errors;
 * things that are probably mistakes (items off the canvas, fonts not bundled) are warnings.
 * The validator never throws on model content; it reports.
 */

import type { Binding, BindingTarget, DashPackage, Dashboard, DotStyle, Item, Screen } from './model.ts';
import { itemBounds } from './bounds.ts';
import { isHex } from './color.ts';
import { fontKey, fontNamesOf, subfamilyHasWeight, type TtfNames } from './fonts.ts';
import { dashboardPath, isGuid, screenPath, stableGuid, walkItems } from './ids.ts';
import { referencedProperties } from './ncalc.ts';
import { unknownFunctions } from './ncalcFunctions.ts';

export interface ValidateOptions {
  /**
   * Properties the plugin exposes, with or without the prefix (`OpenDash.ShiftLights` or
   * `ShiftLights`). Every `[<prefix>.X]` read by an expression must be one of them.
   */
  declaredProperties: string[];
  /** Property prefix, e.g. `OpenDash`. */
  propertyPrefix: string;
  /**
   * Declared properties this package may not read, with or without the prefix.
   *
   * A screen owns its settings, so a face must read its own group and no other's. Being declared is
   * not enough: every face's properties are declared, and a package reading a neighbour's would
   * validate cleanly and then move when somebody configured the screen beside it. There is no way
   * to notice that except by looking at two dashboards at once on a rig that has two.
   */
  foreignProperties?: string[];
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
/** `BorderColor` is not here: it lives on `BorderStyle`, so SimHub ignores it as an *item* binding.
 *  It is bindable on the `BorderStyle` sub-object, which this model does not emit; see ADR 0011. */
const DRAWABLE_TARGETS: BindingTarget[] = [...GEOMETRY_TARGETS, 'Visible', 'BackgroundColor', 'Opacity', 'BlinkEnabled'];

/** Ellipse fill and stroke. `BackgroundColor` on an ellipse is the DrawableItem background, not the fill. */
const ELLIPSE_TARGETS: BindingTarget[] = ['FillColor', 'EllipseColor'];

/** Which `Bindings` keys each item kind accepts. */
export const ALLOWED_BINDING_TARGETS: Record<Item['kind'], readonly BindingTarget[]> = {
  text: [...DRAWABLE_TARGETS, 'Text', 'TextColor', 'FontSize'],
  rect: DRAWABLE_TARGETS,
  ellipse: [...DRAWABLE_TARGETS, ...ELLIPSE_TARGETS],
  layer: ['Visible', 'Opacity', 'BlinkEnabled', 'Repetitions'],
  widget: [...GEOMETRY_TARGETS, 'Visible', 'InitialScreenIndex'],
  chart: [...DRAWABLE_TARGETS, 'CurrentValue', 'ChartEnabled', 'LineColor', 'Minimum', 'Maximum'],
  linearGauge: [...DRAWABLE_TARGETS, 'Value', 'Minimum', 'Maximum', 'GaugeColor', 'AlternateGaugeColor', 'UseAlternateStyle'],
  radar: [...DRAWABLE_TARGETS, 'Scale'],
  staticMap: DRAWABLE_TARGETS,
  webPage: [...DRAWABLE_TARGETS, 'StartAddress'],
  // `Image` is bindable in SimHub and is deliberately not offered until something needs it: a
  // telltale set draws one item per lamp with `Visible` bound, which is the same picture.
  image: DRAWABLE_TARGETS,
};

/** Targets a colour gradient (Mode 4) can drive. */
export const GRADIENT_TARGETS: readonly BindingTarget[] = ['TextColor', 'BackgroundColor', 'GaugeColor', 'AlternateGaugeColor', 'LineColor', ...ELLIPSE_TARGETS];

/** The kinds `rotation` is verified on. A Layer has no geometry; a widget's rotation is unverified, so it is refused too. */
export const ROTATABLE_KINDS: readonly Item['kind'][] = ['text', 'rect', 'ellipse'];

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

/** Slack for the canvas check of rotated items, whose bounds are floating point: a thousandth of a pixel. */
const EDGE_TOLERANCE = 1e-3;

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
  const bare = (p: string): string => (p.startsWith(prefix) ? p.slice(prefix.length) : p);
  const declared = new Set(ctx.opts.declaredProperties.map(bare));
  const foreign = new Set((ctx.opts.foreignProperties ?? []).map(bare));
  for (const ref of propertyReferences(expression)) {
    if (!ref.startsWith(prefix)) continue;
    const name = ref.slice(prefix.length);
    if (!declared.has(name)) {
      ctx.c.error('property/undeclared', path, `[${ref}] is not a declared ${ctx.opts.propertyPrefix} property`);
    } else if (foreign.has(name)) {
      ctx.c.error('property/another-screens', path, `[${ref}] belongs to another screen; this package may only read its own`);
    }
  }
};

/**
 * Every function an expression names has to be one SimHub dispatches, with an argument count it
 * dispatches on. Both halves matter: `left` is a real SimHub function and `left(x, 4)` still draws
 * nothing, because the engine matches on name *and* parameter count and attaches no delegate when
 * either is wrong. Nothing is reported at runtime, so this is the only place it can be caught.
 */
const checkFunctions = (ctx: Context, expression: string, path: string): void => {
  for (const bad of unknownFunctions(expression)) {
    ctx.c.error(bad.reason === 'unknown' ? 'expression/unknown-function' : 'expression/arity', path, `${bad.message}; SimHub evaluates the whole expression to nothing`);
  }
};

/**
 * Every binding on an item, the one inside its border included.
 *
 * A border's colour is bound in `BorderStyle`'s own `Bindings`, which is a different place in the
 * JSON and the same expression everywhere else: it has to go through the property and function
 * guards like any other, or a border is the one place a typo survives the build.
 */
const bindingsOf = (item: Item): { target: string; binding: Binding; bpath: string; nested: boolean }[] => {
  const all = Object.entries(item.bindings ?? {})
    .filter(([, binding]) => binding)
    .map(([target, binding]) => ({ target, binding: binding as Binding, bpath: `#Bindings.${target}`, nested: false }));
  const border = 'border' in item ? item.border : undefined;
  if (border?.colorBinding) {
    all.push({ target: 'BorderColor', binding: border.colorBinding, bpath: '#BorderStyle.Bindings.BorderColor', nested: true });
  }
  return all;
};

const checkBindings = (ctx: Context, item: Item, path: string): void => {
  const { c } = ctx;
  const allowed = ALLOWED_BINDING_TARGETS[item.kind];
  for (const { target, binding, bpath: suffix, nested } of bindingsOf(item)) {
    const bpath = `${path}${suffix}`;
    if (!nested && !allowed.includes(target as BindingTarget)) {
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
      if (binding.formula === expression || (typeof binding.formula === 'object' && binding.formula !== null && (binding.formula as { interpreter?: string }).interpreter !== 'js')) {
        checkFunctions(ctx, expression, bpath);
      }
    }
  }
};

const checkDotStyle = (ctx: Context, path: string, property: string, style: DotStyle | undefined): void => {
  if (!style) return;
  const { c } = ctx;
  for (const k of ['labelColor', 'dotColor', 'dotBorderColor'] as const) checkColor(c, path, `${property}.${k}`, style[k], false);
  for (const k of ['labelFontSize', 'dotBorderThickness', 'dotRadius'] as const) checkNumber(c, path, `${property}.${k}`, style[k]);
  if (style.labelFont !== undefined && style.labelFont.trim() === '') c.error('font/empty', `${path}#${property}.labelFont`, 'label font is empty');
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
  const rotationOk = checkNumber(c, path, 'rotation', item.rotation);
  if (item.rotation !== undefined && item.rotation !== 0 && !ROTATABLE_KINDS.includes(item.kind)) {
    c.error('rotation/unsupported', `${path}#rotation`, `a ${item.kind} item cannot be rotated`);
  }

  if (item.kind === 'layer') {
    if (item.backgroundColor !== undefined) checkColor(c, path, 'backgroundColor', item.backgroundColor, false);
    const repeats = (item.repetitions ?? 0) > 0 || item.bindings?.Repetitions !== undefined;
    if (checkNumber(c, path, 'repetitions', item.repetitions) && item.repetitions !== undefined && (!Number.isInteger(item.repetitions) || item.repetitions < 0)) {
      c.error('layer/repetitions', `${path}#repetitions`, `repetitions must be a non-negative integer, got ${item.repetitions}`);
    }
    checkNumber(c, path, 'repeatTopOffset', item.repeatTopOffset);
    checkNumber(c, path, 'repeatLeftOffset', item.repeatLeftOffset);
    if (!repeats && (item.repeatTopOffset !== undefined || item.repeatLeftOffset !== undefined)) {
      c.warn('layer/repeat-offset-unused', path, 'a repeat offset is set on a layer that does not repeat');
    }
    // SimHub drops WidgetItems from the copies it stamps, so a repeated row would show its
    // widget once and then nothing.
    if (repeats) {
      let widget: string | undefined;
      walkItems(item.children, path, (v) => {
        if (v.item.kind === 'widget' && widget === undefined) widget = v.path;
      });
      if (widget !== undefined) c.error('layer/repeated-widget', widget, 'SimHub removes widget items from a repeated layer\'s copies');
    }
    return;
  }

  checkColor(c, path, 'backgroundColor', item.backgroundColor, false);
  const r = item.rect;
  const geometryOk = ['left', 'top', 'width', 'height'].every((k) => checkNumber(c, path, `rect.${k}`, r[k as keyof typeof r]));
  if (geometryOk) {
    if (r.width < 0 || r.height < 0) c.error('size/negative', `${path}#rect`, `size ${r.width}x${r.height} is negative`);
    else if (r.width === 0 || r.height === 0) c.warn('size/empty', `${path}#rect`, `size ${r.width}x${r.height} draws nothing`);
    // A rotated item's footprint is the bounds of its rotated rect, so a segment on a round face
    // whose unrotated box pokes past the edge is not reported when its pixels stay inside.
    const b = rotationOk ? itemBounds(item) : r;
    if (b.left < -EDGE_TOLERANCE || b.top < -EDGE_TOLERANCE || b.left + b.width > dashboard.width + EDGE_TOLERANCE || b.top + b.height > dashboard.height + EDGE_TOLERANCE) {
      c.warn(
        'geometry/outside-canvas',
        `${path}#rect`,
        `rect (${r.left}, ${r.top}, ${r.width}x${r.height})${item.rotation ? ` rotated ${item.rotation} deg` : ''} leaves the ${dashboard.width}x${dashboard.height} canvas`,
      );
    }
  }

  if (item.kind === 'ellipse') {
    checkColor(c, path, 'fillColor', item.fillColor, true);
    checkColor(c, path, 'strokeColor', item.strokeColor, true);
    if (checkNumber(c, path, 'strokeThickness', item.strokeThickness) && item.strokeThickness < 0) {
      c.error('ellipse/thickness', `${path}#strokeThickness`, `stroke thickness must not be negative, got ${item.strokeThickness}`);
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

  if (item.kind === 'chart') {
    checkColor(c, path, 'lineColor', item.lineColor, true);
    for (const k of ['currentValue', 'minimum', 'maximum', 'lineThickness', 'pointsCount'] as const) checkNumber(c, path, k, item[k]);
    if (item.pointsCount !== undefined && item.pointsCount < 2) c.error('chart/points', `${path}#pointsCount`, `a trace needs at least 2 points, got ${item.pointsCount}`);
  }

  if (item.kind === 'linearGauge') {
    checkColor(c, path, 'gaugeColor', item.gaugeColor, true);
    checkColor(c, path, 'alternateGaugeColor', item.alternateGaugeColor, false);
    for (const k of ['minimum', 'maximum', 'value', 'steps'] as const) checkNumber(c, path, k, item[k]);
    if (item.minimum !== undefined && item.maximum !== undefined && item.minimum === item.maximum) {
      c.error('gauge/empty-range', `${path}#maximum`, 'minimum and maximum are equal, so the gauge has no range');
    }
  }

  if (item.kind === 'radar') {
    if (checkNumber(c, path, 'scale', item.scale) && item.scale !== undefined && item.scale <= 0) {
      c.error('radar/scale', `${path}#scale`, `scale must be positive, got ${item.scale}`);
    }
    checkDotStyle(ctx, path, 'playerStyle', item.playerStyle);
    checkDotStyle(ctx, path, 'opponentStyle', item.opponentStyle);
  }

  if (item.kind === 'staticMap') {
    checkColor(c, path, 'trackColor', item.trackColor, true);
    checkColor(c, path, 'trackBorderColor', item.trackBorderColor, true);
    checkColor(c, path, 'alternateTrackSectorColor', item.alternateTrackSectorColor, false);
    for (const k of ['trackWidth', 'trackBorderWidth', 'minimumTrackWidth', 'minimumTrackBorderWidth'] as const) checkNumber(c, path, k, item[k]);
    checkDotStyle(ctx, path, 'playerStyle', item.playerStyle);
    checkDotStyle(ctx, path, 'opponentStyle', item.opponentStyle);
    if (item.startLine) {
      checkColor(c, path, 'startLine.color', item.startLine.color, false);
      checkNumber(c, path, 'startLine.width', item.startLine.width);
      checkNumber(c, path, 'startLine.height', item.startLine.height);
    }
  }

  if (item.kind === 'image') {
    // A name that is not in the dashboard's own Images list draws nothing at all, and SimHub
    // reports nothing when it happens, so it has to be an error here rather than a blank box
    // somebody notices on the rig.
    const declared = (dashboard.images ?? []).some((image) => image.name === item.image);
    if (!declared) c.error('image/missing', `${path}#image`, `${JSON.stringify(item.image)} is not an image of ${dashboard.name}.djson`);
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
  if (screen.enabledExpression) {
    // Both checks, not only the property one. A screen's enable expression is the one expression
    // that costs the whole screen when it is wrong: SimHub evaluates an expression naming a
    // function it does not dispatch to nothing, which reads as false, so the screen simply never
    // appears and nothing anywhere says why. Every companion screen carries one of these.
    checkProperties(ctx, screen.enabledExpression, `${path}#enabledExpression`);
    checkFunctions(ctx, screen.enabledExpression, `${path}#enabledExpression`);
  }
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

/**
 * A dashboard's image declarations. The name is what an item references and what the
 * `.ressources` entry is called, so two images of the same name would silently become one entry
 * and one of the two items would draw the other's artwork.
 */
const checkImages = (ctx: Context, dashboard: Dashboard, path: string): void => {
  const { c } = ctx;
  const images = dashboard.images;
  if (images === undefined) return;
  if (!Array.isArray(images)) {
    c.error('image/not-a-list', `${path}#images`, 'images must be a list');
    return;
  }
  const names = new Set<string>();
  for (const image of images) {
    const at = `${path}#images.${image?.name ?? '?'}`;
    if (!image || typeof image.name !== 'string' || image.name.trim() === '') {
      c.error('image/name-empty', at, 'an image has no name');
      continue;
    }
    if (names.has(image.name)) c.error('name/duplicate', at, `image name ${JSON.stringify(image.name)} is used twice`);
    names.add(image.name);
    if (!image.extension.startsWith('.')) c.error('image/extension', at, `extension ${JSON.stringify(image.extension)} does not begin with a dot`);
    for (const k of ['width', 'height', 'length'] as const) {
      if (checkNumber(c, at, k, image[k]) && image[k] <= 0) c.error('image/size', `${at}.${k}`, `${k} must be positive`);
    }
    if (!/^[0-9a-f]{32}$/.test(image.md5)) c.error('image/md5', `${at}.md5`, 'md5 is not 32 lowercase hex characters');
  }
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
  checkImages(ctx, dashboard, path);
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
