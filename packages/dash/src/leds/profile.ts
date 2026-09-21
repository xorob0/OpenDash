/**
 * The flag box profile: one 8x8 matrix, built by `bun run build` beside the fourteen packages.
 *
 * The tree is evaluated outside in, which is what makes it answerable to one brightness and one
 * on-air decision instead of forty:
 *
 *     brightness                     day or night, defaulted
 *       when the game is not running  nothing: the box is dark
 *       when the game is running
 *         when the ignition is off     a dim standby mark
 *         when the ignition is on
 *           matrix 1..4                each offset onto its own panel
 *             flags                    the alert catalogue, in priority order
 *             below the flags          the pit family, the warnings, then the gear
 *             the spotter              an overlay on the edge, painted over all of it
 *
 * **The spotter is the one thing that is not a rank.** It was the second rank below the flags, so
 * any flag at all hid a car alongside and a car alongside blanked the warnings and the gear beneath
 * it. It is now the last container of the matrix, and it lights the edge columns and leaves the
 * rest of the panel absent; SimHub composes containers in order and drops an absent pixel rather
 * than clearing what is under it, so a standing yellow keeps columns three to six and the bar says
 * which side. Nothing suppresses it and it suppresses nothing, which is what an overlay is.
 *
 * **Not racing is dark**, and that is the decision rather than a gap. Idle screens are a refusal
 * in scope.md, and a glowing logo on somebody's desk when nothing is running is the hardest
 * version of that refusal to defend. The branch is declared below and prunes itself away, so the
 * shape says so in one place. Ignition off is the exception: the car being switched off is a real
 * condition and a box that goes dark for it would be indistinguishable from a profile that failed
 * to load, so it gets the smallest mark that is still visibly on.
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
import type { Expr } from '../bind.ts';
import { flagBox, flagBoxMatrix, FLAG_BOX_MATRICES, type FlagBoxMatrix } from '../contract.ts';
import { conditionVisible, flagsShown, noFlagShown, type FlagCondition } from '../flags.ts';
import { ncalc, type MatrixContainer, type MatrixProfile } from '../generator.ts';
// The ignition read is telemetry and lives with the rest of it; the two *gates* composed from it
// live in gates.ts, because the strip asks the same question above its own tree and the answer has
// to be one expression rather than two spellings of one.
import { ignitionIsOff, ignitionIsOn } from './gates.ts';
import { gearGroup } from './gear.ts';
import { noneRaised, pitStates, spotterStates, stateContainers, warningStates } from './states.ts';
import { flagFrames, ignitionOffFrames } from './glyphs.ts';

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
 * `OpenDash.FlagBoxMatrix<N>CriticalOnly` as a condition.
 *
 * Per matrix rather than per tab since the settings a box owns moved under it: a rig with a box in
 * each corner can now show the whole catalogue on one and the critical flags alone on the other,
 * which is the setup the per-matrix group exists for.
 */
export const criticalOnly = (matrix: FlagBoxMatrix): Expr => ncalc.eq(flagBoxMatrix(matrix).criticalOnly(), 'true');

/**
 * The flag effects, highest priority first, each shown only when no higher flag is out *and* the
 * switch has not silenced it.
 */
export function flagContainers(matrix: FlagBoxMatrix): MatrixContainer[] {
  const shown = drawnFlags(false);
  return shown.map((condition) => ({
    kind: 'when' as const,
    description: condition.id,
    formula: conditionVisible(condition, criticalOnly(matrix), shown),
    children: [{ kind: 'animation' as const, description: `${condition.id} glyph`, frames: flagFrames(condition.id) ?? [] }],
  }));
}

/** The catalogue, in order. */
export function flagsGroup(matrix: FlagBoxMatrix): MatrixContainer {
  return { kind: 'group', description: 'Flags', children: flagContainers(matrix) };
}

/**
 * No flag is being shown, which is what everything below the flags needs to be true. A flag the
 * switch has silenced does not hold the panel: with critical-flags-only on, a chequered flag shows
 * the gear rather than nothing.
 */
export const noFlagShowing = (matrix: FlagBoxMatrix): Expr => noFlagShown(criticalOnly(matrix), drawnFlags(false));

/**
 * Everything below the flags, for one matrix, in order: the pit family, the three warnings, then
 * the gear.
 *
 * The order is the point. A low fuel light that hides a limiter warning for the rest of a stint is
 * the failure this ranking exists to prevent — so all of it sits under `noFlagShowing()`, and each
 * layer's condition excludes the layers above it.
 *
 * The spotter is deliberately absent: it is {@link spotterOverlay} now, painted after all of this
 * rather than ranked within it, so the gear shows through the middle of the panel while a car is
 * alongside instead of being blanked by it.
 */
export function belowFlags(matrix: FlagBoxMatrix): MatrixContainer[] {
  const { and, eq } = ncalc;
  const m = flagBoxMatrix(matrix);
  const pit = pitStates();
  const warnings = warningStates(matrix);
  const on = (setting: Expr): Expr => eq(setting, 'true');
  return [
    // Its own switch, not the flags'. A driver who turns flags off on a panel has not asked to lose
    // the pit limiter warning, and reading m.flags() here was a copy of the line below it.
    { kind: 'when', description: 'Pit', formula: on(m.pit()), children: stateContainers(pit, 'Pit') },
    {
      kind: 'when',
      description: 'Warnings',
      formula: and(on(m.warnings()), noneRaised(pit)),
      children: stateContainers(warnings, 'Warning'),
    },
    // Nothing above it is raised, and no more: what the panel rests *on* is the gear group's own
    // gate. Naming the resting state here as well would put one decision in two expressions, which
    // is the shape this rank grew out of.
    {
      kind: 'when',
      description: 'Resting',
      formula: and(noneRaised(pit), noneRaised(warnings)),
      children: [gearGroup(matrix)],
    },
  ].filter((c) => c.children.length > 0) as MatrixContainer[];
}

