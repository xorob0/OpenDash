/**
 * A `.djson` document as a typed scene: what the demo (#395) draws, read from the same JSON the
 * plugin installs.
 *
 * The key names are SimHub's own, as `packages/generator/src/serialize.ts` writes them and
 * `docs/research/simhub-dash-format.md` records them. Defaults are SimHub's where a key is left out
 * (`Opacity` 100, `BlinkDelay` 250, `RepeatTopOffset` 30, `CharWidth` 40, `SpecialCharsWidth` 20,
 * `SpecialChars` `.,:;`), so a document written by DashStudio rather than by the generator reads
 * the same way.
 *
 * Nothing here evaluates anything. A binding keeps its expression as text; `engine.ts` computes it.
 * An item whose `$type` is not one of the twelve the generator writes is kept as `unknown`, so the
 * renderer can draw it as a labelled box rather than drop it.
 */

/** A colour ready for a canvas, and its alpha so a transparent fill can be skipped. */
export interface Paint {
  readonly css: string;
  readonly alpha: number;
}

export const TRANSPARENT: Paint = { css: 'rgba(0,0,0,0)', alpha: 0 };

const paintCache = new Map<string, Paint | null>();

/**
 * A colour as WPF's `ColorConverter` reads one: `#AARRGGBB`, `#RRGGBB`, `#ARGB`, `#RGB`, or a named
 * colour. Null when the text is none of those, which is the case where SimHub's setter throws and
 * the item keeps the colour it had.
 */
export function parsePaint(text: string): Paint | null {
  const cached = paintCache.get(text);
  if (cached !== undefined) return cached;
  const result = computePaint(text.trim());
  if (paintCache.size < 4096) paintCache.set(text, result);
  return result;
}

function computePaint(t: string): Paint | null {
  if (t.startsWith('#')) {
    let hex = t.slice(1);
    if (!/^[0-9a-f]+$/i.test(hex)) return null;
    if (hex.length === 3) hex = `f${hex}`;
    if (hex.length === 4) hex = hex.split('').map((c) => c + c).join('');
    if (hex.length === 6) hex = `ff${hex}`;
    if (hex.length !== 8) return null;
    const n = (i: number) => parseInt(hex.slice(i, i + 2), 16);
    const a = n(0) / 255;
    return { css: `rgba(${n(2)},${n(4)},${n(6)},${Number(a.toFixed(4))})`, alpha: a };
  }
  // WPF's named colours are CSS's, give or take a handful nobody writes; a canvas takes the name.
  if (/^[a-z]+$/i.test(t)) return t.toLowerCase() === 'transparent' ? TRANSPARENT : { css: t.toLowerCase(), alpha: 1 };
  return null;
}

const paintOf = (v: unknown, fallback: Paint = TRANSPARENT): Paint => (typeof v === 'string' ? (parsePaint(v) ?? fallback) : fallback);

export interface Gradient {
  readonly start: Paint;
  readonly startValue: number;
  readonly end: Paint;
  readonly endValue: number;
  readonly middle: Paint | null;
  readonly middleValue: number;
}

/** One entry of `Bindings`: the target property, the expression, and how its value is applied. */
export interface SceneBinding {
  readonly target: string;
  readonly expression: string;
  readonly preExpression: string | null;
  /** Mode 2 writes the value; Mode 4 maps it onto a colour ramp. */
  readonly mode: 'formula' | 'gradient';
  readonly gradient: Gradient | null;
  readonly formatString: string | null;
  /** `Interpreter: 1`. No package writes one, and the demo cannot run JavaScript, so it says so. */
  readonly javascript: boolean;
}

export interface Blink {
  readonly enabled: boolean;
  /** Half period, in milliseconds. */
  readonly delay: number;
  readonly inverted: boolean;
}

export interface Border {
  readonly color: Paint;
  readonly top: number;
  readonly bottom: number;
  readonly left: number;
  readonly right: number;
  readonly radius: { readonly topLeft: number; readonly topRight: number; readonly bottomRight: number; readonly bottomLeft: number };
  readonly colorBinding: SceneBinding | null;
}

