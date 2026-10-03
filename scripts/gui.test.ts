/**
 * What `scripts/gui.ts` decides without a VM: whether the guest's display is in the mode its
 * coordinates were measured in, where in SimHub's window each click lands, what a look at Dash
 * Studio reads, and what a failure to open a dashboard says first. The clicking itself is a remote
 * side effect and is proved by running it, except for what a script run in the desktop makes of
 * one that never finished, which is asked of a fake host (`fakeHost.ts`).
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { withFakeHost, type Answer } from './fakeHost.ts';
import {
  aimAt,
  cardsIn,
  COLOUR_TOLERANCE,
  DASH_STUDIO,
  EXPECTED_SCREEN,
  guessRow,
  hex,
  inDesktopScript,
  lookPoints,
  openDashboards,
  openedLine,
  openFailure,
  parseClientArea,
  parseColumn,
  parseLook,
  pressAfter,
  readLook,
  repeatPass,
  sameColour,
  screenModeProblem,
  TRACK_LAYOUT_OFFER,
  type Attempt,
  type FailureContext,
  type LookSamples,
  type Rgb,
  type Run,
  type Seen,
} from './gui.ts';

describe('the display mode the coordinates need', () => {
  test('the expected mode is the one the file documents and the VM is set up with', () => {
    expect(EXPECTED_SCREEN).toEqual({ width: 3840, height: 2160 });
  });

  test('that mode is no problem at all', () => {
    expect(screenModeProblem({ ...EXPECTED_SCREEN })).toBeNull();
  });

  test('the mode a restarted container comes back at is refused, by name', () => {
    const problem = screenModeProblem({ width: 1280, height: 800 });
    expect(problem).toContain('1280x800');
    expect(problem).toContain('3840x2160');
  });

  test('the refusal says what to run, since naming the mode alone leaves the reader stuck', () => {
    const problem = screenModeProblem({ width: 2560, height: 1600 }) ?? '';
    expect(problem).toContain('/opt/winvm/shared/setres.ps1');
    expect(problem).toContain('-Width 3840 -Height 2160');
    // The mode belongs to the interactive session, so a command run over SSH changes nothing.
    expect(problem).toContain('interactive session');
  });

  test('a mode that is only wrong in one dimension is still wrong', () => {
    expect(screenModeProblem({ width: EXPECTED_SCREEN.width, height: 1600 })).not.toBeNull();
    expect(screenModeProblem({ width: 2560, height: EXPECTED_SCREEN.height })).not.toBeNull();
  });

  // testing-vm.md gave the GUI tools' coordinates as 1280x800 with a 1.25 scale, while the same page's
  // Size row said 3840x2160, and the stale one sent clicks to the wrong place (#540).
  test("testing-vm.md's Size row and GUI row give this mode, and the GUI row's scale follows from it", async () => {
    const doc = await Bun.file(`${import.meta.dir}/../docs/testing-vm.md`).text();
    const row = (name: string): string => doc.split('\n').find((line) => line.startsWith(`| ${name} |`)) ?? '';
    const mode = `${EXPECTED_SCREEN.width}×${EXPECTED_SCREEN.height}`;
    expect(row('Size')).toContain(mode);
    const gui = row('GUI');
    expect(gui).toContain(`Coordinates are full-resolution (${mode}`);
    expect(gui).toContain(`${EXPECTED_SCREEN.width}/1024 = ${EXPECTED_SCREEN.width / 1024}`);
  });
});

// Measured on the guest on 2026-09-29, and the ground the tests below stand on: the offer's band,
// the search box and a filtered row's card, each with and without the offer above them.
const MEASURED = {
  offerBand: [158, 210],
  offerBandX: [1325, 2725],
  noThanks: { x: [2627, 2712], y: [171, 197] },
  enableItNow: [2516, 2620],
  searchBox: [201, 224],
  searchBoxUnderOffer: [262, 285],
  card: [299, 377],
  cardUnderOffer: [360, 438],
} as const;
/** Where `openDashboard` rests the pointer on a row: 0.383 of the width. */
const POINTER_X = Math.round(EXPECTED_SCREEN.width * 0.383);
const within = (v: number, [lo, hi]: readonly [number, number]) => v >= lo && v <= hi;

