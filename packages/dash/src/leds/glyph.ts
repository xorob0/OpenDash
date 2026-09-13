/**
 * Glyphs for the flag box, written as a grid a reviewer can read.
 *
 * Sixty-four pixels is small enough that the only honest way to review a picture is to look at
 * it, so a glyph is eight strings of eight characters and a palette that maps each character to
 * a token. `.` is unlit, which is also how a black flag is drawn: black is the absence of light,
 * so anything black needs an outline in something else or it is nothing at all.
 */
import type { Hex, MatrixFrame } from '../generator.ts';

/** The character that means "nothing lit here". Space is accepted as well, for alignment. */
export const UNLIT = '.';

export type Palette = Readonly<Record<string, Hex>>;

/** An 8x8 picture, or any other rectangle: rows of equal length. */
export type Grid = readonly string[];

const isUnlit = (ch: string): boolean => ch === UNLIT || ch === ' ';

/**
 * Checks the grid is rectangular and every lit character is in the palette, then returns the
 * pixels. A typo in a grid is otherwise a silently dark pixel, which is the failure this whole
 * file exists to make visible.
 */
export function pixelsOf(grid: Grid, palette: Palette, name = 'glyph'): (Hex | null)[][] {
  if (grid.length === 0) throw new RangeError(`${name}: a glyph needs at least one row`);
  const width = grid[0]?.length ?? 0;
  return grid.map((row, y) => {
    if (row.length !== width) {
      throw new RangeError(`${name}: row ${y} is ${row.length} characters, row 0 is ${width}; a glyph is a rectangle`);
    }
    return [...row].map((ch, x) => {
      if (isUnlit(ch)) return null;
      const colour = palette[ch];
      if (colour === undefined) throw new RangeError(`${name}: row ${y} column ${x} is ${JSON.stringify(ch)}, which the palette does not name`);
      return colour;
    });
  });
}

/** One still picture: a single frame held until something else takes the matrix. */
export const still = (grid: Grid, palette: Palette, name?: string): MatrixFrame[] => [{ durationMs: STILL_MS, pixels: pixelsOf(grid, palette, name) }];

/**
 * How long a still frame claims to last. SimHub loops an animation, so a one-frame animation
 * shows continuously whatever this is; a round number keeps the file readable.
 */
export const STILL_MS = 1000;

/** Two frames alternating at `hz`, which is how anything on this box blinks. */
export function blinkFrames(on: Grid, off: Grid, palette: Palette, hz: number, name?: string): MatrixFrame[] {
  if (!Number.isFinite(hz) || hz <= 0) throw new RangeError(`${name ?? 'glyph'}: blink rate must be positive, got ${hz}`);
  const half = Math.round(1000 / hz / 2);
  return [
    { durationMs: half, pixels: pixelsOf(on, palette, name) },
    { durationMs: half, pixels: pixelsOf(off, palette, name) },
  ];
}

/** A grid of the same character, for the "off" half of a blink and for a solid fill. */
export const filled = (rows: number, columns: number, ch: string): Grid => Array.from({ length: rows }, () => ch.repeat(columns));

/** The unlit grid of a size: the dark half of a blink. */
export const dark = (rows: number, columns: number): Grid => filled(rows, columns, UNLIT);
