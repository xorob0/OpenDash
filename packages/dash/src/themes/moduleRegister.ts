/**
 * Where a module finds the register of the theme its process draws (see `ModuleRegister` in
 * `themes/drawing.ts`).
 *
 * A slot rather than an import of the drawings, because the modules sit underneath the drawing code
 * that a theme's register is built from, and importing it from here would close a cycle through the
 * module graph. `themes/drawings.ts` fills the slot for every theme that has a register, and the slot
 * is read by `THEME_ID`, so a process drawing the default reads nothing and the house's code path is
 * the one it runs.
 */
import { THEME_ID } from '../tokens.ts';
import type { ModuleRegister } from './drawing.ts';

const registers = new Map<string, ModuleRegister>();

/** Records a theme's register, which `themes/drawings.ts` does for each theme that declares one. */
export const defineModuleRegister = (themeId: string, register: ModuleRegister): void => {
  registers.set(themeId, register);
};

/** The register of the theme this process draws, or undefined for one that keeps the house's. */
export const moduleRegister = (): ModuleRegister | undefined => registers.get(THEME_ID);