const { offer: BAND, row: ROW, hoveredRow: LIT, page: PAGE } = DASH_STUDIO;
const look = (offer: Rgb[], row: Rgb[]): LookSamples => ({ offer, row });
const noBand: Rgb[] = [PAGE, PAGE, PAGE];
const band: Rgb[] = [BAND, BAND, BAND];

describe('the colours a look knows', () => {
  test('no two of them can be taken for each other', () => {
    const all: Rgb[] = Object.values(DASH_STUDIO);
    for (const a of all)
      for (const b of all) if (a !== b) expect(Math.max(...a.map((v, i) => Math.abs(v - b[i]!)))).toBeGreaterThan(2 * COLOUR_TOLERANCE);
  });

  test('the search box, three levels darker than the page, is not the page', () => {
    expect(sameColour([0x22, 0x22, 0x22], PAGE)).toBe(false);
  });

  test('a colour is written the way a person would look it up', () => {
    expect(hex(LIT)).toBe('#213e4a');
  });
});

describe('where openDashboard clicks and looks', () => {
  // SimHub's client area when maximised on the guest, which is where every value here was measured.
  const client = { left: 0, top: 0, width: 3840, height: 2120 };

  test('a guessed row is where the offsets always put it', () => {
    expect(guessRow(0, 0, false)).toBe(336);
    expect(guessRow(221, 0, false)).toBe(557);
    expect(guessRow(0, 1, false)).toBe(420);
  });

  test('under the offer, a guessed row is its 61 px lower', () => {
    expect(TRACK_LAYOUT_OFFER.shift).toBe(61);
    expect(guessRow(0, 0, true)).toBe(397);
    expect(guessRow(221, 0, true)).toBe(618);
    expect(aimAt(client, guessRow(0, 0, true), true).windowed.y).toBe(483);
  });

  test('the search click lands in the box, with and without the offer', () => {
    expect(within(aimAt(client, 336).search.y, MEASURED.searchBox)).toBe(true);
    expect(within(aimAt(client, 336, true).search.y, MEASURED.searchBoxUnderOffer)).toBe(true);
  });

  test("the offer's points sit inside its band", () => {
    for (const [, y] of lookPoints(336).offer) expect(within(y, MEASURED.offerBand)).toBe(true);
  });

  test("the row's points sit inside the card, with and without the offer", () => {
    for (const [, y] of lookPoints(guessRow(0, 0, false)).row) expect(within(y, MEASURED.card)).toBe(true);
    for (const [, y] of lookPoints(guessRow(0, 0, true)).row) expect(within(y, MEASURED.cardUnderOffer)).toBe(true);
  });

  test('"No thanks" is clicked on "No thanks", and never on "Enable it now" beside it', () => {
    const x = Math.round(EXPECTED_SCREEN.width * TRACK_LAYOUT_OFFER.x);
    expect(within(x, MEASURED.noThanks.x)).toBe(true);
    expect(within(TRACK_LAYOUT_OFFER.y, MEASURED.noThanks.y)).toBe(true);
    expect(within(x, MEASURED.enableItNow)).toBe(false);
  });

  test("the offer's points are inside the band's width as well as its height", () => {
    for (const [x] of lookPoints(336).offer) expect(within(x, MEASURED.offerBandX)).toBe(true);
  });

  // The pointer is drawn into the framebuffer the look reads.
  test('no point is within 100 px of where the pointer rests', () => {
    const noThanks = [Math.round(EXPECTED_SCREEN.width * TRACK_LAYOUT_OFFER.x), TRACK_LAYOUT_OFFER.y] as const;
    for (const rowY of [336, 397, 557, 618]) {
      const { offer, row } = lookPoints(rowY);
      for (const [x, y] of [...offer, ...row]) {
        expect(Math.max(Math.abs(x - POINTER_X), Math.abs(y - rowY))).toBeGreaterThan(100);
        expect(Math.max(Math.abs(x - noThanks[0]), Math.abs(y - noThanks[1]))).toBeGreaterThan(100);
      }
    }
  });
});

