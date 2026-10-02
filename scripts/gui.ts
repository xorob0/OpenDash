#!/usr/bin/env bun
/**
 * gui: the part of the loop that has to be clicked.
 *
 * SimHub opens a dashboard in a window from its Dash Studio page and offers no other way to do it:
 * no command line, no setting, and `SaveAndRestoreOppenedDashboards` does not bring a windowed dash
 * back after a restart, which was measured rather than assumed. So `bun run dev` drives the mouse,
 * over the same QEMU VNC that takes the screenshots.
 *
 * Coordinates are the fragile part and are treated as such. They are measured from SimHub's client
 * area rather than from the screen: everything anchored to its top-left, which is the left menu and
 * every height, is a fixed pixel offset, and everything across the centred content column is a
 * fraction of its width. Both were measured on the 3840 by 2160 guest, and
 * {@link EXPECTED_SCREEN} is now checked rather than assumed. Nothing else is trusted either:
 * `openDashboard` waits for SimHub's window to exist and to fill the screen before it measures
 * anything from it, looks at the screen before it presses Start, asks Windows which dash windows
 * exist afterwards, retries, and fails by saying what it saw, or that SimHub exited when it has,
 * rather than leaving the caller to wonder.
 */
import { existsSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { onHost, powershell, psq, screenshot, simhubRunning, sleep, type Host, type RunResult } from './vm.ts';

const WINVM_DIR = '/opt/winvm';
const VENV_PYTHON = `${WINVM_DIR}/mcp/.venv/bin/python`;

/** Where the left menu's entries sit, in pixels from the top-left of SimHub's client area. */
const MENU = { x: 100, dashStudio: 248 } as const;
/** Where things in the centred content column sit, as a fraction of SimHub's client area width. */
const CONTENT = { searchX: 0.522, rowX: 0.383 } as const;
/**
 * The first dashboard row, and the step between rows, in pixels of a 100% DPI guest from the top of
 * SimHub's client area.
 *
 * `firstRow` is a point inside the first row's card, not its top: the card spans y=302..380 and 336
 * is the middle of it. `quickRunOffset` is measured from that same point, and it deliberately lands
 * **below** the card, at y=422. Clicking a hovered row does not start the dashboard; it opens the
 * Quick run popup, which is drawn under the card with its header at 390 and "Windowed", the item
 * this wants, as the first entry at 422. Both were re-measured against a screenshot of the real
 * list while #303 was open and are right; anything that looks wrong about a Start click landing
 * forty pixels past the bottom of a 78 pixel card is this popup.
 *
 * `lastUsedBand` is the second first row. Dash Studio draws a "Last used" strip above the list
 * holding the dashboards recently opened, and it appears only when one of them matches what is in
 * the search box, so the list starts 221 px lower in some searches and not others. It went
 * unnoticed for as long as every search was a package's full name, which no other dashboard
 * matches; the face is now called plainly "OpenDash", every other package name begins with it, and
 * the band turned up.
 *
 * These are now the fallback, and not how a row is found. The Last used strip also turned out to
 * collapse, which moves the list 64 px rather than 221, and the track-layout offer adds 61 to
 * either, so `openDashboard` reads where the cards are (`cardsIn`) and aims at the one it wants.
 * Only a list that cannot be read is guessed at from these, the plain offset first and then the
 * band's, which is why the passes are a list of offsets.
 */
const LIST = { firstRow: 336, lastUsedBand: 221, rowHeight: 84, quickRunOffset: 86 } as const;
/**
 * SimHub 9.12.6 offers prebuilt track layouts at the top of the Dash Studio page: a #375963 band at
 * y 158..210, from x 1325 to 2725, which moves the search box and every row `shift` pixels down.
 * "No thanks" is the grey button at x 2627..2712, y 171..197, and (0.695 of the width, 182) lands on
 * it; "Enable it now", the blue one just left of it at 2516..2620, opts the guest into sharing its
 * laps and must never be the one pressed.
 *
 * The click is made blind, since it costs nothing when the offer is not there, and is then checked,
 * because a blind click cannot say whether it worked and the offer has once turned up after it. A
 * look at the band decides: an offer still up is clicked once more, and one that stays after that is
 * worked under, `shift` pixels lower, rather than clicked through (#308).
 *
 * It keeps coming back because "No thanks" is only remembered. SimHub holds the answer in memory as
 * `MapOnlineSuggestionDiscarded` and writes it to `PluginsData\Common\DashStudioSettings_2.json`
 * when it exits cleanly, and `simhubStop` in vm.ts kills it, so every install brings the offer back.
 * The decompiled `GraphicalDashPluginListModel` shows it while that flag and `UseOnlineMaps` are both
 * false; setting the flag back to false while SimHub is stopped is how to see it again on purpose.
 */
export const TRACK_LAYOUT_OFFER = { x: 0.695, y: 182, shift: 61 } as const;
/** How long a row is hovered before it is clicked: its Start button appears on hover, not on click. */
const HOVER_SECONDS = 2;

/** A pixel as the guest's framebuffer holds it. */
export type Rgb = readonly [number, number, number];
/**
 * The colours `openDashboard` reads Dash Studio by, sampled on the guest on 2026-09-29 from SimHub
 * 9.12.6's dark theme at 3840x2160.
 *
 * The capture is VNC's RAW encoding, so it is lossless and a solid fill reads as one exact value; the
 * tolerance absorbs drift and nothing else. The closest two colours here, a row and the page, are ten
 * levels apart in every channel, so no pixel can match two of them, and at 2 the search box's #222222
 * is not taken for the page it sits on.
 */
export const DASH_STUDIO = {
  /** The track-layout offer's band. */
  offer: [0x37, 0x59, 0x63],
  /** A dashboard row's card. */
  row: [0x2f, 0x2f, 0x2f],
  /** The same card under the pointer, which is when its Start button is up. */
  hoveredRow: [0x21, 0x3e, 0x4a],
  /** The page behind everything. */
  page: [0x25, 0x25, 0x25],
} as const satisfies Record<string, Rgb>;
export const COLOUR_TOLERANCE = 2;
/**
 * Where a look samples, in pixels from the top-left of SimHub's client area, like `MENU` and `LIST`.
 *
 * Three points each, so that one pixel under a glyph cannot decide anything. The offer's are on x
 * 1330, a column that crosses only the band and the page; the row's are on x 2400, right of every
 * title and left of the star and MORE, 25 px either side of the row's centre, which keeps all three
 * inside a card 78 px tall. The pointer is drawn into the framebuffer, so every point is more than
 * 100 px from where it rests.
 */
const LOOK = { offerX: 1330, offerYs: [165, 184, 203], rowX: 2400, rowSpread: 25, listTo: 1860 } as const;
/**
 * How tall a dashboard row's card is on the look column, with room either side: 79 px measured, and
 * nothing else on x 2400 between the search box and the licence panel is a card's colour at all.
 */
const CARD_HEIGHT = { min: 60, max: 100 } as const;
/** How many times one call may redo a pass at the same offset, when a look says the offset was right. */
export const MAX_REPEATS = 1;

/**
 * The display mode every coordinate in this file was measured in, and the only one they hold in.
 *
 * `MENU`, `LIST` and `LOOK` are absolute pixels, so at a smaller mode they land on whatever Dash Studio has
 * drawn there instead -- which on 2026-09-27 was empty space twice in a row, reported as nothing but
 * "could not be opened". The guest had come back at 1280x800 after a container restart, which is the
 * way this happens: the mode is set on the interactive session and a restart does not keep it.
 *
 * Checked rather than adapted to. A fraction of the height would be a guess at a page whose rows,
 * offer band and search box have never been measured anywhere else, and a guess that opens the wrong
 * dashboard is worse than a refusal that says what to do.
 */
export const EXPECTED_SCREEN = { width: 3840, height: 2160 } as const;

/** Where the mode is set from, which is a script on the share rather than a setting. */
const SETRES = '/opt/winvm/shared/setres.ps1';

/**
 * Why the guest's display cannot be clicked in, or null when it can.
 *
 * Separate from reading the size so that it can be tested without a VM, and shared by
 * `guiProblem` and `openDashboard` so that the two cannot come to disagree about which mode is the
 * one that works.
 */
export function screenModeProblem(size: { width: number; height: number }): string | null {
  if (size.width === EXPECTED_SCREEN.width && size.height === EXPECTED_SCREEN.height) return null;
  return (
    `the guest's display is ${size.width}x${size.height}; Dash Studio can only be clicked at ${EXPECTED_SCREEN.width}x${EXPECTED_SCREEN.height}.\n` +
    `The menu and list coordinates in scripts/gui.ts are absolute pixels measured in that mode, so at any ` +
    `other one they land in empty space and the only symptom is "could not be opened". Nothing was clicked.\n` +
    `Put the guest back and run this again: ${SETRES}, in the interactive session rather than over SSH ` +
    `(\`powershell -File Z:\\setres.ps1 -Width ${EXPECTED_SCREEN.width} -Height ${EXPECTED_SCREEN.height}\`). ` +
    `It prints the mode before and after and every mode the driver offers. A container restart undoes it, ` +
    `so it is worth reading this message as "the VM restarted" rather than as a broken script.`
  );
}

// --------------------------------------------------------------------------- looking at Dash Studio
//
// What `openDashboard` reads off the screen, and what it makes of it, without a VM. The reading is
// done in one VNC session on the host and comes back as a `look` line of nine pixels; everything
// from there on is here, where it can be tested.

export const hex = (c: Rgb): string => `#${c.map((v) => v.toString(16).padStart(2, '0')).join('')}`;

/** True when every channel is within `tolerance` of the other colour's. */
export const sameColour = (a: Rgb, b: Rgb, tolerance = COLOUR_TOLERANCE): boolean => a.every((v, i) => Math.abs(v - b[i]!) <= tolerance);

/**
 * Where a pass guesses its row is, in SimHub's client area, when the list cannot be read:
 * `bandOffset` lower for the Last used band, and `TRACK_LAYOUT_OFFER.shift` lower again when the pass
 * is working under the offer.
 */
export function guessRow(bandOffset: number, index: number, underOffer: boolean): number {
  return LIST.firstRow + bandOffset + (underOffer ? TRACK_LAYOUT_OFFER.shift : 0) + index * LIST.rowHeight;
}

/** The three points a look reads on the offer's band, and the three it reads on the row at `rowY`. */
export function lookPoints(rowY: number): { offer: [number, number][]; row: [number, number][] } {
  return {
    offer: LOOK.offerYs.map((y): [number, number] => [LOOK.offerX, y]),
    row: [-LOOK.rowSpread, 0, LOOK.rowSpread].map((d): [number, number] => [LOOK.rowX, rowY + d]),
  };
}

/** What a look read, in the order `lookPoints` gives. */
export interface LookSamples {
  offer: Rgb[];
  row: Rgb[];
}

const isRgb = (v: unknown): v is Rgb => Array.isArray(v) && v.length === 3 && v.every((c) => Number.isInteger(c) && c >= 0 && c <= 255);
const isTriple = (v: unknown): v is Rgb[] => Array.isArray(v) && v.length === 3 && v.every(isRgb);

/**
 * The `look` line out of the host's output, or null when there is none or it is not nine pixels.
 * Anything else the run printed is ignored, since vncdotool and Python both have things to say.
 */
export function parseLook(stdout: string): LookSamples | null {
  const line = stdout
    .split('\n')
    .map((l) => l.trim())
    .find((l) => l.startsWith('look '));
  if (!line) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(line.slice('look '.length));
  } catch {
    return null;
  }
  if (typeof parsed !== 'object' || parsed === null) return null;
  const { offer, row } = parsed as { offer?: unknown; row?: unknown };
  return isTriple(offer) && isTriple(row) ? { offer, row } : null;
}

