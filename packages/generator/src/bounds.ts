/**
 * Axis-aligned footprint of a rotated rect. SimHub rotates a DrawableItem around its centre, so
 * the pixels an item can touch are the bounds of its rotated rect, not the rect itself; the
 * validator's canvas check and layout tests use this rather than the raw geometry.
 */

import type { DrawableItem, Rect } from './model.ts';

const toRadians = (degrees: number): number => (degrees * Math.PI) / 180;

/** The smallest axis-aligned rect containing `rect` rotated by `rotation` degrees around its centre. */
export const rotatedBounds = (rect: Rect, rotation = 0): Rect => {
  if (rotation === 0 || rotation % 360 === 0) return { ...rect };
  const cos = Math.abs(Math.cos(toRadians(rotation)));
  const sin = Math.abs(Math.sin(toRadians(rotation)));
  const halfW = (rect.width * cos + rect.height * sin) / 2;
  const halfH = (rect.width * sin + rect.height * cos) / 2;
  const cx = rect.left + rect.width / 2;
  const cy = rect.top + rect.height / 2;
  return { left: cx - halfW, top: cy - halfH, width: 2 * halfW, height: 2 * halfH };
};

/** The footprint of an item with a rect, honouring its rotation. */
export const itemBounds = (item: DrawableItem): Rect => rotatedBounds(item.rect, item.rotation ?? 0);
