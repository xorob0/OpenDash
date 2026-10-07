/**
 * Prints the `.djson` of the large face under greenTheme.json, an overlay no build ships, for
 * themes.test.ts. It runs as its own process because a theme is chosen before `ds` exists, and the
 * overlay is put in place before the dynamic import because that import is what builds `ds`.
 *
 * The overlay is the default theme's here rather than a theme of its own, because what is asserted
 * is that the default's packages, folders and identifiers included, change by the colour alone; a
 * theme of its own would be built under its own folders (#202).
 */
import { DEFAULT_THEME_ID, THEMES } from '../../src/themes/index.ts';
import greenTheme from './greenTheme.json';

THEMES[DEFAULT_THEME_ID] = { ...THEMES[DEFAULT_THEME_ID]!, overlay: greenTheme };
const { faceDjson } = await import('./faceDjson.ts');
const { LARGE_FACE } = await import('../../src/zones/index.ts');
process.stdout.write(JSON.stringify(faceDjson([LARGE_FACE])));
