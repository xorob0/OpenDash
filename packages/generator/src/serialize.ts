/**
 * Turns the model into the JSON document SimHub 9.12 reads. Key order follows SimHub's own
 * exports and the MVP spec: `$type` is always the first key of an item (Json.NET only honours it
 * there), colours are `#AARRGGBB`, and properties that equal SimHub's defaults (Opacity 100,
 * BlinkDelay 250, empty Bindings, zero border widths) are omitted. `buildXxxObject` functions
 * return plain objects so that tests and snapshots can inspect the shape without re-parsing.
 */

import type {
  Binding,
  Bindings,
  Border,
  ChartItem,
  Dashboard,
  DotStyle,
  DrawableItem,
  EllipseItem,
  Formula,
  ImageAsset,
  ImageItem,
  Item,
  ItemBase,
  LayerItem,
  LinearGaugeItem,
  RadarItem,
  RectangleItem,
  Screen,
  SeparatorStyle,
  StaticMapItem,
  TextItem,
  WebPageItem,
  WidgetItem,
} from './model.ts';
import { normaliseHex, TRANSPARENT } from './color.ts';
import { assertWholeNumbers } from './intFields.ts';
import { dashboardPath, itemPath, resolveItemId, screenPath, stableGuid } from './ids.ts';

export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };
export type JsonObject = { [key: string]: JsonValue };

export interface SerializeContext {
  /** Folder name of the package; the root of every derived id path. */
  packageName: string;
}

/** Json.NET `$type` strings of the five item kinds, verified against SimHub 9.12 exports. */
export const ITEM_TYPES = {
  text: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.TextItem, SimHub.Plugins',
  rect: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.RectangleItem, SimHub.Plugins',
  ellipse: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.EllipseItem, SimHub.Plugins',
  layer: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.Layer, SimHub.Plugins',
  widget: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.WidgetItem, SimHub.Plugins',
  chart: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.ChartItem, SimHub.Plugins',
  linearGauge: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.LinearGaugeItem, SimHub.Plugins',
  radar: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.RadarItem, SimHub.Plugins',
  staticMap: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.GeneratedStaticMapItem, SimHub.Plugins',
  webPage: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.WebPageItem, SimHub.Plugins',
  image: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.ImageItem, SimHub.Plugins',
} as const satisfies Record<Item['kind'], string>;

export const DEFAULT_OPACITY = 100;
export const DEFAULT_BLINK_DELAY_MS = 250;
export const DEFAULT_ROTATION = 0;
export const DEFAULT_SPECIAL_CHARS = '.,:';
export const DEFAULT_GRID_SIZE = 5;
/** SimHub's own default, written on every image item so a DashStudio round trip is not a diff. */
export const DEFAULT_AUTO_SIZE_SCALE = 1;
export const METADATA_VERSION = 2;

export const H_ALIGN = { left: 0, center: 1, right: 2 } as const;
export const V_ALIGN = { top: 0, center: 1, bottom: 2 } as const;

/** `GaugeOrientation` and `GaugeAlignment` as SimHub's enums number them. */
export const GAUGE_ORIENTATION = { horizontal: 0, vertical: 1 } as const;
export const GAUGE_ALIGNMENT = { start: 0, center: 1, end: 2 } as const;

/** SimHub's own default row pitch of a repeated layer: written even when it is the value we want. */
export const DEFAULT_REPEAT_TOP_OFFSET = 30;

/**
 * A `PlayerStyle` sub-object. SimHub's class defaults differ from the defaults each item applies
 * to it, so all seven keys are always written. `DotBordercolor` is SimHub's spelling.
 */
export const buildDotStyleObject = (style: DotStyle | undefined, fallback: Required<DotStyle>): JsonObject => {
  const s = style ?? {};
  return {
    LabelFont: s.labelFont ?? fallback.labelFont,
    LabelFontSize: s.labelFontSize ?? fallback.labelFontSize,
    LabelColor: normaliseHex(s.labelColor ?? fallback.labelColor),
    DotColor: normaliseHex(s.dotColor ?? fallback.dotColor),
    DotBorderThickness: s.dotBorderThickness ?? fallback.dotBorderThickness,
    DotBordercolor: normaliseHex(s.dotBorderColor ?? fallback.dotBorderColor),
    DotRadius: s.dotRadius ?? fallback.dotRadius,
  };
};

