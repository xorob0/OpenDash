/**
 * The demo's engine (#395): SimHub's order within a tick, its rule for choosing a screen, its paging
 * between frames, and every built package (the faces, the car theme's faces, the companions and the
 * pit walls) run against the race trace on every page it can show, and against every committed
 * trace as it opens, without reaching a construct the evaluator does not compute.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { activeScreens, blinkOn, chooseScreen, Engine, modeOf, type Mode, type Op, type TextOp } from '../lib/demo/engine.ts';
import { Replay } from '../lib/demo/frame.ts';
import { parseTrace } from '../lib/demo/ncalc.ts';
import {
  beginCompanionGlance,
  companionOpenOn,
  endCompanionGlance,
  initialPanel,
  openCompanion,
  panelProperties,
  pitWallPagesFor,
  prefixFor,
  setPage,
  setPitWallPage,
  setPitWallZone,
  update,
  updatePitWall,
  zonesOf,
  type PanelState,
} from '../lib/demo/panel.ts';
import { parseDashboard, type SceneDashboard, type Screen } from '../lib/demo/scene.ts';
import { panelCatalogue, traceFiles } from '../scripts/demo-data.ts';
import { builtFaces, HAS_BUILD, repoRoot, type BuiltFace } from './demoBuild.ts';

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

describe('paging, as SimHub pages a dashboard between frames', () => {
  const s = (name: string, roles: Partial<Screen> = {}): Screen => ({ name, inGame: true, idle: true, pit: true, enabledExpression: '', background: { css: '#000', alpha: 1 }, items: [], ...roles });

  test('walks every enabled screen when they carry the same roles, and filters by role only when they differ', () => {
    const same = [s('A'), s('B'), s('C')];
    expect(activeScreens(same, [true, false, true], false, false)).toEqual([0, 2]);
    const mixed = [s('A', { idle: false, pit: false }), s('B', { idle: false, pit: false }), s('Pit', { idle: false, inGame: false }), s('Idle', { inGame: false, pit: false })];
    expect(activeScreens(mixed, [true, true, true, true], true, false)).toEqual([0, 1]);
    expect(activeScreens(mixed, [true, true, true, true], true, true)).toEqual([2]);
    expect(activeScreens(mixed, [true, true, false, true], true, true)).toEqual([0, 1]);
    expect(activeScreens(mixed, [true, true, true, true], false, false)).toEqual([3]);
  });

  const paged = () => {
    const scene = dashboard(
      ['A', 'B', 'C'].map((n) => screen(n, `[${n}] > 0`, [text('t', `'${n}'`)], { IdleScreen: false })).concat([screen('Idle', '', [], { InGameScreen: false, PitScreen: false })]),
    );
    const engine = new Engine(new Map([[scene.file, scene]]), scene.file);
    return engine;
  };

  test('steps to the next enabled screen and wraps, skips one that is off, and the next frame keeps it', () => {
    const engine = paged();
    const props: Record<string, unknown> = { 'DataCorePlugin.GameRunning': true, A: 1, B: 0, C: 1 };
    const tick = (now = 0) => engine.tick({ properties: (n) => props[n], now }).screen;
    expect(tick()).toBe('A');
    expect(engine.navigate(1)).toBe(true);
    expect(tick()).toBe('C');
    expect(tick()).toBe('C');
    engine.navigate(1);
    expect(tick()).toBe('A');
    engine.navigate(-1);
    expect(tick()).toBe('C');
    // One screen in the ring is nowhere to go.
    props.A = 0;
    expect(tick()).toBe('C');
    expect(engine.navigate(1)).toBe(false);
  });

  test('a rewind of the clock keeps the screen paged to, and a reset does not', () => {
    const engine = paged();
    const props: Record<string, unknown> = { 'DataCorePlugin.GameRunning': true, A: 1, B: 1, C: 1 };
    const tick = (now: number) => engine.tick({ properties: (n) => props[n], now }).screen;
    expect(tick(1000)).toBe('A');
    engine.navigate(1);
    expect(tick(2000)).toBe('B');
    expect(tick(0)).toBe('B');
    engine.reset();
    expect(tick(0)).toBe('A');
  });
});

describe('the stand-ins', () => {
  const FROM_FILE = 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.ImageFromFileItem, SimHub.Plugins';
  const WEB = 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.WebPageItem, SimHub.Plugins';
  const kinds = (ops: readonly Op[]) => ops.map((o) => (o.kind === 'standIn' ? `standIn ${o.label}` : o.kind));

  test('an image from a file with no file named draws nothing, so what is beneath it shows', () => {
    const item = { $type: FROM_FILE, Name: 'crest', ImagePath: '', Width: 50, Height: 50, Bindings: { ImagePath: { Formula: { Expression: "isnull([Crest], '')" }, Mode: 2 } } };
    const tick = run(dashboard([screen('S', '', [text('under', "'shield'"), item])]));
    expect(kinds(tick({}).ops)).toEqual(['text']);
    expect(kinds(tick({ Crest: 'C:\\crest.png' }).ops)).toEqual(['text', 'standIn Image from file: C:\\crest.png']);
  });

  test('a web page names the address it would show', () => {
    const item = { $type: WEB, Name: 'web', StartAddress: '', Width: 50, Height: 50, Bindings: { StartAddress: { Formula: { Expression: "isnull([Url], '')" }, Mode: 2 } } };
    const tick = run(dashboard([screen('S', '', [item])]));
    expect(kinds(tick({}).ops)).toEqual(['standIn Web page']);
    expect(kinds(tick({ Url: 'https://example.com' }).ops)).toEqual(['standIn Web page: https://example.com']);
  });
});

const catalogue = panelCatalogue();
const faces = builtFaces();
const readReplay = (file: string) => new Replay(parseTrace(readFileSync(path.join(repoRoot, 'traces', file), 'utf8')));
const moments = (replay: Replay) => [0, replay.duration / 2, replay.duration].map((t) => ({ t, properties: replay.at(t) }));

/** The panel a package opens with, as the page builds it. */
const panelFor = (face: BuiltFace, now = 0): PanelState => {
  if (face.group === 'companion') return openCompanion(catalogue, initialPanel(catalogue, null, { screen: 'companion' }), now);
  if (face.group === 'pitwall') return initialPanel(catalogue, null, { screen: 'pitwall' });
  return initialPanel(catalogue, prefixFor(catalogue, face.width, face.height), { theme: face.theme });
};

