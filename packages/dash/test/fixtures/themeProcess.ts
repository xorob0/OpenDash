/**
 * The theme process of `src/buildTheme.ts`, with a test theme that has colours of its own registered
 * first, for themeCatalogue.test.ts: no theme the build ships has colours yet, and this is the path
 * the first one (#205) will take. Registered before the dynamic import, because that import is what
 * builds `ds`, in the colours `OPENDASH_THEME` names.
 */
import { THEMES } from '../../src/themes/index.ts';
import { GREEN_GEAR_LEFT_THEME_ID, greenGearLeftTheme } from './gearLeftTheme.ts';

THEMES[GREEN_GEAR_LEFT_THEME_ID] = greenGearLeftTheme;
const { main } = await import('../../src/buildTheme.ts');
process.exit(main());
