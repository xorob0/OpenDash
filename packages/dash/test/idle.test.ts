/**
 * The idle screen, on every package the build composes. #763.
 *
 * Three things are held here and nowhere else. **The roles**, because they are what makes SimHub show
 * this screen rather than the racing face: an idle screen that is also an in-game screen, or a racing
 * screen that keeps the idle role, is the bug the ticket is about and neither is visible in a
 * dashboard's drawing. **The place in the list**, because SimHub's own `UpdateMetadatas` previews the
 * first in-game screen and half the suite reaches for `screens[0]`. And **the fit**, because the round
 * faces are discs and the stack is the one drawing on the screen that has no rectangle of its own.
 *
 * The step each frame lands on is written out rather than derived, so that a size moving shows as a
 * diff at review: that is the whole of what the ramp in `idle.ts` decides.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages } from '../src/build.ts';
import { rect } from '../src/design/geometry.ts';
import { IDLE_BLOCKS, IDLE_MARGIN, IDLE_SCREEN_NAME, IDLE_STATE, IDLE_UPDATE_BARE, idleItems, idleScreen, updateMarkText, updateMarkVisible, updateMarkWidest } from '../src/idle.ts';
import { CLOCK_FORMAT_SETTING, UPDATE_AVAILABLE, UPDATE_VERSION, UPDATE_VERSION_MAX_LENGTH } from '../src/contract.ts';
import { localClock, twelveHour } from '../src/second/values.ts';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { evalNcalc } from './ncalcEval.ts';
import { INNER_INSET } from '../src/layouts/round.ts';
import { LAYOUTS } from '../src/layouts/index.ts';
import { ds } from '../src/tokens.ts';
import { itemsOf, walkItems } from '../src/walk.ts';
import { measureText, type MeasuredFace } from '../src/design/advances.ts';
import type { Dashboard, Screen, TextItem } from '../src/generator.ts';

const PACKAGES = composePackages({ version: '0.0.0-test', log: () => {} }, true);

/** The main dashboard of a package, which is the one a display opens. */
const mainOf = (dashboards: readonly Dashboard[]): Dashboard => dashboards[0]!;

const textsOf = (screen: Screen): TextItem[] => [...walkItems(screen.items)].filter((i): i is TextItem => i.kind === 'text');

/** Which measured face an item draws in, as `textFit.test.ts` reads it. */
const faceOf = (item: TextItem): MeasuredFace => {
  if (item.font === 'Barlow') return item.fontWeight === 'Bold' ? 'BarlowBold' : 'BarlowMedium';
  if (item.fontWeight === 'Bold') return 'BarlowCondensedBold';
  if (item.fontWeight === 'Light') return 'BarlowCondensedLight';
  return 'BarlowCondensedSemiBold';
};

/**
 * The letters of an item rather than the box they sit in: left aligned, so the ink starts at the box's
 * own left edge and ends where the advances (or the monospace cells) say it does.
 *
 * The wordmark's boxes are a quarter wider than its letters on purpose, so a check that used the box
 * would be asking the margin and the disc about pixels that are never drawn.
 */
const inkOf = (item: TextItem): { left: number; top: number; right: number; bottom: number } => {
  const mono = item.monospace;
  const text = item.widest ?? item.text;
  const width = mono
    ? [...text].reduce((w, ch) => w + (mono.specialChars?.includes(ch) ? mono.specialCharsWidth : mono.charWidth), 0)
    : measureText(faceOf(item), text, item.fontSize);
  return { left: item.rect.left, top: item.rect.top, right: item.rect.left + width, bottom: item.rect.top + item.rect.height };
};

const idleOf = (dashboard: Dashboard): Screen => {
  const screen = dashboard.screens.find((s) => s.name === IDLE_SCREEN_NAME);
  if (!screen) throw new Error(`${dashboard.name} has no idle screen`);
  return screen;
};

