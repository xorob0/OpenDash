/**
 * SimHub's RGB LED profile, as a typed model. Nothing here knows about openDash.
 *
 * A `.ledsprofile` is one `SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile` serialised with
 * Newtonsoft at `Formatting.Indented` — plain JSON, no compression, no `$type` graph. The format
 * is written down in docs/research/simhub-leds-format.md, verified against SimHub 9.12.6 by
 * decompiling `SimHub.Plugins.dll` and by round-tripping hand-written files through SimHub's own
 * `Profile` type on the test VM.
 *
 * The model covers the containers a generated profile actually needs rather than all fifty-six
 * SimHub declares; `raw` is the escape hatch for the rest, and keeps a profile expressible without
 * this file growing a case per effect.
 */
import type { JsonObject } from '../serialize.ts';

/**
 * A colour as SimHub's `ColorTranslator.FromHtml` reads it: `#RRGGBB`, `#AARRGGBB`, a .NET named
 * colour, or `R, G, B`. **Three-digit shorthand is not accepted** — `#F80` parses to ARGB
 * `00000F80`, a transparent near-black, and fails silently as an unlit LED. `isLedColor` rejects
 * it for that reason.
 */
export type LedColor = string;

/** SimHub's transparent, which is how an effect says "leave this LED alone". */
export const LED_TRANSPARENT: LedColor = 'Transparent';

const HEX6 = /^#[0-9A-Fa-f]{6}$/;
const HEX8 = /^#[0-9A-Fa-f]{8}$/;
const RGB_TRIPLE = /^\d{1,3}, ?\d{1,3}, ?\d{1,3}$/;
const RGB_QUAD = /^\d{1,3}, ?\d{1,3}, ?\d{1,3}, ?\d{1,3}$/;
const NAMED = /^[A-Za-z]+$/;

/** Whether SimHub will read this string as the colour it looks like. */
export const isLedColor = (value: unknown): value is LedColor =>
  typeof value === 'string' && (HEX6.test(value) || HEX8.test(value) || RGB_TRIPLE.test(value) || RGB_QUAD.test(value) || NAMED.test(value));

/** Which engine evaluates an expression. `Interpreter` 0 is NCalc, 1 is Javascript. */
export type LedInterpreter = 'ncalc' | 'javascript';

/**
 * One `ExpressionValue`. The same type the `.djson` bindings use, with the same NCalc
 * `[PropertyName]` syntax and the same `isnull()` helper — which is what lets a generated profile
 * read plugin properties and still work with the plugin absent.
 */
export interface LedExpression {
  expression: string;
  /** Defaults to NCalc, which is what SimHub writes when the key is absent. */
  interpreter?: LedInterpreter;
}

/** One band of an `RPMSegments` strip. Serialised as `ledCount;startValue;color;useBlink;blinkColor`. */
export interface LedSegment {
  ledCount: number;
  /** Percent, percent-of-redline or absolute RPM, according to the container's `rpmMode`. */
  startValue: number;
  color: LedColor;
  blinkColor?: LedColor;
}

/** What an `RPMSegments` segment's `startValue` means. */
export type RpmMode = 'percent' | 'redlinePercent' | 'rpms';

export const RPM_MODES: Record<RpmMode, number> = { percent: 0, redlinePercent: 1, rpms: 2 };

/** One frame of an `Animation`: a grid of pixels and how long it is shown. */
export interface LedFrame {
  /** `pixels[row][column]`. A cell left undefined is not written and stays `Color.Empty`. */
  pixels: readonly (readonly (LedColor | undefined)[])[];
  durationMs: number;
}

/** What every container carries, from `LedsContainerBase`. */
interface LedContainerBase {
  /** 1-based index of the first LED this effect paints. SimHub's default, omitted when 1. */
  startPosition?: number;
  /** Shown as the effect's label in SimHub's editor. Cosmetic. */
  description?: string;
  /** SimHub's default is true, and it is omitted when true. */
  enabled?: boolean;
}

/** A plain group: its children are composed in order. */
export interface LedGroup extends LedContainerBase {
  kind: 'group';
  children: readonly LedContainer[];
}

