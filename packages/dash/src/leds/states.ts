/**
 * What the box shows that is not a flag and not the gear: the pit family, the spotter and the
 * three warnings. Each is an ordered list of its own, ranked below the flags and above the gear.
 *
 * Everything here reads a property SimHub already publishes. Nothing is computed between frames,
 * which is the line scope.md draws and ADR 0009 owns; where that meant a feature could not be
 * built, it is written down in docs/design/flag-box.md rather than approximated.
 *
 * Nothing below the flags moves. The box's one rule is that movement means act: a flag that ends
 * or interrupts the race moves, and everything that merely informs is held. A limiter left on and
 * an oil temperature climbing are both conditions the driver lives with for minutes at a time, so
 * a picture that strobed for those minutes would spend the box's only attention signal on the
 * states least able to give it back. The shape carries the urgency instead.
 *
 * "Held briefly so it cannot strobe" on the face sheet is read here as a plain still frame. A
 * minimum on-time is memory between frames, which ADR 0009 does not admit, so it is not built.
 */
import { flagBoxMatrix, type FlagBoxMatrix } from '../contract.ts';
import { ncalc, type MatrixContainer, type MatrixFrame } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { ds } from '../tokens.ts';
import { blinkFrames, still, type Grid, type Palette } from './glyph.ts';
import { tankIsLow } from '../second/values.ts';

const { and, eq, game, gt, lt, computed, not, num, str } = ncalc;

/** The pit colour is `purpose.pitLimiter`; the warnings borrow the flag palette's danger colours. */
export const STATE_PALETTE: Palette = {
  P: ds.purpose.pitLimiter,
  R: ds.purpose.flag.red,
  O: ds.purpose.flag.orange,
  Y: ds.purpose.flag.yellow,
  W: ds.purpose.flag.white,
};

const DARK: Grid = Array.from({ length: 8 }, () => '........');

/** How fast the pit and warning patterns blink, from the same token the flag band uses. */
export const STATE_BLINK_HZ = ds.indicator.flagBand.flashHz;

// --- The pit family --------------------------------------------------------------------------

/**
 * Limiter on, in the lane: quiet confirmation. A steady frame, not a filled panel — the driver is
 * doing the right thing and does not need to be shouted at. Two pixels thick, so that it is not
 * the black flag's one-pixel outline in a different colour: XOR-78's rule is that meaning cannot
 * rest on colour alone, and a driver who cannot see the difference between those two is being told
 * the wrong thing entirely.
 */
export const LIMITER_IN_LANE: Grid = [
  'PPPPPPPP',
  'PPPPPPPP',
  'PP....PP',
  'PP....PP',
  'PP....PP',
  'PP....PP',
  'PPPPPPPP',
  'PPPPPPPP',
];

/**
 * Limiter still on out of the lane: a mistake costing a second a corner. An exclamation mark, held.
 *
 * It is deliberately not a filled panel. `purpose.pitLimiter` is pure white, the same value as
 * `purpose.flag.white`, so a filled panel here would be the white flag with a blink — and telling
 * a driver "last lap" when you mean "your limiter is on" is the exact failure XOR-78 is about.
 * The token is not changed to fix this: `design/` is the design source and a colour is decided
 * there, so the shape carries the difference instead.
 */
export const LIMITER_OUT_OF_LANE: Grid = [
  '..PPPP..',
  '..PPPP..',
  '..PPPP..',
  '..PPPP..',
  '..PPPP..',
  '........',
  '..PPPP..',
  '..PPPP..',
];

/** Speeding in the lane: a chevron pointing down, meaning slow, filled in danger red. */
export const SPEEDING: Grid = [
  'RR....RR',
  'RRR..RRR',
  '.RRRRRR.',
  '..RRRR..',
  'RR....RR',
  'RRR..RRR',
  '.RRRRRR.',
  '..RRRR..',
];

// --- The spotter -----------------------------------------------------------------------------

/** A car on the left: a bar down the left edge. Two columns, so it is not mistaken for an artefact. */
export const CAR_LEFT: Grid = Array.from({ length: 8 }, () => 'WW......');
export const CAR_RIGHT: Grid = Array.from({ length: 8 }, () => '......WW');
export const CAR_BOTH: Grid = Array.from({ length: 8 }, () => 'WW....WW');