export interface Box {
  readonly left: number;
  readonly top: number;
  readonly width: number;
  readonly height: number;
}

interface Base {
  readonly id: string;
  readonly name: string;
  readonly visible: boolean;
  /** 0 to 1. */
  readonly opacity: number;
  readonly blink: Blink;
  readonly bindings: readonly SceneBinding[];
}

interface Drawable extends Base {
  readonly box: Box;
  /** Degrees clockwise about the centre. */
  readonly rotation: number;
  readonly background: Paint;
  readonly border: Border | null;
}

export interface Monospace {
  readonly charWidth: number;
  readonly specialCharsWidth: number;
  readonly specialChars: string;
}

export interface TextItem extends Drawable {
  readonly kind: 'text';
  readonly font: string;
  readonly weight: string;
  readonly italic: boolean;
  readonly size: number;
  readonly text: string;
  readonly color: Paint;
  readonly hAlign: 'left' | 'center' | 'right';
  readonly vAlign: 'top' | 'center' | 'bottom';
  readonly padding: { readonly top: number; readonly bottom: number; readonly left: number; readonly right: number };
  readonly monospace: Monospace | null;
  readonly wrap: boolean;
}

export interface RectItem extends Drawable {
  readonly kind: 'rect';
}

export interface EllipseItem extends Drawable {
  readonly kind: 'ellipse';
  readonly fill: Paint;
  readonly stroke: Paint;
  readonly thickness: number;
}

export interface LayerItem extends Base {
  readonly kind: 'layer';
  readonly children: readonly Item[];
  /** Extra copies: the layer is drawn `repetitions + 1` times. */
  readonly repetitions: number;
  readonly repeatTop: number;
  readonly repeatLeft: number;
}

export interface WidgetItem extends Drawable {
  readonly kind: 'widget';
  readonly fileName: string;
  readonly initialScreenIndex: number;
  readonly autoSize: boolean;
}

export interface ChartItem extends Drawable {
  readonly kind: 'chart';
  readonly currentValue: number;
  readonly minimum: number;
  readonly maximum: number;
  readonly useMinimum: boolean;
  readonly useMaximum: boolean;
  readonly lineColor: Paint;
  readonly lineThickness: number;
  readonly pointsCount: number;
  readonly enabled: boolean;
  readonly suspended: boolean;
}

export interface GaugeItem extends Drawable {
  readonly kind: 'gauge';
  readonly vertical: boolean;
  readonly alignment: 'start' | 'center' | 'end';
  readonly gaugeColor: Paint;
  readonly alternateColor: Paint;
  readonly useAlternate: boolean;
  readonly minimum: number;
  readonly maximum: number;
  readonly value: number;
  readonly steps: number;
}

export interface ImageItem extends Drawable {
  readonly kind: 'image';
  readonly image: string;
}

/** The kinds the demo draws as a labelled box: they need data a trace does not carry, or a browser. */
export interface StandInItem extends Drawable {
  readonly kind: 'standIn';
  /** What SimHub would draw here, in a word or two. */
  readonly label: string;
  /** The `$type`, short. */
  readonly type: string;
  /** An image-from-file's `ImagePath`, and a web page's `StartAddress`, as written; empty otherwise. */
  readonly imagePath: string;
  readonly startAddress: string;
}

export type Item = TextItem | RectItem | EllipseItem | LayerItem | WidgetItem | ChartItem | GaugeItem | ImageItem | StandInItem;
export type DrawableItem = Exclude<Item, LayerItem>;

export interface Variable {
  readonly name: string;
  readonly expression: string;
  readonly beforeScreenRoles: boolean;
  readonly evaluateOnlyOnce: boolean;
}

export interface Screen {
  readonly name: string;
  readonly inGame: boolean;
  readonly idle: boolean;
  readonly pit: boolean;
  /** Empty means enabled. */
  readonly enabledExpression: string;
  readonly background: Paint;
  readonly items: readonly Item[];
}

export interface ImageDescriptor {
  readonly name: string;
  readonly extension: string;
  readonly width: number;
  readonly height: number;
}

