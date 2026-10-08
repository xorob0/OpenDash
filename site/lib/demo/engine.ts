/**
 * One tick of a dashboard, the way SimHub runs one, turned into a list of things to draw.
 *
 * The order is SimHub's (`EditorModel.UpdateData`, decompiled for #362 and recorded in
 * `docs/research/simhub-dash-format.md`):
 *
 *   1. the dashboard variables marked `EvaluateBeforeScreenRoles`, in list order;
 *   2. every screen's enabled expression, then the choice of screen by mode and role;
 *   3. the other variables, then the chosen screen's bindings, item by item, `Visible` first: an
 *      item that is not visible has nothing else evaluated, and neither do its children.
 *
 * A widget is a dashboard of its own, with its own variables and its own state, on the screen its
 * bound `InitialScreenIndex` names. A repeated layer stamps its children once per copy, each copy
 * evaluated with its own `repeatindex()`.
 *
 * Every expression is computed by the evaluator from #726. `evaluateBinding` already turns a failure
 * SimHub has too (a null compared, an opponent call a version 1 trace does not carry) into an empty
 * field; anything else it throws is an unsupported construct or a syntax error, and that is never
 * swallowed: it is collected in {@link Engine.problems}, the item is marked, and the page shows the
 * list. A demo that drew a blank where it did not know the answer would be the silent divergence
 * ADR 0008 warns about.
 *
 * Nothing here touches a canvas. The output is a list of {@link Op}s, which `renderer.ts` draws and a
 * test can read.
 */
import { E } from './ncalc.ts';
import { TRANSPARENT, parsePaint, type Box, type ChartItem, type DrawableItem, type GaugeItem, type Gradient, type Item, type Paint, type SceneBinding, type SceneDashboard, type Screen, type TextItem } from './scene.ts';

// ------------------------------------------------------------------------------------- output

export interface ResolvedBorder {
  readonly color: Paint;
  readonly top: number;
  readonly bottom: number;
  readonly left: number;
  readonly right: number;
  readonly radius: { readonly topLeft: number; readonly topRight: number; readonly bottomRight: number; readonly bottomLeft: number };
}

interface OpBase {
  /** Where the item is, for the outline overlay and for a problem: `file / screen / layer > item`. */
  readonly path: string;
  readonly box: Box;
  readonly rotation: number;
  /** Opacity and blink, multiplied down from every enclosing layer. */
  readonly alpha: number;
  readonly background: Paint;
  readonly border: ResolvedBorder | null;
  /** Set when a binding of this item reached something the evaluator does not compute. */
  readonly error: string | null;
}

export interface TextOp extends OpBase {
  readonly kind: 'text';
  readonly item: TextItem;
  readonly text: string;
  readonly color: Paint;
  readonly size: number;
}
export interface RectOp extends OpBase {
  readonly kind: 'rect';
}
export interface EllipseOp extends OpBase {
  readonly kind: 'ellipse';
  readonly fill: Paint;
  readonly stroke: Paint;
  readonly thickness: number;
}
export interface GaugeOp extends OpBase {
  readonly kind: 'gauge';
  readonly item: GaugeItem;
  /** 0 to 1: how much of the track is filled. */
  readonly fraction: number;
  readonly color: Paint;
}
export interface ImageOp extends OpBase {
  readonly kind: 'image';
  /** `<dashboard file>/<image name>`, the key the page loaded the picture under. */
  readonly key: string;
}
export interface ChartOp extends OpBase {
  readonly kind: 'chart';
  readonly points: readonly number[];
  readonly capacity: number;
  readonly minimum: number;
  readonly maximum: number;
  readonly color: Paint;
  readonly thickness: number;
}
export interface StandInOp extends OpBase {
  readonly kind: 'standIn';
  readonly label: string;
}
export interface WidgetOp extends OpBase {
  readonly kind: 'widget';
  /** The widget dashboard's own size, drawn at `scale` from the box's corner plus `offset`. */
  readonly width: number;
  readonly height: number;
  readonly scale: number;
  readonly offsetX: number;
  readonly offsetY: number;
  readonly screenBackground: Paint;
  readonly ops: readonly Op[];
}