/** The `PlayerStyle` defaults of a map item and of a radar item, from SimHub's initialisers. */
export const MAP_PLAYER_STYLE: Required<DotStyle> = {
  labelFont: 'Segoe UI',
  labelFontSize: 14,
  labelColor: '#FFFFFFFF',
  dotColor: '#FFFF0000',
  dotBorderThickness: 4,
  dotBorderColor: '#FFFFFFFF',
  dotRadius: 30,
};
export const MAP_OPPONENT_STYLE: Required<DotStyle> = { ...MAP_PLAYER_STYLE, dotColor: '#FFFFFFFF', dotBorderThickness: 2, dotBorderColor: '#FF000000' };
export const RADAR_PLAYER_STYLE: Required<DotStyle> = { ...MAP_PLAYER_STYLE, dotBorderThickness: 0, dotRadius: 40 };
export const RADAR_OPPONENT_STYLE: Required<DotStyle> = { ...RADAR_PLAYER_STYLE, dotColor: '#FFFFFFFF', dotRadius: 30 };

/** The `StartLine` sub-object of a generated map. */
export const buildStartLineObject = (line: SeparatorStyle | undefined): JsonObject => ({
  Color: normaliseHex(line?.color ?? '#FFFF0000'),
  Enabled: line?.enabled ?? true,
  Height: line?.height ?? 40,
  Width: line?.width ?? 5,
});

const BINDING_MODE = { formula: 2, gradient: 4 } as const;

/**
 * `BorderStyle`: zero widths and radii are omitted; an empty border is `{}`.
 *
 * Every one of its eight numbers is an `int` in SimHub, so each is rounded here rather than left
 * as whatever arithmetic produced it. A drawing that scales -- the tyre glyph's corner, the car's
 * nose -- computes a radius as a fraction of its box and lands on 3.5999999999999996 for a 18 px
 * one, and `JsonTextReader.ReadAsInt32` refuses that string outright. It does not refuse the
 * *property*: the exception unwinds the whole `LoadFromFile`, so the dashboard holding it draws
 * nothing at all. {@link assertWholeNumbers} is the backstop for the fields written elsewhere.
 */
export const buildBorderObject = (border: Border | undefined): JsonObject => {
  const o: JsonObject = {};
  if (!border) return o;
  if (border.color !== undefined) o.BorderColor = normaliseHex(border.color);
  const put = (key: string, value: number | undefined): void => {
    if (value === undefined) return;
    const whole = Math.round(value);
    if (whole !== 0) o[key] = whole;
  };
  put('BorderTop', border.top);
  put('BorderBottom', border.bottom);
  put('BorderLeft', border.left);
  put('BorderRight', border.right);
  const r = border.radius;
  if (typeof r === 'number') {
    put('RadiusTopLeft', r);
    put('RadiusTopRight', r);
    put('RadiusBottomLeft', r);
    put('RadiusBottomRight', r);
  } else if (r) {
    put('RadiusTopLeft', r.topLeft);
    put('RadiusTopRight', r.topRight);
    put('RadiusBottomLeft', r.bottomLeft);
    put('RadiusBottomRight', r.bottomRight);
  }
  // Inside `BorderStyle` and not on the item: SimHub recurses into the bindable sub-objects of an
  // item, and the same formula written at item level resolves to no property at all.
  if (border.colorBinding) o.Bindings = { BorderColor: buildBindingObject(border.colorBinding) };
  return o;
};

/** `Formula`: NCalc by default; JavaScript adds `Interpreter: 1, JSExt: 0`. */
export const buildFormulaObject = (formula: string | Formula): JsonObject => {
  const f: Formula = typeof formula === 'string' ? { expression: formula } : formula;
  const o: JsonObject = {};
  if (f.interpreter === 'js') {
    o.Interpreter = 1;
    o.JSExt = 0;
  }
  o.Expression = f.expression;
  if (f.preExpression !== undefined && f.preExpression !== '') o.PreExpression = f.preExpression;
  return o;
};

/** One entry of `Bindings`: Mode 2 (formula) or Mode 4 (colour gradient). */
export const buildBindingObject = (binding: Binding): JsonObject => {
  const o: JsonObject = {};
  if (binding.mode === 'formula') {
    if (binding.formatString !== undefined) o.FormatString = binding.formatString;
    o.Formula = buildFormulaObject(binding.formula);
    o.Mode = BINDING_MODE.formula;
    return o;
  }
  o.Formula = buildFormulaObject(binding.formula);
  o.StartColor = normaliseHex(binding.startColor);
  o.EnableMiddleColor = binding.middleColor !== undefined;
  o.MiddleColor = normaliseHex(binding.middleColor ?? '#FF000000');
  o.MiddleColorValue = binding.middleValue ?? (binding.startValue + binding.endValue) / 2;
  o.EndColor = normaliseHex(binding.endColor);
  o.StartColorValue = binding.startValue;
  o.EndColorValue = binding.endValue;
  o.Mode = BINDING_MODE.gradient;
  return o;
};