/** A stretch of one colour down the look column, first and last y inclusive. */
export interface Run {
  top: number;
  bottom: number;
  colour: Rgb;
}

/** The `column` line out of the host's output, or null when there is none or it is not a list of runs. */
export function parseColumn(stdout: string): Run[] | null {
  const line = stdout
    .split('\n')
    .map((l) => l.trim())
    .find((l) => l.startsWith('column '));
  if (!line) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(line.slice('column '.length));
  } catch {
    return null;
  }
  if (!Array.isArray(parsed)) return null;
  const runs: Run[] = [];
  for (const r of parsed) {
    if (!Array.isArray(r) || r.length !== 3 || !Number.isInteger(r[0]) || !Number.isInteger(r[1]) || !isRgb(r[2])) return null;
    runs.push({ top: r[0], bottom: r[1], colour: r[2] });
  }
  return runs;
}

/**
 * The dashboard rows Dash Studio is listing, as the top and bottom of each card down the look
 * column, in the order they are listed.
 *
 * Measured rather than assumed because the list does not start in one place. The track-layout
 * offer moves it 61 px, the Last used band 221 px when it is open and 64 px when it has been
 * collapsed, and the two stack; a fixed offset per case was two guesses out of four and silently
 * wrong for the rest. A lit card counts: the pointer may still be resting on one from before.
 */
export function cardsIn(runs: readonly Run[]): [number, number][] {
  return runs
    .filter((r) => sameColour(r.colour, DASH_STUDIO.row) || sameColour(r.colour, DASH_STUDIO.hoveredRow))
    .filter((r) => r.bottom - r.top + 1 >= CARD_HEIGHT.min && r.bottom - r.top + 1 <= CARD_HEIGHT.max)
    .map((r): [number, number] => [r.top, r.bottom]);
}

/** Whether the offer's band is on screen: all three points, none, or some, which is the band moving. */
export type OfferState = 'up' | 'down' | 'partial';
export function offerState(offer: readonly Rgb[]): OfferState {
  const on = offer.filter((c) => sameColour(c, DASH_STUDIO.offer)).length;
  return on === offer.length ? 'up' : on === 0 ? 'down' : 'partial';
}

/**
 * What was under the pointer when a pass was about to press Start.
 *
 * `hovered` is the row lit, which is the Start button up; `row` is a row that had not lit; `page` is
 * nothing there at all; `moved` is the offer come or gone since the pass decided where to click, so
 * the rows are not where the click would land; `unknown` is a colour none of the above; `unread` is
 * a look that could not be taken. `empty` is decided before any of those: the list was read and
 * held no row to hover.
 */
export type Seen =
  | { kind: 'empty' }
  | { kind: 'hovered' }
  | { kind: 'row' }
  | { kind: 'page' }
  | { kind: 'moved'; now: OfferState }
  | { kind: 'unknown'; colour: string }
  | { kind: 'unread'; why: string };

/**
 * Reads a look taken with the pointer on the row, by a pass that expects the offer `expected`.
 *
 * The offer comes first because it decides where the row is: a band that is not in the state the
 * pass assumed means the row this is looking at is the wrong one, whatever colour it is. Then two
 * of the three row points have to agree, so that one glyph cannot decide.
 */
export function readLook(s: LookSamples, expected: 'up' | 'down'): Seen {
  const now = offerState(s.offer);
  if (now !== expected) return { kind: 'moved', now };
  const most = (c: Rgb) => s.row.filter((p) => sameColour(p, c)).length >= 2;
  if (most(DASH_STUDIO.hoveredRow)) return { kind: 'hovered' };
  if (most(DASH_STUDIO.row)) return { kind: 'row' };
  if (most(DASH_STUDIO.page)) return { kind: 'page' };
  return { kind: 'unknown', colour: hex(s.row[1]!) };
}

/**
 * Whether a pass presses Start after what it saw.
 *
 * Withheld only where a press cannot be right: nothing under the pointer, or rows that have moved
 * away from it. Everything else is pressed as it was before the look existed -- an unlit row after a
 * second dwell, a colour nobody measured, a look that failed -- so that a look which is wrong about
 * something costs a message and never a dashboard that would have opened.
 */
export const pressAfter = (seen: Seen): boolean => seen.kind !== 'empty' && seen.kind !== 'page' && seen.kind !== 'moved';

/** One pass of `openDashboard`, as its failure message reports it. */
export interface Attempt {
  rowY: number;
  /** `rowY` is a card found in the list; false when the list could not be read and an offset was guessed. */
  measured: boolean;
  /** The look after the first "No thanks" still found the offer, whole or in part. */
  offerSeen: boolean;
  /** The offer was still up after the second, so this pass worked `TRACK_LAYOUT_OFFER.shift` lower. */
  underOffer: boolean;
  seen: Seen;
  /** 2 when an unlit row was given a second dwell. */
  looks: number;
  pressed: boolean;
  /** Dashboards the pass opened that were not the one asked for, closed again. */
  strays: string[];
}

/**
 * Whether a dashboard that opened instead of the one asked for came from a list the filter never
 * narrowed. One whose name the filter matches was listed because of it, so the keys arrived and the
 * row was the wrong one; only one the filter does not match says the keys did not.
 */
export const unfiltered = (strays: readonly string[], filter: string): boolean =>
  strays.some((n) => !n.toLowerCase().includes(filter.toLowerCase()));

/**
 * Whether the next pass should be this pass again rather than the next one: when the page moved
 * under it, or when a lit row opened a dashboard the filter does not match, which says the list was
 * never filtered and typing it again is what might help.
 */
export const repeatPass = (a: Attempt, filter: string): boolean =>
  a.seen.kind === 'moved' || (a.pressed && a.seen.kind === 'hovered' && unfiltered(a.strays, filter));

/** What a success prints. One line, because `bun run dev` indents it under its step. */
export const openedLine = (name: string, attempt: number, underOffer: boolean): string =>
  `opened ${name}${attempt > 1 ? ` (on attempt ${attempt})` : ''}` +
  (underOffer ? `, ${TRACK_LAYOUT_OFFER.shift} px lower under SimHub's track-layout offer, which two "No thanks" clicks did not dismiss` : '');

/** A pass's reading in a few words, for its line in a failure and for `OPENDASH_GUI_DEBUG`. */
export function describeSeen(seen: Seen, looks = 1): string {
  switch (seen.kind) {
    case 'empty':
      return 'Dash Studio listed no dashboard row to hover';
    case 'hovered':
      return 'the row lit up';
    case 'row':
      return `a row, not lit after ${looks * HOVER_SECONDS} s`;
    case 'page':
      return 'only the page background';
    case 'moved':
      return seen.now === 'up' ? 'the track-layout offer had appeared' : seen.now === 'down' ? 'the track-layout offer had gone' : 'the track-layout offer was moving';
    case 'unknown':
      return `a colour it does not know, ${seen.colour}`;
    case 'unread':
      return `the screen could not be read (${seen.why})`;
  }
}

/** What `openDashboard` knew when it gave up, besides its passes. */
export interface FailureContext {
  name: string;
  filter: string;
  /** `simhubRunning` after the last pass: false is #303, null is a guest that did not answer. */
  running: boolean | null;
  /** Why a later pass never started, when SimHub's window was not there for it. */
  noWindow?: string;
  /** The offer was on screen when this gave up, after the last look. */
  offerAtEnd?: boolean;
  /** Where the screen as it gave up was saved, relative to the working directory. */
  picture?: string;
}

