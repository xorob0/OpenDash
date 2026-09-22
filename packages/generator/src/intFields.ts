/**
 * The fields SimHub deserialises as `int`, and the walk that refuses a fraction at one of them.
 *
 * Json.NET reads an `int` property with `JsonTextReader.ReadAsInt32`, which throws
 * `JsonReaderException: Input string '3.5999999999999996' is not a valid integer` rather than
 * truncating. The throw is not confined to the property or even to the item: it unwinds
 * `EditorModel.LoadFromFile`, so **the whole dashboard fails to load and draws nothing**. A face
 * whose zones are widgets loses those zones entirely while the rest of it draws, which is exactly
 * how it reached a driver in 0.3.0-rc.1: one corner radius computed as 0.2 x 18 blanked zone B,
 * zone C, every companion and every pit wall zone, in every package, with nothing in the log until
 * somebody opened one of the sub-dashboards on its own.
 *
 * The list is the public integer properties of `SimHub.Plugins.OutputPlugins.GraphicalDash.*` read
 * out of the decompiled 9.12.6 `SimHub.Plugins.dll`, less the names that are floating point on
 * another class in the same namespaces -- `Left`, `Top`, `Width`, `Height`, `Bottom`, `Right`,
 * `X`, `Y` are `double` on `DrawableItem` and `int` elsewhere, so they cannot be judged by name
 * alone and are left out. A name here is one that is an `int` wherever it appears, which is what
 * makes the check safe to run over a whole document without a type for every node.
 */

/** A JSON value as the serialiser produces it; the same shape as serialize.ts's JsonValue. */
type Value = string | number | boolean | null | Value[] | { [key: string]: Value };

/**
 * Every field name that is an `int` on every class that declares it.
 *
 * Sorted, so that adding one is a one-line diff. `ZIndex`, `RenderingSkip` and `InitialScreenIndex`
 * are the ones OpenDash writes most often; the rest are here because the cost of carrying a name
 * we never write is nothing and the cost of missing one is a blank dashboard.
 */
export const SIMHUB_INT_FIELDS: ReadonlySet<string> = new Set([
  'AdditionalItemsCount',
  'AllBorders',
  'AllCornerRadius',
  'AllMargins',
  'AllPaddings',
  'AvailableLedCount',
  'BaseHeight',
  'BaseWidth',
  'BorderBottom',
  'BorderLeft',
  'BorderRight',
  'BorderTop',
  'Brightness',
  'ButtonsCount',
  'ColorColumns',
  'ColorRows',
  'Contrast',
  'CrossHairPosition',
  'Decimals',
  'DisabledOpacity',
  'DisplayHeight',
  'DisplayWidth',
  'FlashInterval',
  'ForceRefreshDelaySeconds',
  'GearBlinkDelay',
  'GridSize',
  'HardwareLEDs',
  'ImageRefreshIntervalSeconds',
  'Index',
  'Initial8x8Rotation',
  'InitialScreenIndex',
  'LeaderboardPosition',
  'LedCount',
  'LedNumber',
  'Ledcount',
  'LedsCount',
  'LedsPhysicalPosition',
  'LedsUpdateDivisor',
  'LeftRightShift',
  'Length',
  'LineTickness',
  'MainPreviewIndex',
  'MarginBottom',
  'MarginLeft',
  'MarginRight',
  'MarginTop',
  'MaxBrightness',
  'MaxPoints',
  'MinimumLedSpacing',
  'OverlayMaxDuration',
  'OverlayMinDuration',
  'PaddingBottom',
  'PaddingLeft',
  'PaddingRight',
  'PaddingTop',
  'PictureHeight',
  'PitlimiterBlinkDelay',
  'PressedOpacity',
  'PrimarySplitSize',
  'Priority',
  'RadiusBottomLeft',
  'RadiusBottomRight',
  'RadiusTopLeft',
  'RadiusTopRight',
  'RawLedCount',
  'RedlineBlinkDelay',
  'RedlineFlashOpacity',
  'RenderHeight',
  'RenderWidth',
  'RenderingSkip',
  'RepeatIndex',
  'Repeated',
  'Repetitions',
  'ScreenSid',
  'SecondsDecimals',
  'SectorIndex',
  'SectorsCount',
  'SelectedIndex',
  'ShadowBlur',
  'ShadowDepth',
  'ShadowDirection',
  'Sid',
  'Size',
  'SplitLineThickness',
  'SplitSize',
  'TopBottomShift',
  'Total',
  'TotalLeds',
  'Version',
  'ZIndex',
]);

/** Where a fraction was found, and what it was. */
export interface FractionalField {
  /** A JSON path from the document root, as the SimHub log spells one: `Screens[6].Items[2].BorderStyle.RadiusTopLeft`. */
  path: string;
  value: number;
}

/** Every `int` field of the document holding a number SimHub could not read as one. */
export function fractionalIntFields(document: Value): FractionalField[] {
  const found: FractionalField[] = [];
  const walk = (node: Value, path: string): void => {
    if (Array.isArray(node)) {
      node.forEach((child, i) => walk(child, `${path}[${i}]`));
      return;
    }
    if (node === null || typeof node !== 'object') return;
    for (const [key, value] of Object.entries(node)) {
      const here = path === '' ? key : `${path}.${key}`;
      if (typeof value === 'number' && SIMHUB_INT_FIELDS.has(key) && !Number.isInteger(value)) {
        found.push({ path: here, value });
      }
      walk(value, here);
    }
  };
  walk(document, '');
  return found;
}

/**
 * Throws unless every `int` field of the document holds a whole number.
 *
 * Called from the serialisers rather than from the validator, because the validator judges the
 * model and this is a fact about the JSON: a radius is one model field and four JSON ones, and a
 * fraction can arrive from anything that scales. Throwing here means a document SimHub would
 * refuse is never written -- the build stages every package before it writes one, so the output
 * directory is left as it was, which is what a bad package already does.
 */
export function assertWholeNumbers(document: Value, what: string): void {
  const bad = fractionalIntFields(document);
  if (bad.length === 0) return;
  const shown = bad
    .slice(0, 5)
    .map((f) => `  ${f.path} = ${f.value}`)
    .join('\n');
  const rest = bad.length > 5 ? `\n  ...and ${bad.length - 5} more` : '';
  throw new Error(
    `${what}: ${bad.length} field(s) SimHub reads as an integer hold a fraction, and it refuses the whole file for one of them:\n${shown}${rest}`,
  );
}
