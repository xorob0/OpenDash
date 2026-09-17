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
 *
 * The two ghosted neighbours live here as well, rather than on the one face that first drew them:
 * zone A's pages and the round faces both draw the cluster, and the binding underneath it is the
 * text-to-text map below, which is not something to write twice.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { GEAR_CELL, gearCells, monoWidth } from '../design/metrics.ts';
import { numeral } from '../elements/numeral.ts';
import { lastGear } from '../shift.ts';
import { ds } from '../tokens.ts';

const { game, iff, eq, str, not } = ncalc;

/** The gear is one character; SimHub reports "N" and "R" too, drawn in the letter-wide gear cell. */
export const GEAR_CHARS = { digits: 1, specials: 0 } as const;

/** Pixels the gear's box takes beyond its cell, so that WPF clips nothing. */
export const GEAR_BOX_SLACK = 4;

/** Gear font sizes: 260 everywhere, 180 on faces too short for it (the nano). */
export const GEAR_SIZES = {
  standard: ds.size.gear,
  nano: ds.size.gearSm,
} as const;

/**
 * Where `gear` puts its cell in `frame`: the letter-wide cell, centred across the frame's width.
 *
 * Read rather than repeated, because zone A places the two ghosted neighbours off the edges of this
 * cell. Centring the gear in one place and guessing where it landed in another is how the ghosts
 * came to be spaced from the column's edges instead of from the gear.
 */
export function gearCell(frame: Rect, size: number): { left: number; width: number } {
  const width = monoWidth(gearCells(size), GEAR_CHARS);
  return { left: frame.left + (frame.width - width) / 2, width };
}

/** The gear centred in `frame`, horizontally on its cell width and vertically on its font size. */
export function gear(frame: Rect, size: number = GEAR_SIZES.standard, prefix = 'hero'): Item[] {
  const mono = gearCells(size);
  const { left, width } = gearCell(frame, size);
  // A frame too narrow for the cell has only bad answers: narrowing the cell clips the glyph, and
  // keeping it draws over whatever the frame was protecting. Neither is something to do quietly,
  // so a layout that cannot give the gear its cell has to say what it wants instead.
  if (width + GEAR_BOX_SLACK > frame.width) {
    throw new Error(
      `${prefix}.gear: a ${size} px gear needs ${width + GEAR_BOX_SLACK} px and the frame gives ${frame.width}. ` +
        `Either give it more room or set a size of at most ${Math.floor((frame.width - GEAR_BOX_SLACK) / GEAR_CELL)}.`,
    );
  }
  const top = frame.top + (frame.height - size) / 2;
  // The box takes a few pixels beyond the cell so that WPF never clips the glyph, and no more:
  // on a round face the slots start just past it.
  const maxWidth = Math.min(frame.left + frame.width - left, width + GEAR_BOX_SLACK);
  return [numeral(`${prefix}.gear`, '4', left, top, size, GEAR_CHARS, { weight: 'Bold', mono, maxWidth, bind: game('Gear') })];
}

/** The share of the gear a ghosted neighbour is drawn at, where a face draws no other. */
export const GHOST_RATIO = 0.42;

/** How a face draws the gear's two ghosted neighbours: how far off its cell, and how large. */
export interface GearGhosts {
  /** Pixels between the gear's cell and each ghost. */
  gap: number;
  /** A ghost's size as a share of the gear's, when the face draws something other than the 0.42 of the zone sheets. */
  ratio?: number;
}

/** The size a ghosted neighbour is drawn at beside a gear of `size`. */
const ghostSize = (size: number, ghosts: GearGhosts): number => Math.round(size * (ghosts.ratio ?? GHOST_RATIO));

/** What the gear and its two ghosts take across, which is what a frame has to give the cluster. */
export const ghostedGearWidth = (size: number, ghosts: GearGhosts): number =>
  monoWidth(gearCells(size), GEAR_CHARS) + 2 * (ghosts.gap + monoWidth(gearCells(ghostSize(size, ghosts)), GEAR_CHARS) + GEAR_BOX_SLACK);

