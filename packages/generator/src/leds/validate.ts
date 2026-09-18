/**
 * What has to be true of a profile before it is written. Reports, never throws, in the same
 * `{ ok, errors, warnings }` shape `validatePackage` uses.
 *
 * Every rule here exists because the failure it catches is silent. SimHub does not refuse a bad
 * profile: an unknown `ContainerType` loads as a disabled `UnknownContainer`, a mis-parsed colour
 * becomes a transparent near-black, an effect past the end of the strip paints nothing, and a
 * mis-arity NCalc call evaluates to nothing. In every case the profile opens, reports no error,
 * and a light is simply not there — which is the LED version of the rule
 * `packages/dash/test/textFit.test.ts` keeps for text.
 */
import { unknownFunctions } from '../ncalcFunctions.ts';
import { propertyReferences, type ValidateOptions, type ValidationIssue, type ValidationResult } from '../validate.ts';
import { isGuid } from '../ids.ts';
import {
  REMAP_POSITIONS,
  childrenOf,
  containerTypeOf,
  isLedColor,
  KNOWN_CONTAINER_TYPES,
  ledCountOf,
  type LedContainer,
  type LedExpression,
  type LedProfile,
} from './model.ts';

/** How a profile is validated. `ledCount` is the strip the profile is generated for. */
export interface ValidateProfileOptions extends Partial<ValidateOptions> {
  /**
   * How many LEDs the device has. When given, an effect whose `StartPosition` plus `LedCount`
   * runs past the end fails — the rule #301 asks for, and the reason it is an error rather
   * than a warning is that nothing else would ever tell you.
   */
  ledCount?: number;
}

const strip = (name: string, prefix: string): string => (name.startsWith(`${prefix}.`) ? name.slice(prefix.length + 1) : name);

/** Every expression a container carries, with the path it sits at. */
const expressionsOf = (c: LedContainer, path: string): { expr: LedExpression; path: string }[] => {
  const at = (expr: LedExpression | undefined, key: string) => (expr ? [{ expr, path: `${path}#${key}` }] : []);
  return c.kind === 'conditionalGroup'
    ? at(c.trigger, 'TriggerFormula')
    : c.kind === 'customStatus'
      ? [...at(c.enabledFormula, 'EnabledFormula'), ...at(c.blinkFormula, 'BlinkFormula')]
      : c.kind === 'dynamicColor'
        ? at(c.colorFormula, 'ColorFormula')
        : c.kind === 'scriptedContent'
          ? at(c.contentFormula, 'ContentFormula')
          : [];
};

/** Every colour a container names, so that none of them can be the shorthand SimHub misreads. */
const colorsOf = (c: LedContainer): { color: string; key: string }[] => {
  const out: { color: string; key: string }[] = [];
  if (c.kind === 'staticColor' || c.kind === 'customStatus') {
    out.push({ color: c.color, key: 'Color' });
    if (c.blinkColor) out.push({ color: c.blinkColor, key: 'BlinkingColor' });
  }
  if (c.kind === 'rpmSegments') {
    c.segments.forEach((s, i) => {
      out.push({ color: s.color, key: `Segments[${i}]` });
      if (s.blinkColor) out.push({ color: s.blinkColor, key: `Segments[${i}].blink` });
    });
  }
  if (c.kind === 'animation') {
    c.frames.forEach((f, fi) =>
      f.pixels.forEach((row, r) => row.forEach((color, col) => color !== undefined && out.push({ color, key: `Frames[${fi}].${r},${col}` }))),
    );
  }
  return out;
};

/**
 * A container's `StartPosition` is relative to the group holding it, not to the strip.
 * `IContainerGroupExtensions.GetGroupResult` builds the group's own `LedResult` at
 * `group.StartPosition - 1` and then `LedResult.Merge`s each child at the *child's* position into
 * that buffer, so the offsets accumulate down the tree. `offset` is the sum of the enclosing
 * groups' positions, which is what makes the fit rule below the real one rather than a guess.
 */