const quoted = (s: string) => `"${s}"`;

/** The pass a failure is explained by: the last one that pressed, else the last that saw something. */
function leadAttempt(attempts: readonly Attempt[]): Attempt | null {
  return attempts.filter((a) => a.pressed).at(-1) ?? attempts.filter((a) => a.seen.kind !== 'page' && a.seen.kind !== 'empty').at(-1) ?? null;
}

/** Line one of a failure, which is the only line `bun run shots` and `bun run clips` keep. */
function failureLead(ctx: FailureContext, attempts: readonly Attempt[]): string {
  if (ctx.running === false)
    return `SimHub exited while it was being clicked, so the coordinates are not what to look at; \`bun run vm logs 80\` shows how it ended (#303).`;
  if (ctx.noWindow) return `SimHub's window was not there when attempt ${attempts.length + 1} began (${ctx.noWindow}), so nothing more was clicked.`;
  const lead = leadAttempt(attempts);
  if (!lead) {
    const guessed = attempts.filter((a) => a.seen.kind === 'page').map((a) => `y ${a.rowY}`);
    const where =
      guessed.length === 0
        ? 'Dash Studio listed no dashboard row at all below the search box'
        : `there was no dashboard row under the pointer at ${[...new Set(guessed)].join(' or ')}, only the page background`;
    return (
      `${where}, so nothing was pressed: ${quoted(ctx.filter)} matched nothing in the list. ` +
      `Either it is not installed (SimHub reads the list only at startup, and \`bun run vm install\` restarts it), or the search box kept an earlier run's text and the name was typed after it.`
    );
  }
  const at = `y ${lead.rowY}`;
  const seen = lead.seen;
  if (seen.kind === 'moved') {
    if (seen.now === 'up')
      return `SimHub's track-layout offer appeared after it had been checked for, which moves the search box and every row ${TRACK_LAYOUT_OFFER.shift} px down, so the row at ${at} was not pressed.`;
    if (seen.now === 'down') return `SimHub's track-layout offer went away while this was working ${TRACK_LAYOUT_OFFER.shift} px below it, so the rows moved back up and the row at ${at} was not pressed.`;
    return `SimHub's track-layout offer was moving (only part of its band was on screen), so where the rows were could not be known and the row at ${at} was not pressed.`;
  }
  if (lead.strays.length > 0)
    return unfiltered(lead.strays, ctx.filter)
      ? `the lit row at ${at} opened ${lead.strays.map(quoted).join(', ')} instead, so typing ${quoted(ctx.filter)} had not narrowed Dash Studio's list to it (the keys did not all reach the search box); it was closed again.`
      : `the lit row at ${at} opened ${lead.strays.map(quoted).join(', ')} instead, which ${quoted(ctx.filter)} also matches, so the list was filtered and the row was the wrong one: pass a filter only ${quoted(ctx.name)} matches, or its index in the list. It was closed again.`;
  switch (seen.kind) {
    case 'hovered':
      return `the row at ${at} lit up under the pointer and Start then Windowed were pressed, but no ${quoted(ctx.name)} window appeared within 14 s: the Windowed entry of the Quick run menu was missed.`;
    case 'row':
      return `a dashboard row was under the pointer at ${at} but had not lit up after ${lead.looks * HOVER_SECONDS} s, so SimHub was not taking the pointer (a window in front of it, or SimHub too busy to draw); Start was pressed anyway, as before, and nothing opened.`;
    case 'unknown':
      return `the look before Start read ${seen.colour} at (${LOOK.rowX}, ${lead.rowY}), which is none of the colours scripts/gui.ts knows (a row ${hex(DASH_STUDIO.row)}, lit ${hex(DASH_STUDIO.hoveredRow)}, the page ${hex(DASH_STUDIO.page)}), so Start was pressed unchecked, as before, and nothing opened.`;
    case 'unread':
      return `the look before Start could not read the screen (${seen.why}), so Start was pressed unchecked, as before, and nothing opened.`;
    case 'page':
    case 'empty':
      // leadAttempt never returns an unpressed page or empty reading, and neither is ever pressed.
      return `there was no dashboard row under the pointer at ${at}.`;
  }
}

/** Why the offer was there at all, for a failure that met it. */
export const OFFER_COMES_BACK =
  'It comes back after every SimHub restart that ends in a kill, which is how `bun run vm` restarts it: "No thanks" is kept in memory and saved only when SimHub exits cleanly ' +
  '(MapOnlineSuggestionDiscarded in PluginsData\\Common\\DashStudioSettings_2.json).';

/**
 * The whole message a failed `openDashboard` returns. Line one says what was seen and where the
 * picture is, because it is the only line two of the callers print; the lines after it are the passes
 * one by one, whether this is #303, and the offer if it was met.
 *
 * A SimHub that exited leads everything else. The default advice is about the clicking, and on
 * 2026-09-13 it cost an hour: SimHub had exited under the clicks, so no coordinate was ever going to
 * help, and the message sends the reader to SimHub's log instead of to Dash Studio.
 */
export function openFailure(ctx: FailureContext, attempts: readonly Attempt[]): string {
  const picture = ctx.picture ? ` The screen as this gave up is ${ctx.picture}.` : '';
  const lines = [`could not open ${ctx.name}: ${failureLead(ctx, attempts)}${picture}`];
  attempts.forEach((a, i) => {
    const outcome = !a.pressed ? 'nothing pressed' : a.strays.length > 0 ? `opened ${a.strays.map(quoted).join(', ')} instead, closed` : 'Start and Windowed pressed, no window';
    const where = a.seen.kind === 'empty' ? '' : ` at y ${a.rowY}${a.measured ? '' : ', guessed because the list could not be read'}`;
    const under = a.underOffer ? ` (under the track-layout offer)` : '';
    lines.push(`  attempt ${i + 1}${where}${under}: ${describeSeen(a.seen, a.looks)}; ${outcome}`);
  });
  if (ctx.noWindow) lines.push(`  attempt ${attempts.length + 1} did not start: ${ctx.noWindow}`);
  if (ctx.running === false)
    lines.push(
      'When this happened on 2026-09-13 the log held WatchDog "Abnormal Inactivity" dumps and no exception, which is a guest too short of CPU or memory to keep SimHub up rather than a fault in any package.',
    );
  if (ctx.running === true) lines.push('SimHub was still running after the last attempt, so this is not #303.');
  if (ctx.running === null) lines.push('Whether SimHub is still running could not be read: the guest did not answer.');
  const stillUp = attempts.flatMap((a, i) => (a.offerSeen ? [i + 1] : []));
  if (stillUp.length > 0) lines.push(`SimHub's track-layout offer was still up after the first "No thanks" in attempt ${stillUp.join(' and ')}.`);
  // An offer the last pass was already working under is no news.
  const offerCameBack = ctx.offerAtEnd === true && attempts.at(-1)?.underOffer !== true;
  if (offerCameBack)
    lines.push(`SimHub's track-layout offer was up when this gave up, so it may have come back after the last look and moved everything ${TRACK_LAYOUT_OFFER.shift} px down.`);
  if (stillUp.length > 0 || offerCameBack) lines.push(OFFER_COMES_BACK);
  lines.push(
    ctx.running === false
      ? 'Running this again restarts it.'
      : `Open it by hand once (Dash Studio, find ${ctx.name}, Start, Windowed) and run this again; everything else will be in place.`,
  );
  return lines.join('\n');
}

/** Runs a snippet against the guest's VNC through the Python environment the host already has. */
function vnc(host: Host, body: string, timeoutMs = 90_000): RunResult {
  return onHost(
    host,
    `${VENV_PYTHON} - <<'PY'
import logging, time
from vncdotool import api
logging.getLogger('vncdotool').setLevel(logging.ERROR)
client = api.connect('127.0.0.1::5900', password=None, timeout=15)
try:
${body
  .split('\n')
  .map((line) => `    ${line}`)
  .join('\n')}
finally:
    client.disconnect()
PY`,
    timeoutMs,
  );
}

/**
 * Moves the pointer and presses. `dwellSeconds` keeps the pointer there first, for the controls
 * that reveal themselves on hover -- a dashboard row's Start button, a menu item's highlight -- and
 * it has to happen inside this one VNC session: a move in one connection and a press in the next
 * arrives with the hover already forgotten.
 */
export const click = (host: Host, x: number, y: number, dwellSeconds = 0): RunResult =>
  vnc(host, `client.mouseMove(${Math.round(x)}, ${Math.round(y)})\ntime.sleep(${dwellSeconds})\nclient.mousePress(1)`);

/**
 * Moves the pointer without pressing. A pointer left resting on a control opens its tooltip, and a
 * WPF tooltip is a top-level window that `Process.MainWindowHandle` will hand back in place of the
 * window itself, so a caller that clicks and then measures parks the pointer somewhere inert first.
 */
export const movePointer = (host: Host, x: number, y: number): RunResult =>
  vnc(host, `client.mouseMove(${Math.round(x)}, ${Math.round(y)})`);

/**
 * Types a string a key at a time. vncdotool's proxy has no `type`, and its key names are words for
 * anything that is not a bare character.
 */
export function type(host: Host, text: string): RunResult {
  const special: Record<string, string> = { ' ': 'space', '-': 'minus', '.': 'period', '_': 'underscore', '/': 'fslash' };
  const keys = [...text].map((c) => special[c] ?? c);
  return vnc(host, keys.map((k) => `client.keyPress(${JSON.stringify(k)})\ntime.sleep(0.02)`).join('\n'));
}

