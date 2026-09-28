/**
 * The idle screen: what a display shows when no game is running. #113.
 *
 * Every screen OpenDash ships already declared `IdleScreen: true` on its racing screen, so SimHub has
 * always had something to show between sessions and what it showed was the face with no data in it --
 * a rev bar at zero, twenty empty leaderboard rows, dashes where the lap times go. That is the bug
 * this file ends: one screen per package with idle content and `inGame: false`, and the racing screens
 * giving up the idle role.
 *
 * ## How SimHub picks it (verified against 9.12.6, decompiled)
 *
 * `EditorModel.CheckGameModeScreen` calls `UpdateScreenEnabledStatus` first and filters by role
 * afterwards, so **the enabled expression outranks the role**: it keeps the screens whose
 * `ScreenEnabledExpression` evaluates above zero (an empty expression is enabled) and picks among
 * *those* by mode -- Game when `GameRunning` and some enabled screen is an in-game screen, Pit when in
 * addition the car is in the pit lane and some enabled screen is a pit screen, Idle when neither and
 * some enabled screen is an idle screen, and Indeterminate when none of the three holds, which shows
 * whatever comes first. An idle screen therefore has to be enabled in every configuration a package
 * has, or the mode it exists for is the one that falls through. That is why {@link idleScreen} writes
 * no expression at all, where the face gates its two rev-bar arrangements on `OpenDash.RevBar` and the
 * companion gates its twenty-one screens on the module rotation.
 *
 * `Dashboard.GetActiveScreens`, which is what the Next and Previous screen actions and the companion's
 * tap paging walk, filters by role **only when the roles differ between screens**: it compares
 * `"{PitScreen};{InGameScreen};{IdleScreen}"` across the enabled screens and keeps every one of them
 * while they are all the same string. Until this screen existed they were all the same string, which
 * is what `docs/second-screens.md` recorded. They differ now, so while a game runs the ring is the
 * in-game screens alone and a driver cannot page onto the idle screen; with no game running the ring
 * is this screen alone and paging does nothing, which is the right answer for a rig at rest.
 *
 * And `EditorModel.UpdateMetadatas` sets `MainPreviewIndex` to the first in-game screen, falling back
 * to the first idle one. The idle screen is therefore appended **last** everywhere, so index 0 stays
 * the racing face: the gallery thumbnail, SimHub's own screen previews and every `screens[0]` in the
 * tests all still mean the screen they meant before.
 *
 * ## What it draws
 *
 * The wordmark, the wall clock, and one line naming the state. Nothing else, and in particular no
 * reading that does not exist without a game: a last best lap, the car, the driver and the session are
 * all a game's to publish, and a row of dashes where one of them would go is the thing the racing face
 * was already doing wrong. `docs/design/voice.md` is why the line reads "No game running" rather than
 * a sentence about SimHub waiting for telemetry -- it names the state and leaves the mechanism out.
 *
 * The three are a list rather than an arrangement, because #104 hands the idle screen to the user and
 * what that needs is blocks which can be turned off, reordered and restyled rather than a composition
 * to be taken apart first. {@link IDLE_BLOCKS} is the list, and {@link idleItems} is the only thing
 * that knows where they go.
 *
 * **The room under the state line is deliberately left empty.** #83 puts "an update is available"
 * there, this being the one dashboard surface where a message costs nothing, and it is out of scope
 * here: a fourth block on the end of the stack is all it should need.
 */
import type { Hex, Rect, Screen, TextItem } from './generator.ts';
import { wordmark, wordmarkWidth } from './components/wordmark.ts';
import { measureText } from './design/advances.ts';
import { boxSlack, cells, monoWidth } from './design/metrics.ts';
import { centre, distance, rect, type Circle } from './design/geometry.ts';
import { label } from './elements/label.ts';
import { numeral } from './elements/numeral.ts';
import { CHARS, localClock } from './second/values.ts';
import { ds } from './tokens.ts';

