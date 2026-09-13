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
 * The order inside `flags` is `FLAG_CATALOGUE` from flags.ts, the one ordered list the face ranks
 * from too: the box and the face rank the same conditions, and two lists would eventually
 * disagree about which of two live flags wins.
 *
 * The catalogue appears twice, under the two halves of the critical-flags-only switch. That is
 * the shape DNR's profiles use for dark mode and it is worth copying: each branch then carries a
 * clean ranking of its own list, instead of every flag's condition growing a guard for every
 * higher flag that might be suppressed. A suppressed flag stops outranking the ones below it, so
 * with the switch on the box shows the next critical flag down rather than going dark.
 *
 * Colour comes from `design/tokens.json` through `ds`, resolved to a literal here the way the
 * .djson resolves it. A hex value typed into this file is a bug.
 */
import { flagBox } from '../contract.ts';
import { conditionVisible, flagsShown, type FlagCondition } from '../flags.ts';
import { ncalc, type MatrixContainer, type MatrixProfile } from '../generator.ts';
import { flagFrames } from './glyphs.ts';

/** The package name, the profile name and the file stem. Spaces are fine: the packages have them. */
export const FLAG_BOX_PROFILE_NAME = 'openDash Flag box';

/** 8 rows of 8, the size every glyph in this folder is drawn at. */
export const ROWS = 8;
export const COLUMNS = 8;

/**
 * The conditions of a list that have a glyph. A condition without one is not drawn, and is listed
 * in docs/design/flag-box.md with the reason: a drawn alert that never fires is worse than an
 * absent one, because nobody finds out until a race.
 */
export const drawnFlags = (criticalOnly: boolean): FlagCondition[] => flagsShown(criticalOnly).filter((c) => flagFrames(c.id) !== undefined);

/**
 * The flag effects of one list, highest priority first, each shown only when no higher flag in
 * that same list is out.
 */
export function flagContainers(criticalOnly: boolean): MatrixContainer[] {
  const shown = drawnFlags(criticalOnly);
  return shown.map((condition) => ({
    kind: 'when' as const,
    description: condition.id,
    formula: conditionVisible(condition, shown),
    children: [{ kind: 'animation' as const, description: `${condition.id} glyph`, frames: flagFrames(condition.id) ?? [] }],
  }));
}

/** The two branches of the critical-flags-only switch, the whole catalogue first. */
export function flagsGroup(): MatrixContainer {
  const { eq, not } = ncalc;
  const quiet = eq(flagBox.criticalOnly(), 'true');
  return {
    kind: 'group',
    description: 'Flags',
    children: [
      { kind: 'when', description: 'Critical flags only', formula: quiet, children: flagContainers(true) },
      { kind: 'when', description: 'Every flag', formula: not(quiet), children: flagContainers(false) },
    ],
  };
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
        { kind: 'gameRunning', description: 'Racing', children: [flagsGroup()] },
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