export const press = (host: Host, ...keys: string[]): RunResult =>
  vnc(host, keys.map((k) => `client.keyPress(${JSON.stringify(k)})\ntime.sleep(0.05)`).join('\n'));

/** The guest's display size, for the mode check; the clicks are measured from SimHub's window. */
export function screenSize(host: Host): { width: number; height: number } | null {
  const r = onHost(
    host,
    `${VENV_PYTHON} - <<'PY'
import tempfile, os
from vncdotool import api
from PIL import Image
client = api.connect('127.0.0.1::5900', password=None, timeout=15)
try:
    tmp = tempfile.NamedTemporaryFile(suffix='.png', delete=False).name
    client.captureScreen(tmp)
finally:
    client.disconnect()
im = Image.open(tmp); print(im.width, im.height); os.unlink(tmp)
PY`,
    90_000,
  );
  const [w, h] = r.stdout.trim().split(/\s+/).map(Number);
  return Number.isFinite(w) && Number.isFinite(h) && w && h ? { width: w, height: h } : null;
}

let desktopCall = 0;

/**
 * Runs PowerShell in the interactive desktop and brings its output back. It has to be the desktop:
 * a session 0 process enumerates session 0's windows, which are none of these.
 */
export function inDesktopScript(host: Host, script: string, timeoutSeconds = 120): RunResult {
  // A fresh name per call. Sharing one meant a second call could read the first call's output file
  // before its own task had written anything, which is how `openDashboards` once reported that the
  // open dashboard was called "maximised".
  const tag = `opendash_gui_${process.pid}_${++desktopCall}`;
  const share = `${WINVM_DIR}/shared/${tag}.ps1`;
  const out = `${WINVM_DIR}/shared/${tag}.out`;
  const done = `${WINVM_DIR}/shared/${tag}.done`;
  const written = onHost(host, `cat > ${share} <<'SCRIPT'\n${script}\nSCRIPT\nrm -f ${out} ${done}`);
  if (!written.ok) return written;
  const launched = powershell(
    host,
    `$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ${psq(
      // The marker is written after the output file is closed, and it is the marker the host waits
      // for. Waiting for the output file itself does not work: Out-File creates it empty and fills
      // it afterwards, so a wait on existence returns nothing at all.
      `-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "& '\\\\host.lan\\Data\\${tag}.ps1' *>&1 | Out-File -Encoding utf8 '\\\\host.lan\\Data\\${tag}.out'; 'done' | Out-File -Encoding utf8 '\\\\host.lan\\Data\\${tag}.done'"`,
    )}
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances Parallel
Register-ScheduledTask -TaskName '${tag}' -Action $action -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName '${tag}'
'launched'`,
    90,
  );
  if (!launched.ok) return launched;
  // The wait happens on the host, in the same command as the read. Polling from here cost one
  // SSH round trip per second, which made a handful of these calls slower than the clicking they
  // were there to verify.
  // OPENDASH_GUI_DEBUG keeps the script and its output on the share, and names them, which is the
  // only way to see why a step that runs on the far side of a scheduled task produced nothing.
  const keep = Boolean(process.env.OPENDASH_GUI_DEBUG);
  const read = onHost(
    host,
    `for i in $(seq 1 ${Math.max(1, Math.trunc(timeoutSeconds))}); do [ -f ${done} ] && break; sleep 1; done
cat ${out} 2>/dev/null | tr -d '\\000'
${keep ? `echo "[debug] kept ${share}, ${out} and ${done}" >&2` : `rm -f ${share} ${out} ${done}`}`,
    (timeoutSeconds + 30) * 1000,
  );
  if (!keep) powershell(host, `Unregister-ScheduledTask -TaskName '${tag}' -Confirm:$false -ErrorAction SilentlyContinue`, 60);
  return read;
}

/**
 * Holds a key down, screenshots the whole display while it is held, and releases it.
 *
 * All of it in one VNC session, which is the point. **A VNC server releases every held key when the
 * client disconnects**, so holding a key in one call and photographing in the next photographs a
 * key that is no longer down -- which is exactly what made the quick glance look broken when it was
 * working. The capture is the display rather than one window, because `captureDashboard` schedules
 * a task on the guest and could not run inside this session anyway.
 *
 * SimHub's keyboard reader uses RawInput, so this reaches it the way a wheel button would.
 */
export function captureWhileHeld(host: Host, key: string, localPath: string, seconds = 2.5): RunResult {
  const guestPng = `${WINVM_DIR}/shared/held_${Math.round(Date.now())}.png`;
  const held = vnc(
    host,
    `client.keyDown(${JSON.stringify(key)})
time.sleep(${seconds})
client.captureScreen(${JSON.stringify(guestPng)})
time.sleep(0.5)
client.keyUp(${JSON.stringify(key)})`,
    Math.round((seconds + 60) * 1000),
  );
  if (!held.ok) return held;
  const fetched = fetchFromShare(host, guestPng, localPath);
  return fetched.ok ? { ...fetched, stdout: `captured the display while ${key} was held` } : fetched;
}

/** A window enumerator, shared by the calls below so the P/Invoke block is written once. */
export const WINDOW_HELPER = `
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public class OpenDashWindows {
  public delegate bool Enum(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Enum cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  /// <summary>True when the window is in the maximised state, whatever shape that left it.</summary>
  [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  /// <summary>WM_CLOSE, which a WPF window handles as a click on its close box.</summary>
  public static void Close(IntPtr h) { PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero); }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public static int[] Rect(IntPtr h) { RECT r; GetWindowRect(h, out r); return new int[] { r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top }; }
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  public static string Title(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
  public static string Cls(IntPtr h) { var sb = new StringBuilder(512); GetClassName(h, sb, 512); return sb.ToString(); }
  public static List<IntPtr> Visible() {
    var list = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr l) { if (IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
    return list;
  }
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  /// <summary>The client area in screen coordinates, as left, top, width, height.</summary>
  public static int[] Client(IntPtr h) { RECT c; GetClientRect(h, out c); var p = new POINT(); ClientToScreen(h, ref p); return new int[] { p.X, p.Y, c.Right, c.Bottom }; }
  public static void Move(IntPtr h, int x, int y) { SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); }
  /// <summary>Places a window so that its CLIENT area is exactly cx by cy at (x, y).</summary>
  public static void Fit(IntPtr h, int x, int y, int cx, int cy) {
    RECT w, c;
    GetWindowRect(h, out w); GetClientRect(h, out c);
    int chromeX = (w.Right - w.Left) - (c.Right - c.Left);
    int chromeY = (w.Bottom - w.Top) - (c.Bottom - c.Top);
    SetWindowPos(h, IntPtr.Zero, x, y, cx + chromeX, cy + chromeY, 0x0004 | 0x0010);
  }
}
'@
`;

/** The dashboards SimHub currently has open in a window, by name. */
export function openDashboards(host: Host): string[] {
  const r = inDesktopScript(
    host,
    `${WINDOW_HELPER}
foreach ($h in [OpenDashWindows]::Visible()) {
  $t = [OpenDashWindows]::Title($h)
  if ($t -match ' \\(WPF Renderer\\)$') { $t -replace ' \\(WPF Renderer\\)$', '' }
}`,
  );
  return r.stdout
    .split('\n')
    .map((l) => l.replace(/^﻿/, '').trim())
    .filter((l) => l.length > 0);
}

/**
 * Closes every open dash window, and returns the names it closed.
 *
 * `bun run shots` walks ten packages on a two-core VM, and a dash window left open keeps rendering
 * at sixty frames a second. Ten of them open at once is not a tidiness problem, it is why the
 * eighth capture comes back half-drawn. WM_CLOSE rather than a killed process, because SimHub owns
 * these windows and would notice.
 */
export function closeDashboards(host: Host): string[] {
  const r = inDesktopScript(
    host,
    `${WINDOW_HELPER}
foreach ($h in [OpenDashWindows]::Visible()) {
  $t = [OpenDashWindows]::Title($h)
  if ($t -notmatch ' \\(WPF Renderer\\)$') { continue }
  [OpenDashWindows]::Close($h)
  $t -replace ' \\(WPF Renderer\\)$', ''
}
Start-Sleep -Milliseconds 900`,
  );
  return r.stdout
    .split('\n')
    .map((l) => l.replace(/^﻿/, '').trim())
    .filter((l) => l.length > 0);
}

