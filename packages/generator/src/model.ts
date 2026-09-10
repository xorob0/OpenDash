/**
 * Typed model of the subset of SimHub's DashStudio scene graph that openDash emits.
 *
 * The property names mirror SimHub 9.12's own classes (verified against the decompiled
 * SimHub.Plugins.dll and against real exports): DrawableItem, TextItem, RectangleItem, Layer,
 * WidgetItem, Dashboard, Screen and DashboardMetadata. The serialiser in ./serialize.ts turns
 * this model into the JSON SimHub reads. Nothing in this file knows about openDash.
 */

/** A colour in `#RRGGBB` or `#AARRGGBB` form. The serialiser always writes `#AARRGGBB`. */
export type Hex = `#${string}`;

/** Absolute pixel geometry. SimHub layout is absolute; all positions are relative to the screen. */
export interface Rect {
  left: number;
  top: number;
  width: number;
  height: number;
}

/**
 * An NCalc (default) or JavaScript formula. Prefer NCalc; JavaScript is only there because
 * SimHub supports it. `preExpression` is evaluated once before `expression`.
 */
export interface Formula {
  expression: string;
  interpreter?: 'ncalc' | 'js';
  preExpression?: string;
}

/** Mode 2 in the file: the formula's value is written to the target property. */
export interface FormulaBinding {
  mode: 'formula';
  formula: string | Formula;
  /** Optional .NET format string applied to the formula's value. */
  formatString?: string;
}

/** Mode 4 in the file: the formula's numeric value is mapped onto a colour ramp. */
export interface GradientBinding {
  mode: 'gradient';
  formula: string | Formula;
  startColor: Hex;
  startValue: number;
  endColor: Hex;
  endValue: number;
  middleColor?: Hex;
  middleValue?: number;
}

export type Binding = FormulaBinding | GradientBinding;

/**
 * Bindable targets. The key is the SimHub property name exactly as it appears in the JSON.
 * Not every target makes sense on every item kind; the validator checks that.
 *
 * `BorderColor` is deliberately absent: it is a member of `BorderStyle`, not of the item, and
 * SimHub's BindingHelper resolves targets with `item.GetType().GetProperty(name)`, so an
 * item-level `Bindings.BorderColor` is silently ignored.
 */
export type BindingTarget =
  | 'Text'
  | 'TextColor'
  | 'Visible'
  | 'BackgroundColor'
  | 'Width'
  | 'Height'
  | 'Left'
  | 'Top'
  | 'Opacity'
  | 'FontSize'
  | 'BlinkEnabled'
  | 'InitialScreenIndex';

export type Bindings = Partial<Record<BindingTarget, Binding>>;

export interface Blink {
  /** Static value; bind `BlinkEnabled` for a dynamic one. */
  enabled?: boolean;
  /** Half period in milliseconds. SimHub's default is 250 (2 Hz). */
  delayMs?: number;
  phaseInverted?: boolean;
}

export interface Border {
  color?: Hex;
  top?: number;
  bottom?: number;
  left?: number;
  right?: number;
  /** One value for all corners, or per corner. */
  radius?: number | { topLeft: number; topRight: number; bottomLeft: number; bottomRight: number };
}

export interface ItemBase {
  /**
   * GUID. When omitted the serialiser derives a stable one from the item's path
   * (dashboard/screen/name), so a rebuild does not churn identifiers.
   */
  id?: string;
  /** Label shown in DashStudio's tree. Must be unique within a screen. */
  name: string;
  visible?: boolean;
  backgroundColor?: Hex;
  /** 0 to 100. SimHub's default is 100. */
  opacity?: number;
  blink?: Blink;
  bindings?: Bindings;
  /** Render every Nth frame. 0 renders every frame. */
  renderingSkip?: number;
  minimumRefreshIntervalMs?: number;
}

export type FontWeight = 'Light' | 'Normal' | 'Medium' | 'SemiBold' | 'Bold' | 'Black';
export type HAlign = 'left' | 'center' | 'right';
export type VAlign = 'top' | 'center' | 'bottom';

export interface TextPadding {
  top?: number;
  bottom?: number;
  left?: number;
  right?: number;
}

/**
 * SimHub can draw each character in a fixed-width cell, which is how it makes proportional
 * fonts behave like tabular ones. `specialChars` (default ".,:") get `specialCharsWidth`. The
 * serialiser always writes `SpecialChars`, because SimHub's own default is ".,:;" (with the
 * semicolon), so leaving the key out would not give ".,:".
 */
export interface Monospace {
  charWidth: number;
  specialCharsWidth: number;
  specialChars?: string;
}

export interface TextItem extends ItemBase {
  kind: 'text';
  rect: Rect;
  text: string;
  /** Font family name as the TTF declares it, e.g. "Barlow Condensed". */
  font: string;
  fontWeight: FontWeight;
  fontStyle?: 'Normal' | 'Italic';
  fontSize: number;
  textColor: Hex;
  hAlign: HAlign;
  vAlign: VAlign;
  padding?: TextPadding;
  monospace?: Monospace;
  wrap?: boolean;
  border?: Border;
}

export interface RectangleItem extends ItemBase {
  kind: 'rect';
  rect: Rect;
  border?: Border;
}

/**
 * A Layer groups items in the editor tree. It has no geometry of its own: children keep
 * absolute coordinates. `Visible`, `Opacity` and blink apply to the whole group.
 */
export interface LayerItem extends ItemBase {
  kind: 'layer';
  children: Item[];
}

/**
 * Embeds another `.djson` of the same package. `initialScreenIndex` selects which of its screens
 * is shown and can be bound, which is how openDash slots switch cards.
 */
export interface WidgetItem extends ItemBase {
  kind: 'widget';
  rect: Rect;
  /** File name relative to the dashboard folder, e.g. "cards.djson". */
  fileName: string;
  initialScreenIndex: number;
  /** Scale the widget to the item's rect. Default true. */
  autoSize?: boolean;
}

export type Item = TextItem | RectangleItem | LayerItem | WidgetItem;

export interface Screen {
  id?: string;
  name: string;
  items: Item[];
  /** Screen roles. All default to true. */
  inGame?: boolean;
  idle?: boolean;
  pit?: boolean;
  backgroundColor?: Hex;
  /** NCalc expression; a screen whose expression is false takes no part in navigation. */
  enabledExpression?: string;
}

export interface DashboardMetadata {
  title: string;
  author: string;
  description?: string;
  category?: string | null;
  /** Semantic version string; the plugin compares it to decide whether to reinstall. */
  version: string;
  /** SimHub version the output was tested against, e.g. "9.12.6". */
  simHubVersion: string;
  mainPreviewIndex?: number;
}

export interface Dashboard {
  id?: string;
  /** File base name without extension; `<name>.djson`. The main dashboard of a package is named after the folder. */
  name: string;
  width: number;
  height: number;
  backgroundColor: Hex;
  screens: Screen[];
  metadata: DashboardMetadata;
}

/** Everything that goes into one `<folder>/` and therefore one `.simhubdash`. */
export interface DashPackage {
  /** Folder name and name of the main dashboard, e.g. "openDash". */
  folderName: string;
  /** The first dashboard must have `name === folderName`. The others are widgets. */
  dashboards: Dashboard[];
  /** Absolute paths to TTF files copied into `_SHFonts/`. */
  fonts: string[];
}