// --- The warnings ----------------------------------------------------------------------------

/**
 * Low fuel: the pump, body and hose, as the face sheet draws it.
 *
 * It was a tank outline, which is a rectangle with a rectangle inside it — the limiter frame's
 * vocabulary, and nothing a driver has seen anywhere else. ISO 2575 registers the pump for the
 * fuel level telltale, so it is the one picture in this family the driver already knows from the
 * road car, and knowing it is worth more than a shape that fits the grid more comfortably.
 */
export const LOW_FUEL: Grid = [
  '.YYYYY..',
  '.Y...Y.Y',
  '.YYYYY.Y',
  '.Y...Y.Y',
  '.Y...YYY',
  '.Y...Y..',
  '.Y...Y..',
  'YYYYYYY.',
];

/**
 * Oil too hot: the can, spout up and a drop falling from it, as the face sheet draws it.
 *
 * It was a disc, which is the meatball's own shape in a second orange — the one thing the
 * uniqueness rule in glyphFit.test.ts exists to refuse, and the confusion that costs most, since
 * the meatball is an instruction to come in and the oil lamp is not. ISO 2575 registers the can
 * for the oil telltale.
 */
export const OIL_HOT: Grid = [
  '.......O',
  '......O.',
  '.....O..',
  '.OOOOO..',
  'OOOOOOO.',
  'OOOOOOO.',
  '.OOOOO..',
  '...O....',
];

/**
 * Water too hot: waves, in orange, so the two temperatures are told apart by shape.
 *
 * Two bands of three rows and a blank, not two single rows. The face sheet's prose says "two rows
 * of waves" and its own artboard draws these eight, and the artboard is the drawing, so it wins.
 */
export const WATER_HOT: Grid = [
  '..OO..OO',
  '.O..OO..',
  'O....OO.',
  '........',
  '..OO..OO',
  '.O..OO..',
  'O....OO.',
  '........',
];

// --- The conditions --------------------------------------------------------------------------

const limiterOn = (): Expr => eq(game('PitLimiterOn'), num(1));
const inLane = (): Expr => eq(game('IsInPitLane'), num(1));

/**
 * Over the pit lane limit, while in the lane.
 *
 * **Both sides are in metres per second, and that is the whole point of this comment.**
 * `PitLimiterSpeed` is published through `KmhToLocalSpeedUnit` (GameManagerBase.cs:1339), so for a
 * driver whose SimHub speed unit is MPH a 60 km/h limit arrives as 37. Comparing that with
 * `SpeedKmh`, which is always km/h, reads as speeding from a standstill — and because this term
 * heads the exclusion chain that gates the spotter, the warnings and the gear, it would not merely
 * light the wrong picture, it would black out everything below the flags for every imperial user.
 * `PitLimiterSpeedMs` (GameManagerBase.cs:1340) is metres per second whatever the user has set.
 *
 * It is also `double?`: a sim or track that publishes no pit limit gives null, so it needs the
 * `isnull()` every other read here already has. The default is a speed nothing reaches, so an
 * unknown limit means "not speeding" rather than "always speeding".
 */
export const SPEEDING_ALLOWANCE_MS = 0.3;
/** Metres per second; nothing in a pit lane approaches it, so an unpublished limit never fires. */
export const NO_PIT_LIMIT_MS = 999;
const speedMs = (): Expr => ncalc.div(ncalc.isnull(game('SpeedKmh'), num(0)), num(3.6));
const pitLimitMs = (): Expr => ncalc.isnull(game('PitLimiterSpeedMs'), num(NO_PIT_LIMIT_MS));
const speeding = (): Expr => and(inLane(), gt(speedMs(), ncalc.add(pitLimitMs(), num(SPEEDING_ALLOWANCE_MS))));

/** One state the box can show: a condition, a picture, and whether it blinks. */
export interface BoxState {
  id: string;
  raised: Expr;
  grid: Grid;
  blink: boolean;
}

/**
 * The pit family, highest first. Speeding outranks both limiter states because it is the one that
 * is costing a penalty right now.
 */