/**
 * Every panel state worth drawing: each page of each zone and the face's other modes; every module
 * of a companion forced in turn and its flag formats; every page of a pit wall with every page of
 * its catalogue in each of its zones, and its other settings.
 */
function states(face: BuiltFace): PanelState[] {
  const base = panelFor(face);
  if (face.group === 'companion') {
    const all = { ...base, companion: { ...base.companion, modules: base.companion.modules.map(() => true) } };
    const forced = (module: number, patch: Partial<PanelState['companion']> = {}): PanelState => ({ ...all, companion: { ...all.companion, ...patch, force: { module, until: Number.POSITIVE_INFINITY } } });
    return [...catalogue.companion.modules.map((_, i) => forced(i)), ...catalogue.companion.flagFormats.map((f) => forced(0, { flagFormat: f }))];
  }
  if (face.group === 'pitwall') {
    const w = catalogue.pitWall;
    return pitWallPagesFor(catalogue, face.width > face.height).flatMap((page, n) => [
      ...w.standardPages.map((p) => page.zones.reduce((s, z) => setPitWallZone(s, z.setting, z.kind === 'wide' ? p.number % w.widePages.length : p.number), setPitWallPage(base, n))),
      updatePitWall(updatePitWall(updatePitWall(setPitWallPage(base, n), 'classOnly', true), 'flagFormat', 'full'), 'webViewUrl', 'https://example.com'),
      updatePitWall(setPitWallPage(base, n), 'flagFormat', 'off'),
    ]);
  }
  if (base.face === null) return catalogue.cards.map((c) => update(base, 'slots', base.slots.map(() => c.number)));
  return [
    ...zonesOf(catalogue, base).flatMap((z) => z.pages.map((p) => setPage(catalogue, base, z.letter, p.number))),
    update(update(update(base, 'flagFormat', 'full'), 'lapReview', 'all'), 'revBar', 'off'),
    update(base, 'revBar', 'rpm'),
    update(base, 'rig', { ...base.rig, ClockFormat: '12h', DeltaPrecision: 'thousandths', PositionMode: 'overall', DeltaReference: 'lastlap' }),
  ];
}