/**
 * Waits for SimHub's main window and maximises it. Once it is there, prints `client x,y WxH`: the
 * part of its client area that is on the screen, which is what every click is measured from,
 * followed by the unclipped area in brackets. Otherwise it prints a line saying what it waited for
 * and never got, which the caller is expected to read.
 *
 * **The waiting is the point of this function, and the lack of it was a bug.** `vm.ts`'s
 * `simhubStart` returns as soon as the process exists, which on this guest is about a second after
 * launch; SimHub's real window arrives twenty-five to fifty seconds later. What sits in
 * `MainWindowHandle` in between is not nothing, which is the trap. Measured on the VM, from the
 * moment the process appears:
 *
 * * for three seconds the handle is zero;
 * * for the next twenty it is the **splash**: a 540x320 window that reports `IsZoomed` as true and
 *   that `SW_MAXIMIZE` stretches to 3840x320, because only its width is free;
 * * then the real window, 1300x760 at 78,0, which maximises to -8,-8 by 3856x2136.
 *
 * So a caller that maximised once and carried on was clicking full-screen coordinates at a 320
 * pixel strip, and a moment later at an unmaximised 1300x760 window with the desktop around it.
 * That is what made `bun run shots` fail on whichever package it photographed first after the
 * install's SimHub restart -- reported, misleadingly, as a package that could not be found.
 *
 * Each round therefore asks for `SW_MAXIMIZE` and then measures, and only a rectangle covering the
 * **working area** counts as arrived. Working area rather than `Bounds`: a maximised window here is
 * -8,-8 by 3856x2136, which is the working area plus the invisible resize border and forty pixels
 * short of `Bounds`, because the taskbar owns them. A test against `Bounds` can never pass, so the
 * old one fired `SetWindowPos` at every already-correct window and could not have told an arrived
 * window from a splash.
 *
 * `SetWindowPos` stays as the fallback for the case it was written for -- `SW_MAXIMIZE` not taking
 * on a window WPF has not finished laying out -- but only when the window is not zoomed afterwards.
 * The splash is zoomed and still the wrong shape, and forcing that one to the working area would
 * make it pass the test and hand the caller a splash to click on.
 *
 * The client area is clipped to the working area before it is reported. A maximised window hangs
 * its resize border off every edge of the working area, and a WPF window that draws its own chrome
 * can hang its client area off with it; clipped, a maximised SimHub reports the working area either
 * way, which is where every coordinate in this file was measured from.
 */
export function maximiseSimHub(host: Host, waitSeconds = 120): RunResult {
  return inDesktopScript(
    host,
    `${WINDOW_HELPER}
Add-Type -AssemblyName System.Windows.Forms
$work = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
function Covers($r) {
  return ($r[0] -le $work.X -and $r[1] -le $work.Y -and
          ($r[0] + $r[2]) -ge ($work.X + $work.Width) -and ($r[1] + $r[3]) -ge ($work.Y + $work.Height))
}
$deadline = (Get-Date).AddSeconds(${Math.trunc(waitSeconds)})
$last = 'SimHub has no main window yet'
while ($true) {
  # The process's own MainWindowHandle, not a title match. SimHub has three visible windows called
  # "SimHub", one of which is a full-screen overlay, and matching on the title maximised that one
  # while every click went to the real window still sitting at 78,0.
  $proc = Get-Process SimHubWPF -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $proc) {
    $last = 'SimHub is not running'
  } elseif ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
    $found = $proc.MainWindowHandle
    # Anything else on the desktop takes the clicks meant for SimHub: an Explorer window left open on
    # the share was doing exactly that. Only the two kinds a developer leaves lying about are
    # minimised, chosen by window class rather than by title, so no shell window is touched.
    foreach ($h in [OpenDashWindows]::Visible()) {
      if ($h -eq $found) { continue }
      $c = [OpenDashWindows]::Cls($h)
      if ($c -eq 'CabinetWClass' -or $c -eq 'ExploreWClass' -or $c -eq 'ConsoleWindowClass') {
        [OpenDashWindows]::ShowWindow($h, 6) | Out-Null   # SW_MINIMIZE
      }
    }
    [OpenDashWindows]::ShowWindow($found, 3) | Out-Null    # SW_MAXIMIZE
    [OpenDashWindows]::SetForegroundWindow($found) | Out-Null
    Start-Sleep -Milliseconds 700
    $r = [OpenDashWindows]::Rect($found)
    if (-not (Covers $r) -and -not [OpenDashWindows]::IsZoomed($found)) {
      # SW_MAXIMIZE did not take. SWP_NOZORDER | SWP_NOACTIVATE.
      [OpenDashWindows]::SetWindowPos($found, [IntPtr]::Zero, $work.X, $work.Y, $work.Width, $work.Height, 0x0004 -bor 0x0010) | Out-Null
      Start-Sleep -Milliseconds 700
      $r = [OpenDashWindows]::Rect($found)
    }
    if (Covers $r) {
      $client = [OpenDashWindows]::Client($found)
      $left = [Math]::Max($client[0], $work.X)
      $top = [Math]::Max($client[1], $work.Y)
      $right = [Math]::Min($client[0] + $client[2], $work.X + $work.Width)
      $bottom = [Math]::Min($client[1] + $client[3], $work.Y + $work.Height)
      "client {0},{1} {2}x{3} (unclipped {4},{5} {6}x{7})" -f $left, $top, ($right - $left), ($bottom - $top), $client[0], $client[1], $client[2], $client[3]
      exit
    }
    $last = "SimHub's window is {0}x{1} at {2},{3}, which does not cover the {4}x{5} working area; still starting" -f $r[2], $r[3], $r[0], $r[1], $work.Width, $work.Height
  }
  if ((Get-Date) -ge $deadline) { break }
  Start-Sleep -Seconds 2
}
"$last after ${Math.trunc(waitSeconds)}s"`,
    // The script does its own waiting, so the host has to outlast it or it reads an empty file and
    // calls a slow start a failure.
    Math.trunc(waitSeconds) + 60,
  );
}

/**
 * Moves every open dash window, and optionally sizes one of them to the dashboard it holds.
 *
 * The size matters more than it sounds. SimHub opens a windowed dash at whatever size it last used
 * and letterboxes the dashboard inside, so a 1920 by 480 face was arriving in an 800 by 200 window
 * and would have been photographed at that size. `client` is the dashboard's own size; the window
 * is grown by its chrome so that the client area matches exactly.
 */
export function placeDashboards(host: Host, x: number, y: number, client?: { name: string; width: number; height: number }): RunResult {
  const sizing = client
    ? `
  if ($t -eq ${psq(`${client.name} (WPF Renderer)`)}) {
    [OpenDashWindows]::Fit($h, ${Math.round(x)}, ${Math.round(y)}, ${Math.round(client.width)}, ${Math.round(client.height)})
    "fitted $t to ${Math.round(client.width)}x${Math.round(client.height)}"
    continue
  }`
    : '';
  return inDesktopScript(
    host,
    `${WINDOW_HELPER}
foreach ($h in [OpenDashWindows]::Visible()) {
  $t = [OpenDashWindows]::Title($h)
  if ($t -notmatch ' \\(WPF Renderer\\)$') { continue }${sizing}
  [OpenDashWindows]::Move($h, ${Math.round(x)}, ${Math.round(y)})
  "moved $t"
}`,
  );
}

export interface OpenOptions {
  /** The package's folder name, as it appears in Dash Studio's list. */
  name: string;
  /**
   * Row of the package in the list filtered by `filter`, counting from zero. Typing a package's
   * full name narrows the list to it, so this is 0 for every package but the one called plainly
   * "OpenDash", whose name is a prefix of every other and which SimHub lists first anyway.
   */
  index?: number;
  /** What to type into the search box; defaults to the name. */
  filter?: string;
}

/** The part of SimHub's client area that is on the screen, in screen pixels. */
export interface ClientArea {
  left: number;
  top: number;
  width: number;
  height: number;
}

/** The client area `maximiseSimHub` reports, or null when what it printed is why there is none. */
export function parseClientArea(report: string): ClientArea | null {
  const match = /^client (-?\d+),(-?\d+) (\d+)x(\d+)/.exec(report.trim());
  if (!match) return null;
  const [left, top, width, height] = match.slice(1).map(Number) as [number, number, number, number];
  return width > 0 && height > 0 ? { left, top, width, height } : null;
}

/**
 * Where each of `openDashboard`'s clicks lands, given SimHub's client area and how far down its
 * row is. Offsets are added to the area's top-left and fractions are of its width, so an area that
 * does not begin at the screen's origin, as with a taskbar on the left or at the top, moves every
 * click with it. Only the maximised width has been measured, so a fraction at any other width is a
 * guess that `openDashboard` does not make: it refuses a window that is not maximised.
 *
 * `underOffer` moves the search box down by the track-layout offer's `shift`, for a pass working
 * under an offer that would not go (#308); the row is whatever `rowY` says, since the row is found
 * in the list rather than assumed.
 */
export function aimAt(client: ClientArea, rowY: number, underOffer = false) {
  const across = (fraction: number) => Math.round(client.left + client.width * fraction);
  const down = (pixels: number) => client.top + pixels;
  const rowX = across(CONTENT.rowX);
  return {
    dashStudio: { x: client.left + MENU.x, y: down(MENU.dashStudio) },
    trackLayoutOffer: { x: across(TRACK_LAYOUT_OFFER.x), y: down(TRACK_LAYOUT_OFFER.y) },
    search: { x: across(CONTENT.searchX), y: down(LIST.firstRow - 123 + (underOffer ? TRACK_LAYOUT_OFFER.shift : 0)) },
    row: { x: rowX, y: down(rowY) },
    windowed: { x: rowX + 49, y: down(rowY + LIST.quickRunOffset) },
  };
}

/** A look at the screen, or why none could be taken. `column` is there when the look was asked for it and could read it. */
type Look = { ok: true; samples: LookSamples; column: Run[] | null } | { ok: false; why: string };

/**
 * Reads the nine pixels of `lookPoints(rowY)` off the guest's framebuffer, after resting the
 * pointer at `hover` for as long as a row takes to light when one is given, and with `listFrom` the
 * look column from there down. Everything given and returned is in SimHub's client area, as the
 * clicks are; the framebuffer is the screen, so the translation is made here and nowhere else.
 *
 * The rest and the read are one VNC session, because a hover does not survive a reconnection. The
 * framebuffer is read where vncdotool keeps it rather than saved as a PNG and reopened: about a
 * third of a second on this host, which matters in a call that looks at least twice per pass.
 */
