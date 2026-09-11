/**
 * The gear: the one readout the hero zone holds, centred in the face's hero rect.
 *
 * The hero is a module, and a module shows one thing. The gear is that thing on every face,
 * which is why this component takes only a rect and a size: a column between two slot grids, a
 * full-width band on the DisplayDash, or the middle of a round face. Speed used to sit beside
 * it and is now card 12, so it can be placed like any other value.
 *
 * The gear is drawn in a letter-wide cell (metrics GEAR_CELL) rather than a digit cell, because
 * SimHub reports "N" and "R" as well as the gear number and "N" is the widest of them.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { GEAR_CELL, gearCells, monoWidth } from '../design/metrics.ts';
import { numeral } from '../elements/numeral.ts';
import { ds } from '../tokens.ts';

const { game } = ncalc;

/** The gear is one character; SimHub reports "N" and "R" too, drawn in the letter-wide gear cell. */
export const GEAR_CHARS = { digits: 1, specials: 0 } as const;

/** Pixels the gear's box takes beyond its cell, so that WPF clips nothing. */
export const GEAR_BOX_SLACK = 4;

/**
 * Gear font sizes: 260 everywhere, 180 on faces too short for it (the nano), and 228 on the 480
 * round, whose two slots leave only 160 px between them. 228 is the largest that fits a cell
 * there; it is smaller than the other faces only because the cell has to hold a non-condensed
 * Barlow (XOR-84), and it goes back to 260 when that is fixed.
 */
export const GEAR_SIZES = {
  standard: ds.size.gear,
  nano: ds.size.gearSm,
  round480: 228,
} as const;

/** The gear centred in `frame`, horizontally on its cell width and vertically on its font size. */
export function gear(frame: Rect, size: number = GEAR_SIZES.standard, prefix = 'hero'): Item[] {
  const mono = gearCells(size);
  const width = monoWidth(mono, GEAR_CHARS);
  // A frame too narrow for the cell has only bad answers: narrowing the cell clips the glyph, and
  // keeping it draws over whatever the frame was protecting. Neither is something to do quietly,
  // so a layout that cannot give the gear its cell has to say what it wants instead.
  if (width + GEAR_BOX_SLACK > frame.width) {
    throw new Error(
      `${prefix}.gear: a ${size} px gear needs ${width + GEAR_BOX_SLACK} px and the frame gives ${frame.width}. ` +
        `Either give it more room or set a size of at most ${Math.floor((frame.width - GEAR_BOX_SLACK) / GEAR_CELL)}.`,
    );
  }
  const left = frame.left + (frame.width - width) / 2;
  const top = frame.top + (frame.height - size) / 2;
  // The box takes a few pixels beyond the cell so that WPF never clips the glyph, and no more:
  // on a round face the slots start just past it.
  const maxWidth = Math.min(frame.left + frame.width - left, width + GEAR_BOX_SLACK);
  return [numeral(`${prefix}.gear`, '4', left, top, size, GEAR_CHARS, { weight: 'Bold', mono, maxWidth, bind: game('Gear') })];
}
