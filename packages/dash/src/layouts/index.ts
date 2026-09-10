/** Every layout the build produces. One file per screen size. */
import { layout1920x480 } from './1920x480.ts';
import type { Layout } from './layout.ts';

export type { Layout, NamedRect } from './layout.ts';
export { layout1920x480 };

export const LAYOUTS: readonly Layout[] = [layout1920x480];