function lookAt(host: Host, client: ClientArea, rowY: number, opts: { hover?: { x: number; y: number }; listFrom?: number } = {}): Look {
  const toScreen = ([x, y]: [number, number]): [number, number] => [client.left + x, client.top + y];
  const { offer, row } = lookPoints(rowY);
  const points = { offer: offer.map(toScreen), row: row.map(toScreen) };
  const { hover, listFrom } = opts;
  const column =
    listFrom === undefined
      ? []
      : [
          // The look column from `listFrom` down, as runs of one colour; a run under 3 px is a glyph's
          // edge and says nothing, so it is left out to keep the line short.
          `x, top, end = ${client.left + LOOK.rowX}, ${client.top + Math.round(listFrom)}, ${client.top + LOOK.listTo}`,
          'runs, prev, start = [], None, top',
          'for y in range(top, end + 1):',
          '    p = list(im.getpixel((x, y)))[:3] if y < end else None',
          '    if p != prev:',
          `        if prev is not None and y - start >= 3: runs.append([start - ${client.top}, y - 1 - ${client.top}, prev])`,
          '        prev, start = p, y',
          `print('column ' + json.dumps(runs, separators=(',', ':')))`,
        ];
  const r = vnc(
    host,
    [
      ...(hover ? [`client.mouseMove(${Math.round(hover.x)}, ${Math.round(hover.y)})`, `time.sleep(${HOVER_SECONDS})`] : []),
      'import json',
      'client.refreshScreen()',
      'im = client.screen',
      `points = ${JSON.stringify(points)}`,
      `print('look ' + json.dumps({k: [list(im.getpixel(tuple(p)))[:3] for p in v] for k, v in points.items()}, separators=(',', ':')))`,
      ...column,
    ].join('\n'),
    60_000,
  );
  if (!r.ok) {
    const last = r.stderr.split('\n').map((l) => l.trim()).filter(Boolean).at(-1);
    return { ok: false, why: last ?? `exit ${r.code}` };
  }
  const samples = parseLook(r.stdout);
  if (!samples) return { ok: false, why: 'no look line in its output' };
  return { ok: true, samples, column: listFrom === undefined ? null : parseColumn(r.stdout) };
}

/**
 * The cards Dash Studio is listing below the search box at `searchY`, in the client area, or null
 * when the list could not be read. A list with fewer than `atLeast` cards is read once more after
 * two seconds, since the search filters as it is typed into and the last keys may still be drawing.
 */
function listedCards(host: Host, client: ClientArea, searchY: number, atLeast: number): [number, number][] | null {
  const read = () => {
    const look = lookAt(host, client, LIST.firstRow, { listFrom: searchY + 12 });
    return look.ok && look.column ? cardsIn(look.column) : null;
  };
  const cards = read();
  if (cards === null || cards.length >= atLeast) return cards;
  sleep(2);
  return read();
}

/** The offer's state right now, or null when the screen could not be read. */
function offerNow(host: Host, client: ClientArea): OfferState | null {
  const look = lookAt(host, client, LIST.firstRow);
  return look.ok ? offerState(look.samples.offer) : null;
}

/**
 * Rests the pointer at `at`, on the screen, on the row at `rowY` in the client area, and says what
 * is under it, for a pass that expects the offer `underOffer`.
 */
function seeRow(host: Host, client: ClientArea, at: { x: number; y: number }, rowY: number, underOffer: boolean): Seen {
  const look = lookAt(host, client, rowY, { hover: at });
  return look.ok ? readLook(look.samples, underOffer ? 'up' : 'down') : { kind: 'unread', why: look.why };
}

/**
 * Where the screen is saved when `openDashboard` gives up on `name`: one file per dashboard, so a
 * batch that loses two keeps both pictures.
 */
const openFailedPng = (name: string): string =>
  path.join(path.resolve(import.meta.dir, '..'), 'build', `vm-open-${name.replace(/[^A-Za-z0-9]+/g, '-').replace(/^-|-$/g, '')}.png`);

/**
 * Opens a dashboard in a window: Dash Studio, dismiss the track-layout offer, filter the list,
 * hover the row so its Start button appears, click it, then Windowed from the Quick run menu.
 *
 * Both clicks need a dwell in front of them. The Start button is revealed by the hover and not by
 * the click, and the Quick run menu highlights its item on hover before it will take a press; a
 * click sent to a coordinate the pointer has only just reached lands on neither. That is SimHub's
 * behaviour rather than a race, and it is why this hovers for two seconds twice.
 *
 * Looks at the screen keep the clicks honest (#308). The first, after "No thanks", says whether
 * the track-layout offer went: one still up is clicked again, and one that stays after that is
 * worked under, the search box `TRACK_LAYOUT_OFFER.shift` pixels lower, instead of being typed past.
 * The second, once the filter is in, reads where the list's cards are, so the row is aimed at
 * rather than assumed. The third, with the pointer resting on that row, says whether it lit: no row
 * to hover, nothing under the pointer, or an offer that came or went since the first look, and Start
 * is not pressed, since the press would land on the wrong thing. Anything else is pressed as before.
 * When nothing opens, the first line of the failure says what the looks saw, and whether SimHub was
 * still running, which is what tells this apart from #303.
 */
export function openDashboard(host: Host, opts: OpenOptions): RunResult {
  const already = openDashboards(host);
  if (already.includes(opts.name)) return { ok: true, code: 0, stdout: `${opts.name} is already open`, stderr: '' };

  const size = screenSize(host);
  if (!size) return { ok: false, code: 1, stdout: '', stderr: 'could not read the guest display size over VNC' };
  // Read first and refused rather than clicked at: a wrong mode is not a coordinate this can
  // correct, and the two attempts below would spend themselves on empty space.
  const wrongMode = screenModeProblem(size);
  if (wrongMode) return { ok: false, code: 1, stdout: '', stderr: wrongMode };

  const row = opts.index ?? 0;
  const filter = opts.filter ?? opts.name;
  const debug = (line: string) => {
    if (process.env.OPENDASH_GUI_DEBUG) console.error(`[debug] ${line}`);
  };

  // Two passes, whose offsets matter only when the list cannot be read and the row is guessed: the
  // plain one first, then the Last used band's. A pass whose look says doing it again would help --
  // the page moved under it, or its keys never reached the search box -- is redone once, which is
  // why this is a list that can grow.
  const bands: number[] = [0, LIST.lastUsedBand];
  const attempts: Attempt[] = [];
  let repeats = 0;
  let noWindow: string | undefined;
  let lastClient: ClientArea | null = null;
  for (let i = 0; i < bands.length; i++) {
    const bandOffset = bands[i]!;
    // Read, not fired and forgotten. Every coordinate below is measured from SimHub's client area
    // and was only ever measured with it filling the screen, so if the window is not there yet
    // there is nothing to click and no offset that helps: say so instead of spending the second
    // attempt clicking the desktop at a different height.
    const maximised = maximiseSimHub(host);
    const client = parseClientArea(maximised.stdout);
    if (!client) {
      const why = maximised.stdout || maximised.stderr || 'maximising SimHub produced nothing';
      if (attempts.length > 0) {
        // A later pass: what the earlier ones saw is still the better half of the explanation.
        noWindow = why;
        break;
      }
      return {
        ok: false,
        code: 1,
        stdout: '',
        stderr:
          `${why}.\n` +
          `Nothing was clicked: every coordinate here is measured from a SimHub filling the screen. ` +
          `Its window takes around half a minute to appear after the process starts, so a restart ` +
          `that is merely slow looks the same as one that failed; \`bun run vm shot\` shows which.`,
      };
    }
    lastClient = client;
    const menu = aimAt(client, LIST.firstRow);
    sleep(2);
    click(host, menu.dashStudio.x, menu.dashStudio.y);
    sleep(4);
    click(host, menu.trackLayoutOffer.x, menu.trackLayoutOffer.y);
    sleep(2);
    // The blind click above cannot say whether it took, and a page it missed is 61 px out on every
    // click that follows, the search box included. So look before typing anything.
    const initial = offerNow(host, client);
    // Caught drawing: give it the time the click was given, and then treat it as whatever it became.
    let first = initial;
    if (first === 'partial') {
      sleep(2);
      first = offerNow(host, client);
    }
    let second: OfferState | null = null;
    if (first === 'up') {
      click(host, menu.trackLayoutOffer.x, menu.trackLayoutOffer.y);
      sleep(2);
      second = offerNow(host, client);
    }
    const offerSeen = initial === 'up' || initial === 'partial';
    // Worked under only on the evidence of a look after the second click. An unreadable look is taken
    // as the offer gone, as it was before looks existed; the row look says 'moved' if it was not.
    const underOffer = second === 'up';
    debug(`attempt ${i + 1}: after "No thanks" the offer is ${[initial, ...(first !== initial ? [first] : []), ...(second ? [second] : [])].map((o) => o ?? 'unread').join(', then ')}`);

    const search = aimAt(client, LIST.firstRow, underOffer).search;
    click(host, search.x, search.y);
    sleep(1);
    // Emptied rather than selected. The box keeps what the last run typed, and a select-all that
    // lands while the box is not yet focused leaves that text in place, so the filter becomes the
    // old name with the new one appended -- which matches no dashboard at all, and the loop then
    // reports that it could not open a package that is installed and listed.
    press(host, 'ctrl-a');
    sleep(1);
    press(host, 'del');
    sleep(1);
    type(host, filter);
    sleep(3);

    // Find the row rather than assume it. The list starts in more places than an offset can say,
    // so the cards are read down one column, and the offsets are only the fallback for a list that
    // could not be read.
    const cards = listedCards(host, client, search.y - client.top, row + 1);
    const card = cards?.[row];
    const rowY = card ? Math.round((card[0] + card[1]) / 2) : guessRow(bandOffset, row, underOffer);
    const aim = aimAt(client, rowY, underOffer);
    const listing = cards === null ? 'unread' : cards.length === 0 ? 'empty' : cards.map(([top, bottom]) => `${top}..${bottom}`).join(', ');

    // Look before pressing: the row lights under the pointer when its Start button is up. A row
    // that has not lit is given one more dwell, since a busy SimHub draws late.
    let seen: Seen = { kind: 'empty' };
    let looks = 0;
    if (cards === null || card) {
      seen = seeRow(host, client, aim.row, rowY, underOffer);
      looks = 1;
      if (seen.kind === 'row') {
        seen = seeRow(host, client, aim.row, rowY, underOffer);
        looks = 2;
      }
    }
    debug(`attempt ${i + 1}: list ${listing}; at y ${rowY}${underOffer ? ', under the offer' : ''}, ${describeSeen(seen, looks)}`);

    let strays: string[] = [];
    const pressed = pressAfter(seen);
    if (pressed) {
      // Hover, then press: the Start button is inside the row and appears only under the pointer.
      click(host, aim.row.x, aim.row.y, HOVER_SECONDS);
      sleep(3);
      click(host, aim.windowed.x, aim.windowed.y, HOVER_SECONDS);
      sleep(14);
      const open = openDashboards(host);
      if (open.includes(opts.name)) return { ok: true, code: 0, stdout: openedLine(opts.name, i + 1, underOffer), stderr: '' };
      // A guess at the wrong offset lands on another row and opens the wrong dashboard. Close what
      // this opened before guessing again, so a failure leaves the rig as it found it.
      strays = open.filter((n) => !already.includes(n));
      if (strays.length > 0) {
        closeDashboards(host);
        sleep(2);
      }
    }
    const attempt: Attempt = { rowY, measured: cards !== null, offerSeen, underOffer, seen, looks, pressed, strays };
    attempts.push(attempt);
    if (repeats < MAX_REPEATS && repeatPass(attempt, filter)) {
      bands.splice(i + 1, 0, bandOffset);
      repeats++;
    }
  }

  // Only a failure gets here, and only a failure pays for these: whether the offer came back after
  // the last look, whether SimHub is still there at all, and the screen as it was left. SimHub is
  // asked here and not earlier: the maximise at the top of each pass already names one that is gone,
  // so the case left is one that died after its window was found, under the clicks.
  const running = simhubRunning(host);
  const end = lastClient && running !== false ? lookAt(host, lastClient, attempts.at(-1)?.rowY ?? LIST.firstRow) : null;
  const offerAtEnd = end?.ok === true && offerState(end.samples.offer) === 'up';
  const picture = openFailedPng(opts.name);
  const shot = screenshot(host, picture);
  return {
    ok: false,
    code: 1,
    stdout: '',
    stderr: openFailure({ name: opts.name, filter, running, noWindow, offerAtEnd, picture: shot.ok ? path.relative(process.cwd(), picture) : undefined }, attempts),
  };
}