/**
 * The spotter, as the last thing painted on a panel.
 *
 * Gated on the panel's own Spotter switch and on nothing else: no flag bit, no pit condition, no
 * exclusion of anything above it. That is the machine-checkable form of "a car alongside is never
 * hidden by a yellow", and it is what the frames make safe — they light two edge columns and leave
 * the other six absent, so what was drawn underneath keeps them.
 *
 * Two branches on the animation switch, the way {@link gearGroup} splits the redline band into
 * flashing and steady: an `AnimationContainer` has no condition on its own frames, so "grows only
 * when the driver asked it to" is two sets of frames under two conditions. Off is the default,
 * because movement on this box means act and a car alongside informs.
 */
export function spotterOverlay(matrix: FlagBoxMatrix): MatrixContainer[] {
  const { eq, not } = ncalc;
  const moves = eq(flagBox.spotterAnimation(), 'true');
  return [
    { kind: 'when', description: 'Spotter growing', formula: moves, children: stateContainers(spotterStates(matrix, true), 'Spotter') },
    { kind: 'when', description: 'Spotter held', formula: not(moves), children: stateContainers(spotterStates(matrix, false), 'Spotter') },
  ];
}

/**
 * One matrix's whole content, offset onto it.
 *
 * A group's `StartPositionMatrix` is an *offset* applied when its children's results are merged
 * (`MultiMatrixResult.Merge(startPositionMatrix + i, …)`), so the subtree below is written once
 * per matrix at matrix 1 and shifted here. The subtree is repeated in the file, which is the cost
 * of SimHub having no way to bind which matrix a container paints; the gate above it is one
 * expression, and `ConditionnalGroupContainer` does not descend when it is false, so a matrix
 * nobody has switched on costs one evaluation a frame rather than a hundred.
 */
export function matrixGroup(matrix: FlagBoxMatrix): MatrixContainer | undefined {
  const { and, eq, not, or } = ncalc;
  const m = flagBoxMatrix(matrix);
  const doesSomething = or(eq(m.flags(), 'true'), eq(m.pit(), 'true'), eq(m.spotter(), 'true'), eq(m.warnings(), 'true'), eq(m.rest(), "'gear'"));
  // "No flag is holding THIS panel": a flag only suppresses what is under it on a panel that is
  // actually showing flags. Gating on noFlagShowing() alone blacked out the spotter, the warnings
  // and the gear on a panel with Flags switched off, for the whole time a flag was out.
  const flagsFree = or(not(eq(m.flags(), 'true')), noFlagShowing(matrix));
  const children = [
    { kind: 'when' as const, description: 'Flags', formula: eq(m.flags(), 'true'), children: [flagsGroup(matrix)] },
    { kind: 'when' as const, description: 'Below the flags', formula: flagsFree, children: belowFlags(matrix) },
    // Last, and outside `flagsFree`: an overlay is painted over whatever the two above it drew. It
    // needs no StartPositionMatrix of its own, since it sits inside the group that carries one.
    { kind: 'when' as const, description: 'Spotter', formula: eq(m.spotter(), 'true'), children: spotterOverlay(matrix) },
  ].filter((c) => c.children.length > 0) as MatrixContainer[];
  if (children.length === 0) return undefined;
  return { kind: 'when', description: `Matrix ${matrix}`, matrix, formula: doesSomething, children };
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
        // Deliberately empty, and pruned away before it is written: not racing is dark. See the
        // header. Nothing here reads an image from the user's disk; a custom idle picture is
        // personalisation, which ADR 0011 owes an answer before anything builds it.
        { kind: 'gameNotRunning', description: 'Not racing', children: [] },
        {
          kind: 'gameRunning',
          description: 'Racing',
          children: [
            { kind: 'when', description: 'Ignition off', formula: ignitionIsOff(), children: [{ kind: 'animation', description: 'Standby', frames: ignitionOffFrames() }] },
            {
              kind: 'when',
              description: 'Ignition on',
              formula: ignitionIsOn(),
              children: FLAG_BOX_MATRICES.map(matrixGroup).filter((c): c is MatrixContainer => c !== undefined),
            },
          ],
        },
      ],
    },
  ];
}

/**
 * Stamped into the profile's `Author`, which SimHub shows in its profile list and round-trips
 * untouched. It is how the plugin tells its own profile from one the user made.
 */
export const FLAG_BOX_AUTHOR = 'openDash';

/**
 * `openDash 0.2.0-rc.1` inside the description. The plugin reads the version back out of an
 * installed profile to decide whether it is current, so this is a machine-read string as well as a
 * human-read one; `flagBoxVersion()` is the other half and the two are tested together.
 */
export const flagBoxDescription = (version: string): string =>
  `The alert catalogue on an 8x8 matrix, ranked the way the face ranks it. Built by openDash ${version}; do not edit here, it is replaced on update.`;

/** The version stamped into a description, or null when it carries none. */
export function flagBoxVersion(description: string | null | undefined): string | null {
  if (typeof description !== 'string') return null;
  const found = /Built by openDash ([0-9A-Za-z.+-]+)/.exec(description);
  return found?.[1] ?? null;
}

/** The profile the build writes. */
export function buildFlagBoxProfile(version = '0.0.0'): MatrixProfile {
  return {
    name: FLAG_BOX_PROFILE_NAME,
    deviceKind: 'matrix8x8',
    author: FLAG_BOX_AUTHOR,
    description: flagBoxDescription(version),
    containers: flagBoxContainers(),
  };
}