/** Every package's main dashboard, by the folder it installs as. */
const MAINS: { folder: string; main: Dashboard }[] = PACKAGES.map(({ pkg }) => ({ folder: pkg.folderName, main: mainOf(pkg.dashboards) }));

/** The round faces, whose idle screen has a disc to stay inside rather than a rectangle. */
const ROUND = new Set(LAYOUTS.filter((l) => l.shape === 'round').map((l) => l.folder));

describe('every package idles on a screen of its own', () => {
  test('there are twenty-two of them, one per package, and none in an embedded dashboard', () => {
    // The count is written down because the failure this catches is a package built by a path that
    // was not given an idle screen, which is invisible from the package that was.
    expect(MAINS).toHaveLength(22);
    for (const { folder, main } of MAINS) {
      expect({ folder, idle: main.screens.filter((s) => s.name === IDLE_SCREEN_NAME).length }).toEqual({ folder, idle: 1 });
    }
    // The cards widget and the zone dashboards are drawn inside a screen of the main one, so a role on
    // them decides nothing; what would be wrong is an idle screen in there, drawing a second wordmark
    // inside a zone.
    for (const { pkg } of PACKAGES) {
      for (const embedded of pkg.dashboards.slice(1)) {
        expect({ dashboard: embedded.name, idle: embedded.screens.some((s) => s.name === IDLE_SCREEN_NAME) }).toEqual({ dashboard: embedded.name, idle: false });
      }
    }
  });

  test('it is last, so screen 0 is still the racing face SimHub previews', () => {
    // `EditorModel.UpdateMetadatas` sets MainPreviewIndex to the first in-game screen (9.12.6), the
    // gallery thumbnail is a photograph of the face, and the tests reach for screens[0].
    for (const { folder, main } of MAINS) {
      expect({ folder, last: main.screens[main.screens.length - 1]!.name }).toEqual({ folder, last: IDLE_SCREEN_NAME });
      expect({ folder, first: main.screens[0]!.name === IDLE_SCREEN_NAME }).toEqual({ folder, first: false });
    }
  });

  test('it is the only idle screen, it is no in-game screen, and no racing screen claims the idle role', () => {
    for (const { folder, main } of MAINS) {
      const idle = idleOf(main);
      expect({ folder, roles: `${idle.inGame};${idle.idle};${idle.pit}` }).toEqual({ folder, roles: 'false;true;false' });
      const racing = main.screens.filter((s) => s !== idle);
      expect({ folder, stillIdle: racing.filter((s) => s.idle !== false).map((s) => s.name) }).toEqual({ folder, stillIdle: [] });
      expect({ folder, inGame: racing.every((s) => s.inGame === true) }).toEqual({ folder, inGame: true });
    }
  });

  test('it is enabled in every configuration, where the screens around it are gated', () => {
    // `CheckGameModeScreen` runs `UpdateScreenEnabledStatus` before it filters by role, so a false
    // expression takes the screen out of the pass entirely and the idle mode falls through to
    // Indeterminate -- which shows whichever screen comes first, the racing face again.
    for (const { folder, main } of MAINS) {
      expect({ folder, expression: idleOf(main).enabledExpression }).toEqual({ folder, expression: undefined });
    }
    // And the screens it sits beside are the gated ones, on at least one package of each kind: that is
    // the configuration the idle screen has to survive.
    const gated = MAINS.filter(({ main }) => main.screens.some((s) => s !== idleOf(main) && s.enabledExpression !== undefined));
    expect(gated.map((m) => m.folder)).toContain('OpenDash');
    expect(gated.map((m) => m.folder)).toContain('OpenDash Companion');
    expect(gated.map((m) => m.folder)).toContain('OpenDash Pit wall');
  });
});