export const pitStates = (): BoxState[] => [
  { id: 'speeding', raised: speeding(), grid: SPEEDING, blink: false },
  { id: 'limiterOutOfLane', raised: and(limiterOn(), not(inLane())), grid: LIMITER_OUT_OF_LANE, blink: false },
  { id: 'limiterInLane', raised: and(limiterOn(), inLane()), grid: LIMITER_IN_LANE, blink: false },
];

/**
 * The spotter, for a box on `side` of the rig. A box that is `both` shows both edges; one that is
 * `left` ignores a car on the right entirely, because a box to the left of the wheel lighting for
 * a car on the right is worse than no box.
 *
 * iRacing publishes a left-right state that distinguishes clear, one side, both sides and two cars
 * on a side — but SimHub folds it into two booleans on the way through (`CarLeftRight` 2, 4 and 5
 * all become `SpotterCarLeft`), so the two-car state does not survive. Three states are shipped
 * rather than a fourth faked; see docs/design/flag-box.md.
 */
export function spotterStates(matrix: FlagBoxMatrix): BoxState[] {
  const side = flagBoxMatrix(matrix).side();
  const left = eq(game('SpotterCarLeft'), num(1));
  const right = eq(game('SpotterCarRight'), num(1));
  const isSide = (name: string): Expr => eq(side, str(name));
  const shows = (name: string): Expr => ncalc.or(isSide('both'), isSide(name));
  return [
    { id: 'carBoth', raised: and(isSide('both'), left, right), grid: CAR_BOTH, blink: false },
    { id: 'carLeft', raised: and(shows('left'), left), grid: CAR_LEFT, blink: false },
    { id: 'carRight', raised: and(shows('right'), right), grid: CAR_RIGHT, blink: false },
  ];
}

/**
 * The three warnings, highest first. Each threshold is a contract property; the temperatures are
 * compared in whatever unit SimHub is already reporting them in, so a driver in Fahrenheit sets a
 * Fahrenheit number and gets a Fahrenheit comparison.
 *
 * The two temperatures belong to the panel and the fuel threshold does not: how hot is too hot is a
 * judgement a driver may want said on one box and not on another, whereas "am I low on fuel" is one
 * answer the whole rig shares with the strip and the faces.
 */
export const warningStates = (matrix: FlagBoxMatrix): BoxState[] => [
  { id: 'oilHot', raised: gt(isTemp(game('OilTemperature')), flagBoxMatrix(matrix).oilTemp()), grid: OIL_HOT, blink: false },
  { id: 'waterHot', raised: gt(isTemp(game('WaterTemperature')), flagBoxMatrix(matrix).waterTemp()), grid: WATER_HOT, blink: false },
  { id: 'lowFuel', raised: tankIsLow(), grid: LOW_FUEL, blink: false },
];

/** A temperature, defaulted so that a car which does not report one never trips a warning. */
const isTemp = (expr: Expr): Expr => ncalc.isnull(expr, num(0));

/** One state as a container: shown when it is raised and nothing above it in `list` is. */
export function stateContainers(list: readonly BoxState[], kind: string): MatrixContainer[] {
  return list.map((state, i) => ({
    kind: 'when' as const,
    description: `${kind} ${state.id}`,
    formula: and(...list.slice(0, i).map((higher) => not(higher.raised)), state.raised),
    children: [{ kind: 'animation' as const, description: `${kind} ${state.id} glyph`, frames: framesOf(state) }],
  }));
}

/**
 * No state below the flags sets `blink`, so the first branch is unreachable today. It is kept
 * rather than deleted because `blink` is what the rule is written in: flagBox.test.ts asserts that
 * every state here is held, and an invariant with no field to name cannot be tested.
 */
const framesOf = (state: BoxState): MatrixFrame[] =>
  state.blink ? blinkFrames(state.grid, DARK, STATE_PALETTE, STATE_BLINK_HZ, state.id) : still(state.grid, STATE_PALETTE, state.id);

/** Nothing in the list is raised: what the next thing down needs to be true. */
export const noneRaised = (list: readonly BoxState[]): Expr => and(...list.map((s) => not(s.raised)));
