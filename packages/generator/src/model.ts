/**
 * Typed model of the subset of SimHub's DashStudio scene graph that openDash emits.
 *
 * The property names mirror SimHub 9.12's own classes (verified against the decompiled
 * SimHub.Plugins.dll and against real exports): DrawableItem, TextItem, RectangleItem, EllipseItem,
 * Layer, WidgetItem, Dashboard, Screen and DashboardMetadata. The serialiser in ./serialize.ts turns
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
 * `BorderColor` is here but belongs to no item kind, which is a fact about the spelling rather than
 * about the capability. SimHub's BindingHelper resolves a target with
 * `item.GetType().GetProperty(name)`, so an item-level `Bindings.BorderColor` finds nothing and is
 * silently ignored; the property lives on `BorderStyle`, which is an `IBindable` carrying bindings
 * of its own that SimHub evaluates every frame. `Border.colorBinding` is where a bound border is
 * written, and the validator refuses the item-level spelling on every kind.
 * See docs/decisions/0011-personalisation.md.
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
  | 'InitialScreenIndex'
  /** Ellipse fill and stroke; ellipse items only. `BackgroundColor` is the DrawableItem background behind the ellipse. */
  | 'FillColor'
  | 'EllipseColor'
  /** Chart: the sample appended on every tick, and whether the trace records at all. */
  | 'CurrentValue'
  | 'ChartEnabled'
  | 'LineColor'
  /** Linear gauge: the value and the ends of its scale, its fill colour and its alternate style. */
  | 'Value'
  | 'Minimum'
  | 'Maximum'
  | 'GaugeColor'
  | 'AlternateGaugeColor'
  | 'UseAlternateStyle'
  /** Radar: pixels per metre are `10 x Scale`. */
  | 'Scale'
  /** A border's colour, which is written inside `BorderStyle` and never on the item. */
  | 'BorderColor'
  /** Web page: the URL. */
  | 'StartAddress'
  /** Layer: the total number of rows a repeated layer stamps. */
  | 'Repetitions';

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
  /**
   * A formula for the border's colour, evaluated every frame like any other binding.
   *
   * It is written into `BorderStyle`'s own `Bindings` rather than the item's, which is the only
   * spelling SimHub reads; `color` is still set, because that is what the editor shows and what
   * the border falls back to. A chip whose outline and ink change together is what this is for.
   */
  colorBinding?: Binding;
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
  /**
   * Degrees clockwise around the item's centre (SimHub's `DrawableItem.Rotation`). Text,
   * rectangle and ellipse items only; omitted from the file when 0.
   */
  rotation?: number;
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
  /**
   * The widest string the item's `Text` binding can produce, when it has one. Build-time only:
   * it is never serialised, and it exists so a fit test measures what the item will draw rather
   * than the design-time sample, which is usually shorter.
   */
  widest?: string;
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
 * An ellipse filling its rect (SimHub's EllipseItem, verified against a 9.12 export). The
 * stroke is centred on the ellipse's edge, so a ring of thickness t drawn on a face of radius R
 * is an ellipse rect inset by t / 2. A ring has a transparent fill.
 */
export interface EllipseItem extends ItemBase {
  kind: 'ellipse';
  rect: Rect;
  /** `FillColor` in the file. */
  fillColor: Hex;
  /** `EllipseColor` in the file: the stroke colour. */
  strokeColor: Hex;
  /** `EllipseThickness` in the file, in px. 0 draws no stroke. */
  strokeThickness: number;
}

/**
 * A Layer groups items in the editor tree. It has no geometry of its own: children keep
 * absolute coordinates. `Visible`, `Opacity` and blink apply to the whole group.
 *
 * A layer can also stamp its children N times, which is how a table draws one row definition as
 * many rows: `repetitions` is the number of *extra* copies (SimHub's `Repetitions` key, total
 * rows = repetitions + 1), each offset by `repeatTopOffset` / `repeatLeftOffset`, and children
 * address their copy with NCalc's `repeatindex()` (1 for the original, 2.. for the copies).
 * SimHub removes widget items from a copy, so a repeated layer must not hold one.
 */
export interface LayerItem extends ItemBase {
  kind: 'layer';
  children: Item[];
  /** Extra copies of the children. 0 or undefined draws the children once. */
  repetitions?: number;
  /** Y step between copies, in px. SimHub's own default is 30, so the serialiser always writes it when repeating. */
  repeatTopOffset?: number;
  /** X step between copies, in px. */
  repeatLeftOffset?: number;
}

/**
 * The dot and label style of a car on a map or a radar (SimHub's `PlayerStyle`, used for both
 * the player and the opponents). SimHub's class defaults differ from the defaults each item
 * applies, so the serialiser always writes all seven keys.
 */
export interface DotStyle {
  labelFont?: string;
  labelFontSize?: number;
  labelColor?: Hex;
  dotColor?: Hex;
  dotBorderThickness?: number;
  /** `DotBordercolor` in the file: SimHub spells the c in lower case. */
  dotBorderColor?: Hex;
  dotRadius?: number;
}

/** The start/finish line of a generated map (SimHub's `SeparatorStyle`). */
export interface SeparatorStyle {
  color?: Hex;
  enabled?: boolean;
  height?: number;
  width?: number;
}

/**
 * A time-series trace (SimHub's `ChartItem`). One series per item: on every dashboard tick
 * SimHub appends `CurrentValue` to a ring buffer of `pointsCount` samples and draws it left
 * (oldest) to right (newest). Overlay N traces by stacking N chart items with transparent
 * backgrounds. There is no time base: the window is `pointsCount` x the refresh interval.
 */
export interface ChartItem extends ItemBase {
  kind: 'chart';
  rect: Rect;
  /** Design-time sample; bind `CurrentValue` for the live one. */
  currentValue?: number;
  /** Y axis bottom and top. With `useMinimum`/`useMaximum` false that end autoscales. */
  minimum?: number;
  maximum?: number;
  useMinimum?: boolean;
  useMaximum?: boolean;
  lineColor: Hex;
  /** `LineTickness` in the file (SimHub's spelling). */
  lineThickness?: number;
  /** Ring buffer length, which is also the trace's horizontal resolution. */
  pointsCount?: number;
  /** False clears the buffer; bind `ChartEnabled` to pause a trace. */
  chartEnabled?: boolean;
  /** True keeps the samples but stops recording. */
  chartSuspended?: boolean;
  border?: Border;
}

/** Which way a linear gauge fills. */
export type GaugeOrientation = 'horizontal' | 'vertical';
/** Which end of its track a linear gauge's fill is anchored to. */
export type GaugeAlignment = 'start' | 'center' | 'end';

/**
 * A bar gauge (SimHub's `LinearGaugeItem`): a `gaugeColor` rectangle over the item's background,
 * filled to `(value - minimum) / (maximum - minimum)`. There is no centre-zero mode, so a delta
 * bar is two gauges meeting in the middle; `maximum < minimum` is allowed and inverts the fill.
 */
export interface LinearGaugeItem extends ItemBase {
  kind: 'linearGauge';
  rect: Rect;
  orientation?: GaugeOrientation;
  /** `start` = left or bottom, `end` = right or top. */
  alignment?: GaugeAlignment;
  gaugeColor: Hex;
  /** Fill colour while `useAlternateStyle` is true. */
  alternateGaugeColor?: Hex;
  useAlternateStyle?: boolean;
  minimum?: number;
  maximum?: number;
  /** Design-time sample; bind `Value` for the live one. */
  value?: number;
  /** Above 0, quantises the fill into that many segments. */
  steps?: number;
  border?: Border;
}

/**
 * The proximity radar (SimHub's `RadarItem`). The player is always drawn at the item's centre;
 * `scale` is the zoom, 10 x scale pixels per metre. It needs the PersistantTracker recorded map
 * on games that report relative coordinates.
 */
export interface RadarItem extends ItemBase {
  kind: 'radar';
  rect: Rect;
  scale?: number;
  /** Heading from the motion vector instead of the car's yaw. */
  useSmoothedPlayerAngle?: boolean;
  playerStyle?: DotStyle;
  opponentStyle?: DotStyle;
}

/**
 * The whole track drawn to fit the item (SimHub's `GeneratedStaticMapItem`), with a dot per car.
 * The outline comes from the PersistantTracker recorded map, so a track with no recorded lap
 * draws nothing.
 */
export interface StaticMapItem extends ItemBase {
  kind: 'staticMap';
  rect: Rect;
  trackColor: Hex;
  trackWidth?: number;
  trackBorderColor: Hex;
  trackBorderWidth?: number;
  alternateTrackSectorColor?: Hex;
  minimumTrackWidth?: number;
  minimumTrackBorderWidth?: number;
  mapShadow?: boolean;
  /** True paints the dots from the car-class colours; openDash keeps the map achromatic. */
  overrideColorsWithCarClassColors?: boolean;
  /** True labels a dot with the class position instead of the overall one. */
  displayPerClassPosition?: boolean;
  playerStyle?: DotStyle;
  opponentStyle?: DotStyle;
  startLine?: SeparatorStyle;
}

/** An embedded browser (SimHub's `WebPageItem`). `startAddress` is the URL and can be bound. */
export interface WebPageItem extends ItemBase {
  kind: 'webPage';
  rect: Rect;
  startAddress?: string;
  /** True lets clicks pass through; SimHub then freezes the item while a game runs. */
  clickThrough?: boolean;
}

/**
 * A raster image from the dashboard's own `Images` list (SimHub's `ImageItem`).
 *
 * `AutoSize` sizes the item from the image and its scale; openDash always wants a fixed box, so
 * the serialiser writes `AutoSize` false and the rect is what is drawn. The image is referenced
 * by the name of an {@link ImageAsset} on the same dashboard, never by a file path: a path would
 * be `ImageFromFileItem`, which reads from the user's disk and cannot be shipped.
 */
export interface ImageItem extends ItemBase {
  kind: 'image';
  rect: Rect;
  /** The {@link ImageAsset.name} this draws. */
  image: string;
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

export type Item = TextItem | RectangleItem | EllipseItem | LayerItem | WidgetItem | ChartItem | LinearGaugeItem | RadarItem | StaticMapItem | WebPageItem | ImageItem;

/** The item kinds that carry a rect (everything but a Layer). */
export type DrawableItem = Exclude<Item, LayerItem>;

/**
 * An image packed into `<dashboard>.djson.ressources` and referenced by name from an image item.
 *
 * Everything but `path` is read off the file by `describeImage`, because SimHub's descriptor
 * states the image's own dimensions, its byte count and an MD5 of those bytes, and a descriptor
 * that disagrees with the bytes beside it is how a dashboard draws nothing at all.
 */
export interface ImageAsset {
  /** What an image item references, and the base name of the `.ressources` entry. */
  name: string;
  /** Including the dot, e.g. ".png". */
  extension: string;
  /** Absolute path to the source file, read when the package is written. */
  path: string;
  /** The image's own pixels, not the box it is drawn in. */
  width: number;
  height: number;
  /** Uncompressed byte count. */
  length: number;
  /** Lowercase hex MD5 of the same bytes. */
  md5: string;
}

/**
 * A licence or notice file that has to travel with a package because of what the package carries.
 *
 * A `.simhubdash` is a redistribution: the SIL Open Font Licence requires its notice to accompany
 * the fonts, and Apache 2.0 requires the NOTICE to accompany the icons. Both are ordinary files
 * in the package folder, which SimHub ignores and a person can read.
 */
export interface NoticeFile {
  /** Name inside the package folder, e.g. "OFL.txt". */
  name: string;
  /** Absolute path to the source file. */
  path: string;
}

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
  /** The images this dashboard's items may reference. Packed into `<name>.djson.ressources`. */
  images?: ImageAsset[];
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
  /** Licences and notices copied into the package root, for what the package redistributes. */
  notices?: NoticeFile[];
}
