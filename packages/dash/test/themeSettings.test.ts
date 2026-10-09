/**
 * A theme's own settings (#715): what each declares, the property it is published under, and, for the
 * AiM's backlight, that every colour of the LCD follows it on every package the theme builds while a
 * package with no plugin draws exactly the white it drew before.
 */
import { describe, expect, test } from 'bun:test';
import { composeTheme } from '../src/build.ts';
import { declaredProperties, PROPERTY_PREFIX, THEME_SETTINGS, themeEntry, themeSettingName, themeSettingNames } from '../src/contract.ts';
import { ncalcEvaluator as E, type Binding, type Border, type Item } from '../src/generator.ts';
import { THEMES } from '../src/themes/index.ts';
import { choiceColours, defaultChoice, themeSettingColour } from '../src/themes/settings.ts';
import { FLAG_LAYERS } from '../src/themes/aim/backlight.ts';

describe('the theme settings', () => {
  const declared = Object.entries(THEME_SETTINGS).flatMap(([themeId, settings]) => settings.map((s) => ({ themeId, s })));

  test('belong to themes that exist, and the AiM declares its backlight first', () => {
    for (const { themeId } of declared) expect({ themeId, known: Object.hasOwn(THEMES, themeId) && themeEntry(themeId) !== undefined }).toEqual({ themeId, known: true });
    expect(declared.map(({ themeId, s }) => `${themeId}.${s.id}`)).toEqual(['aim.backlight']);
  });

  test('are published under one flat name each, shared by every screen', () => {
    expect(themeSettingName('aim', 'backlight')).toBe('ThemeAimBacklight');
    expect(themeSettingNames()).toEqual(['ThemeAimBacklight']);
    for (const name of themeSettingNames()) expect(declaredProperties()).toContain(`${PROPERTY_PREFIX}.${name}`);
  });

  for (const { themeId, s } of declared) {
    test(`${themeId}.${s.id}: a default among its choices, whose values are the tokens' own, and a colour for every token of every choice`, () => {
      expect(s.choices.map((c) => c.id)).toContain(s.default);
      expect(new Set(s.choices.map((c) => c.id)).size).toBe(s.choices.length);
      expect(defaultChoice(themeId, s.id).values).toEqual(s.tokens.map((token) => `{${token}}`));
      for (const choice of s.choices) {
        const colours = choiceColours(themeId, s.id, choice);
        expect(colours).toHaveLength(s.tokens.length);
        for (const colour of colours) expect(colour).toMatch(/^#[0-9A-F]{6}$/);
      }
    });

    test(`${themeId}.${s.id}: the colour follows the choice, and is the default's with no plugin or an id it does not know`, () => {
      for (let token = 0; token < s.tokens.length; token++) {
        const expression = themeSettingColour(themeId, s.id, (choice) => choiceColours(themeId, s.id, choice)[token]!);
        const at = (value: string | undefined): unknown => E.evaluate(expression, { properties: value === undefined ? {} : { [`${PROPERTY_PREFIX}.${themeSettingName(themeId, s.id)}`]: value } });
        const fallback = choiceColours(themeId, s.id, defaultChoice(themeId, s.id))[token];
        expect(at(undefined)).toBe(fallback);
        expect(at('no such choice')).toBe(fallback);
        for (const choice of s.choices) expect({ choice: choice.id, colour: at(choice.id) }).toEqual({ choice: choice.id, colour: choiceColours(themeId, s.id, choice)[token] });
      }
    });
  }
});

describe("the AiM's backlight on the AiM's packages", () => {
  // Composed as the build composes them, in a process drawn in the AiM's colours.
  const packages = composeTheme({ theme: themeEntry('aim')!, version: '0.0.0-test', simHubVersion: '9.12.6' });
  const property = `${PROPERTY_PREFIX}.${themeSettingName('aim', 'backlight')}`;
  const backlight = THEME_SETTINGS.aim!.find((s) => s.id === 'backlight')!;
  const white = choiceColours('aim', 'backlight', defaultChoice('aim', 'backlight'));
  const pairs = new Map(backlight.choices.map((choice) => [choice.id, choiceColours('aim', 'backlight', choice)]));

  const COLOURS = [
    ['textColor', 'TextColor'],
    ['backgroundColor', 'BackgroundColor'],
    ['fillColor', 'FillColor'],
    ['strokeColor', 'EllipseColor'],
    ['lineColor', 'LineColor'],
    ['gaugeColor', 'GaugeColor'],
    ['alternateGaugeColor', 'AlternateGaugeColor'],
  ] as const;
  const formulaOf = (binding: Binding | undefined): string | undefined => (binding?.mode === 'formula' && typeof binding.formula === 'string' ? binding.formula : undefined);

  /** Every colour an item draws by a literal, with the formula that binds it, if any. */
  interface Drawn {
    where: string;
    literal: string;
    formula?: string;
  }
  const drawn: Drawn[] = [];
  const inFlags: Drawn[] = [];
  const walk = (items: readonly Item[], where: string, into: Drawn[]): void => {
    for (const item of items) {
      if (item.kind === 'layer') {
        walk(item.children, `${where}/${item.name}`, into);
        continue;
      }
      const fields = item as unknown as Record<string, unknown>;
      for (const [field, target] of COLOURS) {
        const literal = fields[field];
        if (typeof literal === 'string') into.push({ where: `${where}/${item.name}.${field}`, literal: literal.toUpperCase(), formula: formulaOf(item.bindings?.[target]) });
      }
      const border = fields.border as Border | undefined;
      if (border?.color) into.push({ where: `${where}/${item.name}.border`, literal: border.color.toUpperCase(), formula: formulaOf(border.colorBinding) });
    }
  };
  for (const { pkg } of packages) {
    for (const dashboard of pkg.dashboards) {
      for (const screen of dashboard.screens) {
        const where = `${pkg.folderName}/${dashboard.name}/${screen.name}`;
        walk(screen.items.filter((i) => !(i.kind === 'layer' && FLAG_LAYERS.has(i.name))), where, drawn);
        walk(screen.items.filter((i) => i.kind === 'layer' && FLAG_LAYERS.has(i.name)), where, inFlags);
      }
    }
  }

  test('are built at every size the theme claims, and every main screen still has the three flag layers the pass leaves alone', () => {
    expect(packages).toHaveLength(themeEntry('aim')!.sizes.length);
    for (const { pkg } of packages) {
      for (const screen of pkg.dashboards[0]!.screens.slice(0, 2)) {
        expect({ screen: `${pkg.folderName}/${screen.name}`, flags: screen.items.filter((i) => i.kind === 'layer' && FLAG_LAYERS.has(i.name)).map((i) => i.name) }).toEqual({ screen: `${pkg.folderName}/${screen.name}`, flags: [...FLAG_LAYERS] });
      }
    }
  });

  test('bind every ground and every ink they draw', () => {
    const unbound = drawn.filter((d) => (d.literal === white[0] || d.literal === white[1]) && !d.formula?.includes(property));
    expect(unbound.map((d) => d.where)).toEqual([]);
    expect(drawn.filter((d) => d.formula?.includes(property)).length).toBeGreaterThan(1000);
  });

  test('draw the white with no plugin, and each backlight the driver chooses, in its own ground and ink', () => {
    const bare = drawn.filter((d) => d.formula !== undefined && d.formula.startsWith(`if((isnull([${property}]`) && (d.literal === white[0] || d.literal === white[1]));
    expect(bare.length).toBeGreaterThan(1000);
    // Asked once per formula and colour: the thousand items share a handful of texts, as SimHub evaluates them.
    const distinct = new Map(bare.map((d) => [`${d.literal} ${d.formula}`, d]));
    expect(distinct.size).toBe(2);
    for (const d of distinct.values()) {
      const ground = d.literal === white[0];
      expect({ where: d.where, colour: E.evaluate(d.formula!, { properties: {} }) }).toEqual({ where: d.where, colour: d.literal });
      for (const [id, pair] of pairs) {
        expect({ where: d.where, id, colour: E.evaluate(d.formula!, { properties: { [property]: id } }) }).toEqual({ where: d.where, id, colour: ground ? pair[0]! : pair[1]! });
      }
    }
  });

  test('leave the flags in their own colours', () => {
    expect(inFlags.length).toBeGreaterThan(0);
    expect(inFlags.filter((d) => d.formula?.includes(property)).map((d) => d.where)).toEqual([]);
  });
});