/** `Bindings` keyed by target, in the order the model declares them; undefined when empty. */
export const buildBindingsObject = (bindings: Bindings | undefined): JsonObject | undefined => {
  if (!bindings) return undefined;
  const o: JsonObject = {};
  for (const [target, binding] of Object.entries(bindings)) {
    if (binding) o[target] = buildBindingObject(binding);
  }
  return Object.keys(o).length === 0 ? undefined : o;
};

const appendOpacityAndBlink = (o: JsonObject, item: ItemBase): void => {
  if (item.opacity !== undefined && item.opacity !== DEFAULT_OPACITY) o.Opacity = item.opacity;
  const blink = item.blink;
  if (blink?.enabled) o.BlinkEnabled = true;
  if (blink?.delayMs !== undefined && blink.delayMs !== DEFAULT_BLINK_DELAY_MS) o.BlinkDelay = blink.delayMs;
  if (blink?.phaseInverted) o.BlinkPhasisInverted = true;
};

const appendTail = (o: JsonObject, item: ItemBase, id: string): void => {
  const bindings = buildBindingsObject(item.bindings);
  if (bindings) o.Bindings = bindings;
  o.Id = id;
  o.Name = item.name;
  o.RenderingSkip = item.renderingSkip ?? 0;
  o.MinimumRefreshIntervalMS = item.minimumRefreshIntervalMs ?? 0;
};

/**
 * The DrawableItem keys shared by text, rectangle, ellipse and widget items. `Rotation` follows
 * `Height` and is omitted when 0 (SimHub uses DefaultValueHandling.IgnoreAndPopulate); it is
 * only written for the kinds it is verified on (text, rectangle, ellipse), never for a widget.
 */
const appendDrawable = (o: JsonObject, item: DrawableItem, id: string, opts: { border: boolean; rotation: boolean }): void => {
  o.Left = item.rect.left;
  o.Top = item.rect.top;
  o.Width = item.rect.width;
  o.Height = item.rect.height;
  if (opts.rotation && item.rotation !== undefined && item.rotation !== DEFAULT_ROTATION) o.Rotation = item.rotation;
  o.Visible = item.visible ?? true;
  o.BackgroundColor = normaliseHex(item.backgroundColor ?? TRANSPARENT);
  appendOpacityAndBlink(o, item);
  if (opts.border) {
    const border = item.kind === 'text' || item.kind === 'rect' || item.kind === 'chart' || item.kind === 'linearGauge' ? item.border : undefined;
    o.BorderStyle = buildBorderObject(border);
  }
  appendTail(o, item, id);
};

const buildTextObject = (item: TextItem, id: string): JsonObject => {
  const o: JsonObject = { $type: ITEM_TYPES.text, IsTextItem: true, Font: item.font, FontWeight: item.fontWeight };
  const p = item.padding;
  if (p && ((p.top ?? 0) !== 0 || (p.bottom ?? 0) !== 0 || (p.left ?? 0) !== 0 || (p.right ?? 0) !== 0)) {
    o.TextPadding = {
      PaddingTop: p.top ?? 0,
      PaddingBottom: p.bottom ?? 0,
      PaddingLeft: p.left ?? 0,
      PaddingRight: p.right ?? 0,
    };
  }
  o.FontStyle = item.fontStyle ?? 'Normal';
  o.FontSize = item.fontSize;
  o.Text = item.text;
  o.TextColor = normaliseHex(item.textColor);
  o.HorizontalAlignment = H_ALIGN[item.hAlign];
  o.VerticalAlignment = V_ALIGN[item.vAlign];
  if (item.monospace) {
    o.UseMonospacedText = true;
    o.CharWidth = item.monospace.charWidth;
    o.SpecialCharsWidth = item.monospace.specialCharsWidth;
    // Always written: SimHub 9.12.6's own default is ".,:;" (with the semicolon), so omitting the
    // key would not give the model's ".,:" default.
    o.SpecialChars = item.monospace.specialChars ?? DEFAULT_SPECIAL_CHARS;
  }
  o.TextWrapping = item.wrap ? 'Wrap' : 'NoWrap';
  appendDrawable(o, item, id, { border: true, rotation: true });
  return o;
};