/** The screen's name, which is what SimHub's screen list and the plugin's preview show. */
export const IDLE_SCREEN_NAME = 'Idle';

/**
 * The state, named rather than explained: `docs/design/voice.md` on mechanism, and the one fact a rig
 * at rest can state with no game to ask.
 *
 * Static text and not a binding, on purpose. SimHub is showing this screen *because* `GameRunning` is
 * false, so there is nothing for a binding to look up, and a package installed without the plugin says
 * the same thing as one installed with it.
 */
export const IDLE_STATE = 'No game running';

/** The design-time sample of the clock; the binding writes the real one. */
const CLOCK_SAMPLE = '14:32';

/** The blocks the screen draws, top to bottom. Shed from the end, which is why the order is this one. */
export const IDLE_BLOCKS = ['wordmark', 'clock', 'state'] as const;
export type IdleBlock = (typeof IDLE_BLOCKS)[number];

/**
 * One rung of the idle screen's ramp. Every size is a token of the face's own ramp, since
 * `design/tokens.json` is the only place a size is defined and a screen at rest is no reason for a
 * ninth one.
 */
interface IdleStep {
  wordmark: number;
  clock: number;
  state: number;
  gap: number;
}

/**
 * Largest first. Which rung a screen gets is measured rather than tabulated: there are twenty-two
 * packages to fit, from a 1920 x 1080 pit wall to a 480 px disc, and a table of twenty-two answers is a
 * table that goes stale the next time one of them moves.
 *
 * The state line stays at one of the design's two label sizes on every rung, because it is a label and
 * those are the two sizes there are. What grows with the frame is the mark and the clock.
 *
 * As the packages stand every screen takes the first rung but two, and the two are the ones with no
 * room for it: the 800 x 286 nano strip, and the 480 px round face, whose disc is 456 across. The last
 * two rungs are the floor under a frame nobody has built yet.
 * `packages/dash/test/idle.test.ts` records where each one lands.
 */
const IDLE_STEPS: readonly IdleStep[] = [
  { wordmark: ds.size.hero, clock: ds.size.lapTime, state: ds.size.label, gap: ds.space[5] },
  { wordmark: ds.size.lapTime, clock: ds.size.value, state: ds.size.label, gap: ds.space[5] },
  { wordmark: ds.size.value, clock: ds.size.valueSm, state: ds.size.label, gap: ds.space[4] },
  { wordmark: ds.size.valueSm, clock: ds.size.valueSm, state: ds.size.labelSm, gap: ds.space[3] },
];

/** The margin the stack keeps from the edges of its frame. */
export const IDLE_MARGIN = ds.space[5];

/** Where a block whose ink is `width` wide starts, to be centred on `cx`. */
const left = (cx: number, width: number): number => Math.round(cx - width / 2);

/** The clock's box: its cells plus the slack every value's box gets, which is what `numeral` draws. */
const clockWidth = (fs: number): number => monoWidth(cells('SemiBold', fs), CHARS.minutesClock) + boxSlack(fs);

/** The state line's box, measured in the weight a label is drawn in. */
const stateWidth = (fs: number): number => Math.ceil(measureText('BarlowMedium', IDLE_STATE, fs)) + 2;

/**
 * A block: how wide its ink is at a size, and the drawing itself, centred on `cx` with its canvas line
 * box at `y`.
 *
 * The ink width is declared rather than read back off the items, because the wordmark's boxes are a
 * quarter wider than its letters -- deliberately, so that a SimHub install missing the Light face
 * cannot clip it -- and a stack centred on those boxes would sit visibly left of centre.
 */
interface IdleBlockSpec {
  ink(fs: number): number;
  /**
   * The drawing. `frame` is passed because the wordmark's boxes are the one thing here whose width is
   * not a function of the text alone: their slack is capped by the room the frame leaves, so that a
   * box nobody sees cannot cost the mark a rung of the ramp.
   */
  draw(name: string, cx: number, y: number, fs: number, frame: Rect): TextItem[];
}

