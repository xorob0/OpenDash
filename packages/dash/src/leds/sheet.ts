/**
 * The contact sheet: every glyph the flag box can draw, rendered as one SVG.
 *
 * Glyphs are grids in source, which makes them reviewable in a diff the way a `.djson` is not.
 * This is the other half: a build artefact a reviewer looks at, so a pull request that changes the
 * chequered flag shows the chequered flag. It is what makes an outside contribution to a flag box
 * reviewable at all — no competitor can offer it, because their profile is an opaque binary.
 *
 * The sheet is drawn from the same functions the profile is built from, so it cannot describe a
 * box that is not the one being shipped; `glyphFit.test.ts` checks that every picture in the built
 * profile appears on it.
 */
import { FLAG_CATALOGUE } from '../flags.ts';
import type { Hex, MatrixFrame } from '../generator.ts';
import { GEARS, gearGrid } from './gear.ts';
import { shiftBands } from '../components/revSegments.ts';
import { flagFrames, ignitionOffFrames } from './glyphs.ts';
import { pixelsOf } from './glyph.ts';
import { pitStates, spotterStates, warningStates, STATE_PALETTE } from './states.ts';
import { COLUMNS, ROWS } from './profile.ts';

/** What a glyph is, for the sheet and for the fit test: a name, a family, and its frames. */
export interface Glyph {
  name: string;
  kind: 'flag' | 'pit' | 'spotter' | 'warning' | 'gear' | 'standby';
  /** Rows of columns per frame; `null` where nothing is lit. */
  frames: (Hex | null)[][][];
}

const framesOf = (frames: readonly MatrixFrame[]): (Hex | null)[][][] => frames.map((f) => f.pixels.map((row) => [...row]));

/**
 * Every distinct picture, in the order the box ranks them. A gear appears once per shift band,
 * because the band is its colour and a reviewer has to see all four.
 */
export function glyphCatalogue(): Glyph[] {
  const out: Glyph[] = [];
  for (const condition of FLAG_CATALOGUE) {
    const frames = flagFrames(condition.id);
    if (frames !== undefined) out.push({ name: condition.id, kind: 'flag', frames: framesOf(frames) });
  }
  for (const state of pitStates()) out.push({ name: `Pit ${state.id}`, kind: 'pit', frames: gridFrames(state.grid) });
  // Both variants: the held bar and the growing one are two pictures the profile can draw, and the
  // sheet is what a reviewer reads instead of the file.
  for (const state of spotterStates(1)) out.push({ name: `Spotter ${state.id}`, kind: 'spotter', frames: gridFrames(state.grid) });
  for (const state of spotterStates(1, true)) {
    out.push({ name: `Spotter ${state.id}`, kind: 'spotter', frames: (state.steps ?? [state.grid]).map((grid) => pixelsOf(grid, STATE_PALETTE, state.id)) });
  }
  // Matrix 1, as the spotter above is: a picture does not depend on which panel draws it, and the
  // thresholds the warnings compare against do not change what is drawn.
  for (const state of warningStates(1)) out.push({ name: `Warning ${state.id}`, kind: 'warning', frames: gridFrames(state.grid) });
  for (const band of shiftBands()) {
    for (const gear of GEARS) {
      out.push({ name: `Gear ${gear} ${band.id}`, kind: 'gear', frames: [pixelsOf(gearGrid(gear), { G: band.colour }, `gear ${gear}`)] });
    }
  }
  out.push({ name: 'Standby', kind: 'standby', frames: framesOf(ignitionOffFrames()) });
  return out;
}

/** A state's grid as a single frame; the blink's dark half is not worth a cell on the sheet. */
const gridFrames = (grid: readonly string[]): (Hex | null)[][][] => [pixelsOf(grid, STATE_PALETTE, 'state')];

const CELL = 7;
const GAP = 1;
const PANEL = COLUMNS * (CELL + GAP) - GAP;
const LABEL = 11;
const TILE_W = PANEL + 14;
const TILE_H = PANEL + LABEL + 14;
const COLS = 8;
/** The unlit LED: not pure black, so the panel's shape is visible around a sparse glyph. */
const UNLIT = '#16181C';
const PAPER = '#0A0B0D';
const INK = '#8A9099';

const escape = (s: string): string => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/**
 * The sheet. Deterministic, so a diff of it is a real change rather than a re-render: no dates, no
 * random ids, and the glyphs in catalogue order.
 */
export function contactSheet(glyphs: readonly Glyph[] = glyphCatalogue()): string {
  const rows = Math.ceil(glyphs.length / COLS);
  const width = COLS * TILE_W;
  const height = rows * TILE_H + 18;
  const parts: string[] = [
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">`,
    `<rect width="${width}" height="${height}" fill="${PAPER}"/>`,
    `<text x="6" y="12" font-family="monospace" font-size="9" fill="${INK}">OpenDash flag box, ${glyphs.length} glyphs, ${ROWS}x${COLUMNS}</text>`,
  ];
  glyphs.forEach((glyph, i) => {
    const x = (i % COLS) * TILE_W + 7;
    const y = Math.floor(i / COLS) * TILE_H + 24;
    parts.push(`<text x="${x}" y="${y - 3}" font-family="monospace" font-size="7" fill="${INK}">${escape(glyph.name)}</text>`);
    // The first frame is the picture; a second frame only ever means "and then dark", or the blue
    // flag's arrow one pixel along, neither of which a still sheet can show honestly.
    const frame = glyph.frames[0] ?? [];
    for (let row = 0; row < ROWS; row++) {
      for (let col = 0; col < COLUMNS; col++) {
        const colour = frame[row]?.[col] ?? null;
        const cx = x + col * (CELL + GAP);
        const cy = y + row * (CELL + GAP);
        parts.push(`<rect x="${cx}" y="${cy}" width="${CELL}" height="${CELL}" rx="1" fill="${colour ?? UNLIT}"/>`);
      }
    }
    if (glyph.frames.length > 1) {
      parts.push(`<text x="${x + PANEL - 6}" y="${y + PANEL + 8}" font-family="monospace" font-size="6" fill="${INK}">${glyph.frames.length}f</text>`);
    }
  });
  parts.push('</svg>');
  return `${parts.join('\n')}\n`;
}