const buildRectObject = (item: RectangleItem, id: string): JsonObject => {
  const o: JsonObject = { $type: ITEM_TYPES.rect, IsRectangleItem: true };
  appendDrawable(o, item, id, { border: true, rotation: true });
  return o;
};

/** Ellipse keys as a SimHub 9.12 export writes them: fill, stroke colour and thickness, then the DrawableItem keys. */
const buildEllipseObject = (item: EllipseItem, id: string): JsonObject => {
  const o: JsonObject = {
    $type: ITEM_TYPES.ellipse,
    FillColor: normaliseHex(item.fillColor),
    EllipseColor: normaliseHex(item.strokeColor),
    EllipseThickness: item.strokeThickness,
  };
  appendDrawable(o, item, id, { border: true, rotation: true });
  return o;
};

/** Layers carry no geometry or background (both `[JsonIgnore]` in SimHub); children are absolute. */
const buildLayerObject = (item: LayerItem, id: string, path: string): JsonObject => {
  const o: JsonObject = { $type: ITEM_TYPES.layer, Group: true, Visible: item.visible ?? true };
  const repetitions = item.repetitions ?? 0;
  if (repetitions > 0 || item.bindings?.Repetitions) {
    o.Repetitions = repetitions;
    // Always written when repeating: SimHub's own default is 30, so a horizontal strip that
    // leaves the key out would step 30 px down as well as across.
    o.RepeatTopOffset = item.repeatTopOffset ?? DEFAULT_REPEAT_TOP_OFFSET;
    if (item.repeatLeftOffset !== undefined && item.repeatLeftOffset !== 0) o.RepeatLeftOffset = item.repeatLeftOffset;
    o.PrepareRepetitions = true;
    if (item.bindings?.Repetitions) o.RepetitionsBound = true;
  }
  appendOpacityAndBlink(o, item);
  o.Childrens = item.children.map((child) => buildItemObject(child, path));
  appendTail(o, item, id);
  return o;
};

const buildWidgetObject = (item: WidgetItem, id: string): JsonObject => {
  const o: JsonObject = {
    $type: ITEM_TYPES.widget,
    NextScreenCommand: 0,
    PreviousScreenCommand: 0,
    AutoSize: item.autoSize ?? true,
    FileName: item.fileName,
    InitialScreenIndex: item.initialScreenIndex,
    FreezePageChanges: false,
    EnableScreenRolesAndActivation: false,
    IgnoreSavedScreensEx: true,
  };
  appendDrawable(o, item, id, { border: false, rotation: false });
  return o;
};

/** A chart's own keys, then the DrawableItem ones. `LineTickness` is SimHub's spelling. */
const buildChartObject = (item: ChartItem, id: string): JsonObject => {
  const o: JsonObject = {
    $type: ITEM_TYPES.chart,
    ChartSuspended: item.chartSuspended ?? false,
    ChartEnabled: item.chartEnabled ?? true,
    CurrentValue: item.currentValue ?? 0,
    Minimum: item.minimum ?? 0,
    UseMinimum: item.useMinimum ?? true,
    UseMaximum: item.useMaximum ?? true,
    LineColor: normaliseHex(item.lineColor),
    LineTickness: item.lineThickness ?? 2,
    Maximum: item.maximum ?? 100,
    PointsCount: item.pointsCount ?? 100,
  };
  appendDrawable(o, item, id, { border: true, rotation: false });
  return o;
};

/** A linear gauge. `BackgroundColor` is the track, and SimHub's ctor makes it blue, so it is always written (appendDrawable does). */
const buildLinearGaugeObject = (item: LinearGaugeItem, id: string): JsonObject => {
  const o: JsonObject = {
    $type: ITEM_TYPES.linearGauge,
    IsLinearGauge: true,
    GaugeOrientation: GAUGE_ORIENTATION[item.orientation ?? 'horizontal'],
    GaugeAlignment: GAUGE_ALIGNMENT[item.alignment ?? 'start'],
    AutoSize: false,
    GaugeColor: normaliseHex(item.gaugeColor),
    AlternateGaugeColor: normaliseHex(item.alternateGaugeColor ?? item.gaugeColor),
    UseAlternateStyle: item.useAlternateStyle ?? false,
    Minimum: item.minimum ?? 0,
    Maximum: item.maximum ?? 100,
    Value: item.value ?? 0,
    Steps: item.steps ?? 0,
  };
  appendDrawable(o, item, id, { border: true, rotation: false });
  return o;
};