export interface SceneDashboard {
  /** The file it was read from, e.g. `OpenDash.djson`. */
  readonly file: string;
  readonly width: number;
  readonly height: number;
  readonly background: Paint;
  readonly screens: readonly Screen[];
  readonly variables: readonly Variable[];
  readonly images: readonly ImageDescriptor[];
  /** The `$type`s met that the demo does not know, for the page to name. */
  readonly unknownTypes: readonly string[];
}

export class SceneError extends Error {
  override name = 'SceneError';
}

type Json = Record<string, unknown>;

const isObject = (v: unknown): v is Json => typeof v === 'object' && v !== null && !Array.isArray(v);
const num = (v: unknown, fallback: number): number => (typeof v === 'number' && Number.isFinite(v) ? v : fallback);
const bool = (v: unknown, fallback: boolean): boolean => (typeof v === 'boolean' ? v : fallback);
const str = (v: unknown, fallback: string): string => (typeof v === 'string' ? v : fallback);
const list = (v: unknown): unknown[] => (Array.isArray(v) ? v : []);

const H_ALIGN = ['left', 'center', 'right'] as const;
const V_ALIGN = ['top', 'center', 'bottom'] as const;
const GAUGE_ALIGN = ['start', 'center', 'end'] as const;

function parseBinding(target: string, raw: unknown): SceneBinding | null {
  if (!isObject(raw)) return null;
  const formula = isObject(raw.Formula) ? raw.Formula : {};
  const mode = raw.Mode === 4 ? 'gradient' : 'formula';
  const gradient: Gradient | null =
    mode === 'gradient'
      ? {
          start: paintOf(raw.StartColor),
          startValue: num(raw.StartColorValue, 0),
          end: paintOf(raw.EndColor),
          endValue: num(raw.EndColorValue, 1),
          middle: raw.EnableMiddleColor === true ? paintOf(raw.MiddleColor) : null,
          middleValue: num(raw.MiddleColorValue, 0.5),
        }
      : null;
  return {
    target,
    expression: str(formula.Expression, ''),
    preExpression: typeof formula.PreExpression === 'string' && formula.PreExpression !== '' ? formula.PreExpression : null,
    mode,
    gradient,
    formatString: typeof raw.FormatString === 'string' ? raw.FormatString : null,
    javascript: formula.Interpreter === 1,
  };
}

function parseBindings(raw: unknown): SceneBinding[] {
  if (!isObject(raw)) return [];
  const out: SceneBinding[] = [];
  for (const [target, value] of Object.entries(raw)) {
    const b = parseBinding(target, value);
    if (b) out.push(b);
  }
  return out;
}

function parseBorder(raw: unknown): Border | null {
  if (!isObject(raw)) return null;
  const top = num(raw.BorderTop, 0);
  const bottom = num(raw.BorderBottom, 0);
  const left = num(raw.BorderLeft, 0);
  const right = num(raw.BorderRight, 0);
  const radius = {
    topLeft: num(raw.RadiusTopLeft, 0),
    topRight: num(raw.RadiusTopRight, 0),
    bottomRight: num(raw.RadiusBottomRight, 0),
    bottomLeft: num(raw.RadiusBottomLeft, 0),
  };
  const bindings = isObject(raw.Bindings) ? raw.Bindings : {};
  const colorBinding = parseBinding('BorderColor', bindings.BorderColor);
  if (top + bottom + left + right === 0 && radius.topLeft + radius.topRight + radius.bottomLeft + radius.bottomRight === 0 && !colorBinding) return null;
  return { color: paintOf(raw.BorderColor), top, bottom, left, right, radius, colorBinding };
}

function base(o: Json, fallbackName: string): Base {
  return {
    id: str(o.Id, ''),
    name: str(o.Name, fallbackName),
    visible: bool(o.Visible, true),
    opacity: num(o.Opacity, 100) / 100,
    blink: { enabled: bool(o.BlinkEnabled, false), delay: num(o.BlinkDelay, 250), inverted: bool(o.BlinkPhasisInverted, false) },
    bindings: parseBindings(o.Bindings),
  };
}

