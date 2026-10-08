/**
 * The demo's engine (#395): SimHub's order within a tick, its rule for choosing a screen, and every
 * built face run against the race trace on every page of every zone without reaching a construct
 * the evaluator does not compute.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { blinkOn, chooseScreen, Engine, modeOf, type Mode, type Op, type TextOp } from '../lib/demo/engine.ts';
import { Replay } from '../lib/demo/frame.ts';
import { parseTrace } from '../lib/demo/ncalc.ts';
import { initialPanel, panelProperties, prefixFor, setPage, update, type PanelState } from '../lib/demo/panel.ts';
import { parseDashboard, type SceneDashboard, type Screen } from '../lib/demo/scene.ts';
import { panelCatalogue } from '../scripts/demo-data.ts';
import { builtFaces, HAS_BUILD, repoRoot } from './demoBuild.ts';

const TEXT = 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.TextItem, SimHub.Plugins';
const LAYER = 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.Layer, SimHub.Plugins';

const text = (name: string, expression: string, extra: Record<string, unknown> = {}) => ({
  $type: TEXT,
  Font: 'Barlow',
  FontWeight: 'Medium',
  FontSize: 20,
  Text: '',
  TextColor: '#FFFFFFFF',
  Left: 0,
  Top: 0,
  Width: 100,
  Height: 30,
  Name: name,
  Bindings: { Text: { Formula: { Expression: expression }, Mode: 2 } },
  ...extra,
});

const screen = (name: string, enabled: string, items: unknown[], roles: Partial<Record<'InGameScreen' | 'IdleScreen' | 'PitScreen', boolean>> = {}) => ({
  Name: name,
  InGameScreen: true,
  IdleScreen: true,
  PitScreen: true,
  ...roles,
  ScreenEnabledExpression: { Expression: enabled },
  BackgroundColor: '#FF000000',
  Items: items,
});

const dashboard = (screens: unknown[], variables: unknown[] = []): SceneDashboard =>
  parseDashboard({ ...(variables.length ? { Variables: { DashboardVariables: variables } } : {}), BaseWidth: 200, BaseHeight: 100, BackgroundColor: '#FF000000', Screens: screens }, 'test.djson');

const texts = (ops: readonly Op[]): string[] => ops.flatMap((o) => (o.kind === 'text' ? [o.text] : o.kind === 'widget' ? texts(o.ops) : []));

const run = (scene: SceneDashboard) => {
  const engine = new Engine(new Map([[scene.file, scene]]), scene.file);
  return (properties: Record<string, unknown>, now = 0) => engine.tick({ properties: (n) => properties[n], now });
};

describe('the screen a dashboard draws', () => {
  const s = (name: string, roles: Partial<Screen> = {}): Screen => ({ name, inGame: true, idle: true, pit: true, enabledExpression: '', background: { css: '#000', alpha: 1 }, items: [], ...roles });
  const screens = [s('Main', { idle: false }), s('Other', { idle: false }), s('Idle', { inGame: false, pit: false })];
  const none = new Map<Mode, number>();

  test('stays on the current screen while it is enabled and the mode has not changed', () => {
    expect(chooseScreen(screens, [true, true, true], 'game', { screen: 1, mode: 'game', remembered: none })).toBe(1);
  });

  test('leaves a screen that has been disabled for the first enabled screen carrying the role', () => {
    expect(chooseScreen(screens, [true, false, true], 'game', { screen: 1, mode: 'game', remembered: none })).toBe(0);
    expect(chooseScreen(screens, [false, true, true], 'game', { screen: 0, mode: 'game', remembered: none })).toBe(1);
  });

  test('stays on the screen it was forced to when the others come back', () => {
    expect(chooseScreen(screens, [true, true, true], 'game', { screen: 1, mode: 'game', remembered: new Map([['game', 1]]) })).toBe(1);
  });

  test('goes back to the screen a mode was last on when the mode returns', () => {
    expect(chooseScreen(screens, [true, true, true], 'game', { screen: 2, mode: 'idle', remembered: new Map<Mode, number>([['game', 1]]) })).toBe(1);
    expect(chooseScreen(screens, [true, true, true], 'idle', { screen: 1, mode: 'game', remembered: none })).toBe(2);
  });

  test('picks the mode from the game and the screens that are enabled', () => {
    expect(modeOf(screens, [true, true, true], true, false)).toBe('game');
    expect(modeOf(screens, [true, true, true], true, true)).toBe('pit');
    expect(modeOf(screens, [true, true, true], false, false)).toBe('idle');
    expect(modeOf(screens, [false, false, true], true, false)).toBe('idle');
    expect(modeOf(screens, [true, true, false], false, false)).toBe('indeterminate');
  });

  test('follows an enabled expression from tick to tick, as SimHub does', () => {
    const tick = run(dashboard([screen('On', '[X] > 0', [text('t', "'on'")]), screen('Off', '!([X] > 0)', [text('t', "'off'")])]));
    const game = { 'DataCorePlugin.GameRunning': 1 };
    expect(tick({ ...game, X: 1 }).screen).toBe('On');
    expect(tick({ ...game, X: 0 }).screen).toBe('Off');
    expect(texts(tick({ ...game, X: 2 }).ops)).toEqual(['on']);
  });
});

describe('one tick, in SimHub order', () => {
  test('a before-screen-roles variable reaches the enabled expression on the frame it is computed', () => {
    const scene = dashboard(
      [screen('A', "[variable.pick] = 'a'", [text('t', "'A ' + [variable.late]")]), screen('B', "[variable.pick] = 'b'", [text('t', "'B ' + [variable.late]")])],
      [
        { VariableName: 'pick', ValueExpression: { Expression: '[P]' }, EvaluateBeforeScreenRoles: true },
        { VariableName: 'late', ValueExpression: { Expression: 'rootdashboardscreenname()' }, EvaluateBeforeScreenRoles: false },
      ],
    );
    const tick = run(scene);
    expect(texts(tick({ P: 'b', 'DataCorePlugin.GameRunning': 1 }).ops)).toEqual(['B B']);
    expect(texts(tick({ P: 'a', 'DataCorePlugin.GameRunning': 1 }).ops)).toEqual(['A A']);
  });

  test('an invisible item evaluates nothing else, so an unsupported binding behind it is never reached', () => {
    const tick = run(dashboard([screen('S', '', [text('t', 'nosuchfunction(1)', { Bindings: { Visible: { Formula: { Expression: 'false' }, Mode: 2 }, Text: { Formula: { Expression: 'nosuchfunction(1)' }, Mode: 2 } } })])]));
    expect(texts(tick({}).ops)).toEqual([]);
  });

  test('an unsupported construct is reported and the item marked, never drawn as a quiet blank', () => {
    const scene = dashboard([screen('S', '', [text('t', 'nosuchfunction(1)')])]);
    const engine = new Engine(new Map([[scene.file, scene]]), scene.file);
    const frame = engine.tick({ properties: () => null, now: 0 });
    expect((frame.ops[0] as TextOp).error).not.toBeNull();
    expect([...engine.problems.values()].map((p) => p.kind)).toEqual(['unsupported']);
  });

  test('a failure SimHub has too draws the empty string and is not a problem', () => {
    const scene = dashboard([screen('S', '', [text('t', '[Missing] > 0')])]);
    const engine = new Engine(new Map([[scene.file, scene]]), scene.file);
    expect(texts(engine.tick({ properties: () => null, now: 0 }).ops)).toEqual(['']);
    expect(engine.problems.size).toBe(0);
  });

  test('a repeated layer stamps its children once per copy, each with its own repeatindex() and offset', () => {
    const tick = run(dashboard([screen('S', '', [{ $type: LAYER, Name: 'rows', Repetitions: 2, RepeatTopOffset: 40, Childrens: [text('row', "'row ' + repeatindex()")] }])]));
    const ops = tick({}).ops as TextOp[];
    expect(ops.map((o) => [o.text, o.box.top])).toEqual([
      ['row 1', 0],
      ['row 2', 40],
      ['row 3', 80],
    ]);
  });

  test('state SimHub keeps between frames is dropped when the clock goes back', () => {
    const tick = run(dashboard([screen('S', '', [text('t', "if(changed(1000, [V]), 'moved', 'still')")])]));
    expect(texts(tick({ V: 1 }, 0).ops)).toEqual(['still']);
    expect(texts(tick({ V: 2 }, 100).ops)).toEqual(['moved']);
    expect(texts(tick({ V: 2 }, 50).ops)).toEqual(['still']);
  });

  test('a blinking item is on for the first half period and off for the next, or the other way round', () => {
    expect([0, 249, 250, 499, 500].map((t) => blinkOn(t, 250, false))).toEqual([true, true, false, false, true]);
    expect(blinkOn(0, 250, true)).toBe(false);
  });
});

describe('every built face against the race trace', () => {
  const faces = builtFaces();
  const catalogue = panelCatalogue();
  const replay = new Replay(parseTrace(readFileSync(path.join(repoRoot, 'traces', 'race.ndjson'), 'utf8')));
  const frames = [0, replay.duration / 2, replay.duration].map((t) => ({ t, properties: replay.at(t) }));

  test.if(!HAS_BUILD)('skipped: build/manifest.json is absent or stale, run bun run build at the repository root', () => {
    expect(HAS_BUILD).toBe(false);
  });

  /** Every panel state worth drawing: each page of each zone, and the face's other modes. */
  const states = (prefix: string | null): PanelState[] => {
    const base = initialPanel(catalogue, prefix);
    if (prefix === null) return catalogue.cards.map((c) => update(base, 'slots', base.slots.map(() => c.number)));
    return [
      ...catalogue.zones.flatMap((z) => z.pages.map((p) => setPage(catalogue, base, z.letter, p.number))),
      update(update(update(base, 'flagFormat', 'full'), 'lapReview', 'all'), 'revBar', 'off'),
      update(base, 'revBar', 'rpm'),
      update(base, 'rig', { ...base.rig, ClockFormat: '12h', DeltaPrecision: 'thousandths', PositionMode: 'overall', DeltaReference: 'lastlap' }),
    ];
  };

  test.each(faces.map((f) => [f.folder, f] as const))('%s draws every page without reaching a construct the evaluator does not compute', (_folder, face) => {
    const prefix = prefixFor(catalogue, face.width, face.height);
    if (!/round/.test(face.folder)) expect(prefix).not.toBeNull();
    const engine = new Engine(face.library, face.main);
    let drawn = 0;
    for (const state of states(prefix)) {
      const overlay = panelProperties(catalogue, state);
      engine.reset();
      for (const { t, properties } of frames) {
        const frame = engine.tick({ properties: (n) => (overlay.has(n) ? overlay.get(n) : properties[n]), now: t });
        drawn += texts(frame.ops).filter((s) => s !== '').length;
      }
    }
    expect(drawn).toBeGreaterThan(0);
    expect([...engine.problems.values()].map((p) => `${p.kind}: ${p.where}\n${p.message}`)).toEqual([]);
  });
});