const buildRadarObject = (item: RadarItem, id: string): JsonObject => {
  const o: JsonObject = {
    $type: ITEM_TYPES.radar,
    PlayerStyle: buildDotStyleObject(item.playerStyle, RADAR_PLAYER_STYLE),
    OpponentStyle: buildDotStyleObject(item.opponentStyle, RADAR_OPPONENT_STYLE),
    UseSmoothedPlayerAngle: item.useSmoothedPlayerAngle ?? true,
    Scale: item.scale ?? 0.5,
  };
  appendDrawable(o, item, id, { border: false, rotation: false });
  return o;
};

const buildStaticMapObject = (item: StaticMapItem, id: string): JsonObject => {
  const o: JsonObject = {
    $type: ITEM_TYPES.staticMap,
    AlternateTrackSectorColor: normaliseHex(item.alternateTrackSectorColor ?? item.trackColor),
    MapShadow: item.mapShadow ?? false,
    OverrideColorsWithCarClassColors: item.overrideColorsWithCarClassColors ?? false,
    DisplayPerClassPosition: item.displayPerClassPosition ?? false,
    MinimumTrackBorderWidth: item.minimumTrackBorderWidth ?? 0,
    MinimumTrackWidth: item.minimumTrackWidth ?? 0,
    OpponentStyle: buildDotStyleObject(item.opponentStyle, MAP_OPPONENT_STYLE),
    PlayerStyle: buildDotStyleObject(item.playerStyle, MAP_PLAYER_STYLE),
    StartLine: buildStartLineObject(item.startLine),
    TrackBorderColor: normaliseHex(item.trackBorderColor),
    TrackBorderWidth: item.trackBorderWidth ?? 2,
    TrackColor: normaliseHex(item.trackColor),
    TrackWidth: item.trackWidth ?? 10,
  };
  appendDrawable(o, item, id, { border: false, rotation: false });
  return o;
};

const buildWebPageObject = (item: WebPageItem, id: string): JsonObject => {
  const o: JsonObject = {
    $type: ITEM_TYPES.webPage,
    StartAddress: item.startAddress ?? '',
    AllowTransparency: false,
    ClickThrough: item.clickThrough ?? false,
  };
  appendDrawable(o, item, id, { border: false, rotation: false });
  return o;
};

/**
 * `AutoSize` false with `AutoSizeScale` at its default: openDash sizes an image from its rect,
 * not from the image's own pixels, so that a telltale occupies the same box whichever artwork
 * ends up behind it. `AutoSizeScale` is written even though it is unused, because SimHub
 * populates it on load and leaving it out makes every round trip through DashStudio a diff.
 */
const buildImageObject = (item: ImageItem, id: string): JsonObject => {
  const o: JsonObject = {
    $type: ITEM_TYPES.image,
    Image: item.image,
    AutoSize: false,
    AutoSizeScale: DEFAULT_AUTO_SIZE_SCALE,
  };
  appendDrawable(o, item, id, { border: false, rotation: false });
  return o;
};

/**
 * One item as SimHub's JSON. `parentPath` is the screen's or enclosing layer's path; the item's
 * own path is `<parentPath>/<name>` and its id derives from it unless `item.id` is set.
 */
export const buildItemObject = (item: Item, parentPath: string): JsonObject => {
  const path = itemPath(parentPath, item.name);
  const id = resolveItemId(item, path);
  switch (item.kind) {
    case 'text':
      return buildTextObject(item, id);
    case 'rect':
      return buildRectObject(item, id);
    case 'ellipse':
      return buildEllipseObject(item, id);
    case 'layer':
      return buildLayerObject(item, id, path);
    case 'widget':
      return buildWidgetObject(item, id);
    case 'chart':
      return buildChartObject(item, id);
    case 'linearGauge':
      return buildLinearGaugeObject(item, id);
    case 'radar':
      return buildRadarObject(item, id);
    case 'staticMap':
      return buildStaticMapObject(item, id);
    case 'image':
      return buildImageObject(item, id);
    case 'webPage':
      return buildWebPageObject(item, id);
  }
};