function drawable(o: Json, fallbackName: string): Drawable {
  return {
    ...base(o, fallbackName),
    box: { left: num(o.Left, 0), top: num(o.Top, 0), width: num(o.Width, 0), height: num(o.Height, 0) },
    rotation: num(o.Rotation, 0),
    background: paintOf(o.BackgroundColor),
    border: parseBorder(o.BorderStyle),
  };
}

/** The last segment of a `$type`, e.g. `TextItem`. */
export const shortType = (type: string): string => type.split(',')[0]!.split('.').pop() ?? type;

const STAND_INS: Record<string, string> = {
  RadarItem: 'Radar',
  GeneratedStaticMapItem: 'Track map',
  WebPageItem: 'Web page',
  ImageFromFileItem: 'Image from file',
};

function parseItem(raw: unknown, index: number, unknown: Set<string>): Item | null {
  if (!isObject(raw)) return null;
  const type = shortType(str(raw.$type, ''));
  const fallbackName = `${type || 'item'} ${index}`;
  switch (type) {
    case 'TextItem': {
      const pad = isObject(raw.TextPadding) ? raw.TextPadding : {};
      return {
        kind: 'text',
        ...drawable(raw, fallbackName),
        font: str(raw.Font, 'Segoe UI'),
        weight: str(raw.FontWeight, 'Normal'),
        italic: raw.FontStyle === 'Italic',
        size: num(raw.FontSize, 12),
        text: str(raw.Text, ''),
        color: paintOf(raw.TextColor, { css: '#ffffff', alpha: 1 }),
        hAlign: H_ALIGN[num(raw.HorizontalAlignment, 0)] ?? 'left',
        vAlign: V_ALIGN[num(raw.VerticalAlignment, 0)] ?? 'top',
        padding: { top: num(pad.PaddingTop, 0), bottom: num(pad.PaddingBottom, 0), left: num(pad.PaddingLeft, 0), right: num(pad.PaddingRight, 0) },
        monospace: raw.UseMonospacedText === true ? { charWidth: num(raw.CharWidth, 40), specialCharsWidth: num(raw.SpecialCharsWidth, 20), specialChars: str(raw.SpecialChars, '.,:;') } : null,
        wrap: raw.TextWrapping === 'Wrap',
      };
    }
    case 'RectangleItem':
      return { kind: 'rect', ...drawable(raw, fallbackName) };
    case 'EllipseItem':
      return { kind: 'ellipse', ...drawable(raw, fallbackName), fill: paintOf(raw.FillColor), stroke: paintOf(raw.EllipseColor), thickness: num(raw.EllipseThickness, 0) };
    case 'Layer': {
      const children = parseItems(raw.Childrens, unknown);
      const repeats = raw.PrepareRepetitions === true || raw.Repetitions !== undefined;
      return {
        kind: 'layer',
        ...base(raw, fallbackName),
        children,
        repetitions: repeats ? Math.max(0, num(raw.Repetitions, 0)) : 0,
        repeatTop: num(raw.RepeatTopOffset, 30),
        repeatLeft: num(raw.RepeatLeftOffset, 0),
      };
    }
    case 'WidgetItem':
      return { kind: 'widget', ...drawable(raw, fallbackName), fileName: str(raw.FileName, ''), initialScreenIndex: num(raw.InitialScreenIndex, 0), autoSize: bool(raw.AutoSize, true) };
    case 'ChartItem':
      return {
        kind: 'chart',
        ...drawable(raw, fallbackName),
        currentValue: num(raw.CurrentValue, 0),
        minimum: num(raw.Minimum, 0),
        maximum: num(raw.Maximum, 100),
        useMinimum: bool(raw.UseMinimum, true),
        useMaximum: bool(raw.UseMaximum, true),
        lineColor: paintOf(raw.LineColor, { css: '#ffffff', alpha: 1 }),
        lineThickness: num(raw.LineTickness, 2),
        pointsCount: Math.max(2, Math.round(num(raw.PointsCount, 100))),
        enabled: bool(raw.ChartEnabled, true),
        suspended: bool(raw.ChartSuspended, false),
      };
    case 'LinearGaugeItem':
      return {
        kind: 'gauge',
        ...drawable(raw, fallbackName),
        vertical: raw.GaugeOrientation === 1,
        alignment: GAUGE_ALIGN[num(raw.GaugeAlignment, 0)] ?? 'start',
        gaugeColor: paintOf(raw.GaugeColor),
        alternateColor: paintOf(raw.AlternateGaugeColor, paintOf(raw.GaugeColor)),
        useAlternate: bool(raw.UseAlternateStyle, false),
        minimum: num(raw.Minimum, 0),
        maximum: num(raw.Maximum, 100),
        value: num(raw.Value, 0),
        steps: Math.max(0, Math.round(num(raw.Steps, 0))),
      };
    case 'ImageItem':
      return { kind: 'image', ...drawable(raw, fallbackName), image: str(raw.Image, '') };
    default: {
      if (!(type in STAND_INS)) unknown.add(type || '(no $type)');
      return {
        kind: 'standIn',
        ...drawable(raw, fallbackName),
        label: STAND_INS[type] ?? (type || 'Unknown item'),
        type,
        imagePath: str(raw.ImagePath, ''),
        startAddress: str(raw.StartAddress, ''),
      };
    }
  }
}