const validateContainer = (c: LedContainer, path: string, offset: number, opts: ValidateProfileOptions, errors: ValidationIssue[], warnings: ValidationIssue[]): void => {
  const type = containerTypeOf(c);

  // An unresolvable ContainerType becomes a disabled UnknownContainer: the effect vanishes quietly.
  if (!KNOWN_CONTAINER_TYPES.has(type)) {
    errors.push({ code: 'leds/container-type', path, message: `${type} is not a ContainerType SimHub 9.12.6's RGB driver resolves; it would load as a disabled UnknownContainer` });
  }

  const start = c.startPosition ?? 1;
  if (!Number.isInteger(start) || start < 1) {
    errors.push({ code: 'leds/start-position', path, message: `StartPosition is ${start}; LED positions are 1-based` });
  }

  const count = ledCountOf(c);
  if (count !== undefined && (!Number.isInteger(count) || count < 1)) {
    errors.push({ code: 'leds/led-count', path, message: `LedCount is ${count}` });
  }

  // The fit rule. An effect running off the end of the strip paints nothing and says nothing.
  // A matrix is positioned in two dimensions, so the linear rule does not apply to it.
  const absolute = offset + start;
  if (opts.ledCount !== undefined && count !== undefined && count >= 1 && start >= 1 && c.kind !== 'animation') {
    const last = absolute + count - 1;
    if (last > opts.ledCount) {
      errors.push({
        code: 'leds/off-strip',
        path,
        message: `covers LEDs ${absolute}..${last} of a ${opts.ledCount}-LED strip; the last ${last - opts.ledCount} would not be drawn`,
      });
    }
  }

  if (c.kind === 'remapGroup') {
    if (c.positions.length === 0) errors.push({ code: 'leds/remap-empty', path, message: 'a RemapGroup with no positions drives nothing' });
    for (const p of c.positions) {
      if (!Number.isInteger(p) || p < 1) errors.push({ code: 'leds/remap-position', path, message: `position ${p} is not a 1-based LED index` });
      else if (opts.ledCount !== undefined && p > opts.ledCount) {
        errors.push({ code: 'leds/remap-position', path, message: `position ${p} is past the end of a ${opts.ledCount}-LED strip` });
      }
    }
    const seen = new Set(c.positions);
    if (seen.size !== c.positions.length) warnings.push({ code: 'leds/remap-duplicate', path, message: 'a physical LED appears twice in the remap; the later one wins' });
    // SetResultBase indexes Positions[i] for every lit LED below 64. Short is not quiet: it throws.
    if (opts.ledCount !== undefined && c.positions.length < Math.min(opts.ledCount, REMAP_POSITIONS)) {
      errors.push({
        code: 'leds/remap-short',
        path,
        message: `remaps ${c.positions.length} of ${opts.ledCount} LEDs; SetResultBase indexes every lit position and throws past the end of the list`,
      });
    }
  }

  if (c.kind === 'animation') {
    if (c.rows < 1 || c.columns < 1) errors.push({ code: 'leds/animation-size', path, message: `${c.columns}x${c.rows} is not a grid` });
    if (c.frames.length === 0) errors.push({ code: 'leds/animation-empty', path, message: 'an Animation with no frames draws nothing' });
    c.frames.forEach((f, i) => {
      if (f.durationMs <= 0) errors.push({ code: 'leds/frame-duration', path: `${path}#Frames[${i}]`, message: `FrameDuration is ${f.durationMs} ms` });
      if (f.pixels.length > c.rows) errors.push({ code: 'leds/frame-shape', path: `${path}#Frames[${i}]`, message: `${f.pixels.length} rows in a ${c.rows}-row animation` });
      f.pixels.forEach((row, r) => {
        if (row.length > c.columns) errors.push({ code: 'leds/frame-shape', path: `${path}#Frames[${i}]`, message: `row ${r} has ${row.length} pixels in a ${c.columns}-column animation` });
      });
    });
  }

  for (const { color, key } of colorsOf(c)) {
    if (!isLedColor(color)) {
      errors.push({
        code: 'leds/color',
        path: `${path}#${key}`,
        message: `${color} is not a colour SimHub reads; three-digit shorthand parses to a transparent near-black rather than failing`,
      });
    }
  }

  for (const { expr, path: where } of expressionsOf(c, path)) {
    if (expr.expression.trim() === '') {
      errors.push({ code: 'leds/expression-empty', path: where, message: 'an empty expression evaluates to nothing' });
      continue;
    }
    // NCalc only: a Javascript body is not NCalc and its function names are not NCalc's.
    if ((expr.interpreter ?? 'ncalc') === 'ncalc') {
      for (const u of unknownFunctions(expr.expression)) {
        errors.push({ code: `leds/ncalc-${u.reason}`, path: where, message: u.message });
      }
      if (opts.declaredProperties && opts.propertyPrefix) {
        const declared = new Set(opts.declaredProperties.map((p) => strip(p, opts.propertyPrefix!)));
        const foreign = new Set((opts.foreignProperties ?? []).map((p) => strip(p, opts.propertyPrefix!)));
        for (const ref of propertyReferences(expr.expression)) {
          if (!ref.startsWith(`${opts.propertyPrefix}.`)) continue;
          const name = strip(ref, opts.propertyPrefix);
          if (foreign.has(name)) errors.push({ code: 'leds/property-foreign', path: where, message: `${ref} belongs to another screen` });
          else if (!declared.has(name)) errors.push({ code: 'leds/property-undeclared', path: where, message: `${ref} is not declared in the contract` });
        }
      }
    }
  }

  // A remap group renumbers rather than offsets: its children address the remapped run from 1.
  const childOffset = c.kind === 'remapGroup' ? 0 : absolute - 1;
  childrenOf(c).forEach((child, i) => validateContainer(child, `${path}/${containerTypeOf(child)}[${i}]`, childOffset, opts, errors, warnings));
};

/** Whether a profile is fit to be written, and what is wrong with it if not. */
export const validateProfile = (profile: LedProfile, opts: ValidateProfileOptions = {}): ValidationResult => {
  const errors: ValidationIssue[] = [];
  const warnings: ValidationIssue[] = [];
  const root = profile.name;

  if (profile.name.trim() === '') errors.push({ code: 'leds/name', path: root, message: 'a profile needs a name; it is what the user picks it by' });
  // SimHub logs "Two profiles with the same unique id exists" and ignores the duplicate.
  if (!isGuid(profile.profileId)) errors.push({ code: 'leds/profile-id', path: root, message: `ProfileId ${profile.profileId} is not a lower-case GUID` });
  if (profile.containers.length === 0) errors.push({ code: 'leds/empty', path: root, message: 'a profile with no containers lights nothing' });
  if (profile.globalBrightness !== undefined && (profile.globalBrightness < 0 || profile.globalBrightness > 100)) {
    errors.push({ code: 'leds/brightness', path: root, message: `GlobalBrightness is ${profile.globalBrightness}; SimHub's range is 0 to 100` });
  }

  const ledCount = opts.ledCount ?? profile.ledCount;
  profile.containers.forEach((c, i) => validateContainer(c, `${root}/${containerTypeOf(c)}[${i}]`, 0, { ...opts, ledCount }, errors, warnings));

  return { ok: errors.length === 0, errors, warnings };
};