export const buildScreenObject = (screen: Screen, dashboard: Dashboard, ctx: SerializeContext): JsonObject => {
  const path = screenPath(ctx.packageName, dashboard.name, screen.name);
  return {
    RenderingSkip: 0,
    Name: screen.name,
    InGameScreen: screen.inGame ?? true,
    IdleScreen: screen.idle ?? true,
    PitScreen: screen.pit ?? true,
    ScreenId: screen.id ?? stableGuid(path),
    AllowOverlays: true,
    IsForegroundLayer: false,
    IsOverlayLayer: false,
    OverlayTriggerExpression: { Expression: '' },
    ScreenEnabledExpression: { Expression: screen.enabledExpression ?? '' },
    OverlayMaxDuration: 0,
    OverlayMinDuration: 0,
    IsBackgroundLayer: false,
    BackgroundColor: normaliseHex(screen.backgroundColor ?? dashboard.backgroundColor),
    Background: 'None',
    MinimumRefreshIntervalMS: 0,
    Items: screen.items.map((item) => buildItemObject(item, path)),
  };
};

const screenIndexes = (dashboard: Dashboard, role: (screen: Screen) => boolean): number[] =>
  dashboard.screens.flatMap((screen, index) => (role(screen) ? [index] : []));

/** The `Metadata` object, also written verbatim as the `.djson.metadata` sidecar. */
export const buildMetadataObject = (dashboard: Dashboard): JsonObject => {
  const m = dashboard.metadata;
  return {
    SimHubVersion: m.simHubVersion,
    Category: m.category ?? null,
    Title: m.title,
    Description: m.description ?? null,
    Author: m.author,
    Width: dashboard.width,
    Height: dashboard.height,
    DashboardVersion: m.version,
    ScreenCount: dashboard.screens.length,
    InGameScreensIndexs: screenIndexes(dashboard, (s) => s.inGame ?? true),
    IdleScreensIndexs: screenIndexes(dashboard, (s) => s.idle ?? true),
    PitScreensIndexs: screenIndexes(dashboard, (s) => s.pit ?? true),
    MainPreviewIndex: m.mainPreviewIndex ?? 0,
    IsOverlay: false,
    OverlaySizeWarning: true,
    MetadataVersion: METADATA_VERSION,
    EnableOnDashboardMessaging: false,
    // DashboardPreferredTouchMode: User, Simple, Advanced.
    PreferredTouchMode: { user: 0, simple: 1, advanced: 2 }[m.touchMode ?? 'user'],
  };
};

/**
 * One entry of a dashboard's `Images` list. `Modified` and `Optimized` are DashStudio's record of
 * what a person did to an image after importing it, and a generated package has done neither.
 */
export const buildImageDescriptor = (image: ImageAsset): JsonObject => ({
  Name: image.name,
  Extension: image.extension,
  Modified: false,
  Optimized: false,
  Width: image.width,
  Height: image.height,
  Length: image.length,
  MD5: image.md5,
});

export const buildDashboardObject = (dashboard: Dashboard, ctx: SerializeContext): JsonObject => ({
  Version: 2,
  Id: dashboard.id ?? stableGuid(dashboardPath(ctx.packageName, dashboard.name)),
  BaseHeight: dashboard.height,
  BaseWidth: dashboard.width,
  BackgroundColor: normaliseHex(dashboard.backgroundColor),
  Screens: dashboard.screens.map((screen) => buildScreenObject(screen, dashboard, ctx)),
  SnapToGrid: true,
  HideLabels: false,
  ShowForeground: true,
  ForegroundOpacity: 100,
  ShowBackground: true,
  BackgroundOpacity: 100,
  ShowBoundingRectangles: true,
  GridSize: DEFAULT_GRID_SIZE,
  Images: (dashboard.images ?? []).map(buildImageDescriptor),
  Metadata: buildMetadataObject(dashboard),
  ShowOnScreenControls: true,
  IsOverlay: false,
  EnableClickThroughOverlay: true,
  EnableOnDashboardMessaging: false,
  UseStrictJSIsolation: true,
  // false, otherwise the DashStudio editor shows the "Legacy Javascript isolation is enabled" banner.
  UseStrictJSIsolationWarning: false,
});

/** The `.djson` text: 2-space indented JSON in SimHub's key order. */
export const serializeDashboard = (dashboard: Dashboard, ctx: SerializeContext): string => {
  const document = buildDashboardObject(dashboard, ctx);
  assertWholeNumbers(document, `the dashboard ${dashboard.name}`);
  return JSON.stringify(document, null, 2);
};

/** The `.djson.metadata` sidecar text. */
export const serializeMetadata = (dashboard: Dashboard): string => JSON.stringify(buildMetadataObject(dashboard), null, 2);
