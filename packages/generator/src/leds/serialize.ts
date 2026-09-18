/**
 * A `LedProfile` as the JSON SimHub reads. Pure `buildXxxObject(model) => JsonObject` functions,
 * with `serializeProfile` doing nothing but `JSON.stringify(obj, null, 2)`, matching the `.djson`
 * serialiser beside it.
 *
 * Every key written here was read off a real profile or out of the decompiled 9.12.6 source; keys
 * whose value equals SimHub's own default are omitted, because SimHub fills them in on first save
 * and a generated file that omits them is smaller and reads as what it actually decides.
 */
import type { JsonObject, JsonValue } from '../serialize.ts';
import {
  REMAP_POSITIONS,
  RPM_MODES,
  containerTypeOf,
  type LedAnimation,
  type LedConditionalGroup,
  type LedContainer,
  type LedCustomStatus,
  type LedDynamicColor,
  type LedExpression,
  type LedFrame,
  type LedGroup,
  type LedProfile,
  type LedRaw,
  type LedRemapGroup,
  type LedRpmSegments,
  type LedScriptedContent,
  type LedSegment,
  type LedStaticColor,
} from './model.ts';

const INTERPRETERS: Record<NonNullable<LedExpression['interpreter']>, number> = { ncalc: 0, javascript: 1 };

/**
 * An `ExpressionValue`. `Interpreter` is written only for Javascript, since 0 is the default, and
 * `JSExt` is never written: its default is `Local | Global`, which is what a generated profile
 * wants and what SimHub writes back.
 */
export const buildExpressionObject = (e: LedExpression): JsonObject => ({
  ...(e.interpreter && e.interpreter !== 'ncalc' ? { Interpreter: INTERPRETERS[e.interpreter] } : {}),
  Expression: e.expression,
});

/** `ledCount;startValue;normalColor;useBlinkingColor;blinkingColor`, the `LedSegmentJsonConverter` form. */
export const buildSegmentString = (s: LedSegment): string =>
  [s.ledCount, formatInvariant(s.startValue), s.color, s.blinkColor ? 1 : 0, s.blinkColor ?? ''].join(';');

/** `LedSegmentJsonConverter` writes the start value with the invariant culture and no thousands separator. */
const formatInvariant = (n: number): string => (Number.isInteger(n) ? String(n) : String(n));

/**
 * `row,col,color` triples joined by `;`, the `FrameColorsConverter` form. A cell left undefined is
 * not written at all, and SimHub reads it back as `Color.Empty`.
 */
export const buildFrameColors = (frame: LedFrame): string => {
  const parts: string[] = [];
  frame.pixels.forEach((row, r) => row.forEach((color, c) => color !== undefined && parts.push(`${r},${c},${color}`)));
  return parts.join(';');
};

const buildFrameObject = (frame: LedFrame): JsonObject => ({ Colors: buildFrameColors(frame), FrameDuration: frame.durationMs });

/** What every container carries, in the order `LedsContainerBase` declares it. */
const base = (c: LedContainer): JsonObject => ({
  ...(c.description !== undefined ? { Description: c.description } : {}),
  ...(c.startPosition !== undefined && c.startPosition !== 1 ? { StartPosition: c.startPosition } : {}),
  ...(c.enabled === false ? { IsEnabled: false } : {}),
});

const group = (c: LedGroup): JsonObject => ({ LedContainers: c.children.map(buildContainerObject) });

const conditionalGroup = (c: LedConditionalGroup): JsonObject => ({
  TriggerFormula: buildExpressionObject(c.trigger),
  ...(c.clearBackgroundWhenActive ? { ClearBackgroundWhenActive: true } : {}),
  LedContainers: c.children.map(buildContainerObject),
});

/**
 * `[{ "Position": n }, ...]`, padded to {@link REMAP_POSITIONS} with the natural position, which is
 * what SimHub's own `LoadDefaultSettings` writes. The padding is not cosmetic: `SetResultBase`
 * indexes `Positions[i]` for every lit LED below 64, so a list shorter than the run throws.
 */
export const buildRemapPositions = (positions: readonly number[]): JsonValue =>
  Array.from({ length: REMAP_POSITIONS }, (_, i) => ({ Position: positions[i] ?? i + 1 }));

