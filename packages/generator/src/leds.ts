/**
 * The SimHub LED matrix profile (`.ledsprofile`) as a model and a serialiser, the way model.ts
 * and serialize.ts are for a `.djson`. An 8x8 flag box is SimHub's `RGBMatrixDriver`, which is a
 * different assembly from the strip driver with a different container vocabulary; the facts this
 * file encodes are in docs/research/simhub-leds-format.md and the two easy ones to get wrong are
 * repeated where they bite:
 *
 *   - `ContainerType` is the bare .NET class name, `Container` suffix included. The strip driver
 *     trims the namespace and the suffix; this one does not, and there is no UnknownContainer
 *     fallback, so a name SimHub cannot resolve throws and takes the whole profile with it.
 *   - A group writes its position as `StartPositionXEx`/`StartPositionYEx`. A leaf writes
 *     `StartPositionX`/`StartPositionY`. A group that writes the leaf spelling lands at the origin
 *     without complaining.
 *   - Siblings compose painter's-algorithm: `MultiMatrixResult.Merge` runs in list order, so the
 *     LATER sibling paints over the earlier one. Every group openDash emits has mutually exclusive
 *     children, so order does not decide anything here — but it would if that ever stopped being
 *     true, and the opposite assumption is the natural one to make.
 */

import { isHex, normaliseHex } from './color.ts';
import { stableGuid } from './ids.ts';
import type { Formula, Hex } from './model.ts';
import { unknownFunctions } from './ncalcFunctions.ts';
import { buildFormulaObject, type JsonObject, type JsonValue } from './serialize.ts';
import { propertyReferences, type ValidationIssue, type ValidationResult } from './validate.ts';

export const PROFILE_EXTENSION = '.ledsprofile';

/**
 * `DeviceKind`, a [Flags] enum in SimHub. Only the values openDash can target are here;
 * `DeviceMetadata` spells the sizes rows-by-columns, so Matrix8x8 is 8 rows of 8.
 */
export const DEVICE_KIND = {
  matrix8x8: 2,
  psp: 4,
  matrix4x4: 8,
  matrix5x4: 0x10,
  matrix5x7: 0x20,
} as const;
export type DeviceKindName = keyof typeof DEVICE_KIND;

/** Rows and columns of each device kind, from SimHub's DeviceKindMetadata. */
export const DEVICE_SIZE: Record<DeviceKindName, { rows: number; columns: number }> = {
  matrix8x8: { rows: 8, columns: 8 },
  psp: { rows: 5, columns: 4 },
  matrix4x4: { rows: 4, columns: 4 },
  matrix5x4: { rows: 4, columns: 5 },
  matrix5x7: { rows: 7, columns: 5 },
};

/** SimHub composes at most four matrix contents: `MultiMatrixResult` holds `MatrixResult[4]`. */
export const MAX_MATRICES = 4;

/**
 * One frame of an animation. `pixels` is rows of columns, `null` where nothing is lit; a still
 * picture is one frame. Row and column indices are zero-based here and in the file, unlike a
 * container's one-based StartPosition.
 */
export interface MatrixFrame {
  /** Milliseconds this frame is held. */
  durationMs: number;
  pixels: readonly (readonly (Hex | null)[])[];
}

/** Fields every container carries. Positions are one-based and default to 1. */
export interface MatrixContainerBase {
  /** Shown in SimHub's effect list. Omitted from the file when absent. */
  description?: string;
  /** Default true. A disabled container is written and ignored. */
  enabled?: boolean;
  /** Which of the four matrix contents this paints, 1..4. Default 1. */
  matrix?: number;
  /** One-based column and row of the container's top-left. Default 1. */
  x?: number;
  y?: number;
  /** Stable id; derived from the container's path in the tree when absent. */
  id?: string;
}

/** A hand-drawn picture or animation: the only container openDash draws its own glyphs with. */
export interface AnimationContainer extends MatrixContainerBase {
  kind: 'animation';
  frames: readonly MatrixFrame[];
  /** The editor's pen colour. Cosmetic, but SimHub writes it, so it is deterministic here. */
  penColor?: Hex;
}