/**
 * The gear below and above, ghosted either side of the one a driver is in, named off `name`.
 *
 * Drawn rather than described: it is what tells a driver at a glance which way the box is going,
 * and it costs two text items. SimHub reports the gear as a number, so the neighbours are that
 * number plus and minus one; at the ends of the box they show nothing rather than 0 or 7.
 *
 * Placed off the gear's own cell rather than off the column's edges. The gear is centred and the
 * ghosts follow it, so the three read as one cluster whatever the column is: spacing them from the
 * edges put half a column between the ghost and the gear on the 600 px DisplayDash band. The two
 * cells are symmetric about the gear's, so the ink of the row is centred wherever the gear is.
 */
export function gearGhosts(frame: Rect, size: number, ghosts: GearGhosts, name: string): Item[] {
  const small = ghostSize(size, ghosts);
  const mono = gearCells(small);
  const width = monoWidth(mono, GEAR_CHARS);
  const cell = gearCell(frame, size);
  const current = game('Gear');
  const top = frame.top + (frame.height - small) / 2;
  const sides = [
    { id: 'below', x: cell.left - ghosts.gap - width, step: -1 as const, visibleBind: undefined },
    // The gear above is nothing to show in the car's top gear, which the ladder cannot know: the
    // mapping stops at the eight gears SimHub reports, and this stops at the count the car declares.
    { id: 'above', x: cell.left + cell.width + ghosts.gap, step: 1 as const, visibleBind: not(lastGear()) },
  ];
  return sides.map((side) =>
    numeral(`${name}.${side.id}`, side.id === 'below' ? '3' : '5', side.x, top, small, GEAR_CHARS, {
      mono,
      // Ghosted with the dim ink rather than with opacity: SimHub's opacity is an item property
      // and the dim colour is the token for exactly this -- something present but not being read.
      color: ds.color.text.dim,
      maxWidth: width + GEAR_BOX_SLACK,
      bind: gearNeighbour(current, side.step),
      visibleBind: side.visibleBind,
    }),
  );
}

/**
 * The gear with its neighbours ghosted either side, as one row centred in `frame`.
 *
 * A frame too narrow for the row drops the ghosts rather than drawing outside it, which is what
 * `fitFields` does on the second screens and is the only answer that keeps a cluster general: the
 * gear is what a driver reads and the ghosts are what tell him which way the box is going, so the
 * pair goes before the frame does. The gear itself still throws where even its own cell does not
 * fit, since a face that cannot hold a gear has said something it did not mean.
 */
export function gearCluster(frame: Rect, ghosts: GearGhosts, size: number = GEAR_SIZES.standard, prefix = 'hero'): Item[] {
  const fits = ghostedGearWidth(size, ghosts) <= frame.width;
  return [...(fits ? gearGhosts(frame, size, ghosts, `${prefix}.gear`) : []), ...gear(frame, size, prefix)];
}

/** The forward gears SimHub can report, as the strings it reports them in. */
const FORWARD_GEARS = ['1', '2', '3', '4', '5', '6', '7', '8'] as const;

/**
 * The gear one above or one below, as text mapped from text.
 *
 * **SimHub publishes the gear as a string** -- "N", "R", "1" -- so it cannot be added to. NCalc's
 * `+` has a string overload, so `[Gear] + 1` in third gear evaluates to "31", and a cell one
 * character wide draws the 3: the right-hand ghost showed the gear the car was already in, which
 * is what the first capture of the 1920 face caught. `-` has no string overload and coerced, which
 * is why only one side of the pair was wrong.
 *
 * Mapping text to text keeps arithmetic out of it entirely. Neutral and reverse match nothing and
 * draw nothing, and so does the gear below first, and the gear above the last one SimHub names --
 * which is the behaviour the ends of the box wanted anyway.
 */
function gearNeighbour(current: Expr, step: 1 | -1): Expr {
  return FORWARD_GEARS.reduce<Expr>((fallback, g) => {
    const neighbour = Number(g) + step;
    if (neighbour < 1 || neighbour > FORWARD_GEARS.length) return fallback;
    return iff(eq(current, str(g)), str(String(neighbour)), fallback);
  }, str(''));
}