const BLOCKS: Record<IdleBlock, IdleBlockSpec> = {
  wordmark: {
    ink: (fs) => wordmarkWidth(fs),
    draw: (name, cx, y, fs, frame) => {
      const x = left(cx, wordmarkWidth(fs));
      return wordmark(name, x, y, fs, { maxWidth: frame.left + frame.width - x }).items;
    },
  },
  clock: {
    ink: (fs) => monoWidth(cells('SemiBold', fs), CHARS.minutesClock),
    draw: (name, cx, y, fs) => [numeral(name, CLOCK_SAMPLE, left(cx, clockWidth(fs)), y, fs, CHARS.minutesClock, { bind: localClock() })],
  },
  state: {
    ink: (fs) => stateWidth(fs),
    draw: (name, cx, y, fs) => [label(name, IDLE_STATE, left(cx, stateWidth(fs)), y, stateWidth(fs), { size: fs, color: ds.color.text.secondary })],
  },
};

/**
 * One drawn arrangement: the items, and the ink each block occupies.
 *
 * The ink is what the fit is judged on. A transparent box overhanging an edge draws nothing, and
 * reading the wordmark's own overhang as a miss would cost a rung of the ramp on the round faces for
 * pixels that are not there.
 */
interface IdleDrawing {
  items: TextItem[];
  ink: Rect[];
}

/** The stack of blocks at a step, centred on `cx`, its first canvas line box at `top`. */
function stack(blocks: readonly IdleBlock[], step: IdleStep, prefix: string, cx: number, top: number, frame: Rect): IdleDrawing {
  const items: TextItem[] = [];
  const ink: Rect[] = [];
  let y = top;
  for (const id of blocks) {
    const fs = step[id];
    const drawn = BLOCKS[id].draw(`${prefix}${id}`, cx, y, fs, frame);
    items.push(...drawn);
    // The block's line box at the width of its letters: the vertical extent is the boxes' own, since a
    // WPF line box is what the glyphs are laid out in, and the horizontal extent is the ink's.
    const boxTop = Math.min(...drawn.map((i) => i.rect.top));
    const boxBottom = Math.max(...drawn.map((i) => i.rect.top + i.rect.height));
    const width = BLOCKS[id].ink(fs);
    ink.push(rect(left(cx, width), boxTop, width, boxBottom - boxTop));
    y += fs + step.gap;
  }
  return { items, ink };
}

/** The union of a set of boxes, which is what a drawing is centred by. */
const boundsOf = (boxes: readonly Rect[]): Rect => {
  const l = Math.min(...boxes.map((b) => b.left));
  const t = Math.min(...boxes.map((b) => b.top));
  const r = Math.max(...boxes.map((b) => b.left + b.width));
  const b = Math.max(...boxes.map((b) => b.top + b.height));
  return rect(l, t, r - l, b - t);
};

/** Every corner of a box: on a round face a centre inside the disc says nothing about the corners. */
const corners = (r: Rect): { x: number; y: number }[] => [
  { x: r.left, y: r.top },
  { x: r.left + r.width, y: r.top },
  { x: r.left, y: r.top + r.height },
  { x: r.left + r.width, y: r.top + r.height },
];

export interface IdleSpec {
  /** The whole screen. The stack is centred in it. */
  frame: Rect;
  /**
   * A round face's inner disc, inside which everything has to stay: SimHub masks no corner, so ink
   * outside the disc is ink drawn off the glass.
   */
  disc?: Circle;
  /** Item name prefix; `idle.` by default. */
  prefix?: string;
}

const inside = (outer: Rect, box: Rect): boolean =>
  box.left >= outer.left && box.top >= outer.top && box.left + box.width <= outer.left + outer.width && box.top + box.height <= outer.top + outer.height;

