/** Every layout the build produces. One file per screen size, in the order the manifest lists them. */
import { layout1920x480 } from './1920x480.ts';
import { layout1280x480 } from './1280x480.ts';
import { layout1280x400 } from './1280x400.ts';
import { layout850x480 } from './850x480.ts';
import { layout800x480 } from './800x480.ts';
import { layout1280x720 } from './1280x720.ts';
import { layout800x286 } from './800x286.ts';
import { layout600x686 } from './600x686.ts';
import { layout480round } from './480round.ts';
import { layout800round } from './800round.ts';
import type { Layout } from './layout.ts';

export type { Layout, NamedRect, Shape } from './layout.ts';
export { rungOf, cardRung, layoutDescription, namedRects } from './layout.ts';
export { flankedLayout, RULE, type FlankedSpec } from './flanked.ts';
export { roundLayout, faceOf, innerDiameter, INNER_INSET, type RoundSpec } from './round.ts';
export { layout1920x480, layout1280x480, layout1280x400, layout850x480, layout800x480, layout1280x720, layout800x286, layout600x686, layout480round, layout800round };

/** The MVP first, then the rectangular sizes in the order of the sizes spec, then the round faces. */
export const LAYOUTS: readonly Layout[] = [
  layout1920x480,
  layout1280x480,
  layout1280x400,
  layout850x480,
  layout800x480,
  layout1280x720,
  layout800x286,
  layout600x686,
  layout480round,
  layout800round,
];