export type Op = TextOp | RectOp | EllipseOp | GaugeOp | ImageOp | ChartOp | StandInOp | WidgetOp;

/** Something the demo could not do as SimHub would, kept for the page to show. */
export interface Problem {
  readonly kind: 'unsupported' | 'syntax' | 'javascript' | 'unknown item' | 'missing widget' | 'not applied';
  readonly where: string;
  readonly message: string;
  /** How many times it was met, across ticks. */
  count: number;
}

// ------------------------------------------------------------------------------ value coercion

/**
 * A value as a `bool` target takes it: `Convert.ToBoolean` reads a number as "not zero" and a
 * string as `True` or `False`. Undefined where SimHub's setter would throw, and the item keeps the
 * value it had. Unverified for strings other than those two, which no package writes.
 */
export function toBool(v: E.Value): boolean | undefined {
  if (typeof v === 'boolean') return v;
  if (v === null) return false;
  if (typeof v === 'string') {
    const t = v.trim().toLowerCase();
    return t === 'true' ? true : t === 'false' ? false : undefined;
  }
  if (E.isNumber(v)) return v.value !== 0;
  return undefined;
}

/** A value as a `double` or `int` target takes it. Undefined where SimHub's setter would throw. */
export function toNumber(v: E.Value): number | undefined {
  if (v === null) return undefined;
  if (typeof v === 'boolean') return v ? 1 : 0;
  if (typeof v === 'string') {
    const n = Number(v.trim());
    return v.trim() !== '' && Number.isFinite(n) ? n : undefined;
  }
  if (E.isNumber(v)) return Number.isFinite(v.value) ? v.value : undefined;
  return undefined;
}

/** A value as a `Color` target takes it, through `ColorConverter`. Undefined on anything else. */
export function toPaint(v: E.Value): Paint | undefined {
  if (typeof v !== 'string') return undefined;
  return parsePaint(v) ?? undefined;
}

/**
 * Whether a screen's enabled expression enables it: empty is enabled, and a non-empty one enables
 * while its value is above zero (true counts as one). A runtime failure disables it.
 */
export function enables(v: E.Value): boolean {
  if (typeof v === 'boolean') return v;
  if (E.isNumber(v)) return v.value > 0;
  if (typeof v === 'string') {
    const t = v.trim().toLowerCase();
    if (t === 'true') return true;
    const n = Number(t);
    return t !== '' && Number.isFinite(n) && n > 0;
  }
  return false;
}

function mix(a: Paint, b: Paint, t: number): Paint {
  const parse = (p: Paint): number[] => {
    const m = /rgba\(([^)]+)\)/.exec(p.css);
    return m ? m[1]!.split(',').map(Number) : [255, 255, 255, p.alpha];
  };
  const x = parse(a);
  const y = parse(b);
  const c = x.map((v, i) => v + ((y[i] ?? v) - v) * t);
  const alpha = c[3] ?? 1;
  return { css: `rgba(${Math.round(c[0]!)},${Math.round(c[1]!)},${Math.round(c[2]!)},${alpha})`, alpha };
}

/** A Mode 4 binding: the formula's number placed on the ramp. */
function gradientPaint(g: Gradient, value: number): Paint {
  if (g.middle) {
    if (value <= g.middleValue) return mix(g.start, g.middle, clamp01((value - g.startValue) / (g.middleValue - g.startValue || 1)));
    return mix(g.middle, g.end, clamp01((value - g.middleValue) / (g.endValue - g.middleValue || 1)));
  }
  return mix(g.start, g.end, clamp01((value - g.startValue) / (g.endValue - g.startValue || 1)));
}