/** A group whose children are drawn only while an expression is true. */
export interface LedConditionalGroup extends LedContainerBase {
  kind: 'conditionalGroup';
  trigger: LedExpression;
  /** Blank the LEDs underneath rather than composing over them. */
  clearBackgroundWhenActive?: boolean;
  children: readonly LedContainer[];
}

/**
 * Reorders physical LED positions into logical ones, which is how one effect tree drives strips
 * that are wired differently. `positions` is 1-based: `positions[i]` is the physical LED that
 * logical LED `i` paints.
 *
 * It is serialised as `[{ "Position": n }, ...]` rather than as a list of numbers, because
 * `RemapGroupContainer.Positions` is an `ObservableCollection<LedPosition>`. And it must be at
 * least as long as the run it covers: `SetResultBase` indexes `Positions[i]` for every lit LED up
 * to 64, so a short list throws once per frame rather than failing quietly. SimHub's own
 * `LoadDefaultSettings` fills all {@link REMAP_POSITIONS} of them, and so does the writer.
 */
export interface LedRemapGroup extends LedContainerBase {
  kind: 'remapGroup';
  positions: readonly number[];
  children: readonly LedContainer[];
}

/**
 * How many entries a `Groups.RemapGroup` carries. `SetResultBase` guards its lookup with
 * `if (i < 64)`, and `LoadDefaultSettings` writes exactly this many.
 */
export const REMAP_POSITIONS = 64;

/** A run of LEDs in one colour. */
export interface LedStaticColor extends LedContainerBase {
  kind: 'staticColor';
  ledCount: number;
  color: LedColor;
  blinkColor?: LedColor;
  blinkDelayMs?: number;
}

/** A run of LEDs in one colour, shown while a formula is true, optionally blinking on another. */
export interface LedCustomStatus extends LedContainerBase {
  kind: 'customStatus';
  ledCount: number;
  color: LedColor;
  enabledFormula: LedExpression;
  blinkFormula?: LedExpression;
  blinkColor?: LedColor;
  blinkDelayMs?: number;
}

/**
 * Every LED of a run computed by one expression returning an array of colours. The door that lets
 * a profile suit every car without a per-car table.
 */
export interface LedScriptedContent extends LedContainerBase {
  kind: 'scriptedContent';
  ledCount: number;
  contentFormula: LedExpression;
}

/** A strip described band by band, which is the shape of a real car's LED bar. */
export interface LedRpmSegments extends LedContainerBase {
  kind: 'rpmSegments';
  segments: readonly LedSegment[];
  rpmMode: RpmMode;
  relativeToRedline?: boolean;
  blinkOnLastGear?: boolean;
  blinkDelayMs?: number;
}

/** A picture, or a sequence of them: the 8x8 matrix effect. */
export interface LedAnimation extends LedContainerBase {
  kind: 'animation';
  rows: number;
  columns: number;
  frames: readonly LedFrame[];
}

/**
 * An effect this model does not spell out. `containerType` is written verbatim and `fields` are
 * merged in, so a profile can use any of SimHub's fifty-six containers without a case here.
 */
export interface LedRaw extends LedContainerBase {
  kind: 'raw';
  containerType: string;
  fields?: JsonObject;
  children?: readonly LedContainer[];
}

export type LedContainer =
  | LedGroup
  | LedConditionalGroup
  | LedRemapGroup
  | LedStaticColor
  | LedCustomStatus
  | LedScriptedContent
  | LedRpmSegments
  | LedAnimation
  | LedRaw;

/** A whole profile: what a `.ledsprofile` file holds, and what one entry of `Profiles` holds. */
export interface LedProfile {
  name: string;
  /** A GUID. SimHub logs and ignores a duplicate, so this must be stable and unique. */
  profileId: string;
  containers: readonly LedContainer[];
  /** Profile-scoped Javascript the `javascript` expressions may call. */
  embeddedJavascript?: string;
  /** 0 to 100. SimHub's default is 100, and it only applies when `useProfileBrightness` is set. */
  globalBrightness?: number;
  useProfileBrightness?: boolean;
  /** How many LEDs the device this profile is generated for has. Not written; validation uses it. */
  ledCount?: number;
}

