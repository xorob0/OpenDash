/**
 * What the box shows that is not a flag and not the gear: the pit family, the spotter and the
 * three warnings. Each is an ordered list of its own, ranked below the flags and above the gear.
 *
 * Everything here reads a property SimHub already publishes. Nothing is computed between frames,
 * which is the line scope.md draws and ADR 0009 owns; where that meant a feature could not be
 * built, it is written down in docs/design/flag-box.md rather than approximated.
 */
import { flagBox, flagBoxMatrix, type FlagBoxMatrix } from '../contract.ts';
import { ncalc, type MatrixContainer, type MatrixFrame } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { ds } from '../tokens.ts';
import { blinkFrames, still, type Grid, type Palette } from './glyph.ts';

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
 * Limiter still on out of the lane: a mistake costing a second a corner, so it shouts. An
 * exclamation mark, blinking.
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

/** Low fuel: a tank emptying — a bar across the bottom two rows only. */
export const LOW_FUEL: Grid = [
  '.YYYYYY.',
  '.Y....Y.',
  '.Y....Y.',
  '.Y....Y.',
  '.Y....Y.',
  '.Y....Y.',
  '.YYYYYY.',
  '.YYYYYY.',
];

/** Oil too hot: the meatball's cousin, a disc with a drip, in orange. */
export const OIL_HOT: Grid = [
  '...OO...',
  '..OOOO..',
  '.OOOOOO.',
  'OOOOOOOO',
  'OOOOOOOO',
  '.OOOOOO.',
  '..OOOO..',
  '...OO...',
];

/** Water too hot: waves, in orange, so the two temperatures are told apart by shape. */
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
 * Over the pit lane limit, while in the lane. `PitLimiterSpeed` is iRacing's own
 * `TrackPitSpeedLimit` in km/h, and `SpeedKmh` is the car's; comparing two published numbers is
 * arithmetic over properties rather than state between frames, so it is not computed telemetry.
 * A small allowance keeps it from flickering at exactly the limit.
 */
export const SPEEDING_ALLOWANCE_KMH = 1;
const speeding = (): Expr => and(inLane(), gt(game('SpeedKmh'), ncalc.add(game('PitLimiterSpeed'), num(SPEEDING_ALLOWANCE_KMH))));

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
  { id: 'speeding', raised: speeding(), grid: SPEEDING, blink: true },
  { id: 'limiterOutOfLane', raised: and(limiterOn(), not(inLane())), grid: LIMITER_OUT_OF_LANE, blink: true },
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
 */
export const warningStates = (): BoxState[] => [
  { id: 'oilHot', raised: gt(isTemp(game('OilTemperature')), flagBox.oilTemp()), grid: OIL_HOT, blink: true },
  { id: 'waterHot', raised: gt(isTemp(game('WaterTemperature')), flagBox.waterTemp()), grid: WATER_HOT, blink: true },
  { id: 'lowFuel', raised: lt(ncalc.isnull(computed('Fuel_RemainingLaps'), num(999)), flagBox.lowFuelLaps()), grid: LOW_FUEL, blink: true },
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

const framesOf = (state: BoxState): MatrixFrame[] =>
  state.blink ? blinkFrames(state.grid, DARK, STATE_PALETTE, STATE_BLINK_HZ, state.id) : still(state.grid, STATE_PALETTE, state.id);

/** Nothing in the list is raised: what the next thing down needs to be true. */
export const noneRaised = (list: readonly BoxState[]): Expr => and(...list.map((s) => not(s.raised)));