function parseItems(raw: unknown, unknown: Set<string>): Item[] {
  const out: Item[] = [];
  list(raw).forEach((r, i) => {
    const item = parseItem(r, i, unknown);
    if (item) out.push(item);
  });
  return out;
}

const expressionOf = (v: unknown): string => (isObject(v) ? str(v.Expression, '') : '');

/**
 * Reads a `.djson` document, already parsed from JSON. Throws a {@link SceneError} when the
 * document is not a dashboard at all; anything narrower is kept and reported instead.
 */
export function parseDashboard(json: unknown, file: string): SceneDashboard {
  if (!isObject(json)) throw new SceneError(`${file} is not a JSON object`);
  if (!Array.isArray(json.Screens)) throw new SceneError(`${file} has no Screens list`);
  const width = num(json.BaseWidth, NaN);
  const height = num(json.BaseHeight, NaN);
  if (!Number.isFinite(width) || !Number.isFinite(height)) throw new SceneError(`${file} has no BaseWidth or BaseHeight`);
  const background = paintOf(json.BackgroundColor);
  const unknown = new Set<string>();
  const screens: Screen[] = json.Screens.filter(isObject).map((s, i) => ({
    name: str(s.Name, `Screen ${i + 1}`),
    inGame: bool(s.InGameScreen, true),
    idle: bool(s.IdleScreen, true),
    pit: bool(s.PitScreen, true),
    enabledExpression: expressionOf(s.ScreenEnabledExpression),
    background: paintOf(s.BackgroundColor, background),
    items: parseItems(s.Items, unknown),
  }));
  const container = isObject(json.Variables) ? json.Variables : {};
  const variables: Variable[] = list(container.DashboardVariables)
    .filter(isObject)
    .map((v) => ({
      name: str(v.VariableName, ''),
      expression: expressionOf(v.ValueExpression),
      beforeScreenRoles: bool(v.EvaluateBeforeScreenRoles, false),
      evaluateOnlyOnce: bool(v.EvaluateOnlyOnce, false),
    }))
    .filter((v) => v.name !== '');
  const images: ImageDescriptor[] = list(json.Images)
    .filter(isObject)
    .map((i) => ({ name: str(i.Name, ''), extension: str(i.Extension, '.png'), width: num(i.Width, 0), height: num(i.Height, 0) }));
  return { file, width, height, background, screens, variables, images, unknownTypes: [...unknown] };
}

/** Every item of a screen, depth first, layers included. */
export function* itemsOf(items: readonly Item[]): Generator<Item> {
  for (const item of items) {
    yield item;
    if (item.kind === 'layer') yield* itemsOf(item.children);
  }
}

/** The widget files a dashboard includes, by `FileName`, each once. */
export function widgetFiles(dashboard: SceneDashboard): string[] {
  const out = new Set<string>();
  for (const screen of dashboard.screens) for (const item of itemsOf(screen.items)) if (item.kind === 'widget' && item.fileName) out.add(item.fileName);
  return [...out];
}