/** Children in priority order: the first that paints a pixel owns it. */
export interface GroupContainer extends MatrixContainerBase {
  kind: 'group';
  children: readonly MatrixContainer[];
  /** SimHub's `StackLeftToRight`; omitted from the file when false. */
  stackLeftToRight?: boolean;
}

/** "When formula is true": the door an `OpenDash.*` property reaches the lights through. */
export interface ConditionalGroupContainer extends MatrixContainerBase {
  kind: 'when';
  /** NCalc by default, the same ExpressionValue every `.djson` binding uses. */
  formula: string | Formula;
  children: readonly MatrixContainer[];
  /** Blank the matrix before painting the children. Omitted from the file when false. */
  clearBackground?: boolean;
}

/** "When the game is running" / "when it is not". */
export interface GameRunningGroupContainer extends MatrixContainerBase {
  kind: 'gameRunning' | 'gameNotRunning';
  children: readonly MatrixContainer[];
  clearBackground?: boolean;
}

/**
 * "After the car is started", for `durationMs` **milliseconds**, not seconds: SimHub's
 * `GameCarStatedGroupContainer.IsActive` does `CarStartedTime.AddMilliseconds(Duration)` and the
 * property defaults to 1000. Modelling it as seconds would have made every such group a thousand
 * times too short.
 */
export interface CarStartedGroupContainer extends MatrixContainerBase {
  kind: 'carStarted';
  durationMs: number;
  children: readonly MatrixContainer[];
  clearBackground?: boolean;
}

/** A fixed brightness for everything inside it, 0..100. */
export interface BrightnessGroupContainer extends MatrixContainerBase {
  kind: 'brightness';
  percent: number;
  children: readonly MatrixContainer[];
  clearBackground?: boolean;
}

/** A brightness computed per frame. */
export interface BrightnessFormulaGroupContainer extends MatrixContainerBase {
  kind: 'brightnessFormula';
  formula: string | Formula;
  children: readonly MatrixContainer[];
  clearBackground?: boolean;
}

export type MatrixContainer =
  | AnimationContainer
  | GroupContainer
  | ConditionalGroupContainer
  | GameRunningGroupContainer
  | CarStartedGroupContainer
  | BrightnessGroupContainer
  | BrightnessFormulaGroupContainer;

/** A whole profile: what one `.ledsprofile` file holds. */
export interface MatrixProfile {
  /** Shown in SimHub's profile list. */
  name: string;
  deviceKind: DeviceKindName;
  containers: readonly MatrixContainer[];
  /** 0..100. SimHub's own global brightness for the profile. */
  globalBrightness?: number;
  /** Whether the profile's brightness overrides the device's. Default false. */
  useProfileBrightness?: boolean;
  author?: string;
  description?: string;
  /** Stable id; derived from the name when absent. */
  id?: string;
}

/** The `ContainerType` string for each kind. The bare class name: see the header. */
export const CONTAINER_TYPE: Record<MatrixContainer['kind'], string> = {
  animation: 'AnimationContainer',
  group: 'GroupContainer',
  when: 'CustomConditionalGroupContainer',
  gameRunning: 'GameRunningGroupContainer',
  gameNotRunning: 'GameNotRunningGroupContainer',
  carStarted: 'GameCarStatedGroupContainer',
  brightness: 'BrightnessGroupContainer',
  brightnessFormula: 'BrightnessFormulaGroupContainer',
};

/** Kinds that hold children, and therefore write the `Ex` position spelling. */
const GROUP_KINDS = new Set<MatrixContainer['kind']>(['group', 'when', 'gameRunning', 'gameNotRunning', 'carStarted', 'brightness', 'brightnessFormula']);

export const isGroup = (container: MatrixContainer): boolean => GROUP_KINDS.has(container.kind);