describe('reading a look', () => {
  test('a lit row is a Start button up', () => {
    expect(readLook(look(noBand, [LIT, LIT, LIT]), 'down')).toEqual({ kind: 'hovered' });
  });

  test('one pixel under a glyph does not outvote the other two', () => {
    expect(readLook(look(noBand, [LIT, [255, 255, 255], LIT]), 'down').kind).toBe('hovered');
  });

  test('an unlit row, and nothing at all, are told apart', () => {
    expect(readLook(look(noBand, [ROW, ROW, ROW]), 'down').kind).toBe('row');
    expect(readLook(look(noBand, [PAGE, PAGE, PAGE]), 'down').kind).toBe('page');
  });

  test('an offer that appeared moves the rows, whatever is under the pointer', () => {
    expect(readLook(look(band, [LIT, LIT, LIT]), 'down')).toEqual({ kind: 'moved', now: 'up' });
  });

  test('an offer the pass is working under is expected, and a lit row there is a lit row', () => {
    expect(readLook(look(band, [LIT, LIT, LIT]), 'up').kind).toBe('hovered');
  });

  test('an offer that went away while the pass worked under it moves the rows back', () => {
    expect(readLook(look(noBand, [LIT, LIT, LIT]), 'up')).toEqual({ kind: 'moved', now: 'down' });
  });

  test('part of a band is the offer moving, from either side', () => {
    expect(readLook(look([BAND, PAGE, PAGE], [LIT, LIT, LIT]), 'down')).toEqual({ kind: 'moved', now: 'partial' });
    expect(readLook(look([BAND, BAND, PAGE], [LIT, LIT, LIT]), 'up')).toEqual({ kind: 'moved', now: 'partial' });
  });

  test('a colour nobody measured is reported by value', () => {
    expect(readLook(look(noBand, [[0x12, 0x34, 0x56], [0x12, 0x34, 0x56], PAGE]), 'down')).toEqual({ kind: 'unknown', colour: '#123456' });
  });

  test('the tolerance is exactly as wide as it says', () => {
    const by = (d: number): Rgb => [LIT[0] + d, LIT[1], LIT[2]];
    expect(readLook(look(noBand, [by(COLOUR_TOLERANCE), by(COLOUR_TOLERANCE), by(COLOUR_TOLERANCE)]), 'down').kind).toBe('hovered');
    const past = COLOUR_TOLERANCE + 1;
    expect(readLook(look(noBand, [by(past), by(past), by(past)]), 'down').kind).toBe('unknown');
  });
});

describe("the host's look line", () => {
  const good = `look ${JSON.stringify({ offer: noBand, row: [LIT, LIT, LIT] })}`;

  test('is found among whatever else the run printed', () => {
    expect(parseLook(`vncdotool says hello\n${good}\n`)).toEqual({ offer: noBand, row: [LIT, LIT, LIT] });
  });

  test('is absent from a run that failed', () => {
    expect(parseLook('Traceback (most recent call last):')).toBeNull();
  });

  test('is refused unless it is nine pixels of three bytes', () => {
    expect(parseLook(`look ${JSON.stringify({ offer: [PAGE], row: [LIT, LIT, LIT] })}`)).toBeNull();
    expect(parseLook(`look ${JSON.stringify({ offer: [[1.5, 0, 0], PAGE, PAGE], row: [LIT, LIT, LIT] })}`)).toBeNull();
    expect(parseLook(`look ${JSON.stringify({ offer: [[256, 0, 0], PAGE, PAGE], row: [LIT, LIT, LIT] })}`)).toBeNull();
    expect(parseLook('look {')).toBeNull();
    expect(parseLook('look null')).toBeNull();
  });
});

const attempt = (seen: Seen, over: Partial<Attempt> = {}): Attempt => ({
  rowY: 336,
  measured: true,
  offerSeen: false,
  underOffer: false,
  seen,
  looks: 1,
  pressed: pressAfter(seen),
  strays: [],
  ...over,
});
const ctx: FailureContext = { name: 'OpenDash 850x480', filter: 'OpenDash 850x480', running: true };
const firstLine = (s: string) => s.split('\n')[0]!;