/** A window to photograph: by its exact title, or as the main window of a process by its name. */
export type WindowRef = string | { mainWindowOf: string };

/** A rectangle in screen pixels. */
export interface ScreenRect {
  left: number;
  top: number;
  width: number;
  height: number;
}

/**
 * Photographs one window into a local PNG, whole or cropped to a rectangle of the screen.
 *
 * `PrintWindow` with `PW_RENDERFULLCONTENT` asks the window to draw itself, so the result is the
 * window rather than whatever happens to be on top of it. It only works while the window is fully
 * on screen: a window hanging off the bottom comes back with the off-screen part cut, which looks
 * exactly like a clipped glyph and wasted an afternoon.
 *
 * `crop` is in screen pixels, as `maximiseSimHub` and panel-shots's `measurePanel` report
 * them, and is taken out of the window's own picture after it is drawn: the bitmap starts at the
 * window's top-left, which on a maximised window is the invisible resize border at -8,-8, and the
 * offset is worked out on the guest from the rectangle it photographed. A crop that misses the
 * window is a failure rather than an empty file.
 *
 * SimHub's main window has to be named by its process: it shares its title with two other visible
 * windows, one of them a full-screen overlay (see `maximiseSimHub`).
 */
export function captureWindow(host: Host, window: WindowRef, localPath: string, crop?: ScreenRect): RunResult {
  const guestPng = `${WINVM_DIR}/shared/capture_${Math.round(Date.now())}.png`;
  const guestUnc = `\\\\host.lan\\Data\\${path.basename(guestPng)}`;
  const find =
    typeof window === 'string'
      ? `$target = ${psq(window)}
$found = [IntPtr]::Zero
foreach ($h in [OpenDashWindows]::Visible()) { if ([OpenDashWindows]::Title($h) -eq $target) { $found = $h; break } }
if ($found -eq [IntPtr]::Zero) { "no window titled $target"; exit }`
      : `$proc = Get-Process ${psq(window.mainWindowOf)} -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc -or $proc.MainWindowHandle -eq [IntPtr]::Zero) { ${psq(`no main window for the process ${window.mainWindowOf}`)}; exit }
$found = $proc.MainWindowHandle`;
  const cropping = crop
    ? `# Parenthesised, since PowerShell's comma binds tighter than its minus; and the static Intersect,
# since a method that changes a struct in a variable changes a copy.
$asked = New-Object System.Drawing.Rectangle((${Math.round(crop.left)} - $r[0]), (${Math.round(crop.top)} - $r[1]), ${Math.round(crop.width)}, ${Math.round(crop.height)})
$want = [System.Drawing.Rectangle]::Intersect($asked, (New-Object System.Drawing.Rectangle(0, 0, $r[2], $r[3])))
if ($want.Width -le 0 -or $want.Height -le 0) { $bmp.Dispose(); "the crop misses the window at $($r[0]),$($r[1]) $($r[2])x$($r[3])"; exit }
$whole = $bmp
$bmp = $whole.Clone($want, $whole.PixelFormat)
$whole.Dispose()`
    : '';
  const captured = inDesktopScript(
    host,
    `${WINDOW_HELPER}
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public class OpenDashShot {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  public static Bitmap Shoot(IntPtr h, int w, int hgt) {
    var bmp = new Bitmap(w, hgt, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp)) {
      IntPtr hdc = g.GetHdc();
      PrintWindow(h, hdc, 2);
      g.ReleaseHdc(hdc);
    }
    return bmp;
  }
}
'@ -ReferencedAssemblies System.Drawing
${find}
$r = [OpenDashWindows]::Rect($found)
$bmp = [OpenDashShot]::Shoot($found, $r[2], $r[3])
${cropping}
# Saved to local disk and then copied. GDI+ reports success saving straight to the UNC share and
# leaves nothing there, which is a silent way to lose every screenshot.
$local = Join-Path $env:TEMP ${psq(path.basename(guestPng))}
$bmp.Save($local, [System.Drawing.Imaging.ImageFormat]::Png)
"captured {0}x{1}" -f $bmp.Width, $bmp.Height
$bmp.Dispose()
Copy-Item $local ${psq(guestUnc)} -Force
Remove-Item $local -Force -ErrorAction SilentlyContinue`,
  );
  if (!captured.ok || !captured.stdout.startsWith('captured')) {
    return { ok: false, code: 1, stdout: '', stderr: captured.stdout || captured.stderr || 'the capture produced nothing' };
  }
  const fetched = fetchFromShare(host, guestPng, localPath);
  onHost(host, `rm -f ${guestPng}`, 30_000);
  return fetched.ok ? { ...fetched, stdout: `${captured.stdout} to ${localPath}` } : fetched;
}

/**
 * Photographs one dash window, at its own size, into a local PNG. `placeDashboards` first, so the
 * window is wholly on the screen; see {@link captureWindow}.
 */
export const captureDashboard = (host: Host, name: string, localPath: string): RunResult => captureWindow(host, `${name} (WPF Renderer)`, localPath);

/**
 * Turns the mouse wheel over a point: down for a positive count of notches, up for a negative one.
 * VNC's buttons 4 and 5 are the wheel, and WPF's ScrollViewer moves 48 device-independent pixels a
 * notch (three lines of sixteen) unless the content scrolls by items.
 */
export const wheel = (host: Host, x: number, y: number, notches: number): RunResult =>
  vnc(
    host,
    `client.mouseMove(${Math.round(x)}, ${Math.round(y)})
time.sleep(0.3)
for _ in range(${Math.abs(Math.trunc(notches))}):
    client.mousePress(${notches < 0 ? 4 : 5})
    time.sleep(0.04)`,
  );

/** Brings a file out of the share to a local path, directly or over SSH. */
/** What one recording produced, parsed from the recorder's report line. */
export interface Recording {
  width: number;
  height: number;
  frames: number;
  dropped: number;
  /** Frames per second actually achieved, from the recorder's clock. */
  measuredFps: number;
  captureMeanMs: number;
  captureMaxMs: number;
}

export interface RecordOptions {
  /** Seconds to keep. */
  seconds: number;
  fps: number;
  /** Seconds recorded before the kept span and discarded by the encoder: PrintWindow's first calls are slow. */
  preroll: number;
  /** Where frames.raw and frames.ticks land. */
  localDir: string;
}

const REPORT = /^recorded (\d+) frames (\d+) dropped ([\d.]+) fps mean ([\d.]+) ms max ([\d.]+) ms (\d+)x(\d+)$/;