/** The children of a container, empty for a leaf. */
export const childrenOf = (container: MatrixContainer): readonly MatrixContainer[] =>
  'children' in container ? container.children : [];

/**
 * A frame's pixels as SimHub's one string: `row,column,#colour` triples joined by `;`, in row
 * then column order. `Frame.FrameColorsConverter` writes the outer dictionary key first, and
 * `GetPixel(x, y)` reads `Colors[y][x]`, so the row comes first. Unlit pixels are simply absent.
 */
export const encodeFramePixels = (frame: MatrixFrame): string => {
  const parts: string[] = [];
  frame.pixels.forEach((row, y) => {
    row.forEach((colour, x) => {
      if (colour === null || colour === undefined) return;
      parts.push(`${y},${x},${encodeFrameColour(colour)}`);
    });
  });
  return parts.join(';');
};

/**
 * `ColorHelper.ToHtml`: `#RRGGBB`, gaining a leading alpha pair only when alpha is not FF. The
 * model normalises to `#AARRGGBB`, so this drops a fully opaque alpha again.
 */
export const encodeFrameColour = (colour: Hex): string => {
  const normalised = normaliseHex(colour);
  const alpha = normalised.slice(1, 3);
  const rgb = normalised.slice(3);
  return alpha === 'FF' ? `#${rgb}` : `#${alpha}${rgb}`;
};

const positiveInt = (name: string, value: number, max?: number): number => {
  if (!Number.isInteger(value) || value < 1 || (max !== undefined && value > max)) {
    throw new RangeError(`${name} must be an integer 1..${max ?? '∞'}, got ${value}`);
  }
  return value;
};

const percent = (name: string, value: number): number => {
  if (!Number.isFinite(value) || value < 0 || value > 100) throw new RangeError(`${name} must be 0..100, got ${value}`);
  return Math.round(value);
};

/**
 * Where a container sits, which is what seeds its id. The description is preferred over the
 * index so that adding or removing a sibling does not renumber everything after it: an id that
 * churns on an unrelated edit makes every diff of a generated profile unreadable, which is the
 * same reason `itemPath` names items rather than counting them.
 */
export const containerPath = (parentPath: string, index: number, description?: string): string =>
  `${parentPath}/${description !== undefined && description !== '' ? description : index}`;

const buildAnimation = (container: AnimationContainer, rows: number, columns: number): JsonObject => ({
  Columns: columns,
  Rows: rows,
  PenColor: normaliseHex(container.penColor ?? '#FFFFFFFF'),
  Frames: container.frames.map((frame) => {
    if (!Number.isInteger(frame.durationMs) || frame.durationMs < 0) {
      throw new RangeError(`frame duration must be a non-negative integer of milliseconds, got ${frame.durationMs}`);
    }
    frame.pixels.forEach((row, y) => {
      if (y >= rows) throw new RangeError(`frame has a row ${y} on a ${rows}-row matrix`);
      if (row.length > columns) throw new RangeError(`frame row ${y} has ${row.length} pixels on a ${columns}-column matrix`);
    });
    return { Colors: encodeFramePixels(frame), FrameDuration: frame.durationMs };
  }),
});