describe('what is pressed and what is tried again', () => {
  test('Start is withheld only where a press cannot be right', () => {
    expect(pressAfter({ kind: 'empty' })).toBe(false);
    expect(pressAfter({ kind: 'hovered' })).toBe(true);
    expect(pressAfter({ kind: 'row' })).toBe(true);
    expect(pressAfter({ kind: 'unknown', colour: '#123456' })).toBe(true);
    expect(pressAfter({ kind: 'unread', why: 'exit 1' })).toBe(true);
    expect(pressAfter({ kind: 'page' })).toBe(false);
    expect(pressAfter({ kind: 'moved', now: 'up' })).toBe(false);
  });

  test('a pass is redone when the page moved, or a lit row opened a dashboard from an unfiltered list', () => {
    expect(repeatPass(attempt({ kind: 'moved', now: 'up' }), 'OpenDash 850x480')).toBe(true);
    expect(repeatPass(attempt({ kind: 'moved', now: 'partial' }), 'OpenDash 850x480')).toBe(true);
    expect(repeatPass(attempt({ kind: 'hovered' }, { strays: ['Formula Sport'] }), 'OpenDash 850x480')).toBe(true);
  });

  test('and not otherwise, since then the same pass would do the same thing', () => {
    expect(repeatPass(attempt({ kind: 'hovered' }), 'OpenDash 850x480')).toBe(false);
    expect(repeatPass(attempt({ kind: 'page' }), 'OpenDash 850x480')).toBe(false);
    expect(repeatPass(attempt({ kind: 'empty' }), 'OpenDash 850x480')).toBe(false);
    expect(repeatPass(attempt({ kind: 'unknown', colour: '#123456' }, { strays: ['Formula Sport'] }), 'OpenDash 850x480')).toBe(false);
    // The list was filtered, and the row was the wrong one in it: typing it again changes nothing.
    expect(repeatPass(attempt({ kind: 'hovered' }, { strays: ['OpenDash Pit wall'] }), 'OpenDash')).toBe(false);
  });
});

describe('what a success prints', () => {
  test('the line callers already print, unchanged on an ordinary open', () => {
    expect(openedLine('X', 1, false)).toBe('opened X');
    expect(openedLine('X', 2, false)).toBe('opened X (on attempt 2)');
  });

  test('an open under the offer says so, on the same line', () => {
    const line = openedLine('X', 1, true);
    expect(line).toStartWith('opened X, 61 px lower');
    expect(line).not.toContain('\n');
  });
});

