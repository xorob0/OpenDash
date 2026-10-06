/**
 * The zone anatomy, as the default theme declares it: at every size the contract names, the
 * rectangles `zones/faces/*.ts` were read off the artboards, handed back as they are.
 */
import { FACE_SIZES } from '../../contract.ts';
import { zoneRegions } from '../../zones/layout.ts';
import type { Anatomy } from '../anatomy.ts';

export const defaultAnatomy: Anatomy = {
  sizes: FACE_SIZES.map(({ width, height }) => ({ width, height })),
  regions: zoneRegions,
};