describe('what it draws', () => {
  test('the wordmark, the clock, the state and the update mark, and nothing that needs a game', () => {
    for (const { folder, main } of MAINS) {
      const items = textsOf(idleOf(main));
      const names = items.map((i) => i.name);
      // The mark on every package: it is shed where it cannot be drawn clear of the stack, and nothing
      // the build emits is such a frame. A package that lost it would say so here.
      expect({ folder, names }).toEqual({ folder, names: ['idle.wordmark.open', 'idle.wordmark.dash', 'idle.clock', 'idle.clock.unit', 'idle.state', 'idle.update'] });
      // SimHub's own clock -- its digits, and the `AM` or `PM` a twelve-hour clock hangs after them,
      // each placed by how wide the digits draw (#324) -- and the mark's text and visibility, which are
      // the plugin's. Anything else -- a best lap, a car, a driver, a session -- is a game's to publish,
      // and a screen shown because no game is running would draw it as a dash.
      const bound = Object.fromEntries(items.flatMap((i) => Object.entries(i.bindings ?? {}).map(([target, b]) => [`${i.name}.${target}`, typeof b?.formula === 'string' ? b.formula : b?.formula.expression])));
      expect({ folder, bound: Object.keys(bound).sort() }).toEqual({
        folder,
        bound: ['idle.clock.Left', 'idle.clock.Text', 'idle.clock.unit.Left', 'idle.clock.unit.Text', 'idle.clock.unit.Visible', 'idle.update.Text', 'idle.update.Visible'],
      });
      expect({ folder, clock: bound['idle.clock.Text'], word: bound['idle.clock.unit.Text'], shown: bound['idle.clock.unit.Visible'] }).toEqual({
        folder,
        clock: localClock().text,
        word: localClock().meridiem,
        shown: twelveHour(),
      });
      expect({ folder, text: bound['idle.update.Text'], visible: bound['idle.update.Visible'] }).toEqual({ folder, text: updateMarkText(), visible: updateMarkVisible() });
    }
  });

  test('the state line names the state, as it is written, in the label face', () => {
    const state = textsOf(idleOf(MAINS[0]!.main)).find((i) => i.name === 'idle.state')!;
    expect(state.text).toBe(IDLE_STATE);
    expect(state.text).toBe('No game running');
    expect({ font: state.font, weight: state.fontWeight }).toEqual({ font: ds.font.label, weight: 'Medium' });
    // No binding: SimHub is showing this screen because no game is running, so there is nothing to ask.
    expect(state.bindings).toBeUndefined();
  });

  test('the wordmark is the brand’s two weights and the design’s one ink', () => {
    const items = textsOf(idleOf(MAINS[0]!.main));
    const [open, dash] = [items.find((i) => i.name === 'idle.wordmark.open')!, items.find((i) => i.name === 'idle.wordmark.dash')!];
    expect([open.text, open.fontWeight, open.font]).toEqual(['open', 'Light', ds.font.data]);
    expect([dash.text, dash.fontWeight, dash.font]).toEqual(['Dash', 'Bold', ds.font.data]);
    expect([open.fontSize, dash.fontSize]).toEqual([dash.fontSize, dash.fontSize]);
    for (const item of [open, dash]) expect(item.textColor).toBe(ds.color.text.primary);
  });

  test('the blocks are a list, which is what #736 will reorder', () => {
    expect([...IDLE_BLOCKS]).toEqual(['wordmark', 'clock', 'state']);
  });
});

/**
 * Which rung of the ramp each screen lands on, by the size of its wordmark.
 *
 * Read, not asserted for its own sake: the ramp is measured against each frame, so this is where a
 * face that has grown or shrunk shows itself. Two frames take the second rung and both are out of
 * room for the first: the 800 x 286 nano strip is short, and the 480 round's disc is 456 across.
 */
