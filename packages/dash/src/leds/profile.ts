/**
 * The flag box profile: one 8x8 matrix, built by `bun run build` beside the fourteen packages.
 *
 * The tree is evaluated outside in, which is what makes it answerable to one brightness and one
 * on-air decision instead of forty:
 *
 *     brightness                     OpenDash.FlagBoxBrightness, defaulted
 *       when the game is not running  the box at rest
 *       when the game is running
 *         flags                       the alert catalogue, in priority order
 *
 * The order inside `flags` is `FLAG_PRIORITY` from components/flagStrip.ts, and it is imported
 * rather than restated on purpose: the box and the face rank the same conditions, and two lists
 * would eventually disagree about which of two live flags wins.
 *
 * Colour comes from `design/tokens.json` through `ds`, resolved to a literal here the way the
 * .djson resolves it. A hex value typed into this file is a bug.
 */
import { flagBox } from '../contract.ts';
import { FLAG_PRIORITY, flagVisible, type FlagProperty } from '../components/flagStrip.ts';
import type { Hex, MatrixContainer, MatrixFrame, MatrixProfile } from '../generator.ts';
import { ds } from '../tokens.ts';
import { still } from './glyph.ts';

/** The package name, the profile name and the file stem. Spaces are fine: the packages have them. */
export const FLAG_BOX_PROFILE_NAME = 'openDash Flag box';

/** 8 rows of 8, the size every glyph in this folder is drawn at. */
export const ROWS = 8;
export const COLUMNS = 8;

/** A solid 8x8 field of one colour: the simplest honest picture of a flag. */
export const solid = (colour: Hex, name: string): MatrixFrame[] =>
  still(Array.from({ length: ROWS }, () => 'X'.repeat(COLUMNS)), { X: colour }, name);

/**
 * One flag as an effect. Only the green flag is drawn today; XOR-227 draws the catalogue, and
 * until it does the others are deliberately absent rather than approximated, because a drawn
 * alert that never fires is worse than an absent one.
 */
const FLAG_GLYPHS: Partial<Record<FlagProperty, Hex>> = {
  Flag_Green: ds.purpose.flag.green,
};

/** The flag effects, highest priority first, each shown only when no higher flag is out. */
export function flagContainers(): MatrixContainer[] {
  const out: MatrixContainer[] = [];
  for (const flag of FLAG_PRIORITY) {
    const colour = FLAG_GLYPHS[flag];
    if (colour === undefined) continue;
    out.push({
      kind: 'when',
      description: flag,
      formula: flagVisible(flag),
      children: [{ kind: 'animation', description: `${flag} glyph`, frames: solid(colour, flag) }],
    });
  }
  return out;
}

/**
 * Drops a group that has ended up with no children. The tree below declares its whole shape in
 * one place, including branches whose content is still on another ticket; a group with nothing in
 * it paints nothing, so emitting one would only put a warning in a clean build and a dead node in
 * a file somebody has to read.
 */
export function pruneEmpty(containers: readonly MatrixContainer[]): MatrixContainer[] {
  const out: MatrixContainer[] = [];
  for (const container of containers) {
    if (!('children' in container)) {
      out.push(container);
      continue;
    }
    const children = pruneEmpty(container.children);
    if (children.length === 0) continue;
    out.push({ ...container, children });
  }
  return out;
}

/** The whole tree, outside in, with the branches that are still empty pruned away. */
export function flagBoxContainers(): MatrixContainer[] {
  return pruneEmpty(flagBoxTree());
}

/** The tree as declared, empty branches included. `flagBoxContainers` is what the build writes. */
export function flagBoxTree(): MatrixContainer[] {
  return [
    {
      kind: 'brightnessFormula',
      description: 'Brightness',
      formula: flagBox.brightness(),
      children: [
        // The box at rest is XOR-229. It exists as a branch here so the shape is settled before
        // anything has to be drawn into it, and an empty group paints nothing.
        { kind: 'gameNotRunning', description: 'Not racing', children: [] },
        {
          kind: 'gameRunning',
          description: 'Racing',
          children: [{ kind: 'group', description: 'Flags', children: flagContainers() }],
        },
      ],
    },
  ];
}

/** The profile the build writes. */
export function buildFlagBoxProfile(): MatrixProfile {
  return {
    name: FLAG_BOX_PROFILE_NAME,
    deviceKind: 'matrix8x8',
    description: 'openDash flag box: the alert catalogue on an 8x8 matrix, ranked the way the face ranks it.',
    containers: flagBoxContainers(),
  };
}
