/** The components: readouts, grids, the rev bar, the flags, the pit limiter and the gear/speed pair. */
export { cardFrame, centredTop, type CardFrame } from './frame.ts';
export { readout, readoutGeometry, type LabelSpec, type ValueSpec, type ReadoutGeometry } from './readout.ts';
export { readoutRow, type FollowerSpec, type FollowerContext } from './readoutRow.ts';
export { grid2x2, CORNERS, type CellSpec } from './grid2x2.ts';
export { revBar, REDLINE_BLINK_MS, type RevBarFrame } from './revBar.ts';
export { gearSpeed, SPEED_UNIT_GAP, SPEED_CHARS, GEAR_CHARS } from './gearSpeed.ts';
export { flagStrip, flagVisible, FLAG_PRIORITY, FLAG_BLINK_MS, BLACK_FLAG_BORDER, type FlagProperty } from './flagStrip.ts';
export { pitLimiter, PIT_LIMITER_BLINK_MS } from './pitLimiter.ts';