/** The `ContainerType` a container is written as. */
export const CONTAINER_TYPES: Record<Exclude<LedContainer['kind'], 'raw'>, string> = {
  group: 'Base.Group',
  conditionalGroup: 'Groups.CustomConditionalGroup',
  remapGroup: 'Groups.RemapGroup',
  staticColor: 'StaticColor',
  customStatus: 'CustomStatus',
  scriptedContent: 'ScriptedContent',
  rpmSegments: 'RPMSegments',
  animation: 'Animation',
};

/**
 * Every `ContainerType` SimHub 9.12.6's RGB driver resolves, enumerated by reflection over the
 * fifty-six non-abstract subclasses of `LedsContainerBase` in the installed `SimHub.Plugins.dll`.
 * A type not in this list loads as a disabled `UnknownContainer`, which is a silent failure: the
 * profile opens, reports nothing, and the effect is simply not there.
 */
export const KNOWN_CONTAINER_TYPES: ReadonlySet<string> = new Set([
  'Animation',
  'Base.Group',
  'Base.IncludeProfileGroup',
  'Brake',
  'CustomGradient',
  'CustomStatus',
  'DynamicColor',
  'Flags.BlackFlag',
  'Flags.BlueFlag',
  'Flags.GreenFlag',
  'Flags.RedlineReached',
  'Flags.WhiteFlag',
  'Flags.YellowFlag',
  'Fuel',
  'Gap.Delta',
  'Groups.BrakeGroup',
  'Groups.BreathGroup',
  'Groups.BrightnessFormulaGroup',
  'Groups.BrightnessGroup',
  'Groups.CurrentGameGroup',
  'Groups.CustomConditionalGroup',
  'Groups.FormulaShiftGroup',
  'Groups.GameCarInPitLaneGroup',
  'Groups.GameCarModelGroup',
  'Groups.GameCarSpeedLimiterGroup',
  'Groups.GameCarStatedGroup',
  'Groups.GameNotRunningGroup',
  'Groups.GameRunningGroup',
  'Groups.KeepXOfYGroup',
  'Groups.MirrorGroup',
  'Groups.RemapGroup',
  'Groups.RepeatGroup',
  'Groups.ScrollGroup',
  'RPM',
  'RPMSegments',
  'ScriptedContent',
  'Speed',
  'StaticColor',
  'StaticGradient',
  'Status.AbsActive',
  'Status.AbsOn',
  'Status.BrakeActive',
  'Status.DrsAvailable',
  'Status.DrsOn',
  'Status.Gear',
  'Status.LowFuelRemainingLapsAlert',
  'Status.SpeedLimiter',
  'Status.SpeedLimiterAnimation',
  'Status.SpotterCarLeft',
  'Status.SpotterCarRight',
  'Status.TCActive',
  'Status.TCOn',
  'Status.TurnIndicatorLeft',
  'Status.TurnIndicatorRight',
  'Turbo',
]);

/** The `ContainerType` a container will be written as. */
export const containerTypeOf = (c: LedContainer): string => (c.kind === 'raw' ? c.containerType : CONTAINER_TYPES[c.kind]);

/** The children a container composes, which is empty for every leaf effect. */
export const childrenOf = (c: LedContainer): readonly LedContainer[] =>
  c.kind === 'group' || c.kind === 'conditionalGroup' || c.kind === 'remapGroup' ? c.children : c.kind === 'raw' ? (c.children ?? []) : [];

/** How many LEDs a container paints, or undefined for one that does not say. */
export const ledCountOf = (c: LedContainer): number | undefined =>
  c.kind === 'staticColor' || c.kind === 'customStatus' || c.kind === 'scriptedContent'
    ? c.ledCount
    : c.kind === 'rpmSegments'
      ? c.segments.reduce((n, s) => n + s.ledCount, 0)
      : c.kind === 'animation'
        ? c.rows * c.columns
        : undefined;