const remapGroup = (c: LedRemapGroup): JsonObject => ({
  Positions: buildRemapPositions(c.positions),
  LedContainers: c.children.map(buildContainerObject),
});

const staticColor = (c: LedStaticColor): JsonObject => ({
  LedCount: c.ledCount,
  Color: c.color,
  ...blink(c.blinkColor, c.blinkDelayMs),
});

const customStatus = (c: LedCustomStatus): JsonObject => ({
  LedCount: c.ledCount,
  Color: c.color,
  ...blink(c.blinkColor, c.blinkDelayMs),
  EnabledFormula: buildExpressionObject(c.enabledFormula),
  ...(c.blinkFormula ? { BlinkFormula: buildExpressionObject(c.blinkFormula) } : {}),
});

/** `BlinkEnabled` gates the pair, so it is written whenever a blinking colour is given. */
const blink = (color: string | undefined, delayMs: number | undefined): JsonObject =>
  color === undefined ? {} : { BlinkingColor: color, BlinkEnabled: true, ...(delayMs !== undefined ? { BlinkDelay: delayMs } : {}) };

const dynamicColor = (c: LedDynamicColor): JsonObject => ({
  LedCount: c.ledCount,
  ColorFormula: buildExpressionObject(c.colorFormula),
});

const scriptedContent = (c: LedScriptedContent): JsonObject => ({
  LedCount: c.ledCount,
  ContentFormula: buildExpressionObject(c.contentFormula),
});

const rpmSegments = (c: LedRpmSegments): JsonObject => ({
  ...(c.blinkDelayMs !== undefined ? { BlinkDelay: c.blinkDelayMs } : {}),
  SegmentsCount: c.segments.length,
  RpmMode: RPM_MODES[c.rpmMode],
  ...(c.relativeToRedline ? { RelativeToRedline: true } : {}),
  ...(c.blinkOnLastGear ? { BlinkOnLastGear: true } : {}),
  Segments: c.segments.map(buildSegmentString),
});

const animation = (c: LedAnimation): JsonObject => ({
  Animation: { Columns: c.columns, Rows: c.rows, Frames: c.frames.map(buildFrameObject) },
});

const raw = (c: LedRaw): JsonObject => ({
  ...(c.fields ?? {}),
  ...(c.children ? { LedContainers: c.children.map(buildContainerObject) } : {}),
});

/** One container, `ContainerType` last so that the shape reads the way SimHub's own files do. */
export function buildContainerObject(c: LedContainer): JsonObject {
  const body: JsonObject =
    c.kind === 'group'
      ? group(c)
      : c.kind === 'conditionalGroup'
        ? conditionalGroup(c)
        : c.kind === 'remapGroup'
          ? remapGroup(c)
          : c.kind === 'staticColor'
            ? staticColor(c)
            : c.kind === 'customStatus'
              ? customStatus(c)
              : c.kind === 'dynamicColor'
                ? dynamicColor(c)
                : c.kind === 'scriptedContent'
                  ? scriptedContent(c)
                  : c.kind === 'rpmSegments'
                    ? rpmSegments(c)
                    : c.kind === 'animation'
                      ? animation(c)
                      : raw(c);
  return { ...base(c), ...body, ContainerType: containerTypeOf(c) };
}

/**
 * A whole profile. `CarChoices` is written as an empty array rather than omitted: SimHub always
 * writes one, and whether the editor copes with null was not established.
 */
export function buildProfileObject(profile: LedProfile): JsonObject {
  return {
    CarChoices: [],
    ...(profile.embeddedJavascript !== undefined ? { EmbeddedJavascript: profile.embeddedJavascript } : {}),
    ...(profile.globalBrightness !== undefined ? { GlobalBrightness: profile.globalBrightness } : {}),
    LedContainers: profile.containers.map(buildContainerObject) as JsonValue,
    ...(profile.useProfileBrightness ? { UseProfileBrightness: true } : {}),
    Name: profile.name,
    ProfileId: profile.profileId,
  };
}

/** The bytes of a `.ledsprofile`. SimHub writes `Formatting.Indented`, which is two spaces. */
export const serializeProfile = (profile: LedProfile): string => `${JSON.stringify(buildProfileObject(profile), null, 2)}\n`;
