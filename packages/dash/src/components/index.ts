/**
 * The components: readouts, grids, the rev bar and arc, the gear, the flags, the pit limiter and
 * the list row.
 *
 * These are the face's. The second screens have a component layer of their own in `second/` --
 * the class chip, the gauges, the sector strip, the wheel, the page dots and the table -- which is
 * a fifth structure the four-layer model of element, component, card and layout does not name. It
 * is not an oversight to be tidied away by moving those files here: a second-screen part is a
 * function of a rectangle at a density, which is not what a face component is, and the two sets
 * have never shared a caller.
 */
export { cardFrame, centredTop, type CardFrame } from './frame.ts';
export { readout, readoutGeometry, type LabelSpec, type ValueSpec, type ReadoutGeometry } from './readout.ts';
export { readoutRow, type FollowerSpec, type FollowerContext } from './readoutRow.ts';
export { grid2x2, CORNERS, type CellSpec } from './grid2x2.ts';
export { revSegmentOptions, revLayers, stageOf, REDLINE_BLINK_MS, type RevSegmentPlacement, type RevSegmentOptions } from './revSegments.ts';
export { revBar, type RevBarFrame } from './revBar.ts';
export { revArc, revArcAngle, REV_ARC_STEP, type RevArcFrame } from './revArc.ts';
export { gear, GEAR_SIZES, GEAR_CHARS } from './gear.ts';
export { filledBand, outlinedBand, chequerBand, ALERT_BAND_BORDER, ALERT_BAND_STYLES, ALERT_FLASH_MS, ALERT_NAME_WEIGHT, type AlertBandStyle } from './alertBand.ts';
export { flagStrip, flagVisible, FLAG_PRIORITY, FLAG_BLINK_MS, BLACK_FLAG_BORDER, FLAG_STRIP_STYLES, type FlagProperty, type FlagStripStyle } from './flagStrip.ts';
export { flagRing, chequerRim, chequerCount, chequerStep, CHEQUER_SIZE } from './flagRing.ts';
export { pitLimiter, PIT_LIMITER_BLINK_MS } from './pitLimiter.ts';
export {
  popUp,
  popUps,
  popUpFit,
  popUpFrame,
  popUpVisible,
  POP_UPS,
  POP_UP_WIDTH,
  POP_UP_HEIGHT,
  POP_UP_RULE,
  POP_UP_PAD_X,
  POP_UP_GAP,
  POP_UP_VALUE_SIZE,
  POP_UP_SECONDARY_SIZE,
  POP_UP_SECONDS,
  LAP_POP_UP,
  FUEL_POP_UP,
  DRS_POP_UP,
  type PopUpSpec,
  type PopUpLabel,
  type PopUpText,
  type PopUpFit,
} from './popUp.ts';
export { listRow, listRowHeight, LIST_ROW, type ListRowSpec, type ListRowValue } from './listRow.ts';