const clamp01 = (x: number): number => (Number.isFinite(x) ? Math.min(1, Math.max(0, x)) : 0);

// ------------------------------------------------------------------------------ screen choice

export type Mode = 'game' | 'pit' | 'idle' | 'indeterminate';

const hasRole = (screen: Screen, mode: Mode): boolean => (mode === 'game' ? screen.inGame : mode === 'pit' ? screen.pit : mode === 'idle' ? screen.idle : true);

/**
 * Which of the four modes a frame is in, from the screens that are enabled: game while a game runs
 * and an enabled screen is an in-game one, pit within that when the car is in the pit lane or on its
 * limiter and an enabled screen is a pit one, idle when neither and an enabled screen is an idle
 * one, and indeterminate otherwise (`EditorModel.CheckGameModeScreen`).
 */
export function modeOf(screens: readonly Screen[], enabled: readonly boolean[], gameRunning: boolean, inPitLane: boolean): Mode {
  const candidates = screens.filter((_, i) => enabled[i]);
  if (gameRunning && candidates.some((s) => s.inGame)) return inPitLane && candidates.some((s) => s.pit) ? 'pit' : 'game';
  if (candidates.some((s) => s.idle)) return 'idle';
  return 'indeterminate';
}

/**
 * The screen a root dashboard draws this frame (`FindModeScreen`): the current one while the mode
 * has not changed and it is still enabled; otherwise the one this mode was last on, if it is enabled
 * and carries the role; otherwise the first enabled screen carrying the role. With no enabled screen
 * at all the current one stays, since there is nothing else to draw.
 */
export function chooseScreen(
  screens: readonly Screen[],
  enabled: readonly boolean[],
  mode: Mode,
  previous: { screen: number; mode: Mode | null; remembered: ReadonlyMap<Mode, number> },
): number {
  const ok = (i: number | undefined): i is number => i !== undefined && i >= 0 && i < screens.length && enabled[i] === true && hasRole(screens[i]!, mode);
  if (previous.mode === mode && ok(previous.screen)) return previous.screen;
  const remembered = previous.remembered.get(mode);
  if (ok(remembered)) return remembered;
  const first = screens.findIndex((s, i) => enabled[i] && hasRole(s, mode));
  if (first >= 0) return first;
  const any = enabled.findIndex((e) => e);
  return any >= 0 ? any : Math.max(0, Math.min(previous.screen, screens.length - 1));
}

/**
 * The screens SimHub's paging walks (`Dashboard.GetActiveScreens`), by index: the enabled ones, and
 * among them, when their roles differ, the pit screens while a game runs in the pit lane (the
 * in-game ones when there are none), the in-game screens while a game runs, and the idle screens
 * while none does. When every enabled screen carries the same three roles, all of them.
 */
export function activeScreens(screens: readonly Screen[], enabled: readonly boolean[], gameRunning: boolean, inPitLane: boolean): number[] {
  const on = screens.flatMap((_, i) => (enabled[i] ? [i] : []));
  const roles = new Set(on.map((i) => `${screens[i]!.pit};${screens[i]!.inGame};${screens[i]!.idle}`));
  if (roles.size <= 1) return on;
  if (gameRunning) {
    const pit = on.filter((i) => screens[i]!.pit);
    if (inPitLane && pit.length > 0) return pit;
    return on.filter((i) => screens[i]!.inGame);
  }
  return on.filter((i) => screens[i]!.idle);
}

/** Whether a blinking item is drawn at this moment: on for the first half period, off for the next. */
export const blinkOn = (now: number, delay: number, inverted: boolean): boolean => (Math.floor(now / Math.max(1, delay)) % 2 === 0) !== inverted;

// ------------------------------------------------------------------------------------ the run

/** The property names the mode is read from. */
const GAME_RUNNING = 'DataCorePlugin.GameRunning';
const PIT_LIMITER = 'DataCorePlugin.GameData.PitLimiterOn';
const IN_PIT_LANE = 'DataCorePlugin.GameData.IsInPitLane';