describe('what a failure says first', () => {
  // #303: SimHub exiting under the clicks and the clicks missing both end in a dashboard that did not
  // open, and the advice for one is useless for the other.
  test('a SimHub that is gone is named on the first line, whatever the passes saw', () => {
    const line = firstLine(openFailure({ ...ctx, running: false }, [attempt({ kind: 'hovered' })]));
    expect(line).toContain('OpenDash 850x480');
    expect(line).toContain('SimHub exited');
  });

  test('a SimHub that is gone sends the reader to its log, not to Dash Studio', () => {
    const message = openFailure({ ...ctx, running: false }, [attempt({ kind: 'hovered' })]);
    expect(message).toContain('bun run vm logs');
    expect(message).not.toContain('Open it by hand');
  });

  test('a SimHub still running leaves the clicking as the thing to look at', () => {
    const message = openFailure(ctx, [attempt({ kind: 'hovered' })]);
    expect(message).toContain('Open it by hand');
    expect(message).not.toContain('exited');
  });

  test('a SimHub that could not be asked about is not declared dead', () => {
    const unknown = openFailure({ ...ctx, running: null }, [attempt({ kind: 'hovered' })]);
    expect(firstLine(unknown)).toBe(firstLine(openFailure(ctx, [attempt({ kind: 'hovered' })])));
    expect(unknown).not.toContain('exited');
  });

  test('a lit row that opened a dashboard the filter does not match is the filter, by name', () => {
    const line = firstLine(openFailure(ctx, [attempt({ kind: 'hovered' }, { strays: ['Formula Sport'] })]));
    expect(line).toContain('"Formula Sport"');
    expect(line).toContain('had not narrowed');
  });

  // The face's filter is "OpenDash", which every package's name begins with.
  test('one the filter does match is the wrong row, and the keys are not blamed', () => {
    const face = { ...ctx, name: 'OpenDash', filter: 'OpenDash' };
    const line = firstLine(openFailure(face, [attempt({ kind: 'hovered' }, { strays: ['OpenDash Pit wall'] })]));
    expect(line).toContain('which "OpenDash" also matches');
    expect(line).not.toContain('had not narrowed');
  });

  test('a lit row that opened nothing is the Quick run menu', () => {
    const line = firstLine(openFailure(ctx, [attempt({ kind: 'hovered' }), attempt({ kind: 'page' }, { rowY: 557 })]));
    expect(line).toContain('lit up under the pointer');
    expect(line).toContain('no "OpenDash 850x480" window');
  });

  test('a row that would not light says how long it was given', () => {
    expect(firstLine(openFailure(ctx, [attempt({ kind: 'row' }, { looks: 2 })]))).toContain('had not lit up after 4 s');
  });

  test('a colour nobody measured, and a look that failed, are named as such', () => {
    expect(firstLine(openFailure(ctx, [attempt({ kind: 'unknown', colour: '#123456' })]))).toContain('#123456');
    expect(firstLine(openFailure(ctx, [attempt({ kind: 'unread', why: 'exit 1' })]))).toContain('could not read the screen (exit 1)');
  });

  test('an offer that turned up is the offer and its 61 px, which is the whole of #308', () => {
    const line = firstLine(openFailure(ctx, [attempt({ kind: 'moved', now: 'up' }), attempt({ kind: 'moved', now: 'up' }), attempt({ kind: 'page' }, { rowY: 557 })]));
    expect(line).toContain('track-layout offer');
    expect(line).toContain('61 px');
  });

  test('a list read empty says so, and blames the filter', () => {
    const message = openFailure(ctx, [attempt({ kind: 'empty' }), attempt({ kind: 'empty' })]);
    expect(firstLine(message)).toContain('Dash Studio listed no dashboard row at all');
    expect(firstLine(message)).toContain('"OpenDash 850x480" matched nothing');
    // No y for a pass that found nothing to aim at.
    expect(message).toContain('  attempt 1: Dash Studio listed no dashboard row to hover; nothing pressed');
  });

  test('nothing where an offset was guessed names every place it looked', () => {
    const line = firstLine(openFailure(ctx, [attempt({ kind: 'page' }, { measured: false }), attempt({ kind: 'page' }, { rowY: 557, measured: false })]));
    expect(line).toContain('y 336 or y 557');
    expect(line).toContain('matched nothing');
  });

  test('a guessed row says it was guessed', () => {
    expect(openFailure(ctx, [attempt({ kind: 'hovered' }, { measured: false })])).toContain('at y 336, guessed because the list could not be read');
  });

  test('a window gone before a later pass leads, and keeps the passes before it', () => {
    const message = openFailure({ ...ctx, noWindow: "SimHub's window is 1300x760 at 78,0" }, [attempt({ kind: 'hovered' })]);
    expect(firstLine(message)).toContain('attempt 2 began');
    expect(message).toContain('  attempt 1 at y 336: the row lit up; Start and Windowed pressed, no window');
  });
});

describe('the rest of a failure', () => {
  const passes = [
    attempt({ kind: 'moved', now: 'up' }, { offerSeen: true }),
    attempt({ kind: 'hovered' }, { offerSeen: true, underOffer: true, rowY: 397 }),
    attempt({ kind: 'page' }, { underOffer: true, rowY: 618 }),
  ];
  const message = openFailure({ ...ctx, picture: 'build/vm-open.png' }, passes);
  const lines = message.split('\n');

  test('one line per pass, saying where it worked', () => {
    expect(lines.filter((l) => l.startsWith('  attempt ')).length).toBe(3);
    expect(message).toContain('  attempt 2 at y 397 (under the track-layout offer)');
  });

  test('whether this is #303, both ways and when nobody could tell', () => {
    expect(message).toContain('so this is not #303');
    expect(openFailure({ ...ctx, running: null }, passes)).toContain('could not be read');
  });

  test('why the offer was there at all, only when it was met', () => {
    expect(message).toContain('MapOnlineSuggestionDiscarded');
    expect(openFailure(ctx, [attempt({ kind: 'page' })])).not.toContain('MapOnlineSuggestionDiscarded');
    expect(openFailure({ ...ctx, offerAtEnd: true }, [attempt({ kind: 'page' })])).toContain('MapOnlineSuggestionDiscarded');
  });

  test('an offer at the end is news only when the last pass was not already working under it', () => {
    expect(openFailure({ ...ctx, offerAtEnd: true }, [attempt({ kind: 'page' })])).toContain('may have come back');
    expect(openFailure({ ...ctx, offerAtEnd: true }, [attempt({ kind: 'hovered' }, { underOffer: true })])).not.toContain('may have come back');
  });

  // Line one is all that `bun run shots` and `bun run clips` print.
  test('the picture is named on line one, and the way out is last', () => {
    expect(firstLine(message)).toEndWith('The screen as this gave up is build/vm-open.png.');
    expect(lines.at(-1)).toStartWith('Open it by hand once');
  });
});

