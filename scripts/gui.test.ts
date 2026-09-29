/**
 * What `scripts/gui.ts` decides without a VM: whether the guest's display is in the mode its
 * coordinates were measured in, what a look at Dash Studio reads, and what a failure to open a
 * dashboard says first. The clicking itself is a remote side effect and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import {
  COLOUR_TOLERANCE,
  DASH_STUDIO,
  EXPECTED_SCREEN,
  hex,
  lookPoints,
  openedLine,
  openFailure,
  parseLook,
  pressAfter,
  readLook,
  repeatPass,
  sameColour,
  screenModeProblem,
  TRACK_LAYOUT_OFFER,
  where,
  type Attempt,
  type FailureContext,
  type LookSamples,
  type Rgb,
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
});

// Measured on the guest on 2026-09-29, and the ground the tests below stand on: the offer's band,
// the search box and a filtered row's card, each with and without the offer above them.
const MEASURED = {
  offerBand: [158, 210],
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
  test('without the offer, the clicks are the ones it has always made', () => {
    expect(where(0, 0, false)).toEqual({ searchY: 213, rowY: 336, windowedY: 422 });
    expect(where(221, 0, false).rowY).toBe(557);
  });

  test('under the offer, every click is its 61 px lower', () => {
    expect(TRACK_LAYOUT_OFFER.shift).toBe(61);
    expect(where(0, 0, true)).toEqual({ searchY: 274, rowY: 397, windowedY: 483 });
    expect(where(221, 0, true).rowY).toBe(618);
  });

  test('the search click lands in the box, with and without the offer', () => {
    expect(within(where(0, 0, false).searchY, MEASURED.searchBox)).toBe(true);
    expect(within(where(0, 0, true).searchY, MEASURED.searchBoxUnderOffer)).toBe(true);
  });

  test("the offer's points sit inside its band", () => {
    for (const [, y] of lookPoints(336).offer) expect(within(y, MEASURED.offerBand)).toBe(true);
  });

  test("the row's points sit inside the card, with and without the offer", () => {
    for (const [, y] of lookPoints(where(0, 0, false).rowY).row) expect(within(y, MEASURED.card)).toBe(true);
    for (const [, y] of lookPoints(where(0, 0, true).rowY).row) expect(within(y, MEASURED.cardUnderOffer)).toBe(true);
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
    expect(pressAfter({ kind: 'hovered' })).toBe(true);
    expect(pressAfter({ kind: 'row' })).toBe(true);
    expect(pressAfter({ kind: 'unknown', colour: '#123456' })).toBe(true);
    expect(pressAfter({ kind: 'unread', why: 'exit 1' })).toBe(true);
    expect(pressAfter({ kind: 'page' })).toBe(false);
    expect(pressAfter({ kind: 'moved', now: 'up' })).toBe(false);
  });

  test('a pass is redone when the page moved, or a lit row opened the wrong dashboard', () => {
    expect(repeatPass(attempt({ kind: 'moved', now: 'up' }))).toBe(true);
    expect(repeatPass(attempt({ kind: 'hovered' }, { strays: ['Formula Sport'] }))).toBe(true);
  });

  test('and not otherwise, since then the next offset is the better guess', () => {
    expect(repeatPass(attempt({ kind: 'hovered' }))).toBe(false);
    expect(repeatPass(attempt({ kind: 'page' }))).toBe(false);
    expect(repeatPass(attempt({ kind: 'unknown', colour: '#123456' }, { strays: ['Formula Sport'] }))).toBe(false);
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
  test('SimHub gone is #303, whatever the passes saw', () => {
    const line = firstLine(openFailure({ ...ctx, running: false }, [attempt({ kind: 'hovered' })]));
    expect(line).toContain('SimHub is not running any more');
    expect(line).toContain('#303');
  });

  test('a lit row that opened another dashboard is the filter, by name', () => {
    const line = firstLine(openFailure(ctx, [attempt({ kind: 'hovered' }, { strays: ['Formula Sport'] })]));
    expect(line).toContain('"Formula Sport"');
    expect(line).toContain('had not narrowed');
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

  test('nothing anywhere names every place it looked', () => {
    const line = firstLine(openFailure(ctx, [attempt({ kind: 'page' }), attempt({ kind: 'page' }, { rowY: 557 })]));
    expect(line).toContain('y 336 or y 557');
    expect(line).toContain('matched nothing');
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
    expect(message).toContain('  attempt 2 at y 397 (61 px lower, under the track-layout offer)');
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

  test('the picture, and the way out last', () => {
    expect(message).toContain('build/vm-open.png');
    expect(lines.at(-1)).toStartWith('Open it by hand once');
  });
});