/** What a tick reads: the properties by name, and the clock in milliseconds. */
export interface TickInput {
  readonly properties: (name: string) => unknown;
  readonly now: number;
}

interface Walk {
  readonly input: TickInput;
  readonly scope: (repeat: readonly number[]) => E.Scope;
  readonly where: string;
  readonly ops: Op[];
}

class DashboardRun {
  private state = new E.CallState();
  private variables = new Map<string, E.Value>();
  private evaluatedOnce = new Set<string>();
  private screen = 0;
  private mode: Mode | null = null;
  private remembered = new Map<Mode, number>();
  private lastScreenName: string | null = null;
  /** What the last tick saw, for SimHub's paging between ticks. */
  private lastEnabled: readonly boolean[] = [];
  private lastGame = false;
  private lastPit = false;
  private charts = new Map<string, number[]>();
  private widgets = new Map<string, DashboardRun>();

  constructor(
    readonly scene: SceneDashboard,
    private readonly engine: Engine,
    private readonly root: boolean,
  ) {}

  reset(): void {
    this.state.clear();
    this.variables.clear();
    this.evaluatedOnce.clear();
    this.screen = 0;
    this.mode = null;
    this.remembered.clear();
    this.lastScreenName = null;
    this.lastEnabled = [];
    this.charts.clear();
    for (const w of this.widgets.values()) w.reset();
  }

  /**
   * The clock went back. Everything the trace's past fed is dropped: the state `changed` and its
   * siblings keep, and the charts' buffers. What the driver chose is not: the screen paged to, the
   * one each mode remembers, and the dashboard's variables, which on a companion are the memory of
   * that choice. A loop of the replay is not SimHub restarting.
   */
  rewind(): void {
    this.state.clear();
    this.charts.clear();
    for (const w of this.widgets.values()) w.rewind();
  }

  /**
   * SimHub's `SelectNextScreen` or `SelectPreviousScreen` between two ticks: the next or previous of
   * the screens its paging walks, wrapping, from the one drawn last. The next tick keeps it while it
   * stays enabled and the mode stays the same, as `FindModeScreen` does. Returns whether it moved.
   */
  navigate(direction: 1 | -1): boolean {
    const ring = activeScreens(this.scene.screens, this.lastEnabled, this.lastGame, this.lastPit);
    if (ring.length === 0) return false;
    const at = ring.indexOf(this.screen);
    const next = at < 0 ? ring[0]! : ring[(at + direction + ring.length) % ring.length]!;
    if (next === this.screen) return false;
    this.screen = next;
    if (this.mode !== null) this.remembered.set(this.mode, next);
    return true;
  }

  /** The screen drawn on the last tick, by name. */
  get screenName(): string | null {
    return this.lastScreenName;
  }

  private scopeFor(input: TickInput, rootScreen: string | null): (repeat: readonly number[]) => E.Scope {
    const properties = (name: string): unknown => {
      if (name.length > 9 && name.slice(0, 9).toLowerCase() === 'variable.') {
        const v = this.variables.get(name.slice(9).toLowerCase());
        return v === undefined ? null : v;
      }
      return input.properties(name);
    };
    const base = { properties, state: this.state, now: input.now, rootScreenName: rootScreen };
    const plain: E.Scope = { ...base, repeat: [] };
    return (repeat) => (repeat.length === 0 ? plain : { ...base, repeat });
  }

  private evaluateVariables(input: TickInput, before: boolean, rootScreen: string | null): void {
    const scope = this.scopeFor(input, rootScreen)([]);
    for (const v of this.scene.variables) {
      if (v.beforeScreenRoles !== before) continue;
      const key = v.name.toLowerCase();
      if (v.evaluateOnlyOnce && this.evaluatedOnce.has(key)) continue;
      const result = this.engine.evaluate(v.expression, scope, `${this.scene.file}: variable ${v.name}`);
      this.variables.set(key, result.ok ? result.value : null);
      this.evaluatedOnce.add(key);
    }
  }

