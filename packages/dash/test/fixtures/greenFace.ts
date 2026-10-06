/**
 * Prints the `.djson` of the large face under greenTheme.json, a theme no build ships, for
 * themes.test.ts. It runs as its own process because a theme is chosen before `ds` exists, and the
 * theme is registered before the dynamic import because that import is what builds `ds`.
 */
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../../src/themes/index.ts';
import greenTheme from './greenTheme.json';

THEMES['test-green'] = { ...THEMES[DEFAULT_THEME_ID]!, overlay: greenTheme };
process.env[THEME_ENV] = 'test-green';
const { faceDjson } = await import('./faceDjson.ts');
const { LARGE_FACE } = await import('../../src/zones/index.ts');
process.stdout.write(JSON.stringify(faceDjson([LARGE_FACE])));
