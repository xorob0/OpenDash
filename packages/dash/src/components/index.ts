/** The components: readouts, grids, the rev bar and arc, the flags, the pit limiter and the gear/speed variants. */
export { cardFrame, centredTop, type CardFrame } from './frame.ts';
export { readout, readoutGeometry, type LabelSpec, type ValueSpec, type ReadoutGeometry } from './readout.ts';
export { readoutRow, type FollowerSpec, type FollowerContext } from './readoutRow.ts';
export { grid2x2, CORNERS, type CellSpec } from './grid2x2.ts';
export { revSegmentOptions, revLayers, stageOf, REDLINE_BLINK_MS, type RevSegmentPlacement, type RevSegmentOptions } from './revSegments.ts';
export { revBar, type RevBarFrame } from './revBar.ts';
export { revArc, revArcAngle, REV_ARC_STEP, type RevArcFrame } from './revArc.ts';
export {
  gearSpeedRow,
  gearSpeedBand,
  gearSpeedStack,
  GEAR_SPEED_SIZES,
  STACK_SPEED_SIZE,
  STACK_UNIT_WIDTH,
  SPEED_UNIT_GAP,
  SPEED_CHARS,
  GEAR_CHARS,
  type GearSpeedSizes,
} from './gearSpeed.ts';
export { flagStrip, flagVisible, FLAG_PRIORITY, FLAG_BLINK_MS, BLACK_FLAG_BORDER, FLAG_STRIP_STYLES, type FlagProperty, type FlagStripStyle } from './flagStrip.ts';
export { flagRing, CHEQUER_COUNT, CHEQUER_STEP, CHEQUER_SIZE } from './flagRing.ts';
export { pitLimiter, PIT_LIMITER_BLINK_MS } from './pitLimiter.ts';