  /**
   * One tick. A root dashboard chooses its screen; a widget is handed the index its item's
   * `InitialScreenIndex` resolved to.
   */
  tick(input: TickInput, widgetIndex: number | null, rootScreen: string | null, where: string): { ops: Op[]; screen: Screen | undefined } {
    const screens = this.scene.screens;
    this.evaluateVariables(input, true, this.root ? this.lastScreenName : rootScreen);
    const plain = this.scopeFor(input, this.root ? this.lastScreenName : rootScreen)([]);
    const enabled = screens.map((s, i) =>
      s.enabledExpression.trim() === '' ? true : (() => {
        const r = this.engine.evaluate(s.enabledExpression, plain, `${this.scene.file} / ${s.name}: enabled expression`);
        return r.ok && enables(r.value);
      })(),
    );
    if (this.root) {
      const p = input.properties;
      const truthy = (name: string) => toBool(E.toValue(p(name) ?? null)) === true;
      this.lastGame = truthy(GAME_RUNNING);
      this.lastPit = truthy(PIT_LIMITER) || truthy(IN_PIT_LANE);
      this.lastEnabled = enabled;
      const mode = modeOf(screens, enabled, this.lastGame, this.lastPit);
      this.screen = chooseScreen(screens, enabled, mode, { screen: this.screen, mode: this.mode, remembered: this.remembered });
      this.mode = mode;
      this.remembered.set(mode, this.screen);
    } else if (widgetIndex !== null) {
      // `GetAvailableScreenAtIndex`: the index among the widget's screens. Unverified out of range,
      // where this keeps the screen it was on rather than guess.
      const index = Math.trunc(widgetIndex);
      if (index >= 0 && index < screens.length) this.screen = index;
    }
    const screen = screens[this.screen];
    this.lastScreenName = screen?.name ?? null;
    const own = this.root ? this.lastScreenName : rootScreen;
    this.evaluateVariables(input, false, own);
    const ops: Op[] = [];
    if (screen) {
      const walk: Walk = { input, scope: this.scopeFor(input, own), where: `${where}${this.scene.file} / ${screen.name}`, ops };
      this.visit(screen.items, walk, [], 0, 0, 1, '');
    }
    return { ops, screen };
  }

  private visit(items: readonly Item[], walk: Walk, repeat: readonly number[], dx: number, dy: number, alpha: number, path: string): void {
    for (const item of items) {
      const here = `${path} > ${item.name}`;
      const scope = walk.scope(repeat);
      const where = `${walk.where}${here}`;
      const bound = new Map<string, SceneBinding>();
      for (const b of item.bindings) bound.set(b.target, b);
      let error: string | null = null;
      const value = (target: string): E.Value | undefined => {
        const b = bound.get(target);
        if (!b) return undefined;
        const r = this.engine.evaluate(b.expression, scope, `${where} .${target}`, b);
        if (r.error) error = r.error;
        if (!r.ok) return undefined;
        if (b.mode === 'gradient' && b.gradient) {
          const n = toNumber(r.value);
          return n === undefined ? undefined : gradientPaint(b.gradient, n).css;
        }
        return r.value;
      };
      // Visible first: an item that is not visible evaluates nothing else.
      const visibleValue = value('Visible');
      const visible = visibleValue === undefined ? item.visible : (toBool(visibleValue) ?? item.visible);
      if (!visible) continue;

      const opacityValue = value('Opacity');
      const opacity = opacityValue === undefined ? item.opacity : (toNumber(opacityValue) ?? item.opacity * 100) / 100;
      const blinkValue = value('BlinkEnabled');
      const blinking = blinkValue === undefined ? item.blink.enabled : (toBool(blinkValue) ?? item.blink.enabled);
      const shown = blinking ? (blinkOn(walk.input.now, item.blink.delay, item.blink.inverted) ? 1 : 0) : 1;
      const a = alpha * opacity * shown;

      if (item.kind === 'layer') {
        const repsValue = value('Repetitions');
        const reps = Math.max(0, Math.trunc(repsValue === undefined ? item.repetitions : (toNumber(repsValue) ?? item.repetitions)));
        if (reps === 0 && item.repetitions === 0 && repsValue === undefined) {
          this.visit(item.children, walk, repeat, dx, dy, a, here);
        } else {
          for (let copy = 1; copy <= reps + 1; copy++) {
            this.visit(item.children, walk, [...repeat, copy], dx + (copy - 1) * item.repeatLeft, dy + (copy - 1) * item.repeatTop, a, `${here}[${copy}]`);
          }
        }
        continue;
      }
      this.drawable(item, walk, value, () => error, repeat, dx, dy, a, here);
    }
  }

