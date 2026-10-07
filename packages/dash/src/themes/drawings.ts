/**
 * What each theme draws itself, by id (see `themes/drawing.ts`). A theme with no entry is drawn
 * entirely the house's way, which is the default theme's whole drawing.
 *
 * A registry of its own rather than a field of `THEMES`, because drawing code reads `ds` and
 * `THEMES` is read before `ds` exists; `themes/faces.ts`, which builds faces and so already needs
 * `ds`, is the only reader. Nothing here may read a theme's own colours when it is imported, since
 * every process imports every drawing and only a theme's own process has its colours.
 */
import type { ThemeDrawing } from './drawing.ts';
import { porscheDrawing } from './porsche/drawing.ts';

export const THEME_DRAWINGS: Readonly<Record<string, ThemeDrawing>> = {
  porsche: porscheDrawing,
};