const WORDMARK_SIZES: Record<string, number> = {
  OpenDash: ds.size.hero,
  'OpenDash 1280x480': ds.size.hero,
  'OpenDash 1280x400': ds.size.hero,
  'OpenDash 850x480': ds.size.hero,
  'OpenDash 800x480': ds.size.hero,
  'OpenDash 1280x720': ds.size.hero,
  'OpenDash 800x286': ds.size.lapTime,
  'OpenDash 600x686': ds.size.hero,
  'OpenDash slots 1920x480': ds.size.hero,
  'OpenDash slots 1280x480': ds.size.hero,
  'OpenDash slots 1280x400': ds.size.hero,
  'OpenDash slots 850x480': ds.size.hero,
  'OpenDash slots 800x480': ds.size.hero,
  'OpenDash slots 1280x720': ds.size.hero,
  'OpenDash slots 800x286': ds.size.lapTime,
  'OpenDash slots 600x686': ds.size.hero,
  'OpenDash 480 round': ds.size.lapTime,
  'OpenDash 800 round': ds.size.hero,
  'OpenDash Companion': ds.size.hero,
  'OpenDash Companion portrait': ds.size.hero,
  'OpenDash Pit wall': ds.size.hero,
  'OpenDash Pit wall portrait': ds.size.hero,
};

describe('it fits every frame the build emits', () => {
  test('each screen takes the largest rung of the ramp its frame has room for', () => {
    const sizes = Object.fromEntries(MAINS.map(({ folder, main }) => [folder, textsOf(idleOf(main)).find((i) => i.name === 'idle.wordmark.dash')!.fontSize]));
    expect(sizes).toEqual(WORDMARK_SIZES);
  });

  test('every box is inside the canvas, and every letter inside the margin', () => {
    for (const { folder, main } of MAINS) {
      for (const item of textsOf(idleOf(main))) {
        const box = item.rect;
        // The box, which is what the validator's `geometry/outside-canvas` warning is about: a package
        // is built with no warnings at all, and a transparent box off the edge is still one.
        const boxInside = box.left >= 0 && box.top >= 0 && box.left + box.width <= main.width && box.top + box.height <= main.height;
        expect({ folder, item: item.name, box, canvas: `${main.width}x${main.height}`, inside: boxInside }).toMatchObject({ inside: true });
        // And the margin, measured on the letters.
        const ink = inkOf(item);
        const clear = ink.left >= IDLE_MARGIN && ink.top >= IDLE_MARGIN && ink.right <= main.width - IDLE_MARGIN && ink.bottom <= main.height - IDLE_MARGIN;
        expect({ folder, item: item.name, ink, margin: IDLE_MARGIN, clear }).toMatchObject({ clear: true });
      }
    }
  });

  test('no box is ever narrower than the letters in it', () => {
    // `wordmark` caps its boxes at the room the frame leaves, so the 480 px portrait companion draws
    // the mark at the same size as every landscape package instead of losing a rung to a transparent
    // box. The cap may take the slack and may never take a letter: WPF clips what does not fit and
    // says nothing, which is the whole reason the slack is there.
    for (const { folder, main } of MAINS) {
      for (const item of textsOf(idleOf(main))) {
        const ink = inkOf(item);
        expect({ folder, item: item.name, box: item.rect.width, letters: ink.right - ink.left, holds: item.rect.width >= ink.right - ink.left }).toMatchObject({ holds: true });
      }
    }
  });

  test('nothing on a round face lies outside the inner disc', () => {
    // The ring is the outer 12 px and SimHub masks no corner, so the disc is the whole of the glass a
    // drawing may use; `layouts/round.ts` and `layouts.test.ts` say the same of the racing face.
    const round = MAINS.filter((m) => ROUND.has(m.folder));
    expect(round.map((m) => m.folder)).toEqual(['OpenDash 480 round', 'OpenDash 800 round']);
    for (const { folder, main } of round) {
      const middle = { x: main.width / 2, y: main.height / 2 };
      const r = main.width / 2 - INNER_INSET;
      for (const item of textsOf(idleOf(main))) {
        // Measured on the letters for the same reason as the margin: a transparent box crossing the
        // disc draws nothing.
        const ink = inkOf(item);
        for (const c of [
          { x: ink.left, y: ink.top },
          { x: ink.right, y: ink.top },
          { x: ink.left, y: ink.bottom },
          { x: ink.right, y: ink.bottom },
        ]) {
          expect({ folder, item: item.name, corner: c, radius: r, inside: Math.hypot(c.x - middle.x, c.y - middle.y) <= r }).toMatchObject({ inside: true });
        }
      }
    }
  });

  test('a frame too small for the stack sheds rather than drawing outside it', () => {
    // Nothing OpenDash ships reaches this, and the rule is the same one every module follows: shrink,
    // then shed, never draw outside. A 320 x 64 strip keeps the mark alone.
    const items = idleItems({ frame: rect(0, 0, 320, 64) });
    expect(items.map((i) => i.name)).toEqual(['idle.wordmark.open', 'idle.wordmark.dash']);
    for (const item of items) expect({ item: item.name, top: item.rect.top, inside: item.rect.top >= 0 && item.rect.top + item.rect.height <= 64 }).toMatchObject({ inside: true });
  });

  test('a screen on a rectangle that is not the whole dashboard is centred on the rectangle it was given', () => {
    // The screen takes a frame rather than a size, so that #736 can put it anywhere. Nothing ships
    // that way yet, and the property is worth one test rather than a comment.
    const offset = idleScreen({ frame: rect(400, 100, 400, 200) });
    for (const item of offset.items) {
      if (item.kind !== 'text') throw new Error('the idle screen draws text');
      expect({ item: item.name, left: item.rect.left, inside: item.rect.left >= 400 }).toMatchObject({ inside: true });
      expect({ item: item.name, top: item.rect.top, inside: item.rect.top >= 100 && item.rect.top + item.rect.height <= 300 }).toMatchObject({ inside: true });
    }
  });
});