const problemsOf = (engine: Engine) => [...engine.problems.values()].map((p) => `${p.kind}: ${p.where}\n${p.message}`);

describe('every built package against the race trace', () => {
  const frames = moments(readReplay('race.ndjson'));

  test.if(!HAS_BUILD)('skipped: build/manifest.json is absent or stale, run bun run build --all-themes at the repository root', () => {
    expect(HAS_BUILD).toBe(false);
  });

  test.each(faces.map((f) => [f.folder, f] as const))('%s draws every page without reaching a construct the evaluator does not compute', (_folder, face) => {
    if (face.group === 'face' && !/round/.test(face.folder)) expect(prefixFor(catalogue, face.width, face.height)).not.toBeNull();
    const engine = new Engine(face.library, face.main);
    let drawn = 0;
    const screens: [string | null, string][] = [];
    for (const state of states(face)) {
      const overlay = panelProperties(catalogue, state);
      engine.reset();
      for (const { t, properties } of frames) {
        const frame = engine.tick({ properties: (n) => (overlay.has(n) ? overlay.get(n) : properties[n]), now: t });
        drawn += texts(frame.ops).filter((s) => s !== '').length;
        // The screen is the one the panel asks for: the forced module, the pit wall's page.
        if (face.group === 'companion') screens.push([frame.screen, catalogue.companion.modules[state.companion.force!.module]!.id]);
        if (face.group === 'pitwall' && face.width > face.height) screens.push([frame.screen, pitWallPagesFor(catalogue, true)[state.pitWall.page]!.id]);
      }
    }
    expect(drawn).toBeGreaterThan(0);
    expect(screens.filter(([drawn, asked]) => drawn !== asked)).toEqual([]);
    expect(problemsOf(engine)).toEqual([]);
  });
});

describe('every built package against every committed trace', () => {
  const traces = traceFiles();

  // Version 1 until the nine are re-recorded on the VM with their opponent calls (#257), and version
  // 2 afterwards; the demo reads both, a version 1 trace drawing the opponent pages empty.
  test('the nine traces are all there, each a version the demo reads', () => {
    expect(traces.length).toBe(9);
    expect(traces[0]).toBe('race.ndjson');
    for (const file of traces) expect([1, 2]).toContain(readReplay(file).trace.header.trace);
  });

  test.each(faces.map((f) => [f.folder, f] as const))('%s draws every scenario as it opens without reaching a construct the evaluator does not compute', (_folder, face) => {
    const engine = new Engine(face.library, face.main);
    const overlay = panelProperties(catalogue, panelFor(face));
    for (const file of traces) {
      engine.reset();
      for (const { t, properties } of moments(readReplay(file))) engine.tick({ properties: (n) => (overlay.has(n) ? overlay.get(n) : properties[n]), now: t });
    }
    expect(problemsOf(engine)).toEqual([]);
  });
});

