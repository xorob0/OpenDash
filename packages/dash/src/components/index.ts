/** The components: readouts, grids, the rev bar and arc, the gear, the flags and the pit limiter. */
export { cardFrame, centredTop, type CardFrame } from './frame.ts';
export { readout, readoutGeometry, type LabelSpec, type ValueSpec, type ReadoutGeometry } from './readout.ts';
export { readoutRow, type FollowerSpec, type FollowerContext } from './readoutRow.ts';
export { grid2x2, CORNERS, type CellSpec } from './grid2x2.ts';
export { revSegmentOptions, revLayers, stageOf, REDLINE_BLINK_MS, type RevSegmentPlacement, type RevSegmentOptions } from './revSegments.ts';
export { revBar, type RevBarFrame } from './revBar.ts';
export { revArc, revArcAngle, REV_ARC_STEP, type RevArcFrame } from './revArc.ts';
export { gear, GEAR_SIZES, GEAR_CHARS } from './gear.ts';
export { flagStrip, flagVisible, FLAG_PRIORITY, FLAG_BLINK_MS, BLACK_FLAG_BORDER, FLAG_STRIP_STYLES, type FlagProperty, type FlagStripStyle } from './flagStrip.ts';
export { flagRing, CHEQUER_COUNT, CHEQUER_STEP, CHEQUER_SIZE } from './flagRing.ts';
export { pitLimiter, PIT_LIMITER_BLINK_MS } from './pitLimiter.ts';