// The look column (x 2400) as the guest drew it on 2026-09-29, from the search box down, in the three
// layouts seen that day. `[top, bottom, colour]`, runs under 3 px left out as the host leaves them out.
const run = (top: number, bottom: number, colour: Rgb): Run => ({ top, bottom, colour });
const SEARCH: Rgb = [0x22, 0x22, 0x22];
const LAYOUTS = {
  /** Filtered to one package, nothing above the list. */
  plain: [run(226, 298, PAGE), run(299, 377, ROW), run(378, 1859, PAGE)],
  /** The same, under the offer: 61 px lower. */
  underOffer: [run(287, 359, PAGE), run(360, 438, ROW), run(439, 1859, PAGE)],
  /** A Last used group collapsed above the list, which no offset covered. */
  collapsedLastUsed: [run(226, 362, PAGE), run(363, 441, ROW), run(442, 1859, PAGE)],
  /** Unfiltered with Last used open: its cards are left of the column, and the list's start at 529. */
  openLastUsed: [run(226, 528, PAGE), run(529, 607, ROW), run(608, 611, PAGE), run(612, 690, ROW), run(691, 1859, PAGE)],
};

describe('finding the rows in the list', () => {
  test('one filtered row, wherever the list starts', () => {
    expect(cardsIn(LAYOUTS.plain)).toEqual([[299, 377]]);
    expect(cardsIn(LAYOUTS.underOffer)).toEqual([[360, 438]]);
    expect(cardsIn(LAYOUTS.collapsedLastUsed)).toEqual([[363, 441]]);
  });

  test('rows in list order, with the Last used band left out', () => {
    expect(cardsIn(LAYOUTS.openLastUsed)).toEqual([
      [529, 607],
      [612, 690],
    ]);
  });

  test('a row still lit from the last hover is a row', () => {
    expect(cardsIn([run(299, 377, LIT)])).toEqual([[299, 377]]);
  });

  test('the search box, a thin line and a tall panel are not rows', () => {
    expect(cardsIn([run(201, 224, SEARCH), run(300, 320, ROW), run(400, 700, ROW)])).toEqual([]);
  });

  test('the centre of a measured row is inside the card, where the offsets used to aim', () => {
    const [[top, bottom]] = cardsIn(LAYOUTS.plain) as [[number, number]];
    expect(Math.round((top + bottom) / 2)).toBe(338);
    expect(within(Math.round((top + bottom) / 2), MEASURED.card)).toBe(true);
  });
});

describe("the host's column line", () => {
  test('is read as runs', () => {
    expect(parseColumn('noise\ncolumn [[226,298,[37,37,37]],[299,377,[47,47,47]]]\n')).toEqual([run(226, 298, PAGE), run(299, 377, ROW)]);
  });

  test('an empty column is an empty list, not an unread one', () => {
    expect(parseColumn('column []')).toEqual([]);
  });

  test('is refused when it is not runs of three', () => {
    expect(parseColumn('look {}')).toBeNull();
    expect(parseColumn('column {')).toBeNull();
    expect(parseColumn('column [[1,2]]')).toBeNull();
    expect(parseColumn('column [[1.5,2,[0,0,0]]]')).toBeNull();
    expect(parseColumn('column [[1,2,[0,0,300]]]')).toBeNull();
  });
});

/**
 * #303: the clicks are measured from SimHub's client area rather than from the display. On the rig
 * the maximised client area is the working area, so every click has to land exactly where it did
 * when the coordinates were measured against the screen.
 */