describe('it costs the packages nothing they were not already carrying', () => {
  test('the idle screen reads the plugin for the update mark and the clock format, and nothing else', () => {
    // An idle screen whose content depended on a plugin setting would be a screen that changes with a
    // plugin the package does not require. The mark is one exception, and it is an exception only in
    // the direction of silence: both reads carry a default that draws nothing (below). The clock
    // format is the other, and it is an exception only in the direction of the clock the screen has
    // always drawn: without the plugin it reads `24h` (#324, and `clockFormat.test.ts`).
    for (const { folder, main } of MAINS) {
      const items = itemsOf({ ...main, screens: [idleOf(main)] });
      const properties = items.flatMap((i) => Object.values(i.bindings ?? {}).map((b) => (typeof b?.formula === 'string' ? b.formula : (b?.formula.expression ?? ''))));
      const read = [...new Set(properties.flatMap((p) => [...p.matchAll(/\[(OpenDash\.[A-Za-z0-9]+)\]/g)].map((m) => m[1]!)))].sort();
      expect({ folder, read }).toEqual({ folder, read: [`OpenDash.${CLOCK_FORMAT_SETTING}`, `OpenDash.${UPDATE_AVAILABLE}`, `OpenDash.${UPDATE_VERSION}`] });
    }
  });
});