/** One container and everything under it. `path` seeds the ids so a rebuild does not churn them. */
export const buildContainerObject = (container: MatrixContainer, path: string, deviceKind: DeviceKindName): JsonObject => {
  const { rows, columns } = DEVICE_SIZE[deviceKind];
  const o: JsonObject = { ContainerType: CONTAINER_TYPE[container.kind] };
  if (container.description !== undefined) o.Description = container.description;
  o.ContainerId = container.id ?? stableGuid(path);
  o.IsEnabled = container.enabled ?? true;
  o.StartPositionMatrix = positiveInt('matrix', container.matrix ?? 1, MAX_MATRICES);
  // Groups rename these two, and a group that writes the leaf spelling lands at the origin.
  const xKey = isGroup(container) ? 'StartPositionXEx' : 'StartPositionX';
  const yKey = isGroup(container) ? 'StartPositionYEx' : 'StartPositionY';
  o[xKey] = positiveInt('x', container.x ?? 1, columns);
  o[yKey] = positiveInt('y', container.y ?? 1, rows);
  // `DeviceKind` is deliberately NOT written. MatrixContainerBase declares it with a private setter
  // and no [JsonProperty], so Json.NET marks it non-writable and drops it on load; SimHub sets it
  // itself from the driver when the profile is opened. Emitting it added one dead field per
  // container -- six hundred of them in the shipped profile -- that no reader ever sees.

  switch (container.kind) {
    case 'animation':
      o.Animation = buildAnimation(container, rows, columns);
      return o;
    case 'when':
      o.TriggerFormula = buildFormulaObject(container.formula);
      break;
    case 'brightness':
      o.Brightness = percent('brightness', container.percent);
      break;
    case 'brightnessFormula':
      o.BrightnessFormula = buildFormulaObject(container.formula);
      break;
    case 'carStarted':
      if (!Number.isInteger(container.durationMs) || container.durationMs < 0) {
        throw new RangeError(`carStarted durationMs must be a non-negative integer, got ${container.durationMs}`);
      }
      o.Duration = container.durationMs;
      break;
    case 'group':
      if (container.stackLeftToRight === true) o.StackLeftToRight = true;
      break;
    default:
      break;
  }
  // DefaultValueHandling.Ignore in SimHub: written only when true.
  if (container.kind !== 'group' && (container as { clearBackground?: boolean }).clearBackground === true) {
    o.ClearBackgroundWhenActive = true;
  }
  o.LedContainers = childrenOf(container).map((child, i) => buildContainerObject(child, containerPath(path, i, child.description), deviceKind));
  return o;
};

/** The profile object, in the shape `FromJsonFile<RGBMatrixProfile>` reads back. */
export const buildProfileObject = (profile: MatrixProfile): JsonObject => {
  const id = profile.id ?? stableGuid(`ledsprofile/${profile.name}`);
  const brightness = percent('globalBrightness', profile.globalBrightness ?? 100);
  const o: JsonObject = {
    CarChoices: [],
    CarChoice: null,
    GameCode: null,
    UseStrictJSIsolation: false,
    EmbeddedJavascript: null,
    GlobalBrightness: brightness,
    GlobalBrightnessPreset: { CurrentMode: 0, Brightness: brightness },
    LedContainers: profile.containers.map((child, i) => buildContainerObject(child, containerPath(`ledsprofile/${profile.name}`, i, child.description), profile.deviceKind)),
    Name: profile.name,
    ProfileId: id,
    UseProfileBrightness: profile.useProfileBrightness ?? false,
  };
  if (profile.author !== undefined) o.Author = profile.author;
  if (profile.description !== undefined) o.Description = profile.description;
  return o;
};

/**
 * The file SimHub reads. `JsonExtensions.ToJsonFile(profile, path, preserveReferences: false)` is
 * `JsonConvert.SerializeObject(item, Formatting.Indented)`, which is two spaces and no trailing
 * newline; matching it byte for byte keeps a hand-exported profile diffable against a built one.
 */
export const serializeProfile = (profile: MatrixProfile): string => JSON.stringify(buildProfileObject(profile) as JsonValue, null, 2);

/** Every container in the tree, parents before children, in file order. */
export function* walkContainers(containers: readonly MatrixContainer[]): Generator<MatrixContainer> {
  for (const container of containers) {
    yield container;
    yield* walkContainers(childrenOf(container));
  }
}

// --- Validation -----------------------------------------------------------------------------

/**
 * The same checks a package gets, on the things a profile can get wrong. Every `[OpenDash.X]` an
 * expression reads has to be a property the plugin attaches, which is what stops the profile and
 * the settings panel drifting apart; every function has to be one SimHub dispatches, with an
 * arity it dispatches on, because SimHub evaluates a bad expression to nothing and says nothing.
 */