/**
 * Whether a drawing fits: its ink inside the frame's margin and inside the disc where there is one,
 * and its boxes inside the frame itself.
 *
 * Two rules and not one, because the wordmark's boxes are wider than its letters. The letters are what
 * a reader sees, so they are what the margin and the disc are about; the boxes are what the validator
 * sees, and one leaving the canvas is a `geometry/outside-canvas` warning on a package that is meant to
 * build without any. The box rule is a backstop rather than the thing that decides a rung: the slack
 * that would break it is capped by `wordmark`'s `maxWidth` first, so a box never costs the mark a size
 * the letters had room for.
 */
function fits(drawing: IdleDrawing, spec: IdleSpec): boolean {
  const room = rect(spec.frame.left + IDLE_MARGIN, spec.frame.top + IDLE_MARGIN, spec.frame.width - 2 * IDLE_MARGIN, spec.frame.height - 2 * IDLE_MARGIN);
  const disc = spec.disc;
  const middle = disc ? { x: disc.cx, y: disc.cy } : { x: 0, y: 0 };
  for (const box of drawing.ink) {
    if (!inside(room, box)) return false;
    if (disc && corners(box).some((c) => distance(c, middle) > disc.r)) return false;
  }
  return drawing.items.every((item) => inside(spec.frame, item.rect));
}

/** The stack at a step, centred on the frame -- or on the disc, which is a round face's own centre. */
function placed(blocks: readonly IdleBlock[], step: IdleStep, spec: IdleSpec): IdleDrawing {
  const prefix = spec.prefix ?? 'idle.';
  const middle = spec.disc ? { x: spec.disc.cx, y: spec.disc.cy } : centre(spec.frame);
  // Drawn once at the origin to learn its own height, a line box hanging above and below the canvas
  // box it was asked for, and then drawn where that puts it, so the items carry no second offset.
  const span = boundsOf(stack(blocks, step, prefix, middle.x, 0, spec.frame).items.map((i) => i.rect));
  return stack(blocks, step, prefix, middle.x, Math.round(middle.y - span.height / 2 - span.top), spec.frame);
}

/**
 * The idle screen's items, centred in the frame at the largest step that fits it.
 *
 * It shrinks before it sheds, which is the opposite of what a zone page does, and for the reason
 * `pitwall.ts` gives for drawing its panel rows explicitly rather than fitting them: a page in a zone
 * is one of twenty-one and sheds its secondary rows to keep its primary one legible, where these three
 * blocks are the whole screen and losing one of them costs more than a rung of the ramp.
 *
 * Shedding is the floor under that rather than the plan. Nothing OpenDash ships reaches it --
 * `packages/dash/test/idle.test.ts` records which step each frame lands on -- and a 320 x 64 strip
 * somebody adds later gets the wordmark rather than an exception.
 */
export function idleItems(spec: IdleSpec): TextItem[] {
  for (let keep = IDLE_BLOCKS.length; keep > 0; keep--) {
    const blocks = IDLE_BLOCKS.slice(0, keep);
    for (const step of IDLE_STEPS) {
      const drawing = placed(blocks, step, spec);
      if (fits(drawing, spec)) return drawing.items;
    }
  }
  // The wordmark alone at the smallest step, on a frame no larger than a few words. Returned rather
  // than thrown, because a screen that draws nothing is worse than one that draws its own name large.
  return placed(['wordmark'], IDLE_STEPS[IDLE_STEPS.length - 1]!, spec).items;
}

export interface IdleScreenSpec extends IdleSpec {
  background?: Hex;
}

/**
 * The screen, with the roles that make SimHub show it and nothing else.
 *
 * No `enabledExpression`: an empty one is enabled, and an idle screen a setting can disable is a
 * package that goes back to showing the racing face at rest in whichever configuration disabled it.
 */
export function idleScreen(spec: IdleScreenSpec): Screen {
  return {
    name: IDLE_SCREEN_NAME,
    inGame: false,
    idle: true,
    pit: false,
    backgroundColor: spec.background ?? ds.color.surface.base,
    items: idleItems(spec),
  };
}