describe("where the clicks land in SimHub's window", () => {
  const maximised = { left: 0, top: 0, width: 3840, height: 2120 };

  test('the client area is read from what the maximise prints', () => {
    expect(parseClientArea('client 0,0 3840x2120 (unclipped -8,-8 3856x2136)')).toEqual(maximised);
  });

  test('a maximise that never arrived has no client area to aim at', () => {
    expect(parseClientArea('SimHub is not running after 120s')).toBeNull();
    expect(
      parseClientArea("SimHub's window is 1300x760 at 78,0, which does not cover the 3840x2120 working area; still starting after 120s"),
    ).toBeNull();
    expect(parseClientArea('')).toBeNull();
  });

  test('a maximised SimHub is clicked exactly where the coordinates were measured', () => {
    expect(aimAt(maximised, 336)).toEqual({
      dashStudio: { x: 100, y: 248 },
      trackLayoutOffer: { x: 2669, y: 182 },
      search: { x: 2004, y: 213 },
      row: { x: 1471, y: 336 },
      windowed: { x: 1520, y: 422 },
    });
  });

  test('a client area away from the origin moves every click with it', () => {
    const moved = aimAt({ ...maximised, left: 60, top: 40 }, 336);
    const home = aimAt(maximised, 336);
    for (const key of Object.keys(home) as (keyof typeof home)[]) {
      expect(moved[key]).toEqual({ x: home[key].x + 60, y: home[key].y + 40 });
    }
  });

  test('under an offer that would not go, the search box is clicked its 61 px lower', () => {
    expect(aimAt(maximised, 336, true).search).toEqual({ x: 2004, y: 274 });
    // The row is wherever it was found; only the search box moves with the offer here.
    expect(aimAt(maximised, 397, true).row).toEqual({ x: 1471, y: 397 });
  });
});

describe('a script run in the desktop', () => {
  const isLaunch = (script: string) => script.includes('New-ScheduledTaskAction');
  /** The scheduled task doing its job: the output, then the marker the host waits for. */
  const runs =
    (output: string) =>
    (script: string, share: string): Answer => {
      if (!isLaunch(script)) return {};
      const tag = /Data\\(opendash_gui_\w+)\.ps1/.exec(script)?.[1];
      writeFileSync(path.join(share, `${tag}.out`), output);
      writeFileSync(path.join(share, `${tag}.done`), 'done');
      return { stdout: 'launched' };
    };
  /** A task that was started and never finished, which is what a hung or dead desktop looks like. */
  const hangs = (script: string): Answer => (isLaunch(script) ? { stdout: 'launched' } : {});

  test('brings back what it printed, and leaves nothing on the share', () => {
    const { r, left } = withFakeHost(runs('SimHub\nOpenDash 8.8 (main)\n'), (host, _calls, share) => ({
      r: inDesktopScript(host, "'hello'", 5),
      left: readdirSync(share),
    }));
    expect(r).toMatchObject({ ok: true, stdout: 'SimHub\nOpenDash 8.8 (main)' });
    expect(left).toEqual([]);
  });

  test('that times out is a failure that says so, not an empty answer', () => {
    const { r, left } = withFakeHost(hangs, (host, _calls, share) => ({ r: inDesktopScript(host, "'hello'", 1), left: readdirSync(share) }));
    expect(r.ok).toBe(false);
    expect(r.stderr).toContain('timed out');
    expect(left).toEqual([]);
  });

  test('that could not be launched leaves no script behind on the share', () => {
    const { r, left } = withFakeHost(
      (script) => (isLaunch(script) ? { status: 1, stderr: 'Register-ScheduledTask : Access is denied.' } : {}),
      (host, _calls, share) => ({ r: inDesktopScript(host, "'hello'", 1), left: readdirSync(share) }),
    );
    expect(r.ok).toBe(false);
    expect(left).toEqual([]);
  });

  test('a list of open dashboards that timed out is no list, rather than none open', () => {
    expect(withFakeHost(hangs, (host) => openDashboards(host, 1))).toBeNull();
    expect(withFakeHost(runs('OpenDash 8.8\n'), (host) => openDashboards(host, 5))).toEqual(['OpenDash 8.8']);
  });
});