export function validateProfile(profile: MatrixProfile, opts: ValidateProfileOptions): ValidationResult {
  const errors: ValidationIssue[] = [];
  const warnings: ValidationIssue[] = [];
  const prefix = `${opts.propertyPrefix}.`;
  const bare = (p: string): string => (p.startsWith(prefix) ? p.slice(prefix.length) : p);
  const declared = new Set(opts.declaredProperties.map(bare));
  const { rows, columns } = DEVICE_SIZE[profile.deviceKind];
  const ids = new Map<string, string>();

  const checkExpression = (formula: string | Formula, path: string): void => {
    const f: Formula = typeof formula === 'string' ? { expression: formula } : formula;
    for (const expression of [f.expression, f.preExpression].filter((e): e is string => e !== undefined && e !== '')) {
      for (const ref of propertyReferences(expression)) {
        if (!ref.startsWith(prefix)) continue;
        if (!declared.has(ref.slice(prefix.length))) {
          errors.push({ code: 'property/undeclared', path, message: `[${ref}] is not a declared ${opts.propertyPrefix} property` });
        }
      }
      for (const bad of unknownFunctions(expression)) {
        errors.push({
          code: bad.reason === 'unknown' ? 'expression/unknown-function' : 'expression/arity',
          path,
          message: `${bad.message}; SimHub evaluates the whole expression to nothing`,
        });
      }
    }
  };

  const visit = (container: MatrixContainer, path: string): void => {
    const id = container.id ?? stableGuid(path);
    const seen = ids.get(id);
    if (seen !== undefined) errors.push({ code: 'leds/duplicate-id', path, message: `container id ${id} is already used by ${seen}` });
    else ids.set(id, path);

    const matrix = container.matrix ?? 1;
    if (!Number.isInteger(matrix) || matrix < 1 || matrix > MAX_MATRICES) {
      errors.push({ code: 'leds/matrix-range', path, message: `matrix ${matrix} is outside 1..${MAX_MATRICES}; SimHub composes at most ${MAX_MATRICES}` });
    }
    if (container.kind === 'animation') {
      if (container.frames.length === 0) warnings.push({ code: 'leds/no-frames', path, message: 'an animation with no frames paints nothing' });
      container.frames.forEach((frame, i) => {
        if (frame.pixels.length > rows) {
          errors.push({ code: 'leds/frame-size', path: `${path}#frame${i}`, message: `frame has ${frame.pixels.length} rows on a ${rows}-row matrix` });
        }
        frame.pixels.forEach((row, y) => {
          if (row.length > columns) {
            errors.push({ code: 'leds/frame-size', path: `${path}#frame${i}`, message: `frame row ${y} has ${row.length} pixels on a ${columns}-column matrix` });
          }
          for (const colour of row) {
            if (colour !== null && colour !== undefined && !isHex(colour)) {
              errors.push({ code: 'color/invalid', path: `${path}#frame${i}`, message: `${JSON.stringify(colour)} is not #RRGGBB or #AARRGGBB` });
            }
          }
        });
      });
    }
    if (container.kind === 'when' || container.kind === 'brightnessFormula') checkExpression(container.formula, path);
    if (isGroup(container) && childrenOf(container).length === 0) {
      warnings.push({ code: 'leds/empty-group', path, message: 'a group with no children paints nothing' });
    }
    childrenOf(container).forEach((child, i) => visit(child, containerPath(path, i, child.description)));
  };

  const root = `ledsprofile/${profile.name}`;
  profile.containers.forEach((container, i) => visit(container, containerPath(root, i, container.description)));
  if (profile.containers.length === 0) errors.push({ code: 'leds/empty', path: root, message: 'a profile with no containers lights nothing' });
  return { ok: errors.length === 0, errors, warnings };
}

export interface ValidateProfileOptions {
  /** Properties the plugin exposes, with or without the prefix. */
  declaredProperties: string[];
  /** Property prefix, e.g. `OpenDash`. */
  propertyPrefix: string;
}