  private drawable(
    item: DrawableItem,
    walk: Walk,
    value: (target: string) => E.Value | undefined,
    error: () => string | null,
    repeat: readonly number[],
    dx: number,
    dy: number,
    alpha: number,
    path: string,
  ): void {
    const n = (target: string, fallback: number): number => {
      const v = value(target);
      return v === undefined ? fallback : (toNumber(v) ?? fallback);
    };
    const paint = (target: string, fallback: Paint): Paint => {
      const v = value(target);
      return v === undefined ? fallback : (toPaint(v) ?? fallback);
    };
    const box: Box = {
      left: n('Left', item.box.left) + dx,
      top: n('Top', item.box.top) + dy,
      width: n('Width', item.box.width),
      height: n('Height', item.box.height),
    };
    const background = paint('BackgroundColor', item.background);
    let border: ResolvedBorder | null = null;
    if (item.border) {
      let color = item.border.color;
      const b = item.border.colorBinding;
      if (b) {
        const r = this.engine.evaluate(b.expression, walk.scope(repeat), `${walk.where}${path} .BorderStyle.BorderColor`, b);
        if (r.ok) color = toPaint(r.value) ?? color;
      }
      border = { ...item.border, color };
    }
    const fullPath = `${walk.where}${path}`;
    const common = { path: fullPath, box, rotation: item.rotation, alpha, background, border };
    const key = `${path}|${repeat.join(',')}`;
    const ops = walk.ops;

    switch (item.kind) {
      case 'text': {
        const t = value('Text');
        // A Text binding that fails draws the empty string, as the dash does.
        const text = item.bindings.some((b) => b.target === 'Text') ? (t === undefined ? '' : E.valueToString(t)) : item.text;
        ops.push({ kind: 'text', ...common, item, text, color: paint('TextColor', item.color), size: n('FontSize', item.size), error: error() });
        return;
      }
      case 'rect':
        ops.push({ kind: 'rect', ...common, error: error() });
        return;
      case 'ellipse':
        ops.push({ kind: 'ellipse', ...common, fill: paint('FillColor', item.fill), stroke: paint('EllipseColor', item.stroke), thickness: item.thickness, error: error() });
        return;
      case 'gauge': {
        const min = n('Minimum', item.minimum);
        const max = n('Maximum', item.maximum);
        const v = n('Value', item.value);
        let fraction = max === min ? 0 : clamp01((v - min) / (max - min));
        // Unverified: a stepped gauge fills whole steps only.
        if (item.steps > 0) fraction = Math.floor(fraction * item.steps) / item.steps;
        const altValue = value('UseAlternateStyle');
        const alternate = altValue === undefined ? item.useAlternate : (toBool(altValue) ?? item.useAlternate);
        const color = alternate ? paint('AlternateGaugeColor', item.alternateColor) : paint('GaugeColor', item.gaugeColor);
        ops.push({ kind: 'gauge', ...common, item, fraction, color, error: error() });
        return;
      }
      case 'image':
        ops.push({ kind: 'image', ...common, key: `${this.scene.file}/${item.image}`, error: error() });
        return;
      case 'chart':
        ops.push(this.chart(item, key, common, n, value, error()));
        return;
      case 'standIn': {
        // Evaluated all the same, so an address or a path that cannot be computed is still reported.
        const address = value('StartAddress');
        const file = value('ImagePath');
        value('Scale');
        if (!['RadarItem', 'GeneratedStaticMapItem', 'WebPageItem', 'ImageFromFileItem'].includes(item.type)) {
          this.engine.report('unknown item', fullPath, `${item.type || 'an item with no $type'} is not a kind the demo draws`);
        }
        let label = item.label;
        if (item.type === 'ImageFromFileItem') {
          // A picture from a file on the rig. With no file named there is nothing to draw, which is
          // how a Porsche face with no crest set shows the placeholder shield beneath it.
          const path = file === undefined ? item.imagePath : file === null ? '' : E.valueToString(file);
          if (path.trim() === '') return;
          label = `${item.label}: ${path}`;
        } else if (item.type === 'WebPageItem') {
          const url = address === undefined ? item.startAddress : address === null ? '' : E.valueToString(address);
          if (url.trim() !== '') label = `${item.label}: ${url}`;
        }
        ops.push({ kind: 'standIn', ...common, label, error: error() });
        return;
      }
      case 'widget': {
        const index = n('InitialScreenIndex', item.initialScreenIndex);
        let run = this.widgets.get(key);
        if (!run) {
          const scene = this.engine.dashboard(item.fileName);
          if (!scene) {
            this.engine.report('missing widget', fullPath, `${item.fileName} was not loaded`);
            ops.push({ kind: 'standIn', ...common, label: item.fileName, error: `${item.fileName} was not loaded` });
            return;
          }
          run = new DashboardRun(scene, this.engine, false);
          this.widgets.set(key, run);
        }
        const rootScreen = this.root ? this.lastScreenName : null;
        const inner = run.tick(walk.input, index, rootScreen, `${fullPath} => `);
        const w = run.scene.width;
        const h = run.scene.height;
        const scale = item.autoSize && w > 0 && h > 0 ? Math.min(box.width / w, box.height / h) : 1;
        ops.push({
          kind: 'widget',
          ...common,
          width: w,
          height: h,
          scale,
          offsetX: item.autoSize ? (box.width - w * scale) / 2 : 0,
          offsetY: item.autoSize ? (box.height - h * scale) / 2 : 0,
          screenBackground: inner.screen?.background ?? TRANSPARENT,
          ops: inner.ops,
          error: error(),
        });
        return;
      }
    }
  }

