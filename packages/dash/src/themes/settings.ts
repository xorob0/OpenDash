/**
 * A theme's settings as its packages read them (#715). What a theme offers is declared in its
 * `settings.json` and listed in `THEME_SETTINGS` in `contract.ts`; this is how a package draws a colour
 * that follows the driver's choice.
 *
 * A colour is a runtime setting (ADR 0011), so a choice changes nothing but colours and every package
 * is still measured once, at build time. The expression is a chain of literals chosen by the published
 * id, ending in the default's colour, which is the overlay's own and what a package with no plugin
 * draws. Every item drawing the same colour carries the same text, and SimHub evaluates an expression
 * that reads no variable once a frame for all of them, by its text (docs/research/simhub-dash-format.md),
 * so a face that binds a thousand items to the backlight evaluates a handful of chains.
 *
 * The Porsche's setting boxes would take the same two steps the AiM's backlight took: an entry in its
 * own `settings.json`, listed in `THEME_SETTINGS`, and a binding from {@link themeSettingColour} on the
 * box's border in `settingBox`.
 */
import type { Expr } from '../bind.ts';
import { setting, themeSetting, type ThemeSettingChoice } from '../contract.ts';
import { ncalc, type Hex } from '../generator.ts';
import { applyOverlay, BASE_TREE, resolveTokenIn, type Tree } from '../tokens.ts';
import { THEMES } from './index.ts';

const { eq, iff, str } = ncalc;

const ALIAS = /^\{([A-Za-z0-9_.$-]+)\}$/;

/** Each theme's tokens, which a choice's aliases are read against whichever theme the process builds. */
const trees = new Map<string, Tree>();

function treeOf(themeId: string): Tree {
  const known = trees.get(themeId);
  if (known) return known;
  const theme = THEMES[themeId];
  if (!theme) throw new Error(`${JSON.stringify(themeId)} is not a theme`);
  const tree = applyOverlay(BASE_TREE, theme.overlay, themeId);
  trees.set(themeId, tree);
  return tree;
}

/** The colours a choice gives its setting's tokens, in the order of the setting's `tokens`, each `#RRGGBB`. */
export function choiceColours(themeId: string, settingId: string, choice: ThemeSettingChoice): Hex[] {
  const declared = themeSetting(themeId, settingId);
  if (choice.values.length !== declared.tokens.length) {
    throw new Error(`${themeId}.${settingId}: the choice ${choice.id} gives ${choice.values.length} values to ${declared.tokens.length} tokens`);
  }
  return choice.values.map((value) => {
    const alias = ALIAS.exec(value)?.[1];
    if (alias === undefined) throw new Error(`${themeId}.${settingId}: the choice ${choice.id} writes ${value}, where a choice aliases a token of the overlay`);
    const colour = resolveTokenIn(treeOf(themeId), alias);
    if (typeof colour !== 'string' || !/^#[0-9A-F]{6}$/.test(colour)) throw new Error(`${themeId}.${settingId}: ${alias} is not a #RRGGBB colour (${String(colour)})`);
    return colour as Hex;
  });
}

/** The setting's default choice, whose colours are the tokens' own. */
export function defaultChoice(themeId: string, settingId: string): ThemeSettingChoice {
  const declared = themeSetting(themeId, settingId);
  const found = declared.choices.find((c) => c.id === declared.default);
  if (!found) throw new Error(`${themeId}.${settingId}: the default ${declared.default} is not one of its choices`);
  return found;
}

/**
 * A colour that follows a theme's setting: `colourOf` the choice the driver made, and of the default
 * with no plugin or with an id this build does not know. A choice that draws the default's colour is
 * left out of the chain, which is then shorter and reads the same.
 */
export function themeSettingColour(themeId: string, settingId: string, colourOf: (choice: ThemeSettingChoice) => Hex): Expr {
  const declared = themeSetting(themeId, settingId);
  const fallback = colourOf(defaultChoice(themeId, settingId));
  const chosen = setting.themeSetting(themeId, settingId);
  return declared.choices
    .filter((choice) => choice.id !== declared.default)
    .reduceRight((otherwise, choice) => {
      const colour = colourOf(choice);
      return colour.toUpperCase() === fallback.toUpperCase() ? otherwise : iff(eq(chosen, str(choice.id)), str(colour), otherwise);
    }, str(fallback));
}