/** `recorded 140 frames 0 dropped 19.94 fps mean 9.8 ms max 21.0 ms 850x480` as numbers. */
export function parseRecordReport(text: string): Recording {
  const line = text
    .split('\n')
    .map((l) => l.trim())
    .find((l) => l.startsWith('recorded '));
  const m = line ? REPORT.exec(line) : null;
  if (!m) throw new Error(`no recording report in: ${text.trim().split('\n').slice(-3).join(' | ') || '(nothing)'}`);
  return {
    frames: Number(m[1]),
    dropped: Number(m[2]),
    measuredFps: Number(m[3]),
    captureMeanMs: Number(m[4]),
    captureMaxMs: Number(m[5]),
    width: Number(m[6]),
    height: Number(m[7]),
  };
}

/**
 * Records one dash window as raw frames, at its own size, for a clip.
 *
 * The same `PrintWindow` as a still, called on a fixed cadence from a C# loop inside the desktop
 * session. Frames are copied out of the bitmap as raw BGRA into one file on the guest's own disk
 * by a writer thread; a PNG per frame would cost ten times the capture itself and could not keep
 * 20 fps. A bounded queue means a disk stall drops frames rather than shifting the clock, and every
 * written frame's timestamp goes to a `.ticks` file, so the encoder can be told the rate that was
 * actually achieved. The first `preroll` seconds are recorded too and skipped when encoding.
 *
 * Refuses a window that hangs off the screen, for the same reason as captureDashboard: the part
 * off screen comes back cut. `placeDashboards` first.
 */
export function recordDashboard(host: Host, name: string, opts: RecordOptions): RunResult & { recording?: Recording } {
  const tag = `opendash_clip_${Math.round(Date.now())}`;
  const guestRaw = `\\\\host.lan\\Data\\${tag}.raw`;
  const guestTicks = `\\\\host.lan\\Data\\${tag}.ticks`;
  const total = Math.round(opts.fps * (opts.seconds + opts.preroll));
  const script = `${WINDOW_HELPER}
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System; using System.Collections.Concurrent; using System.Diagnostics; using System.Drawing; using System.Drawing.Imaging; using System.Globalization; using System.IO; using System.Runtime.InteropServices; using System.Text; using System.Threading;
public class OpenDashClip {
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
  [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
  public static string Record(IntPtr h, int w, int hgt, int fps, int total, string path) {
    int frameBytes = w * hgt * 4, written = 0, dropped = 0;
    long interval = Stopwatch.Frequency / fps, costSum = 0, costMax = 0;
    var queue = new BlockingCollection<byte[]>(24);
    var pool = new ConcurrentBag<byte[]>();
    var ticks = new StringBuilder();
    var writer = new Thread(() => {
      using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
        foreach (var buf in queue.GetConsumingEnumerable()) { fs.Write(buf, 0, frameBytes); written++; pool.Add(buf); }
    });
    writer.Start();
    var bmp = new Bitmap(w, hgt, PixelFormat.Format32bppArgb);
    var g = Graphics.FromImage(bmp);
    var sw = Stopwatch.StartNew();
    timeBeginPeriod(1);
    try {
      for (int i = 0; i < total; i++) {
        long due = i * interval, wait = due - sw.ElapsedTicks;
        if (wait > 2 * Stopwatch.Frequency / 1000) Thread.Sleep((int)(wait * 1000 / Stopwatch.Frequency) - 1);
        while (sw.ElapsedTicks < due) Thread.SpinWait(20);
        long t0 = sw.ElapsedTicks;
        IntPtr hdc = g.GetHdc(); PrintWindow(h, hdc, 2); g.ReleaseHdc(hdc);
        byte[] buf; if (!pool.TryTake(out buf)) buf = new byte[frameBytes];
        var d = bmp.LockBits(new Rectangle(0, 0, w, hgt), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(d.Scan0, buf, 0, frameBytes); bmp.UnlockBits(d);
        long cost = sw.ElapsedTicks - t0; costSum += cost; if (cost > costMax) costMax = cost;
        if (queue.TryAdd(buf)) ticks.Append((t0 * 1000.0 / Stopwatch.Frequency).ToString("0.000", CultureInfo.InvariantCulture)).Append('\\n'); else { dropped++; pool.Add(buf); }
        long late = (sw.ElapsedTicks - due) / interval; if (late > 1) { i += (int)(late - 1); dropped += (int)(late - 1); }
      }
    } finally { timeEndPeriod(1); queue.CompleteAdding(); writer.Join(); g.Dispose(); bmp.Dispose(); }
    File.WriteAllText(path + ".ticks", ticks.ToString());
    double s = sw.ElapsedTicks / (double)Stopwatch.Frequency, ms = 1000.0 / Stopwatch.Frequency;
    return string.Format(CultureInfo.InvariantCulture, "recorded {0} frames {1} dropped {2:0.00} fps mean {3:0.0} ms max {4:0.0} ms {5}x{6}",
      written, dropped, written / s, costSum * ms / Math.Max(1, written), costMax * ms, w, hgt);
  }
}
'@ -ReferencedAssemblies System.Drawing
$target = ${psq(`${name} (WPF Renderer)`)}
Get-ChildItem (Join-Path $env:TEMP 'opendash_clip_*') -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
foreach ($h in [OpenDashWindows]::Visible()) {
  if ([OpenDashWindows]::Title($h) -ne $target) { continue }
  $r = [OpenDashWindows]::Rect($h)
  $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  if ($r[0] -lt 0 -or $r[1] -lt 0 -or ($r[0] + $r[2]) -gt $screen.Width -or ($r[1] + $r[3]) -gt $screen.Height) {
    "off-screen $($r[0]),$($r[1]) $($r[2])x$($r[3]) on $($screen.Width)x$($screen.Height)"
    exit
  }
  $local = Join-Path $env:TEMP ${psq(`${tag}.raw`)}
  $report = [OpenDashClip]::Record($h, $r[2], $r[3], ${opts.fps}, ${total}, $local)
  # Written to local disk and then copied: the share is slow to write frame by frame, and GDI+ is
  # not involved here, but a 200 MB stream to a UNC path still stalls the queue.
  Copy-Item $local ${psq(guestRaw)} -Force
  Copy-Item "$local.ticks" ${psq(guestTicks)} -Force
  Remove-Item $local, "$local.ticks" -Force -ErrorAction SilentlyContinue
  $report
  exit
}
"no window titled $target"`;
  const ran = inDesktopScript(host, script, opts.seconds + opts.preroll + 240);
  if (!ran.ok) return ran;
  let recording: Recording;
  try {
    recording = parseRecordReport(ran.stdout);
  } catch (e) {
    return { ok: false, code: 1, stdout: '', stderr: (e as Error).message };
  }
  mkdirSync(opts.localDir, { recursive: true });
  const rawHost = `${WINVM_DIR}/shared/${tag}.raw`;
  const ticksHost = `${WINVM_DIR}/shared/${tag}.ticks`;
  const rawLocal = path.join(opts.localDir, 'frames.raw');
  const ticksLocal = path.join(opts.localDir, 'frames.ticks');
  // Moved rather than copied when the host is this machine: the raw file is hundreds of megabytes
  // and the share is on the same disk.
  const fetched = host.local
    ? onHost(host, `mv -f '${rawHost}' '${rawLocal}' && mv -f '${ticksHost}' '${ticksLocal}'`, 300_000)
    : (() => {
        const a = fetchFromShare(host, rawHost, rawLocal);
        if (!a.ok) return a;
        const b = fetchFromShare(host, ticksHost, ticksLocal);
        onHost(host, `rm -f '${rawHost}' '${ticksHost}'`, 30_000);
        return b;
      })();
  if (!fetched.ok) return fetched;
  return { ok: true, code: 0, stdout: ran.stdout.trim(), stderr: '', recording };
}

function fetchFromShare(host: Host, remotePath: string, localPath: string): RunResult {
  mkdirSync(path.dirname(path.resolve(localPath)), { recursive: true });
  if (host.local) return onHost(host, `cp '${remotePath}' '${localPath}'`);
  const r = Bun.spawnSync(['scp', '-q', '-o', 'BatchMode=yes', `${host.name}:${remotePath}`, localPath]);
  return { ok: r.exitCode === 0, code: r.exitCode, stdout: localPath, stderr: new TextDecoder().decode(r.stderr).trim() };
}

/**
 * Why nothing here can be clicked, or null when it can: the VNC tooling on the host, and the mode
 * the guest's display is in.
 *
 * Every caller asks this as soon as the guest answers and before it builds anything, which is the
 * earliest it can be asked -- a display mode needs the guest up and nothing more. What sits between
 * there and the first click is the build, the install, SimHub's restart and the emulator, four
 * minutes of `bun run dev` that a wrong display mode does not change and that a refusal afterwards
 * would have spent for nothing.
 *
 * `openDashboard` checks the mode again at the click, because it is also called on its own.
 */
export function guiProblem(host: Host): string | null {
  const tooling = host.local ? existsSync(VENV_PYTHON) : onHost(host, `test -x ${VENV_PYTHON}`, 30_000).ok;
  if (!tooling) return `the VNC tooling is not on the VM host (${VENV_PYTHON} is missing), so a dash cannot be opened from here`;
  const size = screenSize(host);
  if (!size) return 'could not read the guest display size over VNC, so there is no telling where a click would land';
  return screenModeProblem(size);
}

// `guiAvailable`, a boolean, was what the four callers asked and each of them then printed a guess at
// why the answer was no -- "the VNC tooling is not on the VM host" while the tooling was there and
// the guest was simply at the wrong resolution, which is how a container restart came to look like
// broken coordinates. A reason is the whole value of the check, so the reason is what it returns.