  /** A chart keeps a ring buffer per drawn copy and appends `CurrentValue` once a tick. */
  private chart(
    item: ChartItem,
    key: string,
    common: Omit<OpBase, 'error'>,
    n: (target: string, fallback: number) => number,
    value: (target: string) => E.Value | undefined,
    error: string | null,
  ): ChartOp {
    const enabledValue = value('ChartEnabled');
    const enabled = enabledValue === undefined ? item.enabled : (toBool(enabledValue) ?? item.enabled);
    let buffer = this.charts.get(key);
    if (!buffer) {
      buffer = [];
      this.charts.set(key, buffer);
    }
    const current = n('CurrentValue', item.currentValue);
    if (!enabled) buffer.length = 0;
    else if (!item.suspended) {
      buffer.push(current);
      if (buffer.length > item.pointsCount) buffer.splice(0, buffer.length - item.pointsCount);
    }
    let minimum = n('Minimum', item.minimum);
    let maximum = n('Maximum', item.maximum);
    if (!item.useMinimum && buffer.length) minimum = Math.min(...buffer);
    if (!item.useMaximum && buffer.length) maximum = Math.max(...buffer);
    let color = item.lineColor;
    const lc = value('LineColor');
    if (lc !== undefined) color = toPaint(lc) ?? color;
    return { kind: 'chart', ...common, points: [...buffer], capacity: item.pointsCount, minimum, maximum, color, thickness: item.lineThickness, error };
  }
}