describe('the update mark', () => {
  const AVAILABLE = `OpenDash.${UPDATE_AVAILABLE}`;
  const VERSION = `OpenDash.${UPDATE_VERSION}`;
  const shown = (props: Record<string, unknown>): string | null => (evalNcalc(updateMarkVisible(), props) === true ? String(evalNcalc(updateMarkText(), props)) : null);

  test('without the plugin it draws nothing: the absence of the plugin triggers nothing', () => {
    // ADR 0003's promise, and the ticket's rule in particular: a user running the dashboard alone is a
    // supported user, not an incomplete installation.
    expect(shown({})).toBeNull();
  });

  test('it is silent whenever the plugin says there is nothing to offer', () => {
    // Up to date, the check switched off, no answer yet, no network: the plugin publishes false for all
    // of them (UpdateMark in the plugin, and UpdateMarkTests.cs), and false is silence here.
    expect(shown({ [AVAILABLE]: false, [VERSION]: '' })).toBeNull();
    expect(shown({ [AVAILABLE]: false, [VERSION]: '0.4.0' })).toBeNull();
  });

  test('it names the release and where to take it, and says so without a number it could not fit', () => {
    expect(shown({ [AVAILABLE]: true, [VERSION]: '0.4.0' })).toBe('UPDATE TO 0.4.0 IN SIMHUB');
    expect(shown({ [AVAILABLE]: true, [VERSION]: '0.10.0-rc.10' })).toBe('UPDATE TO 0.10.0-rc.10 IN SIMHUB');
    expect(shown({ [AVAILABLE]: true, [VERSION]: '' })).toBe(IDLE_UPDATE_BARE);
    expect(shown({ [AVAILABLE]: true })).toBe(IDLE_UPDATE_BARE);
  });

  test('its box holds the longest version the plugin will publish, and the version this repository is at', () => {
    const version = readFileSync(join(import.meta.dir, '..', '..', '..', 'VERSION'), 'utf8').trim();
    expect(version.length).toBeLessThanOrEqual(UPDATE_VERSION_MAX_LENGTH);
    const widest = updateMarkWidest();
    for (const { folder, main } of MAINS) {
      const mark = textsOf(idleOf(main)).find((i) => i.name === 'idle.update')!;
      expect({ folder, widest: mark.widest }).toEqual({ folder, widest });
      for (const drawn of [shown({ [AVAILABLE]: true, [VERSION]: version })!, IDLE_UPDATE_BARE]) {
        expect({ folder, drawn, fits: measureText('BarlowMedium', drawn, mark.fontSize) <= mark.rect.width }).toMatchObject({ fits: true });
      }
    }
  });

  test('it is small, in the corner, and clear of everything else on the screen', () => {
    for (const { folder, main } of MAINS) {
      const items = textsOf(idleOf(main));
      const mark = items.find((i) => i.name === 'idle.update')!;
      // Small enough to ignore: the small label, in the label grey, never larger than the state line.
      expect({ folder, size: mark.fontSize, color: mark.textColor }).toEqual({ folder, size: ds.size.labelSm, color: ds.color.text.label });
      // Clear of the stack, measured on the boxes rather than the letters, which is the stricter of the two.
      for (const other of items.filter((i) => i !== mark)) {
        const a = mark.rect;
        const b = other.rect;
        const apart = a.left >= b.left + b.width || b.left >= a.left + a.width || a.top >= b.top + b.height || b.top >= a.top + a.height;
        expect({ folder, other: other.name, apart }).toMatchObject({ apart: true });
      }
      // In the corner: the bottom margin on every shape, and the right margin on a rectangle, where the
      // text is right aligned so that a short version still sits in it.
      const bottom = mark.rect.top + mark.rect.height;
      if (ROUND.has(folder)) {
        expect({ folder, hAlign: mark.hAlign, centred: Math.abs(mark.rect.left + mark.rect.width / 2 - main.width / 2) <= 1 }).toMatchObject({ hAlign: 'center', centred: true });
        expect({ folder, low: bottom > main.height * 0.75 }).toMatchObject({ low: true });
      } else {
        expect({ folder, hAlign: mark.hAlign, right: mark.rect.left + mark.rect.width, bottom }).toEqual({ folder, hAlign: 'right', right: main.width - IDLE_MARGIN, bottom: main.height - IDLE_MARGIN });
      }
    }
  });

  test('a frame with no room for it sheds it rather than crowding the stack', () => {
    // The 320 x 64 strip keeps the wordmark alone, and the mark goes before it would touch it.
    expect(idleItems({ frame: rect(0, 0, 320, 64) }).map((i) => i.name)).not.toContain('idle.update');
  });
});