describe('the second screens and the car theme, as the page drives them', () => {
  const find = (folder: string) => faces.find((f) => f.folder === folder);
  const race = moments(readReplay('race.ndjson'))[1]!.properties;
  const driver = (face: BuiltFace) => {
    const engine = new Engine(face.library, face.main);
    let clock = 0;
    return {
      engine,
      tick: (state: PanelState) => {
        clock += 50;
        const overlay = panelProperties(catalogue, state, clock);
        return engine.tick({ properties: (n) => (overlay.has(n) ? overlay.get(n) : race[n]), now: 10_000 });
      },
      get clock() {
        return clock;
      },
    };
  };
  const id = (i: number) => catalogue.companion.modules[i]!.id;

  test.if(find('OpenDash Companion') !== undefined)('a companion opens on its start module, pages as NextScreen does, and comes back from a glance to the module it was on', () => {
    const d = driver(find('OpenDash Companion')!);
    let state = openCompanion(catalogue, initialPanel(catalogue, null, { screen: 'companion' }), 0);
    expect(d.tick(state).screen).toBe(id(catalogue.companion.defaultStart));
    // While the start module is forced it is the only screen enabled, so a tap has nowhere to go.
    expect(d.engine.navigate(1)).toBe(false);
    // Once the window has passed the paging is SimHub's: past the modules the rotation leaves off.
    while (d.clock < catalogue.companion.openOnWindowMs + 100) d.tick(state);
    d.engine.navigate(1);
    expect(d.tick(state).screen).toBe(id(1));
    d.engine.navigate(1);
    d.engine.navigate(1);
    d.engine.navigate(1);
    expect(d.tick(state).screen).toBe(id(4));
    // The sixth module, energy, is off on a fresh install, and the paging steps over it both ways.
    d.engine.navigate(1);
    expect(d.tick(state).screen).toBe(id(6));
    d.engine.navigate(-1);
    expect(d.tick(state).screen).toBe(id(4));
    // Hold the glance: its module, whatever the driver had paged to.
    state = beginCompanionGlance(state);
    for (let i = 0; i < 3; i++) expect(d.tick(state).screen).toBe(id(catalogue.companion.defaultGlance));
    // Let go: -2 for the back window, which the dashboard's own variables resolve to the module it was on.
    state = endCompanionGlance(catalogue, state, d.clock);
    expect(companionOpenOn(catalogue, state, d.clock)).toBe(catalogue.companion.openOnBack);
    for (let i = 0; i < 3; i++) expect(d.tick(state).screen).toBe(id(4));
    while (d.clock < catalogue.companion.openOnWindowMs + 100 + 5000) d.tick(state);
    expect(companionOpenOn(catalogue, state, d.clock)).toBe(catalogue.companion.openOnNone);
    expect(d.tick(state).screen).toBe(id(4));
    expect(problemsOf(d.engine)).toEqual([]);
  });

  test.if(find('OpenDash Pit wall') !== undefined)('a pit wall shows the page the panel names, and NextScreen does not page it', () => {
    const d = driver(find('OpenDash Pit wall')!);
    const base = initialPanel(catalogue, null, { screen: 'pitwall' });
    pitWallPagesFor(catalogue, true).forEach((page, n) => {
      expect(d.tick(setPitWallPage(base, n)).screen).toBe(page.id);
      d.engine.navigate(1);
      expect(d.tick(setPitWallPage(base, n)).screen).toBe(page.id);
    });
  });

  test.if(find('OpenDash Porsche 1280x480') !== undefined)('a Porsche face opens band D on its own page, and with no crest set draws the placeholder shield', () => {
    const face = find('OpenDash Porsche 1280x480')!;
    const state = panelFor(face);
    const d = driver(face);
    const ops = (list: readonly Op[]): Op[] => list.flatMap((o) => (o.kind === 'widget' ? [o, ...ops(o.ops)] : [o]));
    const drawn = ops(d.tick(state).ops);
    const band = zonesOf(catalogue, state).find((z) => z.letter === 'D')!;
    expect(band.pages.length).toBe(9);
    expect(band.pages[state.zones[3]!]!.id).toBe('porscheFoot');
    expect(drawn.some((o) => o.path.includes('/ porscheFoot >'))).toBe(true);
    expect(drawn.some((o) => o.path.endsWith('porscheFoot.badge'))).toBe(true);
    expect(drawn.some((o) => o.kind === 'standIn' && o.label.startsWith('Image from file'))).toBe(false);
  });
});