/** What one tick produced: the root dashboard's screen and what to draw on it. */
export interface Frame {
  readonly screen: string | null;
  /** The dashboard's background, then the screen's over it. */
  readonly background: Paint;
  readonly screenBackground: Paint;
  readonly ops: readonly Op[];
}

export interface EvaluateResult {
  readonly ok: boolean;
  readonly value: E.Value;
  /** The message of an unsupported construct or a syntax error, which the item is marked with. */
  readonly error: string | null;
}

/**
 * A face: its main dashboard and the widgets it includes, run tick by tick.
 *
 * The clock is the trace's. When it goes backwards (the scrubber moved back, or the replay looped),
 * every piece of state SimHub keeps between frames is dropped, because state carried from the end
 * of a lap into its start is state the dash never had. The screen the driver paged to is kept: that
 * is theirs, not the trace's.
 */
export class Engine {
  readonly problems = new Map<string, Problem>();
  private readonly run: DashboardRun;
  private last = -Infinity;

  constructor(
    private readonly library: ReadonlyMap<string, SceneDashboard>,
    main: string,
  ) {
    const scene = library.get(main);
    if (!scene) throw new Error(`${main} is not among the dashboards loaded`);
    this.run = new DashboardRun(scene, this, true);
  }

  get main(): SceneDashboard {
    return this.run.scene;
  }

  dashboard(file: string): SceneDashboard | undefined {
    return this.library.get(file) ?? [...this.library.entries()].find(([k]) => k.toLowerCase() === file.toLowerCase())?.[1];
  }

  reset(): void {
    this.run.reset();
    this.last = -Infinity;
  }

  /** SimHub's NextScreen (1) or PreviousScreen (-1) on the main dashboard. Returns whether it moved. */
  navigate(direction: 1 | -1): boolean {
    return this.run.navigate(direction);
  }

  report(kind: Problem['kind'], where: string, message: string): void {
    const key = `${kind}|${message}`;
    const seen = this.problems.get(key);
    if (seen) seen.count += 1;
    else this.problems.set(key, { kind, where, message, count: 1 });
  }

  /**
   * One expression, as SimHub applies a binding: a runtime failure is an empty result and not a
   * problem, because the dash fails the same way; anything else the evaluator throws is reported.
   */
  evaluate(expression: string, scope: E.Scope, where: string, binding?: SceneBinding): EvaluateResult {
    if (binding?.javascript) {
      this.report('javascript', where, 'a JavaScript binding, which the demo does not run');
      return { ok: false, value: null, error: 'JavaScript binding' };
    }
    if (binding?.preExpression) this.report('not applied', where, 'a pre-expression, which the demo does not run');
    if (binding?.formatString) this.report('not applied', where, `a FormatString (${binding.formatString}), which the demo does not apply`);
    try {
      const r = E.evaluateBinding(expression, scope);
      return r.ok ? { ok: true, value: r.value, error: null } : { ok: false, value: null, error: null };
    } catch (e) {
      const message = e instanceof Error ? e.message : String(e);
      this.report(e instanceof E.NCalcSyntaxError ? 'syntax' : 'unsupported', where, message);
      return { ok: false, value: null, error: message.split('\n')[0] ?? message };
    }
  }

  /** Computes the frame at `now`, in milliseconds of trace time. */
  tick(input: TickInput): Frame {
    if (input.now < this.last) this.run.rewind();
    this.last = input.now;
    const { ops, screen } = this.run.tick(input, null, null, '');
    return { screen: screen?.name ?? null, background: this.run.scene.background, screenBackground: screen?.background ?? TRANSPARENT, ops };
  }
}
